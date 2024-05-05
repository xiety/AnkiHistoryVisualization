using System.Drawing;
using System.Text.RegularExpressions;

using Dic = System.Collections.Generic.Dictionary<AnkiHistoryVisualization.Note, AnkiHistoryVisualization.WordInfo[]>;

namespace AnkiHistoryVisualization;

public class PoetryWordImageGenerator(int columns) : BaseImageGenerator<PoetryWordContext>(framesPerDay: 8, colorBackground)
{
    private static readonly Font font = new("Verdana", 9);

    private static readonly Color[] colors = [Color.Red, Color.Blue, Color.Green, Color.Yellow];

    private static readonly Color colorStabilityMax = Color.Magenta;
    private static readonly Color colorBackground = Color.FromArgb(0, 0, 0);
    private static readonly Pen penPercent = Pens.White;
    private static readonly Pen penOutline = new(Color.Black, 2);

    private const int requiredStability = 60;
    private const int rowHeight = 16;
    private const int offsetY = 12;
    private const int margin = 2;
    private const int gap = 1;
    private const int columnGap = 2;

    protected override Size CalculateImageSize(PoetryWordContext context)
        => context.ImageSize;

    protected override PoetryWordContext CreateContext(Note[] notes)
    {
        var converted = notes.ToDictionary(a => a, a => ConvertNote(a).ToArray());

        var stringFormat = new StringFormat(StringFormat.GenericTypographic);
        stringFormat.FormatFlags |= StringFormatFlags.MeasureTrailingSpaces;

        var columnWidth = 0;
        var rows = (converted.Count + columns - 1) / columns;
        var widths = new Dictionary<string, int>();
        var positions = new Dictionary<(Note, int), Rectangle>();

        using var bitmap = new Bitmap(1, 1, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);

        using (var g = Graphics.FromImage(bitmap))
        {
            foreach (var (note, words) in converted)
            {
                var x = 0;

                foreach (var word in words)
                {
                    if (!widths.TryGetValue(word.Text, out var wordWidth))
                    {
                        wordWidth = Measure(g, stringFormat, word.Text);
                        widths.Add(word.Text, wordWidth);
                    }

                    x += wordWidth;

                    if (columnWidth < x)
                        columnWidth = x;
                }
            }
        }

        var total = new Size(margin * 2 + columns * (columnWidth + columnGap), offsetY + margin * 2 + rows * (rowHeight + gap));

        var bitmapText = new Bitmap(total.Width, total.Height, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);

        using (var g = Graphics.FromImage(bitmapText))
        {
            var index = 0;

            foreach (var (note, words) in converted)
            {
                var row = index % rows;
                var column = index / rows;

                var x = margin + column * (columnWidth + columnGap);
                var y = offsetY + row * (rowHeight + gap);

                var wordIndex = 0;

                foreach (var word in words)
                {
                    var wordWidth = widths[word.Text];

                    g.DrawStringOutlined(word.Text, font, Brushes.White, penOutline, new PointF(x + 2, y), stringFormat);

                    positions.Add((note, wordIndex), new(x, y, wordWidth - 1, rowHeight));

                    x += wordWidth;

                    wordIndex++;
                }

                index++;
            }
        }

        return new(total, columnWidth, converted, positions, bitmapText);
    }

    protected override void DrawImage(Graphics g, Note[] notes, PoetryWordContext context, DateOnly minDate, DateOnly date, float fraction)
    {
        var stringFormat = new StringFormat(StringFormat.GenericTypographic);
        stringFormat.FormatFlags |= StringFormatFlags.MeasureTrailingSpaces;

        var index = 0;

        foreach (var (note, words) in context.Converted)
        {
            var wordIndex = 0;

            foreach (var word in words)
            {
                var cell = context.Positions[(note, wordIndex)];

                if (word.Cloze > 0)
                {
                    var card = note.Cards.FirstOrDefault(a => a.CardType == word.Cloze - 1);

                    if (card is not null)
                    {
                        DrawCard(g, context, minDate, date, fraction, cell, note, card, requiredStability);
                        DrawCardPercent(g, context, minDate, date, fraction, cell, note, card, requiredStability);
                    }
                }

                wordIndex++;
            }

            index++;
        }

        g.DrawImage(context.Image, 0, 0);
    }

    private static int Measure(Graphics g, StringFormat stringFormat, string text)
        => (int)MathF.Ceiling(g.MeasureSize(text, font, stringFormat).Width) + 5;

    protected override void DrawReview(Graphics g, PoetryWordContext context, Note note, Card card, float fraction, RectangleF cell, Revlog revlog, float percentStability)
    {
        var colorStability = ColorUtils.Blend(colorBackground, colorStabilityMax, percentStability);
        var revlogColor = colors[revlog.Ease - 1];
        //var color = ColorUtils.Blend(color, colorStability, fraction);
        var color = revlogColor;

        g.FillRectangle(new SolidBrush(color), cell);
    }

    protected override void DrawStability(Graphics g, PoetryWordContext context, Note note, Card card, RectangleF cell, float percentStability)
    {
        var colorStability = ColorUtils.Blend(colorBackground, colorStabilityMax, percentStability);
        g.FillRectangle(new SolidBrush(colorStability), cell);
    }

    protected override void DrawPercent(Graphics g, PoetryWordContext context, Note note, Card card, RectangleF cell, int stabilityDays, float percent, bool isNew)
    {
        g.DrawLine(penPercent, cell.Left, cell.Bottom, cell.Left + (percent * cell.Width), cell.Bottom);
    }

    private static List<WordInfo> GroupSiblings(List<WordInfo> acc, WordInfo item)
    {
        var last = acc.LastOrDefault();

        if (last is not null && last.Cloze == 0)
            acc[^1] = new(item.Cloze, last.Text + item.Text);
        else if (item.Cloze != 0 || acc.Count == 0)
            acc.Add(item);
        else if (last is not null)
            acc[^1] = new(last.Cloze, last.Text + item.Text);

        return acc;
    }

    private static List<WordInfo> ConvertNote(Note note)
        => ParseNote(note).Aggregate(new List<WordInfo>(), GroupSiblings);

    private static IEnumerable<WordInfo> ParseNote(Note note)
    {
        var matchDiv = Regs.RegexDiv().Match(note.Text);
        var text = matchDiv.Groups[1].Value;

        var parsedElements = new List<(int, string)>();
        var matches = Regs.RegexCloze().Matches(text);

        foreach (var match in matches.Cast<Match>())
        {
            if (match.Groups["cloze"].Success)
            {
                var number = int.Parse(match.Groups["number"].Value);
                var word = match.Groups["text"].Value;
                yield return new WordInfo(number, word);
            }
            else if (match.Groups["plainText"].Success)
            {
                var word = match.Groups["plainText"].Value;
                yield return new WordInfo(0, word);
            }
        }
    }
}

public static partial class Regs
{
    [GeneratedRegex(@"<div[^>]*>\s*\d+\.\s*(.*?)<\/div>(?!.*<div)", RegexOptions.Singleline | RegexOptions.RightToLeft)]
    public static partial Regex RegexDiv();

    [GeneratedRegex(@"(?<cloze>\{\{c(?<number>\d+)::(?<text>.*?)\}\})|(?<plainText>[^{}]+)")]
    public static partial Regex RegexCloze();
}

public record WordInfo(int Cloze, string Text);
public record PoetryWordContext(Size ImageSize, int ColumnWidth, Dic Converted, Dictionary<(Note, int), Rectangle> Positions, Bitmap Image);
