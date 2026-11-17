namespace ScreenIt.Core;

// Platform-neutral annotation color. No WPF, Win32, filesystem or network dependencies.
public readonly record struct AnnotationColor(byte R, byte G, byte B)
{
    public static AnnotationColor FromRgb(int r, int g, int b) => new(Byte(r), Byte(g), Byte(b));
    private static byte Byte(int value) => (byte)Math.Clamp(value, 0, 255);
    public string Hex => $"#{R:X2}{G:X2}{B:X2}";
    public HsvColor ToHsv()
    {
        double r = R / 255d, g = G / 255d, b = B / 255d;
        double max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b)), delta = max - min;
        double hue = 0;
        if (delta > 0)
        {
            if (max == r) hue = 60 * (((g - b) / delta) % 6);
            else if (max == g) hue = 60 * (((b - r) / delta) + 2);
            else hue = 60 * (((r - g) / delta) + 4);
        }
        if (hue < 0) hue += 360;
        return new(hue, max <= 0 ? 0 : delta / max, max);
    }
    public static AnnotationColor FromHsv(HsvColor hsv) => FromHsv(hsv.Hue, hsv.Saturation, hsv.Value);
    public static AnnotationColor FromHsv(double hue, double saturation, double value)
    {
        if (!double.IsFinite(hue)) hue = 0;
        if (!double.IsFinite(saturation)) saturation = 0;
        if (!double.IsFinite(value)) value = 0;
        hue = ((hue % 360) + 360) % 360;
        saturation = Math.Clamp(saturation, 0, 1);
        value = Math.Clamp(value, 0, 1);
        double c = value * saturation, x = c * (1 - Math.Abs((hue / 60) % 2 - 1)), m = value - c;
        var (r, g, b) = hue switch
        {
            < 60 => (c, x, 0d),
            < 120 => (x, c, 0d),
            < 180 => (0d, c, x),
            < 240 => (0d, x, c),
            < 300 => (x, 0d, c),
            _ => (c, 0d, x)
        };
        return new(Byte((int)Math.Round((r + m) * 255)), Byte((int)Math.Round((g + m) * 255)), Byte((int)Math.Round((b + m) * 255)));
    }
}

public readonly record struct HsvColor(double Hue, double Saturation, double Value)
{
    public HsvColor Normalized() => new(((Hue % 360) + 360) % 360, Math.Clamp(Saturation, 0, 1), Math.Clamp(Value, 0, 1));
}
