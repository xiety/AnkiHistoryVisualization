using AnkiDatabase;

using Microsoft.EntityFrameworkCore;

namespace AnkiHistoryVisualization;

public static class DataRetriever
{
    public static Card[] Retrieve(string database_path, string deck_name, int? card_type)
    {
        using var db = new AnkiDbContext(database_path);

        var col = db.Col.First();

        var cards = db.Cards
            .Where(a => a.Deck.Name == deck_name)
            .Where(a => a.Queue != 0 && a.Queue != -1)
            .Where(a => a.Data != "{}")
            .Where(a => (card_type == null || a.Ord == card_type))
            .OrderBy(a => a.Id)
            .Include(a => a.Deck)
            .Include(a => a.Revlogs.Where(a => a.Type != 4))
            .Include(a => a.Note)
            .OrderBy(a => a.Id)
            .ToArray(); //materialize

        var lines = cards
            .Select(card =>
            {
                var fields = card.Note.Flds.Split("\u001f");
                var number = fields[0];
                var full = fields[1];

                var revlogs = card.Revlogs
                    .GroupBy(a => ConvertDateWithNight(a.Id, 4))
                    .Select(a => new Revlog(a.Key, (int)a.MinBy(a => a.Id)!.Ease))
                    .OrderBy(a => a.Date)
                    .ToArray();

                var due = DateOnly.FromDateTime(col.Crt.AddDays(card.Due));

                return new Card(card.Id, number, full, due, revlogs);
            })
            .ToArray();

        return lines;
    }

    private static DateOnly ConvertDateWithNight(DateTime dateTime, int nextDayHour)
    {
        var dateOnly = DateOnly.FromDateTime(dateTime);

        if (dateTime.Hour <= nextDayHour)
            dateOnly = dateOnly.AddDays(-1);

        return dateOnly;
    }
}

public record Card(long CardId, string Number, string Text, DateOnly Due, Revlog[] Revlogs);
public record Revlog(DateOnly Date, int Ease);
