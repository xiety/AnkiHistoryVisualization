using System.Drawing;
using System.Text.RegularExpressions;

namespace AnkiHistoryVisualization;

public partial class PoetryWordImageGenerator(int columns) : BaseImageGenerator<PoetryWordContext>(framesPerDay: 4, colorBackground)
{
    private static readonly Font font = new("Verdana", 9);

    private static readonly Color[] reviewColors = [Color.Red, Color.DodgerBlue, Color.Green, Color.Yellow];

    private static readonly Color colorStabilityMax = Color.Magenta;
    private static readonly Color colorBackground = Color.FromArgb(0, 0, 0);
    private static readonly Pen penPercent = Pens.White;
    private static readonly Pen penOutline = new(Color.Black, 2);

    private const int requiredStability = 60;
    private const int rowHeight = 15;
    private const int offsetY = 12;
    private const int margin = 2;
    private const int verticalGap = 3;
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

        var index = 0;

        foreach (var (note, words) in converted)
        {
            var row = index % rows;
            var column = index / rows;

            var x = margin + column * (columnWidth + columnGap);
            var y = offsetY + row * (rowHeight + verticalGap);

            var wordIndex = 0;

            foreach (var word in words)
            {
                var wordWidth = widths[word.Text];

                positions.Add((note, wordIndex), new(x, y, wordWidth - 2, rowHeight));

                x += wordWidth;
                wordIndex++;
            }

            index++;
        }

        var total = new Size(
            margin * 2 + columns * (columnWidth + columnGap),
            offsetY + margin * 2 + rows * (rowHeight + verticalGap)
        );

        return new(total, columnWidth, converted, positions);
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
                if (word.Cloze > 0)
                {
                    var cell = context.Positions[(note, wordIndex)];
                    var card = note.Cards.FirstOrDefault(a => a.CardType == word.Cloze - 1);

                    if (card is not null)
                    {
                        var calc = Calculate(minDate, date, fraction, card);

                        DrawReview(g, cell, word.Text, calc, fraction);
                    }
                    else
                    {
                        g.DrawStringOutlined(word.Text, font, new SolidBrush(Color.FromArgb(50, 50, 50)), new(Color.Black, 1), new PointF(cell.X + 2, cell.Y - 1), stringFormat);
                    }
                }

                wordIndex++;
            }

            index++;
        }
    }

    private static int Measure(Graphics g, StringFormat stringFormat, string text)
        => (int)MathF.Ceiling(g.MeasureSize(text, font, stringFormat).Width) + 5;

    protected static void DrawReview(Graphics g, RectangleF cell, string text, CalcResults calc, float fraction)
    {
        var stringFormat = new StringFormat(StringFormat.GenericTypographic);
        stringFormat.FormatFlags |= StringFormatFlags.MeasureTrailingSpaces;

        if (!calc.IsNew)
        {
            var stabilityPercent = Math.Min(calc.Stability, requiredStability) / (float)requiredStability;
            var colorStability = ColorUtils.Blend(colorBackground, colorStabilityMax, stabilityPercent);

            if (calc.LastReview is int review)
            {
                var colorRevlog = reviewColors[review - 1];

                var color = calc.LastReviewDays switch
                {
                    0 => colorRevlog,
                    _ => colorStability,
                };

                g.FillRectangle(new SolidBrush(color), cell);

                g.DrawLine(penPercent, cell.Left, cell.Bottom + 1, cell.Left + (calc.Percent * (cell.Width - 1)), cell.Bottom + 1);
            }

            g.DrawStringOutlined(text, font, Brushes.White, penOutline, new PointF(cell.X + 2, cell.Y - 1), stringFormat);
        }
        else
        {
            g.DrawStringOutlined(text, font, new SolidBrush(Color.FromArgb(50, 50, 50)), penOutline, new PointF(cell.X + 2, cell.Y - 1), stringFormat);
        }
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
        var matchesDiv = Regs.RegexDiv().Matches(note.Text);
        var matchDiv = matchesDiv.Reverse().First(a => !a.Value.Contains("::word}}"));
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

    public static partial class Regs
    {
        [GeneratedRegex(@"<div[^>]*>\s*\d+\.\s*(.*?)<\/div>", RegexOptions.Singleline)]
        public static partial Regex RegexDiv();

        [GeneratedRegex(@"(?<cloze>\{\{c(?<number>\d+)::(?<text>.*?)\}\})|(?<plainText>[^{}]+)")]
        public static partial Regex RegexCloze();
    }
}

public record WordInfo(int Cloze, string Text);
public record PoetryWordContext(Size ImageSize, int ColumnWidth, Dictionary<Note, WordInfo[]> Converted, Dictionary<(Note, int), Rectangle> Positions);
