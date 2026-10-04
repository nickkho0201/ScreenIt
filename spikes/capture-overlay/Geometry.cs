// Physical-pixel geometry only. No WPF types or global desktop DPI scale here.
internal readonly record struct PxPoint(double X, double Y);
internal readonly record struct PxRect(int X, int Y, int Width, int Height);
internal static class Geometry
{
    public static PxPoint FromDip(PxPoint p, double sx, double sy) => new(p.X * sx, p.Y * sy);
    public static PxPoint ToDip(PxPoint p, double sx, double sy) => new(p.X / sx, p.Y / sy);
    public static PxPoint Local(PxPoint screen, int left, int top) => new(screen.X - left, screen.Y - top);
    public static PxRect Bounds(PxPoint a, PxPoint b, int width, int height)
    {
        if (!double.IsFinite(a.X + a.Y + b.X + b.Y) || width <= 0 || height <= 0) throw new ArgumentException("Invalid geometry");
        int x = (int)Math.Floor(Math.Clamp(Math.Min(a.X, b.X), 0, width));
        int y = (int)Math.Floor(Math.Clamp(Math.Min(a.Y, b.Y), 0, height));
        int right = (int)Math.Ceiling(Math.Clamp(Math.Max(a.X, b.X), 0, width));
        int bottom = (int)Math.Ceiling(Math.Clamp(Math.Max(a.Y, b.Y), 0, height));
        return new(x, y, right - x, bottom - y);
    }
}
internal sealed class Selection
{
    public PxRect? Completed { get; private set; }
    public PxRect? Preview { get; private set; }
    public bool Active { get; private set; }
    public bool Paused { get; private set; }
    private PxRect? before;
    private PxPoint anchor;
    public void Begin(PxPoint p)
    {
        if (Active && Paused) { Paused = false; return; } // A: next click resumes original anchor.
        before = Completed; anchor = p; Active = true; Paused = false;
    }
    public void Move(PxPoint p, int w, int h) { if (Active && !Paused) Preview = Geometry.Bounds(anchor, p, w, h); }
    public void End(PxPoint p, int w, int h)
    {
        Move(p, w, h);
        if (Preview is { Width: >= 2, Height: >= 2 } r) Completed = r;
        else Completed = before; // Ignore accidental 0/1 pixel gestures, preserving previous region.
        Active = false; Paused = false; Preview = null;
    }
    public void Interrupt(bool preservePreview)
    {
        if (!Active) return;
        if (preservePreview) Paused = true;
        else CancelGesture();
    }
    public void CancelGesture() { if (Active) Completed = before; Active = false; Paused = false; Preview = null; }
    public void Full(int w, int h) { CancelGesture(); Completed = new(0, 0, w, h); }
    public void Clear() { CancelGesture(); Completed = null; }
}
