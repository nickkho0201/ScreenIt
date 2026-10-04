namespace ScreenIt.Core;
// Physical-pixel geometry only. No WPF types or global desktop DPI scale here.

public readonly record struct PxRect(int X, int Y, int Width, int Height);
public static class Geometry
{
    public static P FromDip(P p, double sx, double sy) => new(p.X * sx, p.Y * sy);
    public static P ToDip(P p, double sx, double sy) => new(p.X / sx, p.Y / sy);
    public static P Local(P screen, int left, int top) => new(screen.X - left, screen.Y - top);
    public static PxRect Bounds(P a, P b, int width, int height)
    {
        if (!double.IsFinite(a.X + a.Y + b.X + b.Y) || width <= 0 || height <= 0) throw new ArgumentException("Invalid geometry");
        int x = (int)Math.Floor(Math.Clamp(Math.Min(a.X, b.X), 0, width));
        int y = (int)Math.Floor(Math.Clamp(Math.Min(a.Y, b.Y), 0, height));
        int right = (int)Math.Ceiling(Math.Clamp(Math.Max(a.X, b.X), 0, width));
        int bottom = (int)Math.Ceiling(Math.Clamp(Math.Max(a.Y, b.Y), 0, height));
        return new(x, y, right - x, bottom - y);
    }
}
public sealed class Selection
{
    public PxRect? Completed { get; private set; }
    public PxRect? Preview { get; private set; }
    public bool Active { get; private set; }

    private PxRect? before;
    private P anchor;
    public void Begin(P p)
    {

        before = Completed; anchor = p; Active = true;
    }
    public void Move(P p, int w, int h) { if (Active) Preview = Geometry.Bounds(anchor, p, w, h); }
    public void End(P p, int w, int h)
    {
        Move(p, w, h);
        if (Preview is { Width: >= 2, Height: >= 2 } r) Completed = r;
        else Completed = before; // Ignore accidental 0/1 pixel gestures, preserving previous region.
        Active = false; Preview = null;
    }
    public void CancelGesture() { if (Active) Completed = before; Active = false; Preview = null; }
    public void Full(int w, int h) { CancelGesture(); Completed = new(0, 0, w, h); }
    public void Clear() { CancelGesture(); Completed = null; }
}
