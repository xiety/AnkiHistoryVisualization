//using System.Drawing;

//namespace AnkiHistoryVisualization;

//public static class SquareImagesGenerator
//{
//    private static readonly Font fontNumber = new("Arial", 8);
//    private static readonly Font fontName = new("Arial", 8);
//    private static readonly Font fontTitle = new("Arial", 6);

//    private static readonly Color[] colors = [Color.Red, Color.Blue, Color.Green, Color.Yellow];

//    private static readonly Color colorStabilityMax = Color.Magenta;
//    private static readonly Color colorBackground = Color.FromArgb(20, 20, 20);
//    private static readonly Color colorCell = Color.FromArgb(60, 60, 60);
//    private static readonly Brush brushShadow = Brushes.Black;
//    private static readonly Brush brushText = Brushes.White;
//    private static readonly Brush brushTitle = Brushes.White;
//    private static readonly Pen penPercent = Pens.White;
//    private static readonly Pen penBorder = Pens.White;

//    private const int requiredStability = 90;
//    private const int framesPerDay = 4;
//    private const int offsetY = 14;
//    private const int boxSize = 10;
//    private const int gap = 0;

//    private static readonly StringFormat stringFormatCenter = new()
//    {
//        Alignment = StringAlignment.Center,
//        LineAlignment = StringAlignment.Center
//    };

//    public static IEnumerable<Bitmap> Generate(Card[] cards)
//    {
//        cards = cards.OrderBy(a => a.Revlogs.Min(b => b.Date)).ToArray();

//        var minDate = cards.SelectMany(a => a.Revlogs.Select(b => b.Date)).Min();
//        var maxDate = cards.SelectMany(a => a.Revlogs.Select(b => b.Date)).Max();

//        var numberHorizontal = (int)Math.Ceiling(1.5 * Math.Sqrt(cards.Length));
//        var numberVertical = (int)Math.Ceiling(cards.Length / (float)numberHorizontal);

//        var width = (numberHorizontal * boxSize + (numberHorizontal - 1) * gap + 2) & ~1;
//        var height = (offsetY + numberVertical * boxSize + (numberVertical - 1) * gap + 2) & ~1;

//        foreach (var date in minDate.EnumerateToInclusive(maxDate))
//        {
//            foreach (var frameInsideDay in Enumerable.Range(0, framesPerDay))
//            {
//                var fraction = frameInsideDay / (float)framesPerDay;

//                using var bitmap = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
//                using var g = Graphics.FromImage(bitmap);

//                DrawImage(g, cards, minDate, date, fraction, numberHorizontal);

//                yield return bitmap;
//            }

//            Console.WriteLine(date);
//        }
//    }

//    private static void DrawImage(Graphics g, Card[] cards, DateOnly minDate, DateOnly date, float fraction, int numberHorizontal)
//    {
//        g.Clear(colorBackground);

//        g.DrawString($"{date:yyyy.MM.dd}", fontTitle, brushTitle, 1, 1);

//        var index = 0;

//        foreach (var card in cards)
//        {
//            var x = (index % numberHorizontal) * (boxSize + gap);
//            var y = offsetY + (index / numberHorizontal) * (boxSize + gap);

//            g.FillRectangle(brushShadow, x + 3, y + 3, boxSize, boxSize);

//            var revlog = card.Revlogs.FirstOrDefault(a => a.Date == date);
//            var revlogPrev = card.Revlogs.LastOrDefault(a => a.Date <= date);
//            var revlogNext = card.Revlogs.FirstOrDefault(a => a.Date > date);

//            var percentStability = CalcStabilityPercent(card, revlogPrev, revlogNext);
//            var colorStability = ColorUtils.Blend(colorCell, colorStabilityMax, percentStability);

//            if (revlog is not null)
//            {
//                var color = colors[revlog.Ease - 1];
//                var faded = ColorUtils.Blend(color, colorStability, fraction);

//                // review on this day
//                g.FillRectangle(new SolidBrush(faded), x, y, boxSize, boxSize);
//            }
//            else
//            {
//                // stability if no review
//                g.FillRectangle(new SolidBrush(colorStability), x, y, boxSize, boxSize);
//            }

//            // progress to next review
//            var percent = CalcPercentToNextReview(minDate, date, fraction, card, revlogPrev, revlogNext);
//            g.DrawLine(penPercent, x + 2, y + boxSize - 2, x + 2 + (percent * (boxSize - 4)), y + boxSize - 2);

//            g.DrawRectangle(penBorder, x, y, boxSize, boxSize);

//            index++;
//        }
//    }

//    private static float CalcPercentToNextReview(DateOnly minDate, DateOnly date, float fraction, Card card, Revlog? revlogPrev, Revlog? revlogNext)
//    {
//        var datePrev = revlogPrev is not null ? revlogPrev.Date : minDate;
//        var dateNext = revlogNext is not null ? revlogNext.Date : card.Due;

//        var percent = (date.DayNumber - datePrev.DayNumber + fraction) / (float)(dateNext.DayNumber - datePrev.DayNumber);

//        return Math.Clamp(percent, 0f, 1f);
//    }

//    private static float CalcStabilityPercent(Card card, Revlog? revlogPrev, Revlog? revlogNext)
//    {
//        if (revlogPrev is null)
//            return 0;

//        var dateNext = revlogNext is not null ? revlogNext.Date : card.Due;
//        var stability = dateNext.DayNumber - revlogPrev.Date.DayNumber;

//        return Math.Min(requiredStability, stability) / (float)requiredStability;
//    }
//}
