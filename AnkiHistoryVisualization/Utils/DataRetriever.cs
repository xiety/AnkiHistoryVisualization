using System.Linq.Expressions;
using System.Text.Json;

using AnkiDatabase;

using Microsoft.EntityFrameworkCore;

namespace AnkiHistoryVisualization;

public static class DataRetriever
{
    public static Note[] Retrieve(string databasePath, string deckName, int? cardType)
    {
        using var db = new AnkiDbContext(databasePath);

        var col = db.Col.First();

        Expression<Func<CardTable, bool>> filter = a => a.Deck.Name.StartsWith(deckName) && a.Deck.Name.EndsWith(deckName)
                                                     && (a.Queue == 1 || a.Queue == 2 || a.Queue == -2) //TODO: learning, review, buried
                                                     && a.Data != "{}"
                                                     && (cardType == null || a.Ord == cardType);

        var query = db.Notes
            .Include(a => a.Cards.AsQueryable().Where(filter).OrderBy(b => b.Ord))
            .ThenInclude(a => a.Deck)
            .Include(a => a.Cards)
            .ThenInclude(a => a.Revlogs)
            .Where(a => a.Cards.AsQueryable().Any(filter))
            .OrderBy(a => a.Id);

        var sql = query.ToQueryString();

        var notes = query.ToArray(); //materialize

        var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        var data = notes
            .Select(note =>
            {
                var fields = note.Flds.Split("\u001f");
                var number = fields[0];
                var full = fields[1];

                var cards = note.Cards.Select(card =>
                {
                    var due = card.Queue switch
                    {
                        1 => DateOnly.FromDayNumber(card.Due / 86400),
                        -2 or 2 => DateOnly.FromDateTime(col.Crt.AddDays(card.Due)),
                        _ => throw new Exception()
                    };

                    var data = JsonSerializer.Deserialize<DataInfo>(card.Data, jsonOptions)!;
                    var difficulty = (data.D - 1.0f) / 9.0f;

                    var revlogs = ConvertRevLogs(card.Revlogs).ToArray();

                    return new Card(card.Id, card.Ord, due, difficulty, revlogs);
                }).ToArray();

                return new Note(note.Id, number, full, cards);
            })
            .ToArray();

        return data;
    }

    private static IEnumerable<Revlog> ConvertRevLogs(IEnumerable<RevlogTable> revlogs)
    {
        DateOnly? lastDate = null;
        DateOnly? dueDateIncorrect = null;
        DateOnly? lastReviewDate = null;
        var hasReviewForThisDate = false;

        foreach (var revlog in revlogs.OrderBy(a => a.Id))
        {
            var date = ConvertDateWithNight(revlog.Id, 4);

            if (date != lastDate)
                hasReviewForThisDate = false;

            if (revlog.Type is RevlogType.Filtered or RevlogType.Learn or RevlogType.Review)
            {
                if (!hasReviewForThisDate)
                {
                    var calcDue = lastReviewDate is DateOnly d
                        ? d.AddDays(revlog.LastIvl)
                        : (DateOnly?)null;

                    yield return new(date, revlog.Ease, calcDue);

                    hasReviewForThisDate = true;
                    lastReviewDate = DateOnly.FromDateTime(revlog.Id); //date;
                }
            }

            dueDateIncorrect = date.AddDays(revlog.Ivl);

            lastDate = date;
        }

        //return revlogs
        //    .GroupBy(a => ConvertDateWithNight(a.Id, 4))
        //    .Select(a =>
        //    {
        //        var revlog = a.MinBy(a => a.Id)!;
        //        return new Revlog(a.Key, revlog.Ease, revlog.Ivl);
        //    })
        //    .OrderBy(a => a.Date)
        //    .ToArray();
    }

    private static DateOnly ConvertDateWithNight(DateTime dateTime, int nextDayHour)
    {
        var dateOnly = DateOnly.FromDateTime(dateTime);

        if (dateTime.Hour <= nextDayHour)
            dateOnly = dateOnly.AddDays(-1);

        return dateOnly;
    }

    public class DataInfo
    {
        public float S { get; set; }
        public float D { get; set; }
    }
}

public record Note(long NoteId, string Number, string Text, Card[] Cards);
public record Card(long CardId, int CardType, DateOnly Due, float Difficulty, Revlog[] Revlogs);
public record Revlog(DateOnly Date, int Ease, DateOnly? Due);
