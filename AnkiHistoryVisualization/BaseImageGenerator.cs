using System.Drawing;

namespace AnkiHistoryVisualization;

public abstract class BaseImageGenerator<TContext>(int framesPerDay, Color colorBackground)
{
    private static readonly Font fontTitle = new("Arial", 6);
    private static readonly Brush brushTitle = Brushes.White;

    protected abstract Size CalculateImageSize();
    protected abstract SizeF DrawImage(Graphics g, Note[] notes, TContext context, DateOnly minDate, DateOnly date, float fraction);
    protected abstract TContext CreateContext(Note[] notes);

    protected abstract void DrawReview(Graphics g, float fraction, RectangleF cell, Revlog revlog, float percentStability);
    protected abstract void DrawStability(Graphics g, RectangleF cell, float percentStability);
    protected abstract void DrawPercent(Graphics g, RectangleF cell, float percent);

    public virtual IEnumerable<Bitmap> Generate(Note[] notes)
    {
        var context = CreateContext(notes);

        var imageSize = CalculateImageSize();

        imageSize = new(imageSize.Width & ~1, imageSize.Height & ~1);

        var total = SizeF.Empty;

        var (minDate, maxDate) = DeckUtils.GetMinMaxDate(notes);

        foreach (var date in minDate.EnumerateToInclusive(maxDate))
        {
            foreach (var frameInsideDay in Enumerable.Range(0, framesPerDay))
            {
                var fraction = frameInsideDay / (float)framesPerDay;

                using var bitmap = new Bitmap(imageSize.Width, imageSize.Height, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
                using var g = Graphics.FromImage(bitmap);

                g.Clear(colorBackground);
                g.DrawString($"{date:yyyy.MM.dd}", fontTitle, brushTitle, 15, 1);

                var size = DrawImage(g, notes, context, minDate, date, fraction);

                total = new SizeF(MathF.Max(total.Width, size.Width), MathF.Max(total.Height, size.Height));

                yield return bitmap;
            }

            Console.WriteLine(date);
        }

        Console.WriteLine(total);
    }

    protected void DrawCard(Graphics g, DateOnly minDate, DateOnly date, float fraction, RectangleF cell, Card card, int requiredStability)
    {
        var revlog = card.Revlogs.FirstOrDefault(a => a.Date == date);
        var revlogPrev = card.Revlogs.LastOrDefault(a => a.Date <= date);
        var revlogNext = card.Revlogs.FirstOrDefault(a => a.Date > date);

        var percentStability = CalcStabilityPercent(card, revlogPrev, revlogNext, requiredStability);

        if (revlog is not null)
            DrawReview(g, fraction, cell, revlog, percentStability);
        else
            DrawStability(g, cell, percentStability);

        var percent = CalcPercentToNextReview(minDate, date, fraction, card, revlogPrev, revlogNext);

        DrawPercent(g, cell, percent);
    }

    protected static float CalcPercentToNextReview(DateOnly minDate, DateOnly date, float fraction, Card card, Revlog? revlogPrev, Revlog? revlogNext)
    {
        var datePrev = revlogPrev is not null ? revlogPrev.Date : minDate;
        var dateNext = revlogNext is not null ? revlogNext.Date : card.Due;

        var percent = (date.DayNumber - datePrev.DayNumber + fraction) / (float)(dateNext.DayNumber - datePrev.DayNumber);

        return Math.Clamp(percent, 0f, 1f);
    }

    protected float CalcStabilityPercent(Card card, Revlog? revlogPrev, Revlog? revlogNext, int requiredStability)
    {
        if (revlogPrev is null)
            return 0;

        var dateNext = revlogNext is not null ? revlogNext.Date : card.Due;
        var stability = dateNext.DayNumber - revlogPrev.Date.DayNumber;

        return Math.Min(requiredStability, stability) / (float)requiredStability;
    }
}
