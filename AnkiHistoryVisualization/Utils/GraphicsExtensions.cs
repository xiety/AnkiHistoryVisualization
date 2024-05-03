using System.Drawing.Drawing2D;

namespace System.Drawing;

public static class GraphicsExtensions
{
    public static SizeF MeasureSize(this Graphics g, string text, Font font, StringFormat stringFormat)
    {
        var localFormat = new StringFormat(stringFormat);
        CharacterRange[] rangesInput = [new(0, text.Length)];
        localFormat.SetMeasurableCharacterRanges(rangesInput);
        var ranges = g.MeasureCharacterRanges(text, font, g.ClipBounds, localFormat);
        return ranges[0].GetBounds(g).Size;
    }

    public static void DrawStringOutlined(this Graphics g, string text, Font font, Brush fill, Pen outline, PointF origin, StringFormat format)
    {
        var path = new GraphicsPath();

        path.AddString(text,
            font.FontFamily,
            (int)font.Style,
            font.Size * (12f / 9f),
            origin,
            format);

        DrawPath(g, fill, outline, path);
    }

    public static void DrawStringOutlined(this Graphics g, string text, Font font, Brush fill, Pen outline, RectangleF layout, StringFormat format)
    {
        var path = new GraphicsPath();

        path.AddString(text,
            font.FontFamily,
            (int)font.Style,
            font.Size * (12f / 9f),
            layout,
            format);

        DrawPath(g, fill, outline, path);
    }

    private static void DrawPath(Graphics g, Brush fill, Pen outline, GraphicsPath path)
    {
        var old = g.SmoothingMode;
        g.SmoothingMode = SmoothingMode.HighQuality;

        g.DrawPath(outline, path);
        g.FillPath(fill, path);

        g.SmoothingMode = old;
    }
}
