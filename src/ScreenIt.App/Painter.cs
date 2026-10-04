using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

// Render projection deliberately has no Comment/body property. Used by overlay and bitmap output alike.
internal abstract record Glyph(Guid Id);
internal sealed record BadgeGlyph(Guid Id, string Label, P Anchor) : Glyph(Id);
internal sealed record ArrowGlyph(Guid Id, P Start, P End) : Glyph(Id);
internal sealed record BoxGlyph(Guid Id, Bounds Rect) : Glyph(Id);
internal static class Painter
{
    public static Glyph[] Project(IEnumerable<Annotation> annotations) => annotations.Select(a => a switch
    {
        Marker m => (Glyph)new BadgeGlyph(m.Id, m.Label, m.Anchor),
        Arrow arrow => new ArrowGlyph(arrow.Id, arrow.Start, arrow.End),
        Box b => new BoxGlyph(b.Id, b.Rect),
        _ => throw new InvalidOperationException("Unknown annotation")
    }).ToArray();
    private static Point Point(P p) => new(p.X, p.Y);
    private static Rect Rect(Bounds r) => new(r.X, r.Y, r.Width, r.Height);
    public static void Draw(DrawingContext dc, IEnumerable<Glyph> glyphs, int width, int height)
    {
        var all = glyphs.ToArray(); var black = new Pen(Brushes.White, 4.5); var colored = new Pen(UtilityUi.Accent, 2.5);
        foreach (var g in all.Where(g => g is not BadgeGlyph))
        {
            if (g is ArrowGlyph a)
            {
                var head = Geo.ArrowHead(a.Start, a.End);
                foreach (var pen in new[] { black, colored })
                {
                    dc.DrawLine(pen, Point(a.Start), Point(a.End)); dc.DrawLine(pen, Point(head.left), Point(a.End)); dc.DrawLine(pen, Point(head.right), Point(a.End));
                }
            }
            if (g is BoxGlyph b) { dc.DrawRectangle(null, black, Rect(b.Rect)); dc.DrawRectangle(null, colored, Rect(b.Rect)); }
        }
        foreach (var m in all.OfType<BadgeGlyph>())
        {
            var r = Geo.Badge(m.Anchor, m.Label, width, height);
            var center = new P(r.X + r.Width / 2, r.Y + r.Height / 2);
            bool displaced = center.Distance(m.Anchor) > 1;
            if (displaced) dc.DrawLine(new Pen(UtilityUi.Accent, 2), Point(m.Anchor), Point(center));
            dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(70,0,0,0)),null,new Rect(r.X,r.Y+1.5,r.Width,r.Height),8,8);
            dc.DrawRoundedRectangle(UtilityUi.Accent,new Pen(Brushes.White,1.5),Rect(r),8,8);
            var text = new FormattedText(m.Label, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface(new FontFamily("Segoe UI"),FontStyles.Normal,FontWeights.SemiBold,FontStretches.Normal), 13, Brushes.White, 1);
            dc.DrawText(text, new Point(center.X - text.Width / 2, center.Y - text.Height / 2));
            if (displaced) dc.DrawEllipse(UtilityUi.Accent,new Pen(Brushes.White,1),Point(m.Anchor),2,2); // Edge-displaced badges keep their precise anchor.
        }
    }
    public static BitmapSource Render(BitmapSource frozen, Glyph[] glyphs)
    {
        int w = frozen.PixelWidth, h = frozen.PixelHeight; var visual = new DrawingVisual();
        RenderOptions.SetBitmapScalingMode(visual, BitmapScalingMode.NearestNeighbor);
        using (var dc = visual.RenderOpen()) { dc.DrawImage(frozen, new Rect(0, 0, w, h)); Draw(dc, glyphs, w, h); }
        var output = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32); output.Render(visual); output.Freeze(); return output;
    }
}
