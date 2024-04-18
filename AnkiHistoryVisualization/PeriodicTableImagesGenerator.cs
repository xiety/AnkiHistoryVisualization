using System.Drawing;

namespace AnkiHistoryVisualization;

public static class PeriodicTableImagesGenerator
{
    private static readonly Font font_number = new("Arial", 8);
    private static readonly Font font_name = new("Arial", 8);
    private static readonly Font font_title = new("Arial", 6);

    private static readonly Color[] colors = [Color.Red, Color.Blue, Color.Green, Color.Yellow];

    private static readonly Color color_stability = Color.Magenta;
    private static readonly Color color_background = Color.FromArgb(20, 20, 20);
    private static readonly Color color_cell = Color.FromArgb(60, 60, 60);
    private static readonly Brush brush_shadow = Brushes.Black;
    private static readonly Brush brush_text = Brushes.White;
    private static readonly Brush brush_title = Brushes.White;
    private static readonly Pen pen_percent = Pens.White;
    private static readonly Pen pen_border = Pens.White;

    private static readonly int required_stability = 90;
    private static readonly int frames_per_day = 4;
    private static readonly int offset_y = 14;
    private static readonly int margin = 2;
    private static readonly int box_size = 30;
    private static readonly int bottom_gap = 10;
    private static readonly int gap = 4;

    private static readonly StringFormat stringFormatCenter = new()
    {
        Alignment = StringAlignment.Center,
        LineAlignment = StringAlignment.Center
    };

    public static IEnumerable<Bitmap> Generate(Position[] positions, Card[] lines)
    {
        var min_date = lines.SelectMany(a => a.Revlogs.Select(b => b.Date)).Min();
        var max_date = lines.SelectMany(a => a.Revlogs.Select(b => b.Date)).Max();

        var width = margin + (positions.Max(a => a.X) * (gap + box_size));
        var height = offset_y + margin + (positions.Max(a => a.Y) * (gap + box_size)) + bottom_gap;

        foreach (var date in min_date.EnumerateToInclusive(max_date))
        {
            foreach (var frame_inside_day in Enumerable.Range(0, frames_per_day))
            {
                var fraction = frame_inside_day / (float)frames_per_day;

                using var bitmap = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
                using var g = Graphics.FromImage(bitmap);

                DrawImage(g, lines, positions, min_date, date, fraction);

                yield return bitmap;
            }

            Console.WriteLine(date);
        }
    }

    private static void DrawImage(Graphics g, Card[] lines, Position[] positions, DateOnly min_date, DateOnly date, float fraction)
    {
        g.Clear(color_background);

        g.DrawString($"{date:yyyy.MM.dd}", font_title, brush_title, 1, 1);

        foreach (var pos in positions)
        {
            var x = ((pos.X - 1) * (box_size + gap)) + margin;
            var y = ((pos.Y - 1) * (box_size + gap)) + offset_y + (pos.Y > 7 ? bottom_gap : 0);

            g.FillRectangle(brush_shadow, x + 3, y + 3, box_size, box_size);

            var card = lines.FirstOrDefault(a => a.Text == pos.Name);

            if (card is not null)
            {
                var revlog = card.Revlogs.Where(a => a.Date == date).FirstOrDefault();
                var prev_revlog_or_null = card.Revlogs.Where(a => a.Date <= date).LastOrDefault()?.Date;
                var next_revlog_or_null = card.Revlogs.Where(a => a.Date > date).FirstOrDefault()?.Date;

                var color_stability = CalcCurrentStabilityColor(card, prev_revlog_or_null, next_revlog_or_null);

                if (revlog is not null)
                {
                    var color = colors[revlog.Ease - 1];
                    var faded = ColorUtils.Blend(color, color_stability, fraction);
                    var current_brush = new SolidBrush(faded);

                    // review on this day
                    g.FillRectangle(current_brush, x, y, box_size, box_size);
                }
                else
                {
                    // stability if no review
                    g.FillRectangle(new SolidBrush(color_stability), x, y, box_size, box_size);
                }

                // progress to next review
                var percent = CalcPercent(min_date, date, fraction, card, prev_revlog_or_null, next_revlog_or_null);
                g.DrawLine(pen_percent, x + 2, y + box_size - 2, x + 2 + (percent * (box_size - 4)), y + box_size - 2);
            }
            else
            {
                // not yet studied elements
                g.FillRectangle(new SolidBrush(color_cell), x, y, box_size, box_size);
            }

            DrawCell(g, x, y, pos.Number, pos.Name);
        }
    }

    private static void DrawCell(Graphics g, int x, int y, int number, string name)
    {
        g.DrawString($"{number}", font_number, brush_text, new RectangleF(x, y + 1, box_size, (box_size / 2) - 1), stringFormatCenter);
        g.DrawString(name, font_name, brush_text, new RectangleF(x, y + (box_size / 2), box_size, (box_size / 2) - 3), stringFormatCenter);
        g.DrawRectangle(pen_border, x, y, box_size, box_size);
    }

    private static float CalcPercent(DateOnly min_date, DateOnly date, float fraction, Card card, DateOnly? prev_revlog_or_null, DateOnly? next_revlog_or_null)
    {
        var percent = 0.0f;

        if (prev_revlog_or_null is DateOnly prev_revlog)
        {
            percent = next_revlog_or_null is DateOnly next_revlog
                ? (float)(date.DayNumber - prev_revlog.DayNumber + fraction) / (next_revlog.DayNumber - prev_revlog.DayNumber)
                : (float)(date.DayNumber - prev_revlog.DayNumber + fraction) / (card.Due.DayNumber - prev_revlog.DayNumber);
        }
        else if (next_revlog_or_null is DateOnly next_revlog)
        {
            percent = (float)(date.DayNumber - min_date.DayNumber + fraction) / (next_revlog.DayNumber - min_date.DayNumber);
        }

        if (percent > 1f)
            percent = 1f;

        return percent;
    }

    private static Color CalcCurrentStabilityColor(Card card, DateOnly? prev_revlog_or_null, DateOnly? next_revlog_or_null)
    {
        var current_stability = 0;

        if (prev_revlog_or_null is DateOnly prev_revlog)
        {
            current_stability = next_revlog_or_null is DateOnly next_revlog
                ? next_revlog.DayNumber - prev_revlog.DayNumber
                : card.Due.DayNumber - prev_revlog.DayNumber;
        }

        var percent = Math.Min(required_stability, current_stability) / (float)required_stability;

        return ColorUtils.Blend(color_cell, color_stability, percent);
    }
}

public record Position(int Number, string Name, int X, int Y);
