using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

// Render projection deliberately has no Comment/body property. Used by overlay and bitmap output alike.
internal abstract record Glyph(Guid Id);
internal sealed record BadgeGlyph(Guid Id, string Label, P Anchor, AnnotationColor? Color = null) : Glyph(Id);
internal sealed record ArrowGlyph(Guid Id, P Start, P End, AnnotationColor? Color = null) : Glyph(Id);
internal sealed record BoxGlyph(Guid Id, Bounds Rect, AnnotationColor? Color = null) : Glyph(Id);
internal sealed record PencilGlyph(Guid Id, IReadOnlyList<P> Points, AnnotationColor? Color = null) : Glyph(Id);
internal static class Painter
{
    // Explicit export palette, independent of theme-dependent UI chrome: Signal Red stroke with a Paper halo.
    internal static readonly SolidColorBrush AnnotationAccent=UtilityUi.Brush(240,77,50);
    internal static readonly SolidColorBrush AnnotationOutline=UtilityUi.Brush(251,254,248);
    internal const double AnnotationStrokeWidth=2.5, AnnotationOutlineWidth=4.5;
    // Objects without an explicit color keep their design's palette exactly as before.
    internal static AnnotationColor DefaultAnnotationColor
    {
        get { var color=(UiDesign.IsEmber ? AnnotationAccent : UtilityUi.Accent).Color; return new(color.R,color.G,color.B); }
    }
    internal static double Luminance(AnnotationColor c)
    {
        static double F(byte b) { double v=b/255d; return v<=.04045 ? v/12.92 : Math.Pow((v+.055)/1.055,2.4); }
        return .2126*F(c.R)+.7152*F(c.G)+.0722*F(c.B);
    }
    internal static double Contrast(AnnotationColor a,AnnotationColor b)
    {
        double x=Luminance(a),y=Luminance(b);return (Math.Max(x,y)+.05)/(Math.Min(x,y)+.05);
    }
    internal static bool IsLight(AnnotationColor c) => Luminance(c)>.55;
    private static readonly AnnotationColor InkColor=new(26,26,26), WhiteColor=new(255,255,255);
    // A halo that contrasts with very light strokes keeps every exported color legible on light and dark sources.
    private static SolidColorBrush Halo(AnnotationColor c, bool ember) => IsLight(c) ? ConstructDesign.Ink : ember ? AnnotationOutline : Brushes.White;
    // Marker number stays readable: white text/border on saturated fills, Ink on yellow/lime/white fills.
    internal static SolidColorBrush MarkerInk(AnnotationColor fill) => Contrast(WhiteColor,fill)>=Contrast(InkColor,fill) ? Brushes.White : ConstructDesign.Ink;
    internal static SolidColorBrush MarkerBorder(AnnotationColor fill) => MarkerInk(fill);
    internal static SolidColorBrush Brush(AnnotationColor c) => UtilityUi.Brush(c.R,c.G,c.B);
    public static Glyph[] Project(IEnumerable<Annotation> annotations) => annotations.Select(a => a switch
    {
        Marker m => (Glyph)new BadgeGlyph(m.Id, m.Label, m.Anchor, m.Color),
        Arrow arrow => new ArrowGlyph(arrow.Id, arrow.Start, arrow.End, arrow.Color),
        Box b => new BoxGlyph(b.Id, b.Rect, b.Color),
        PencilStroke stroke => new PencilGlyph(stroke.Id, stroke.Points, stroke.Color),
        _ => throw new InvalidOperationException("Unknown annotation")
    }).ToArray();
    private static Point Point(P p) => new(p.X, p.Y);
    private static Rect Rect(Bounds r) => new(r.X, r.Y, r.Width, r.Height);
    private static AnnotationColor ColorOf(Glyph g,AnnotationColor fallback) => (g switch
    {
        BadgeGlyph b => b.Color,
        ArrowGlyph a => a.Color,
        BoxGlyph box => box.Color,
        PencilGlyph p => p.Color,
        _ => null
    }) ?? fallback;
    public static void Draw(DrawingContext dc, IEnumerable<Glyph> glyphs, int width, int height)
    {
        var all = glyphs.ToArray();
        // Classic reproduces the 0.2.3 blue export palette; Ember keeps the reviewed Signal/Paper pair.
        bool ember=UiDesign.IsEmber;
        var defaultColor=DefaultAnnotationColor;
        foreach (var g in all.Where(g => g is not BadgeGlyph))
        {
            var color=ColorOf(g,defaultColor);
            var outline = new Pen(Halo(color,ember), AnnotationOutlineWidth);
            var colored = new Pen(Brush(color), AnnotationStrokeWidth);
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
            var color=m.Color ?? defaultColor;
            var accent=Brush(color);            var r = Geo.Badge(m.Anchor, m.Label, width, height);
            var center = new P(r.X + r.Width / 2, r.Y + r.Height / 2);
            bool displaced = center.Distance(m.Anchor) > 1;
            if(!ember)
            {
                var ink=MarkerInk(color);
                if (displaced) dc.DrawLine(new Pen(accent, 2), Point(m.Anchor), Point(center));
                dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(70,0,0,0)),null,new Rect(r.X,r.Y+1.5,r.Width,r.Height),8,8);
                dc.DrawRoundedRectangle(accent,new Pen(ink,1.5),Rect(r),8,8);
                var classicText = new FormattedText(m.Label, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface(new FontFamily("Segoe UI"),FontStyles.Normal,FontWeights.SemiBold,FontStretches.Normal), 13, ink, 1);
                dc.DrawText(classicText, new Point(center.X - classicText.Width / 2, center.Y - classicText.Height / 2));
                if (displaced) dc.DrawEllipse(accent,new Pen(ink,1),Point(m.Anchor),2,2);
                continue;
            }
            if (displaced) { dc.DrawLine(new Pen(ConstructDesign.Paper,3.5),Point(m.Anchor),Point(center));dc.DrawLine(new Pen(accent,1.5),Point(m.Anchor),Point(center)); }
            double cut=Math.Min(4,Math.Min(r.Width,r.Height)/4);
            var badge=new StreamGeometry();using(var path=badge.Open())
            {
                path.BeginFigure(new Point(r.X+cut,r.Y),true,true);
                foreach(var point in new[]{new Point(r.X+r.Width-cut,r.Y),new Point(r.X+r.Width,r.Y+cut),new Point(r.X+r.Width,r.Y+r.Height-cut),new Point(r.X+r.Width-cut,r.Y+r.Height),new Point(r.X+cut,r.Y+r.Height),new Point(r.X,r.Y+r.Height-cut),new Point(r.X,r.Y+cut)}) path.LineTo(point,true,false);
            }
            dc.DrawGeometry(ConstructDesign.Ink,new Pen(ConstructDesign.Paper,3),badge);
            dc.DrawGeometry(null,new Pen(accent,1.5),badge);
            var text = new FormattedText(m.Label, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface(ConstructDesign.Body,FontStyles.Normal,FontWeights.Bold,FontStretches.Normal), 14, ConstructDesign.Paper, 1);
            // Long IDs and tiny sources retain their original hit bounds; shrink text to fit.
            double fit=Math.Min(1,Math.Min(Math.Max(1,r.Width-6)/Math.Max(1,text.Width),Math.Max(1,r.Height-4)/Math.Max(1,text.Height)));
            dc.PushTransform(new TranslateTransform(center.X,center.Y));dc.PushTransform(new ScaleTransform(fit,fit));
            dc.DrawText(text,new Point(-text.Width/2,-text.Height/2));dc.Pop();dc.Pop();
            if (displaced) dc.DrawEllipse(accent,new Pen(ConstructDesign.Paper,1),Point(m.Anchor),2,2);
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
