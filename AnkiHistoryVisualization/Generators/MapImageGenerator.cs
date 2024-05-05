using System.Drawing;
using System.Drawing.Drawing2D;
using System.Xml.Linq;

namespace AnkiHistoryVisualization;

public class MapImageGenerator(MapRegion[] regions) : BaseImageGenerator<MapContext>(framesPerDay: 4, colorBackground)
{
    private static readonly Font font = new("Verdana", 9);

    private static readonly Color[] colors = [Color.Red, Color.Blue, Color.Green, Color.Yellow];

    private static readonly Color colorCell = Color.FromArgb(60, 60, 60);
    private static readonly Color colorStabilityMax = Color.Magenta;
    private static readonly Color colorBackground = Color.FromArgb(0, 0, 0);

    private const int requiredStability = 120;

    private static readonly StringFormat stringFormatCenter = new()
    {
        Alignment = StringAlignment.Center,
        LineAlignment = StringAlignment.Center
    };

    protected override Size CalculateImageSize(MapContext context)
        => new(730, (int)(730f * 9f / 16f));

    protected override MapContext CreateContext(Note[] notes)
        => new(notes.ToDictionary(a => a.Number, ConvertNote));

    private GraphicsPath ConvertNote(Note note)
    {
        var region = regions.First(a => a.Name == note.Number);
        return SvgPathParser.Parse(region.Path, new(0.75f, 0.75f), new(-50, -10));
    }

    protected override void DrawImage(Graphics g, Note[] notes, MapContext context, DateOnly minDate, DateOnly date, float fraction)
    {
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.SmoothingMode = SmoothingMode.HighQuality;

        foreach (var region in regions)
        {
            var graphicsPath = context.GraphicsPaths[region.Name];

            var note = notes.FirstOrDefault(a => a.Number == region.Name);

            if (note is not null)
            {
                var card = note.Cards.First();
                var cell = new RectangleF();

                DrawCard(g, context, minDate, date, fraction, cell, note, card, requiredStability);
            }
            else
            {
                g.FillPath(new SolidBrush(colorStabilityMax), graphicsPath);
            }

            g.DrawPath(Pens.White, graphicsPath);
        }

        foreach (var region in regions)
        {
            var graphicsPath = context.GraphicsPaths[region.Name];

            var note = notes.FirstOrDefault(a => a.Number == region.Name);

            if (note is not null)
            {
                var card = note.Cards.First();
                var cell = new RectangleF();

                DrawCardPercent(g, context, minDate, date, fraction, cell, note, card, requiredStability);
            }
        }
    }

    protected override void DrawReview(Graphics g, MapContext context, Note note, Card card, float fraction, RectangleF cell, Revlog revlog, float percentStability)
    {
        //var revlogColor = colors[revlog.Ease - 1];
        //var rect = new RectangleF(middle.X - 5, middle.Y - 5, 10, 10);
        //g.FillRectangle(new SolidBrush(revlogColor), rect);

        var colorStability = ColorUtils.Blend(colorCell, colorStabilityMax, percentStability);
        var revlogColor = colors[revlog.Ease - 1];
        ////var color = ColorUtils.Blend(color, colorStability, fraction);
        var color = revlogColor;

        var graphicsPath = context.GraphicsPaths[note.Number];
        g.FillPath(new SolidBrush(color), graphicsPath);
    }

    protected override void DrawStability(Graphics g, MapContext context, Note note, Card card, RectangleF cell, float percentStability)
    {
        var colorStability = ColorUtils.Blend(colorCell, colorStabilityMax, percentStability);

        var graphicsPath = context.GraphicsPaths[note.Number];

        g.FillPath(new SolidBrush(colorStability), graphicsPath);
    }

    protected override void DrawPercent(Graphics g, MapContext context, Note note, Card card, RectangleF cell, int stabilityDays, float percent, bool isNew)
    {
        if (!isNew)
        {
            var graphicsPath = context.GraphicsPaths[note.Number];
            var bounds = graphicsPath.GetBounds();

            var middle = new PointF(bounds.Left + bounds.Width / 2f, bounds.Top + bounds.Height / 2f);
            g.DrawStringOutlined(note.Number, font, Brushes.White, new Pen(Color.FromArgb(60, 60, 60)), middle, stringFormatCenter);

            //g.DrawLine(Pens.White, middle.X - 10f, middle.Y, middle.X - 10f + (percent * 20f), middle.Y);

            if (stabilityDays > 7)
            {
                var rect = new RectangleF(middle.X - 10, 15 + middle.Y - 10, 20, 20);
                g.FillPie(Brushes.White, rect, 0, 360f * percent);
            }
        }
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
