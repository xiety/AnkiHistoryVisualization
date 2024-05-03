namespace AnkiHistoryVisualization;

public static class DeckUtils
{
    public static (DateOnly, DateOnly) GetMinMaxDate(Note[] notes)
    {
        var minDate = notes.SelectMany(a => a.Cards.SelectMany(b => b.Revlogs.Select(c => c.Date))).Min();
        var maxDate = notes.SelectMany(a => a.Cards.SelectMany(b => b.Revlogs.Select(c => c.Date))).Max();
        return (minDate, maxDate);
    }
}
