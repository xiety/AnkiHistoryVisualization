using System.Drawing;

namespace AnkiHistoryVisualization;

public class TranslateImageGenerator() : BaseImageGenerator<TranslateContext>(framesPerDay: 4, colorBackground)
{
    private static readonly Font font = new("Verdana", 6);

    private static readonly Color[] colors = [Color.Red, Color.Blue, Color.Green, Color.Yellow];

    private static readonly Color colorStabilityMax = Color.Magenta;
    private static readonly Color colorBackground = Color.FromArgb(0, 0, 0);
    private static readonly Color colorCell = Color.FromArgb(60, 60, 60);
    private static readonly Pen penPercent = Pens.White;
    private static readonly Pen penOutline = new(colorCell, 2);

    private const int requiredStability = 30;
    private const int rowHeight = 10;
    private const int offsetY = 14;
    private const int margin = 2;
    private const int gap = 1;
    private const int imageWidth = 800;

    protected override Size CalculateImageSize(TranslateContext context)
        => context.ImageSize;

    protected override TranslateContext CreateContext(Note[] notes)
    {
        var stringFormat = new StringFormat(StringFormat.GenericTypographic);
        stringFormat.FormatFlags |= StringFormatFlags.MeasureTrailingSpaces;

        var positions = new Dictionary<string, Rectangle>();

        using var bitmap = new Bitmap(1, 1, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);

        var y = offsetY;

        using (var g = Graphics.FromImage(bitmap))
        {
            var x = margin;

            foreach (var note in notes.OrderBy(a => a.Cards[0].Revlogs[0].Date))
            {
                var wordWidth = Measure(g, stringFormat, note.Number);

                if (x + wordWidth >= imageWidth - margin)
                {
                    x = margin;
                    y += rowHeight + gap;
                }

                positions.Add(note.Number, new Rectangle(x, y, wordWidth - 1, rowHeight));

                x += wordWidth;
            }
        }

        var total = new Size(imageWidth, positions.Max(a => a.Value.Bottom) + margin * 2);

        var bitmapText = new Bitmap(total.Width, total.Height, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);

        using (var g = Graphics.FromImage(bitmapText))
        {
            foreach (var note in notes)
            {
                var cell = positions[note.Number];
                g.DrawStringOutlined(note.Number, font, Brushes.White, penOutline, new PointF(cell.Left + 1, cell.Top - 1), stringFormat);
            }
        }

        return new(total, positions, bitmapText);
    }

    protected override void DrawImage(Graphics g, Note[] notes, TranslateContext context, DateOnly minDate, DateOnly date, float fraction)
    {
        var stringFormat = new StringFormat(StringFormat.GenericTypographic);
        stringFormat.FormatFlags |= StringFormatFlags.MeasureTrailingSpaces;

        var index = 0;

        foreach (var note in notes)
        {
            var cell = context.Positions[note.Number];

            var card = note.Cards.FirstOrDefault();

            if (card is not null)
            {
                DrawCard(g, context, minDate, date, fraction, cell, note, card, requiredStability);
                DrawCardPercent(g, context, minDate, date, fraction, cell, note, card, requiredStability);
            }

            index++;
        }

        g.DrawImage(context.Image, 0, 0);
    }

    private static int Measure(Graphics g, StringFormat stringFormat, string text)
        => (int)MathF.Ceiling(g.MeasureSize(text, font, stringFormat).Width) + 5;

    protected override void DrawReview(Graphics g, TranslateContext context, Note note, Card card, float fraction, RectangleF cell, Revlog revlog, float percentStability)
    {
        var colorStability = ColorUtils.Blend(colorCell, colorStabilityMax, percentStability);
        var revlogColor = colors[revlog.Ease - 1];
        //var color = ColorUtils.Blend(color, colorStability, fraction);
        var color = revlogColor;

        g.FillRectangle(new SolidBrush(color), cell);
    }

    protected override void DrawStability(Graphics g, TranslateContext context, Note note, Card card, RectangleF cell, float percentStability)
    {
        var colorStability = ColorUtils.Blend(colorCell, colorStabilityMax, percentStability);
        g.FillRectangle(new SolidBrush(colorStability), cell);
    }

    protected override void DrawPercent(Graphics g, TranslateContext context, Note note, Card card, RectangleF cell, int stabilityDays, float percent, bool isNew)
    {
        g.DrawLine(penPercent, cell.Left, cell.Bottom, cell.Left + (percent * cell.Width), cell.Bottom);
    }
}

public record TranslateContext(Size ImageSize, Dictionary<string, Rectangle> Positions, Bitmap Image);
