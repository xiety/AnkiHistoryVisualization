using System.Drawing;
using System.Text.RegularExpressions;

using Dic = System.Collections.Generic.Dictionary<AnkiHistoryVisualization.Note, AnkiHistoryVisualization.WordInfo[]>;

namespace AnkiHistoryVisualization;

public class PoetryWordImageGenerator() : BaseImageGenerator<Dic>(framesPerDay: 10, colorBackground)
{
    private static readonly Font font = new("Verdana", 9);

    private static readonly Color[] colors = [Color.Red, Color.Blue, Color.Green, Color.Yellow];

    private static readonly Color colorStabilityMax = Color.Magenta;
    private static readonly Color colorBackground = Color.FromArgb(0, 0, 0);
    private static readonly Pen penPercent = Pens.White;
    private static readonly Pen penOutline = new(Color.Black, 2);

    private const int requiredStability = 30;
    private const int rowHeight = 16;
    private const int offsetY = 14;
    private const int margin = 2;
    private const int gap = 1;

    protected override Size CalculateImageSize()
        => new(368 + 2, 610 + 2);

    protected override Dic CreateContext(Note[] notes)
        => notes.ToDictionary(a => a, a => ConvertNote(a).ToArray());

    protected override SizeF DrawImage(Graphics g, Note[] notes, Dic context, DateOnly minDate, DateOnly date, float fraction)
    {
        var total = SizeF.Empty;

        var stringFormat = new StringFormat(StringFormat.GenericTypographic);
        stringFormat.FormatFlags |= StringFormatFlags.MeasureTrailingSpaces;

        var y = (float)offsetY;

        foreach (var (note, words) in context)
        {
            var x = (float)margin;

            foreach (var word in words)
            {
                var wordWidth = MathF.Ceiling(g.MeasureSize(word.Text, font, stringFormat).Width) + 5;

                if (word.Cloze > 0)
                {
                    var card = note.Cards.FirstOrDefault(a => a.CardType == word.Cloze - 1);

                    if (card is not null)
                    {
                        DrawCard(g, minDate, date, fraction, new(x, y, wordWidth, rowHeight), card, requiredStability);
                    }
                }

                g.DrawStringOutlined(word.Text, font, Brushes.White, penOutline, new PointF(x + 2, y), stringFormat);

                x += wordWidth;

                if (total.Width < x)
                    total = total with { Width = x };
            }

            y += rowHeight + gap;

            if (total.Height < y)
                total = total with { Height = y };
        }

        return total;
    }

    protected override void DrawReview(Graphics g, float fraction, RectangleF cell, Revlog revlog, float percentStability)
    {
        var colorStability = ColorUtils.Blend(colorBackground, colorStabilityMax, percentStability);
        var revlogColor = colors[revlog.Ease - 1];
        //var color = ColorUtils.Blend(color, colorStability, fraction);
        var color = revlogColor;

        g.FillRectangle(new SolidBrush(color), cell);
    }

    protected override void DrawStability(Graphics g, RectangleF cell, float percentStability)
    {
        var colorStability = ColorUtils.Blend(colorBackground, colorStabilityMax, percentStability);
        g.FillRectangle(new SolidBrush(colorStability), cell);
    }

    protected override void DrawPercent(Graphics g, RectangleF cell, float percent)
    {
        g.DrawLine(penPercent, cell.Left, cell.Bottom, cell.Left + (percent * cell.Width), cell.Bottom);
    }

    private static List<WordInfo> GroupSiblings(List<WordInfo> acc, WordInfo next)
    {
        if (next.Cloze != 0 || !acc.Any())
        {
            acc.Add(next);
        }
        else
        {
            var last = acc.Last();
            acc[^1] = new (last.Cloze, last.Text + next.Text);
        }

        return acc;
    }

    private static List<WordInfo> ConvertNote(Note note)
        => ParseNote(note).Aggregate(new List<WordInfo>(), GroupSiblings);

    private static IEnumerable<WordInfo> ParseNote(Note note)
    {
        var matchDiv = Regex.Match(note.Text, @"<div[^>]*>\s*\d+\.\s*(.*?)<\/div>(?!.*<div)", RegexOptions.Singleline | RegexOptions.RightToLeft);
        var text = matchDiv.Groups[1].Value;

        var parsedElements = new List<(int, string)>();
        var matches = Regex.Matches(text, @"(?<cloze>\{\{c(?<number>\d+)::(?<text>.*?)\}\})|(?<plainText>[^{}]+)");

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

public record WordInfo(int Cloze, string Text);
