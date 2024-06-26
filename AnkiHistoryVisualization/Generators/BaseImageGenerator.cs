﻿using System.Drawing;

namespace AnkiHistoryVisualization;

public abstract class BaseImageGenerator<TContext>(int framesPerDay, Color colorBackground)
{
    private static readonly Font fontTitle = new("Arial", 6);
    private static readonly Brush brushTitle = Brushes.White;

    protected abstract TContext CreateContext(Note[] notes);
    protected abstract Size CalculateImageSize(TContext context);
    protected abstract void DrawImage(Graphics g, Note[] notes, TContext context, DateOnly minDate, DateOnly date, float fraction);

    public virtual IEnumerable<Bitmap> Generate(Note[] notes)
    {
        var context = CreateContext(notes);

        var imageSize = CalculateImageSize(context);
        imageSize = new(imageSize.Width & ~1, imageSize.Height & ~1);

        var (minDate, maxDate) = DeckUtils.GetMinMaxDate(notes);
        maxDate = DateOnly.FromDateTime(DateTime.Now).AddDays(1);

        foreach (var date in minDate.EnumerateToInclusive(maxDate))
        {
            foreach (var frameInsideDay in Enumerable.Range(0, framesPerDay))
            {
                var fraction = frameInsideDay / (float)framesPerDay;

                using var bitmap = new Bitmap(imageSize.Width, imageSize.Height, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
                using var g = Graphics.FromImage(bitmap);

                g.Clear(colorBackground);
                g.DrawString($"{date:yyyy.MM.dd}", fontTitle, brushTitle, 15, 1);

                DrawImage(g, notes, context, minDate, date, fraction);

                yield return bitmap;
            }

            Console.WriteLine(date);
        }
    }

    protected CalcResults Calculate(DateOnly minDate, DateOnly date, float fraction, Card card)
    {
        var revlog = card.Revlogs.FirstOrDefault(a => a.Date == date);
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

        return new(revlogPrev?.Ease, lastReviewDays, nextDue, stability, percent, duePercent, reviewInDuePercent, revlogPrev is null);
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

public record CalcResults(int? LastReview, int? LastReviewDays, DateOnly? DueDate, int Stability, float Percent, float DuePercent, float ReviewInDuePercent, bool IsNew);
