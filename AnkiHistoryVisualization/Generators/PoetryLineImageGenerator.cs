using System.Drawing;
using System.Text.RegularExpressions;

namespace AnkiHistoryVisualization;

public partial class PoetryLineImageGenerator(int columns, int fontSize) : BaseImageGenerator<PoetryLineContext>(framesPerDay: 4, colorBackground)
{
    private readonly Font font = new("Arial", fontSize, FontStyle.Bold);

    private static readonly Color colorBackground = Color.FromArgb(0, 0, 0);
    private static readonly Brush brushText = new SolidBrush(Color.White);
    private static readonly Pen penPercent = Pens.LightGray;

    private static readonly Color[] colorsReview = [Color.Red, Color.Blue, Color.Green, Color.Yellow];
    private static readonly Color[] colorStabilityMax = [Color.Magenta, Color.Cyan];

    private static readonly int[] requiredStabilities = [60, 365];

    private static readonly Size gap = new(5, 1);

    private const int offsetY = 11;
    private const int margin = 1;
    private const int knowWidth = 5;

    protected override Size CalculateImageSize(PoetryLineContext context)
        => context.ImageSize;

    protected override PoetryLineContext CreateContext(Note[] notes)
    {
        var stringFormat = new StringFormat(StringFormat.GenericTypographic);
        stringFormat.FormatFlags |= StringFormatFlags.MeasureTrailingSpaces;

        using var bitmap = new Bitmap(1, 1, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
        using var g = Graphics.FromImage(bitmap);

        var blockSize = CalcBlockSize(g, notes);

        var rows = (notes.Length + columns - 1) / columns;
        var height = offsetY + margin + rows * (blockSize.Height + gap.Height);
        var width = (blockSize.Width + gap.Width) * columns + margin * 2;

        return new(new(width, height), blockSize, rows);
    }

    private Size CalcBlockSize(Graphics g, Note[] notes)
    {
        var max_x = 0.0f;
        var max_y = 0.0f;

        foreach (var note in notes)
        {
            var text = GetText(note.Text);
            var size = g.MeasureString(text, font);

            if (max_x < size.Width)
                max_x = size.Width;

            if (max_y < size.Height)
                max_y = size.Height;
        }

        return new((int)MathF.Ceiling(max_x), (int)MathF.Ceiling(max_y));
    }

    private static string GetText(string text)
        => Regs.LineRegex().Match(text).Groups[1].Value;

    protected override void DrawImage(Graphics g, Note[] notes, PoetryLineContext context, DateOnly minDate, DateOnly date, float fraction)
    {
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;

        var column = 0;
        var row = 0;

        foreach (var note in notes)
        {
            var card = note.Cards.First();

            var x = margin + column * (context.BlockSize.Width + gap.Width);
            var y = offsetY + row * (context.BlockSize.Height + gap.Height);

            var text = GetText(note.Text);

            var calc = Calculate(minDate, date, fraction, card);

            var colorStability = CalcStabilityColor(calc.Stability);

            if (calc.LastReview is int review && calc.LastReviewDays is 0 or 1)
            {
                var colorReview = colorsReview[review - 1];

                var color = calc.LastReviewDays switch
                {
                    0 => colorReview,
                    1 => ColorUtils.Blend(colorReview, colorBackground, Math.Clamp(fraction, 0f, 1f)),
                    _ => colorStability,
                };

                g.FillRectangle(new SolidBrush(color), x + (knowWidth - 1), y, context.BlockSize.Width - (knowWidth - 1), context.BlockSize.Height);
            }

            g.FillRectangle(new SolidBrush(colorStability), x, y, knowWidth - 1, context.BlockSize.Height);
            g.DrawString(text, font, brushText, knowWidth + x, y - 1);

            var py = y + context.BlockSize.Height - 1;
            g.DrawLine(penPercent, x + knowWidth, py, x + knowWidth + calc.DuePercent * (context.BlockSize.Width - knowWidth), py);
            g.DrawLine(penPercent, x + knowWidth + calc.ReviewInDuePercent * (context.BlockSize.Width - knowWidth), py, x + knowWidth + calc.ReviewInDuePercent * (context.BlockSize.Width - knowWidth), py - 1);

            row++;

            if (row >= context.Rows)
            {
                row = 0;
                column++;
            }
        }
    }

    private static Color CalcStabilityColor(int stability)
    {
        var index = Array.FindIndex(requiredStabilities, req => stability < req);

        var stabilityPercent = (index == -1) ? 1f : stability / (float)requiredStabilities[index];
        var startColor = (index == -1) ? colorStabilityMax[^1] : (index == 0) ? colorBackground : colorStabilityMax[index - 1];
        var endColor = (index == -1) ? colorStabilityMax[^1] : colorStabilityMax[index];

        return ColorUtils.Blend(startColor, endColor, stabilityPercent);
    }

    public static partial class Regs
    {
        [GeneratedRegex(@"{{c1::(.*)}}")]
        public static partial Regex LineRegex();
    }
}

public record PoetryLineContext(Size ImageSize, Size BlockSize, int Rows);
