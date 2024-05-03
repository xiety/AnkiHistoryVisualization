using System.Drawing;

namespace AnkiHistoryVisualization;

public static class PeriodicTableImagesGenerator
{
    private static readonly Font fontNumber = new("Verdana", 8, FontStyle.Bold);
    private static readonly Font fontName = new("Verdana", 8, FontStyle.Bold);
    private static readonly Font fontTitle = new("Arial", 6);

    private static readonly Color[] colors = [Color.Red, Color.Blue, Color.Green, Color.Yellow];

    private static readonly Color colorStabilityMax = Color.Magenta;
    private static readonly Color colorBackground = Color.FromArgb(20, 20, 20);
    private static readonly Color colorCell = Color.FromArgb(60, 60, 60);
    private static readonly Brush brushShadow = Brushes.Black;
    private static readonly Brush brushText = Brushes.White;
    private static readonly Brush brushTitle = Brushes.White;
    private static readonly Pen penPercent = Pens.White;
    private static readonly Pen penBorder = Pens.White;
    private static readonly Pen penOutline = new(colorCell, 2);

    private const int requiredStability = 90;
    private const int framesPerDay = 4;
    private const int offsetY = 14;
    private const int margin = 2;
    private const int boxSize = 30;
    private const int bottomGap = 10;
    private const int gap = 4;

    private static readonly StringFormat stringFormatCenter = new()
    {
        Alignment = StringAlignment.Center,
        LineAlignment = StringAlignment.Center
    };

    public static IEnumerable<Bitmap> Generate(Position[] positions, Note[] notes)
    {
        var (minDate, maxDate) = DeckUtils.GetMinMaxDate(notes);

        var width = margin + (positions.Max(a => a.X) * (gap + boxSize));
        var height = offsetY + margin + (positions.Max(a => a.Y) * (gap + boxSize)) + bottomGap;

        foreach (var date in minDate.EnumerateToInclusive(maxDate))
        {
            foreach (var frameInsideDay in Enumerable.Range(0, framesPerDay))
            {
                var fraction = frameInsideDay / (float)framesPerDay;

                using var bitmap = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
                using var g = Graphics.FromImage(bitmap);

                DrawImage(g, notes, positions, minDate, date, fraction);

                yield return bitmap;
            }

            Console.WriteLine(date);
        }
    }

    private static void DrawImage(Graphics g, Note[] notes, Position[] positions, DateOnly minDate, DateOnly date, float fraction)
    {
        g.Clear(colorBackground);

        g.DrawString($"{date:yyyy.MM.dd}", fontTitle, brushTitle, 1, 1);

        foreach (var pos in positions)
        {
            var x = ((pos.X - 1) * (boxSize + gap)) + margin;
            var y = ((pos.Y - 1) * (boxSize + gap)) + offsetY + (pos.Y > 7 ? bottomGap : 0);

            g.FillRectangle(brushShadow, x + 3, y + 3, boxSize, boxSize);

            var card = notes.FirstOrDefault(a => a.Text == pos.Name)?.Cards.FirstOrDefault();

            if (card is not null)
            {
                var revlog = card.Revlogs.FirstOrDefault(a => a.Date == date);
                var revlogPrev = card.Revlogs.LastOrDefault(a => a.Date <= date);
                var revlogNext = card.Revlogs.FirstOrDefault(a => a.Date > date);

                var percentStability = CalcStabilityPercent(card, revlogPrev, revlogNext);
                var colorStability = ColorUtils.Blend(colorCell, colorStabilityMax, percentStability);

                if (revlog is not null)
                {
                    var revlogColor = colors[revlog.Ease - 1];
                    var color = ColorUtils.Blend(revlogColor, colorStability, fraction);

                    // review on this day
                    g.FillRectangle(new SolidBrush(color), x, y, boxSize, boxSize);
                }
                else
                {
                    // stability if no review
                    g.FillRectangle(new SolidBrush(colorStability), x, y, boxSize, boxSize);
                }

                // progress to next review
                var percent = CalcPercentToNextReview(minDate, date, fraction, card, revlogPrev, revlogNext);
                g.DrawLine(penPercent, x + 2, y + boxSize - 2, x + 2 + (percent * (boxSize - 4)), y + boxSize - 2);
            }
            else
            {
                // not yet studied elements
                g.FillRectangle(new SolidBrush(colorCell), x, y, boxSize, boxSize);
            }

            DrawBox(g, x, y, pos.Number, pos.Name);
        }
    }

    private static void DrawBox(Graphics g, int x, int y, int number, string name)
    {
        g.DrawStringOutlined($"{number}", fontNumber, brushText, penOutline, new RectangleF(x, y + 1, boxSize, (boxSize / 2) - 1), stringFormatCenter);
        g.DrawStringOutlined(name, fontName, brushText, penOutline, new RectangleF(x, y + (boxSize / 2), boxSize, (boxSize / 2) - 3), stringFormatCenter);
        g.DrawRectangle(penBorder, x, y, boxSize, boxSize);
    }

    private static float CalcPercentToNextReview(DateOnly minDate, DateOnly date, float fraction, Card card, Revlog? revlogPrev, Revlog? revlogNext)
    {
        var datePrev = revlogPrev is not null ? revlogPrev.Date : minDate;
        var dateNext = revlogNext is not null ? revlogNext.Date : card.Due;

        var percent = (date.DayNumber - datePrev.DayNumber + fraction) / (float)(dateNext.DayNumber - datePrev.DayNumber);

        return Math.Clamp(percent, 0f, 1f);
    }

    private static float CalcStabilityPercent(Card card, Revlog? revlogPrev, Revlog? revlogNext)
    {
        if (revlogPrev is null)
            return 0;

        var dateNext = revlogNext is not null ? revlogNext.Date : card.Due;
        var stability = dateNext.DayNumber - revlogPrev.Date.DayNumber;

        return Math.Min(requiredStability, stability) / (float)requiredStability;
    }
}

public record Position(int Number, string Name, int X, int Y);
