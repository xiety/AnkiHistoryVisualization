using System.Drawing;

namespace AnkiHistoryVisualization;

public static class ColorUtils
{
    public static Color Blend(Color from, Color to, float k)
    {
        var a = (int)(from.A * (1f - k) + to.A * k);
        var r = (int)(from.R * (1f - k) + to.R * k);
        var g = (int)(from.G * (1f - k) + to.G * k);
        var b = (int)(from.B * (1f - k) + to.B * k);

        return Color.FromArgb(a, r, g, b);
    }
}
