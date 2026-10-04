namespace ScreenIt.Core;
// Immutable annotation records and draft-local snapshot history; physical pixels only.
public readonly record struct P(double X, double Y)
{
    public double Distance(P b) => Math.Sqrt((X - b.X) * (X - b.X) + (Y - b.Y) * (Y - b.Y));
}
public readonly record struct Bounds(double X, double Y, double Width, double Height)
{
    public bool Contains(P p) => p.X >= X && p.Y >= Y && p.X <= X + Width && p.Y <= Y + Height;
}
public abstract record Annotation(Guid Id);
public sealed record Marker(Guid Id, int Number, P Anchor, string Comment, string ScreenshotLetter = "A") : Annotation(Id)
{
    public string Label => ScreenshotLetter + Number;
}
public sealed record Arrow(Guid Id, P Start, P End) : Annotation(Id);
public sealed record Box(Guid Id, Bounds Rect) : Annotation(Id);
public enum Tool { Marker, Arrow, Rectangle }
public enum EditorEnter { Commit, Newline, Blocked }
public sealed class ScreenshotDraft(int width, int height, string letter)
{
    public string Letter { get; } = letter;
    public bool IsCommitted { get; private set; }
    internal void Seal() { IsCommitted = true; undo.Clear(); redo.Clear(); }
    private void Guard() { if (IsCommitted) throw new InvalidOperationException("Screenshot already committed."); }
    private Annotation[] items = [];
    private readonly Stack<Annotation[]> undo = new(), redo = new();
    private int highWater;
    public int Width { get; } = width > 0 ? width : throw new ArgumentOutOfRangeException(nameof(width));
    public int Height { get; } = height > 0 ? height : throw new ArgumentOutOfRangeException(nameof(height));
    public IReadOnlyList<Annotation> Items => Array.AsReadOnly(items);
    public int NextMarker => checked(highWater + 1);
    public bool CanUndo => undo.Count > 0;
    public bool CanRedo => redo.Count > 0;
    private void Apply(Annotation[] next) { Guard(); undo.Push(items); items = next; redo.Clear(); }
    public Marker CreateMarker(P anchor, string comment)
    {
        if (string.IsNullOrWhiteSpace(comment)) throw new ArgumentException("A marker needs a non-empty comment.");
        var marker = new Marker(Guid.NewGuid(), NextMarker, Geo.Clamp(anchor, Width, Height), comment, Letter);
        Apply([..items, marker]); highWater = marker.Number; return marker;
    }
    public bool AddArrow(P a, P b)
    {
        a = Geo.Clamp(a, Width, Height); b = Geo.Clamp(b, Width, Height);
        if (a.Distance(b) < 4) return false;
        Apply([..items, new Arrow(Guid.NewGuid(), a, b)]); return true;
    }
    public bool AddBox(P a, P b)
    {
        var r = Geo.Normalize(a, b, Width, Height);
        if (r.Width < 3 || r.Height < 3) return false;
        Apply([..items, new Box(Guid.NewGuid(), r)]); return true;
    }
    public void Move(Guid id, P anchor)
    {
        if (items.FirstOrDefault(a => a.Id == id) is not Marker m) return;
        anchor = Geo.Clamp(anchor, Width, Height); if (m.Anchor == anchor) return;
        Replace(m with { Anchor = anchor });
    }
    public void Edit(Guid id, string comment)
    {
        if (string.IsNullOrWhiteSpace(comment)) throw new ArgumentException("A marker needs a non-empty comment.");
        if (items.FirstOrDefault(a => a.Id == id) is Marker m && m.Comment != comment) Replace(m with { Comment = comment });
    }
    private void Replace(Annotation replacement) => Apply(items.Select(a => a.Id == replacement.Id ? replacement : a).ToArray());
    public void Delete(Guid id) { if (items.Any(a => a.Id == id)) Apply(items.Where(a => a.Id != id).ToArray()); }
    public bool Undo() { Guard(); if (!CanUndo) return false; redo.Push(items); items = undo.Pop(); return true; }
    public bool Redo() { Guard(); if (!CanRedo) return false; undo.Push(items); items = redo.Pop(); return true; }
    public string Comments() => CommentFormatter.Format(Letter, items);
    public static EditorEnter EnterAction(bool shift, bool control) => control ? EditorEnter.Blocked : shift ? EditorEnter.Newline : EditorEnter.Commit;
}
public static class Geo
{
    public static P Clamp(P p, int w, int h)
    {
        if (!double.IsFinite(p.X) || !double.IsFinite(p.Y)) throw new ArgumentException("Invalid point.");
        return new(Math.Clamp(p.X, 0, w - 1), Math.Clamp(p.Y, 0, h - 1));
    }
    public static P FromDip(P p, double sx, double sy) => new(p.X * sx, p.Y * sy);
    public static P ToDip(P p, double sx, double sy) => new(p.X / sx, p.Y / sy);
    public static Bounds Normalize(P a, P b, int w, int h)
    {
        a = Clamp(a, w, h); b = Clamp(b, w, h);
        return new(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));
    }
    public static Bounds Badge(P anchor, string label, int width, int height)
    {
        double w = Math.Min(width, Math.Max(40, label.Length * 10 + 12)), h = Math.Min(height, 32);
        return new(Math.Clamp(anchor.X - w / 2, 0, width - w), Math.Clamp(anchor.Y - h / 2, 0, height - h), w, h);
    }
    public static Bounds Editor(P anchor, double width, double height, int screenW, int screenH)
    {
        width = Math.Min(width, Math.Max(1, screenW - 16)); height = Math.Min(height, Math.Max(1, screenH - 16));
        double x = anchor.X + 30, y = anchor.Y + 22;
        if (x + width > screenW - 8) x = anchor.X - 30 - width;
        if (y + height > screenH - 8) y = anchor.Y - 22 - height;
        return new(Math.Clamp(x, Math.Min(8, screenW - width), screenW - width), Math.Clamp(y, Math.Min(8, screenH - height), screenH - height), width, height);
    }
    public static (P left, P right) ArrowHead(P start, P end)
    {
        double length = start.Distance(end); if (length == 0) return (end, end);
        double ux = (end.X - start.X) / length, uy = (end.Y - start.Y) / length, head = Math.Min(16, length * .4);
        return (new(end.X - ux * head - uy * head * .5, end.Y - uy * head + ux * head * .5),
            new(end.X - ux * head + uy * head * .5, end.Y - uy * head - ux * head * .5));
    }
    public static double SegmentDistance(P point, P a, P b)
    {
        double dx = b.X - a.X, dy = b.Y - a.Y, length2 = dx * dx + dy * dy;
        double t = length2 == 0 ? 0 : Math.Clamp(((point.X - a.X) * dx + (point.Y - a.Y) * dy) / length2, 0, 1);
        return point.Distance(new(a.X + t * dx, a.Y + t * dy));
    }
    public static Annotation? Hit(IEnumerable<Annotation> items, P point, int w, int h)
    {
        var reverse = items.Reverse().ToArray();
        foreach (var m in reverse.OfType<Marker>()) if (Badge(m.Anchor, m.Label, w, h).Contains(point) || point.Distance(m.Anchor) <= 6) return m;
        foreach (var a in reverse)
        {
            if (a is Arrow arrow && SegmentDistance(point, arrow.Start, arrow.End) <= 6) return a;
            if (a is Box box)
            {
                var r = box.Rect; P[] corners = [new(r.X, r.Y), new(r.X + r.Width, r.Y), new(r.X + r.Width, r.Y + r.Height), new(r.X, r.Y + r.Height)];
                if (Enumerable.Range(0, 4).Any(i => SegmentDistance(point, corners[i], corners[(i + 1) % 4]) <= 6)) return a;
            }
        }
        return null;
    }
}
