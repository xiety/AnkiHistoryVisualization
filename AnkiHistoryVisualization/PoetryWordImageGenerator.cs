using System.Drawing;
using System.Text.RegularExpressions;

namespace AnkiHistoryVisualization;

public static class PoetryWordImageGenerator
{
    private static readonly Font fontTitle = new("Arial", 6);
    private static readonly Font font = new("Arial", 9);

    private static readonly Color[] colors = [Color.Red, Color.Blue, Color.Green, Color.Yellow];

    private static readonly Color colorStabilityMax = Color.Magenta;
    private static readonly Color colorBackground = Color.FromArgb(0, 0, 0);
    private static readonly Brush brushTitle = Brushes.White;
    private static readonly Pen penPercent = Pens.White;
    private static readonly Pen penOutline = new(Color.Black, 2);

    private const int requiredStability = 30;
    private const int rowHeight = 16;
    private const int framesPerDay = 10;
    private const int offsetY = 14;
    private const int margin = 2;
    private const int gap = 1;

    public static IEnumerable<Bitmap> Generate(Note[] notes)
    {
        var lines = notes.ToDictionary(a => a, a => ConvertNote(a).ToArray());

        var (minDate, maxDate) = DeckUtils.GetMinMaxDate(notes);

        var width = 328 + 2;
        var height = 610 + 2;

        var total = new SizeF(0, 0);

        foreach (var date in minDate.EnumerateToInclusive(maxDate))
        {
            foreach (var frameInsideDay in Enumerable.Range(0, framesPerDay))
            {
                var fraction = frameInsideDay / (float)framesPerDay;

                using var bitmap = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
                using var g = Graphics.FromImage(bitmap);

                var size = DrawImage(g, lines, minDate, date, fraction);

                total = new SizeF(MathF.Max(total.Width, size.Width), MathF.Max(total.Height, size.Height));

                yield return bitmap;
            }

            Console.WriteLine(date);
        }

        Console.WriteLine(total);
    }

    private static SizeF DrawImage(Graphics g, Dictionary<Note, WordInfo[]> notes, DateOnly minDate, DateOnly date, float fraction)
    {
        var stringFormat = new StringFormat(StringFormat.GenericTypographic);
        stringFormat.FormatFlags |= StringFormatFlags.MeasureTrailingSpaces;

        g.Clear(colorBackground);

        g.DrawString($"{date:yyyy.MM.dd}", fontTitle, brushTitle, 15, 1);

        var y = (float)offsetY;

        var total = new SizeF(0, 0);

        foreach (var (note, words) in notes)
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
                        var revlog = card.Revlogs.FirstOrDefault(a => a.Date == date);
                        var revlogPrev = card.Revlogs.LastOrDefault(a => a.Date <= date);
                        var revlogNext = card.Revlogs.FirstOrDefault(a => a.Date > date);

                        var percentStability = CalcStabilityPercent(card, revlogPrev, revlogNext);
                        var colorStability = ColorUtils.Blend(colorBackground, colorStabilityMax, percentStability);

                        if (revlog is not null)
                        {
                            var revlogColor = colors[revlog.Ease - 1];
                            //var color = ColorUtils.Blend(color, colorStability, fraction);
                            var color = revlogColor;

                            // review on this day
                            g.FillRectangle(new SolidBrush(color), x, y, wordWidth, rowHeight);
                        }
                        else
                        {
                            // stability if no review
                            g.FillRectangle(new SolidBrush(colorStability), x, y, wordWidth, rowHeight);
                        }

                        // progress to next review
                        var percent = CalcPercentToNextReview(minDate, date, fraction, card, revlogPrev, revlogNext);
                        g.DrawLine(penPercent, x, y + rowHeight, x + (percent * wordWidth), y + rowHeight);
                    }
                }

                g.DrawStringOutlined(word.Text, font, Brushes.White, penOutline, new PointF(x + 2, y + 1), stringFormat);

                total = new SizeF(MathF.Max(total.Width, x + wordWidth), MathF.Max(total.Height, y + rowHeight));

                x += wordWidth;
            }

            y += rowHeight + gap;
        }

        return total;
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

    private static IEnumerable<WordInfo> ConvertNote(Note note)
        => ParseNote(note).Aggregate(new List<WordInfo>(), (acc, next) =>
        {
            if (next.Cloze != 0 || !acc.Any())
            {
                acc.Add(next);
            }
            else
            {
                var last = acc.Last();
                acc[^1] = new(last.Cloze, last.Text + next.Text);
            }

            return acc;
        });

    private static IEnumerable<WordInfo> ParseNote(Note note)
    {
        //<div class="header">Hamlet, Monologue 1 (1)</div><br><div class="line0">  0. Monologue 1 (Begin)</div><div class="line1">  1. {{c1::To}} {{c2::be}}, {{c3::or}} {{c4::not}} {{c5::to}} {{c6::be}}, {{c7::that}} {{c8::is}} {{c9::the}} {{c10::question}}:</div>
        //<div class="line1">  1. {{c1::To}} {{c2::be}}, {{c3::or}} {{c4::not}} {{c5::to}} {{c6::be}}, {{c7::that}} {{c8::is}} {{c9::the}} {{c10::question}}:</div>

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
