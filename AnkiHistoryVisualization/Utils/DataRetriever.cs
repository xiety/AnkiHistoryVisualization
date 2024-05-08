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
                                                     && a.Queue != 0 && a.Queue != -1
                                                     && a.Data != "{}"
                                                     && (cardType == null || a.Ord == cardType);

        var query = db.Notes
            .Include(a => a.Cards.AsQueryable().Where(filter).OrderBy(b => b.Ord))
            .ThenInclude(a => a.Deck)
            .Include(a => a.Cards)
            .ThenInclude(a => a.Revlogs.Where(b => b.Type != 4))
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
                    var due = DateOnly.FromDateTime(col.Crt.AddDays(card.Due));

                    var data = JsonSerializer.Deserialize<DataInfo>(card.Data, jsonOptions)!;
                    var difficulty = (data.D - 1.0f) / 9.0f;

                    var revlogs = card.Revlogs
                        .GroupBy(a => ConvertDateWithNight(a.Id, 4))
                        .Select(a => new Revlog(a.Key, a.MinBy(a => a.Id)!.Ease))
                        .OrderBy(a => a.Date)
                        .ToArray();

                    return new Card(card.Id, card.Ord, due, difficulty, revlogs);
                }).ToArray();

                return new Note(note.Id, number, full, cards);
            })
            .ToArray();

        return data;
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
public record Revlog(DateOnly Date, int Ease);
