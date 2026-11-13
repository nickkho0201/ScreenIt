using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

// Render projection deliberately has no Comment/body property. Used by overlay and bitmap output alike.
internal abstract record Glyph(Guid Id);
internal sealed record BadgeGlyph(Guid Id, string Label, P Anchor) : Glyph(Id);
internal sealed record ArrowGlyph(Guid Id, P Start, P End) : Glyph(Id);
internal sealed record BoxGlyph(Guid Id, Bounds Rect) : Glyph(Id);
internal sealed record PencilGlyph(Guid Id, IReadOnlyList<P> Points) : Glyph(Id);
internal static class Painter
{
    // Explicit export palette, independent of theme-dependent UI chrome: Signal Red stroke with a Paper halo.
    internal static readonly SolidColorBrush AnnotationAccent=UtilityUi.Brush(240,77,50);
    internal static readonly SolidColorBrush AnnotationOutline=UtilityUi.Brush(251,254,248);
    internal const double AnnotationStrokeWidth=2.5, AnnotationOutlineWidth=4.5;
    public static Glyph[] Project(IEnumerable<Annotation> annotations) => annotations.Select(a => a switch
    {
        Marker m => (Glyph)new BadgeGlyph(m.Id, m.Label, m.Anchor),
        Arrow arrow => new ArrowGlyph(arrow.Id, arrow.Start, arrow.End),
        Box b => new BoxGlyph(b.Id, b.Rect),
        PencilStroke stroke => new PencilGlyph(stroke.Id, stroke.Points),
        _ => throw new InvalidOperationException("Unknown annotation")
    }).ToArray();
    private static Point Point(P p) => new(p.X, p.Y);
    private static Rect Rect(Bounds r) => new(r.X, r.Y, r.Width, r.Height);
    public static void Draw(DrawingContext dc, IEnumerable<Glyph> glyphs, int width, int height)
    {
        var all = glyphs.ToArray();
        // Classic reproduces the 0.2.3 blue export palette; Ember keeps the reviewed Signal/Paper pair.
        bool ember=UiDesign.IsEmber;
        var outline = new Pen(ember ? AnnotationOutline : Brushes.White, AnnotationOutlineWidth);
        var colored = new Pen(ember ? AnnotationAccent : UtilityUi.Accent, AnnotationStrokeWidth);
        foreach (var g in all.Where(g => g is not BadgeGlyph))
        {
            if (g is ArrowGlyph a)
            {
                var head = Geo.ArrowHead(a.Start, a.End);
                foreach (var pen in new[] { outline, colored })
                {
                    dc.DrawLine(pen, Point(a.Start), Point(a.End)); dc.DrawLine(pen, Point(head.left), Point(a.End)); dc.DrawLine(pen, Point(head.right), Point(a.End));
                }
            }
            if (g is BoxGlyph b) { dc.DrawRectangle(null, outline, Rect(b.Rect)); dc.DrawRectangle(null, colored, Rect(b.Rect)); }
            if (g is PencilGlyph stroke && stroke.Points.Count >= 2)
            {
                var path = new StreamGeometry();
                using (var context = path.Open())
                {
                    context.BeginFigure(Point(stroke.Points[0]), false, false);
                    for (int i = 1; i < stroke.Points.Count; i++) context.LineTo(Point(stroke.Points[i]), true, false);
                }
                path.Freeze();
                foreach (var pen in new[] { new Pen(outline.Brush, AnnotationOutlineWidth), new Pen(colored.Brush, AnnotationStrokeWidth) })
                {
                    pen.StartLineCap = pen.EndLineCap = PenLineCap.Round; pen.LineJoin = PenLineJoin.Round;
                    dc.DrawGeometry(null, pen, path);
                }
            }
        }
        foreach (var m in all.OfType<BadgeGlyph>())
        {
            var r = Geo.Badge(m.Anchor, m.Label, width, height);
            var center = new P(r.X + r.Width / 2, r.Y + r.Height / 2);
            bool displaced = center.Distance(m.Anchor) > 1;
            if(!ember)
            {
                if (displaced) dc.DrawLine(new Pen(UtilityUi.Accent, 2), Point(m.Anchor), Point(center));
                dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(70,0,0,0)),null,new Rect(r.X,r.Y+1.5,r.Width,r.Height),8,8);
                dc.DrawRoundedRectangle(UtilityUi.Accent,new Pen(Brushes.White,1.5),Rect(r),8,8);
                var classicText = new FormattedText(m.Label, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface(new FontFamily("Segoe UI"),FontStyles.Normal,FontWeights.SemiBold,FontStretches.Normal), 13, Brushes.White, 1);
                dc.DrawText(classicText, new Point(center.X - classicText.Width / 2, center.Y - classicText.Height / 2));
                if (displaced) dc.DrawEllipse(UtilityUi.Accent,new Pen(Brushes.White,1),Point(m.Anchor),2,2);
                continue;
            }
            if (displaced) { dc.DrawLine(new Pen(ConstructDesign.Paper,3.5),Point(m.Anchor),Point(center));dc.DrawLine(new Pen(ConstructDesign.Signal,1.5),Point(m.Anchor),Point(center)); }
            double cut=Math.Min(4,Math.Min(r.Width,r.Height)/4);
            var badge=new StreamGeometry();using(var path=badge.Open())
            {
                path.BeginFigure(new Point(r.X+cut,r.Y),true,true);
                foreach(var point in new[]{new Point(r.X+r.Width-cut,r.Y),new Point(r.X+r.Width,r.Y+cut),new Point(r.X+r.Width,r.Y+r.Height-cut),new Point(r.X+r.Width-cut,r.Y+r.Height),new Point(r.X+cut,r.Y+r.Height),new Point(r.X,r.Y+r.Height-cut),new Point(r.X,r.Y+cut)}) path.LineTo(point,true,false);
            }
            dc.DrawGeometry(ConstructDesign.Ink,new Pen(ConstructDesign.Paper,3),badge);
            dc.DrawGeometry(null,new Pen(ConstructDesign.Signal,1.5),badge);
            var text = new FormattedText(m.Label, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface(ConstructDesign.Body,FontStyles.Normal,FontWeights.Bold,FontStretches.Normal), 14, ConstructDesign.Paper, 1);
            // Long IDs and tiny sources retain their original hit bounds; shrink text to fit.
            double fit=Math.Min(1,Math.Min(Math.Max(1,r.Width-6)/Math.Max(1,text.Width),Math.Max(1,r.Height-4)/Math.Max(1,text.Height)));
            dc.PushTransform(new TranslateTransform(center.X,center.Y));dc.PushTransform(new ScaleTransform(fit,fit));
            dc.DrawText(text,new Point(-text.Width/2,-text.Height/2));dc.Pop();dc.Pop();
            if (displaced) dc.DrawEllipse(ConstructDesign.Signal,new Pen(ConstructDesign.Paper,1),Point(m.Anchor),2,2);
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
