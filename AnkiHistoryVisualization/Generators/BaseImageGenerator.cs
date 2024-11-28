using System.Drawing;
using System.Globalization;

namespace AnkiHistoryVisualization;

public record GenerationParameters
{
    public int AddDays { get; init; } = 0;
    public int DateOffset { get; init; } = 15;
    public int FramesPerDay { get; init; } = 4;
    public Color ColorBackground { get; init; } = Color.Black;
}

public abstract class BaseImageGenerator<TContext>(GenerationParameters parameters)
{
    private static readonly Font fontTitle = new("Arial", 12, FontStyle.Bold);
    private static readonly Brush brushTitle = Brushes.White;

    protected abstract TContext CreateContext(Note[] notes);
    protected abstract Size CalculateImageSize(TContext context);
    protected abstract void DrawImage(Graphics g, Note[] notes, TContext context, DateOnly minDate, DateOnly date, float fraction);

    public virtual IEnumerable<Bitmap> Generate(Note[] notes)
    {
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;

        var context = CreateContext(notes);

        var imageSize = CalculateImageSize(context);
        imageSize = new(imageSize.Width & ~1, imageSize.Height & ~1);

        var (minDate, maxDate) = DeckUtils.GetMinMaxDate(notes);
        maxDate = DateOnly.FromDateTime(DateTime.Now).AddDays(parameters.AddDays);

        foreach (var date in minDate.EnumerateToInclusive(maxDate))
        {
            foreach (var frameInsideDay in Enumerable.Range(0, parameters.FramesPerDay))
            {
                var fraction = frameInsideDay / (float)parameters.FramesPerDay;

                using var bitmap = new Bitmap(imageSize.Width, imageSize.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                using var g = Graphics.FromImage(bitmap);

                g.Clear(parameters.ColorBackground);

                DrawImage(g, notes, context, minDate, date, fraction);

                g.DrawString($"{date:yyyy.MM.dd}", fontTitle, brushTitle, parameters.DateOffset, 1);

                yield return bitmap;
            }

            Console.WriteLine(date);
        }
    }

    protected CalcResults Calculate(DateOnly minDate, DateOnly date, float fraction, Card card)
    {
        var revlogPrev = card.Revlogs.LastOrDefault(a => a.Date <= date);
        var revlogNext = card.Revlogs.FirstOrDefault(a => a.Date > date);

        var stability = CalcStability(card, revlogPrev, revlogNext);

        var revlogPrevDate = revlogPrev?.Date ?? minDate;
        var revlogNextDate = revlogNext?.Date ?? card.Due;
        var nextDue = revlogNext?.Due ?? revlogNextDate;

        var percent = CalcPercentToNextReview(date, fraction, revlogPrevDate, revlogNextDate);
        var duePercent = CalcPercentToNextReview(date, fraction, revlogPrevDate, nextDue);
        var reviewInDuePercent = CalcPercentToNextReview(revlogNextDate, 0, revlogPrevDate, nextDue);

        var lastReviewDays = revlogPrev is not null ? date.DayNumber - revlogPrev.Date.DayNumber : (int?)null;

        return new(revlogPrev?.Ease, revlogPrevDate, revlogNextDate, lastReviewDays, nextDue, stability, percent, duePercent, reviewInDuePercent, revlogPrev is null);
    }

    protected static float CalcPercentToNextReview(DateOnly date, float fraction, DateOnly revlogPrevDate, DateOnly revlogNextDate)
    {
        var percent = (date.DayNumber - revlogPrevDate.DayNumber + fraction) / (float)(revlogNextDate.DayNumber - revlogPrevDate.DayNumber);
        return Math.Clamp(percent, 0f, 1f);
    }

    protected static int CalcStability(Card card, Revlog? revlogPrev, Revlog? revlogNext)
    {
        if (revlogPrev is null)
            return 0;

        var datePrev = revlogPrev.Date;
        var dateNext = revlogNext is not null ? revlogNext.Date : card.Due;

        return dateNext.DayNumber - datePrev.DayNumber;
    }
}

public record CalcResults(
    int? LastReview,
    DateOnly RevlogPrevDate,
    DateOnly RevlogNextDate,
    int? LastReviewDays,
    DateOnly? DueDate,
    int Stability,
    float Percent,
    float DuePercent,
    float ReviewInDuePercent,
    bool IsNew);
