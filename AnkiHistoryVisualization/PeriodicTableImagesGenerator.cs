using System.Drawing;

namespace AnkiHistoryVisualization;

public class PeriodicTableImagesGenerator(Position[] positions) : BaseImageGenerator<object?>(framesPerDay: 4, colorBackground)
{
    private static readonly Font fontNumber = new("Verdana", 8, FontStyle.Bold);
    private static readonly Font fontName = new("Verdana", 8, FontStyle.Bold);

    private static readonly Color[] colors = [Color.Red, Color.Blue, Color.Green, Color.Yellow];

    private static readonly Color colorStabilityMax = Color.Magenta;
    private static readonly Color colorBackground = Color.FromArgb(20, 20, 20);
    private static readonly Color colorCell = Color.FromArgb(60, 60, 60);
    private static readonly Brush brushShadow = Brushes.Black;
    private static readonly Brush brushText = Brushes.White;
    private static readonly Pen penPercent = Pens.White;
    private static readonly Pen penBorder = Pens.White;
    private static readonly Pen penOutline = new(colorCell, 2);

    private const int requiredStability = 90;
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

    protected override Size CalculateImageSize(object? context)
        => new(
                margin + (positions.Max(a => a.X) * (gap + boxSize)),
                offsetY + margin + (positions.Max(a => a.Y) * (gap + boxSize)) + bottomGap
            );

    protected override object? CreateContext(Note[] notes) => null;

    protected override void DrawImage(Graphics g, Note[] notes, object? context, DateOnly minDate, DateOnly date, float fraction)
    {
        foreach (var pos in positions)
        {
            var x = ((pos.X - 1) * (boxSize + gap)) + margin;
            var y = ((pos.Y - 1) * (boxSize + gap)) + offsetY + (pos.Y > 7 ? bottomGap : 0);

            var cell = new RectangleF(x, y, boxSize, boxSize);

            g.FillRectangle(brushShadow, x + 3, y + 3, boxSize, boxSize);

            var card = notes.FirstOrDefault(a => a.Text == pos.Name)?.Cards.FirstOrDefault();

            if (card is not null)
            {
                DrawCard(g, minDate, date, fraction, cell, card, requiredStability);
            }
            else
            {
                // not yet studied elements
                g.FillRectangle(new SolidBrush(colorCell), cell);
            }

            DrawBox(g, x, y, pos.Number, pos.Name);
        }
    }

    protected override void DrawReview(Graphics g, float fraction, RectangleF cell, Revlog revlog, float percentStability)
    {
        var colorStability = ColorUtils.Blend(colorCell, colorStabilityMax, percentStability);

        var revlogColor = colors[revlog.Ease - 1];
        var color = ColorUtils.Blend(revlogColor, colorStability, fraction);

        // review on this day
        g.FillRectangle(new SolidBrush(color), cell);
    }

    protected override void DrawStability(Graphics g, RectangleF cell, float percentStability)
    {
        var colorStability = ColorUtils.Blend(colorCell, colorStabilityMax, percentStability);
        g.FillRectangle(new SolidBrush(colorStability), cell);
    }

    protected override void DrawPercent(Graphics g, RectangleF cell, float percent)
    {
        g.DrawLine(penPercent, cell.Left + 2, cell.Bottom - 2, cell.Left + 2 + (percent * (cell.Width - 4)), cell.Bottom - 2);
    }

    private static void DrawBox(Graphics g, float x, float y, int number, string name)
    {
        g.DrawStringOutlined($"{number}", fontNumber, brushText, penOutline, new RectangleF(x, y + 1, boxSize, (boxSize / 2) - 1), stringFormatCenter);
        g.DrawStringOutlined(name, fontName, brushText, penOutline, new RectangleF(x, y + (boxSize / 2), boxSize, (boxSize / 2) - 3), stringFormatCenter);
        g.DrawRectangle(penBorder, x, y, boxSize, boxSize);
    }
}

public record Position(int Number, string Name, int X, int Y);
