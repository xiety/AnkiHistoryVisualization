using System.Drawing;
using System.Drawing.Drawing2D;
using System.Xml.Linq;

namespace AnkiHistoryVisualization;

public class MapImageGenerator(MapRegion[] regions) : BaseImageGenerator<MapContext>(framesPerDay: 3, colorBackground)
{
    private static readonly Font font = new("Verdana", 9);

    private static readonly Color[] colorsReview = [Color.Red, Color.DodgerBlue, Color.Green, Color.Yellow];

    private static readonly Color colorCell = Color.FromArgb(60, 60, 60);
    private static readonly Color colorStabilityMax = Color.Magenta;
    private static readonly Color colorBackground = Color.FromArgb(0, 0, 0);
    private static readonly Pen penOutline = new(colorCell, 2);

    private const int requiredStability = 150;

    private static readonly StringFormat stringFormatCenter = new()
    {
        Alignment = StringAlignment.Center,
        LineAlignment = StringAlignment.Center
    };

    protected override Size CalculateImageSize(MapContext context)
        => new(730, (int)(730f * 9f / 16f));

    protected override MapContext CreateContext(Note[] notes)
        => new(regions.ToDictionary(a => a.Name, ConvertNote));

    private GraphicsPath ConvertNote(MapRegion region)
        => SvgPathParser.Parse(region.Path, new(0.75f, 0.75f), new(-50, -10));

    protected override void DrawImage(Graphics g, Note[] notes, MapContext context, DateOnly minDate, DateOnly date, float fraction)
    {
        g.SmoothingMode = SmoothingMode.HighQuality;

        foreach (var region in regions)
        {
            var graphicsPath = context.GraphicsPaths[region.Name];
            var note = notes.FirstOrDefault(a => a.Number == region.Name);

            if (note is not null)
            {
                var card = note.Cards.First();
                var calc = Calculate(minDate, date, fraction, card);

                DrawReview(g, graphicsPath, fraction, calc);
            }
            else
            {
                g.FillPath(new SolidBrush(colorStabilityMax), graphicsPath);
            }

            g.DrawPath(new Pen(Color.White, 1.5f), graphicsPath);
        }

        foreach (var region in regions)
        {
            var graphicsPath = context.GraphicsPaths[region.Name];
            var note = notes.FirstOrDefault(a => a.Number == region.Name);

            if (note is not null)
            {
                var card = note.Cards.First();
                var calc = Calculate(minDate, date, fraction, card);

                DrawName(g, graphicsPath, note, card, date, calc);
            }
        }

        g.SmoothingMode = SmoothingMode.None;

        foreach (var region in regions)
        {
            var graphicsPath = context.GraphicsPaths[region.Name];
            var note = notes.FirstOrDefault(a => a.Number == region.Name);

            if (note is not null)
            {
                var card = note.Cards.First();
                var calc = Calculate(minDate, date, fraction, card);

                DrawPercent(g, graphicsPath, note, calc);
            }
        }
    }

    protected static void DrawReview(Graphics g, GraphicsPath graphicsPath, float fraction, CalcResults calc)
    {
        var stabilityPercent = Math.Min(calc.Stability, requiredStability) / (float)requiredStability;
        var colorStability = ColorUtils.Blend(colorCell, colorStabilityMax, stabilityPercent);

        if (calc.LastReview is int review && calc.LastReviewDays == 0)
        {
            var colorReview = colorsReview[review - 1];

            var antiEpilepsy = Math.Clamp(calc.Stability / 14f, 0.25f, 1f);
            var colorEpilepsy = ColorUtils.Blend(colorStability, colorReview, antiEpilepsy);

            g.FillPath(new SolidBrush(colorEpilepsy), graphicsPath);
        }
        else
        {
            g.FillPath(new SolidBrush(colorStability), graphicsPath);
        }
    }

    protected static void DrawName(Graphics g, GraphicsPath graphicsPath, Note note, Card card, DateOnly date, CalcResults calc)
    {
        if (!calc.IsNew)
        {
            var bounds = graphicsPath.GetBounds();
            var middle = CalcMiddlePoint(note.Number, bounds);
            g.DrawStringOutlined(note.Number, font, Brushes.White, penOutline, middle, stringFormatCenter);
        }
    }

    protected static void DrawPercent(Graphics g, GraphicsPath graphicsPath, Note note, CalcResults calc)
    {
        if (!calc.IsNew)
        {
            var bounds = graphicsPath.GetBounds();
            var middle = CalcMiddlePoint(note.Number, bounds);

            var size = new Size(25, 8);

            g.FillRectangle(new SolidBrush(colorCell), middle.X - size.Width / 2, middle.Y + 8, size.Width, size.Height);

            if (calc.LastReview is int review)
            {
                var colorReview = colorsReview[review - 1];
                g.FillRectangle(new SolidBrush(colorReview), middle.X - size.Width / 2, middle.Y + 8, calc.Percent * (size.Width - 1), size.Height);
                g.DrawRectangle(Pens.LightGray, middle.X - size.Width / 2, middle.Y + 8, calc.Percent * (size.Width - 1), size.Height);
                g.DrawRectangle(Pens.White, middle.X - size.Width / 2, middle.Y + 8, size.Width, size.Height);
            }
        }
    }

    private static Point CalcMiddlePoint(string name, RectangleF bounds)
    {
        var p = new Point((int)(bounds.Left + bounds.Width / 2f), (int)(bounds.Top + bounds.Height / 2f));

        // Manual offsets
        return name switch
        {
            "NH" => p with { X = p.X + 30 },
            "DE" => p with { X = p.X + 27 },
            "RI" => p with { X = p.X + 27 },
            "FL" => p with { X = p.X + 39 },
            "VA" => p with { X = p.X + 30 },
            "MI" => p with { X = p.X + 15 },
            "WI" => p with { X = p.X + 4 },
            "MS" => p with { X = p.X + 2 },
            "MN" => p with { X = p.X - 10 },
            "MA" => p with { X = p.X + 3 },
            "LA" => p with { X = p.X - 13 },
            "CA" => p with { X = p.X - 15 },
            "OK" => p with { X = p.X + 5 },
            "VT" => p with { X = p.X + 2, Y = p.Y - 2 },
            "IL" => p with { Y = p.Y - 10 },
            "NY" => p with { X = p.X - 2 },
            "AK" => p with { X = p.X + 45, Y = p.Y - 15 },
            "CT" => p with { X = p.X + 10, Y = p.Y + 10 },
            "ID" => p with { Y = p.Y + 10 },
            _ => p
        };
    }
}

public static class SvgParser
{
    public static MapRegion[] Parse(string filename)
    {
        var ns = XNamespace.Get(@"http://www.w3.org/2000/svg");

        var doc = XDocument.Load(filename);
        var svg = doc.Element(ns + "svg")!;
        var paths = svg.Elements(ns + "path");

        return paths
            .Select(path => new MapRegion(path.Attribute("data-id")!.Value, path.Attribute("d")!.Value))
            .ToArray();
    }
}

public record MapContext(Dictionary<string, GraphicsPath> GraphicsPaths);
public record MapRegion(string Name, string Path);
