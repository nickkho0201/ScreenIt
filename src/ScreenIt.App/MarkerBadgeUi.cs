using System.Globalization;
using System.Windows;
using System.Windows.Media;

// Overlay-only DIP geometry. PNG glyphs and Core hit bounds are deliberately untouched.
internal static class MarkerBadgeUi
{
    internal static Rect Bounds(Rect projected,string label,Rect source,Size viewport)
    {
        // Start with the original physical-pixel badge projected through the actual
        // source/HWND transforms; only bound its UI text size for readability.
        double h=Math.Clamp(projected.Height,22,28);
        var text=new FormattedText(label,CultureInfo.InvariantCulture,FlowDirection.LeftToRight,new Typeface(ConstructDesign.Body,FontStyles.Normal,FontWeights.SemiBold,FontStretches.Normal),13,ConstructDesign.Paper,1);
        double w=Math.Max(32,Math.Max(projected.Width,text.Width+12));
        w=Math.Min(w,Math.Max(1,viewport.Width-8));h=Math.Min(h,Math.Max(1,viewport.Height-8));
        bool fitsSource=source.Width>=w && source.Height>=h;
        if(!fitsSource)
        {
            var anchor=new Point(projected.X+projected.Width/2,projected.Y+projected.Height/2);
            var candidates=new[]{new Rect(anchor.X-w/2,source.Bottom+6,w,h),new Rect(source.Right+6,anchor.Y-h/2,w,h),new Rect(source.Left-w-6,anchor.Y-h/2,w,h),new Rect(anchor.X-w/2,source.Top-h-6,w,h)};
            var fitting=candidates.Where(r=>r.Left>=0 && r.Top>=0 && r.Right<=viewport.Width && r.Bottom<=viewport.Height).OrderBy(r=>(new Point(r.X+r.Width/2,r.Y+r.Height/2)-anchor).Length).ToArray();
            if(fitting.Length>0) return fitting[0];
        }
        Rect area=fitsSource ? source : new Rect(0,0,viewport.Width,viewport.Height);
        return new Rect(Math.Clamp(projected.X+projected.Width/2-w/2,area.Left,area.Right-w),Math.Clamp(projected.Y+projected.Height/2-h/2,area.Top,area.Bottom-h),w,h);
    }
    // Synthetic layout callers use the old 40x32 physical-pixel baseline at 100%.
    internal static Rect Bounds(Point anchor,string label,Size viewport) => Bounds(new Rect(anchor.X-20,anchor.Y-16,40,32),label,new Rect(0,0,viewport.Width,viewport.Height),viewport);
    internal static void Draw(DrawingContext dc,Point anchor,Rect rect,string label,double emphasis)
    {
        double c=Math.Min(3,Math.Min(rect.Width,rect.Height)/4);
        var shape=new StreamGeometry();using(var p=shape.Open())
        {
            p.BeginFigure(new Point(rect.Left+c,rect.Top),true,true);
            foreach(var point in new[]{new Point(rect.Right-c,rect.Top),new Point(rect.Right,rect.Top+c),new Point(rect.Right,rect.Bottom-c),new Point(rect.Right-c,rect.Bottom),new Point(rect.Left,rect.Bottom),new Point(rect.Left,rect.Top+c)}) p.LineTo(point,true,false);
        }
        var center=new Point(rect.Left+rect.Width/2,rect.Top+rect.Height/2);
        bool displaced=(center-anchor).Length>2;
        if(displaced) { dc.DrawLine(new Pen(ConstructDesign.Ink,3),center,anchor);dc.DrawLine(new Pen(ConstructDesign.Signal,1),center,anchor); }
        dc.DrawGeometry(ConstructDesign.Ink,new Pen(ConstructDesign.Signal,1+emphasis),shape);
        var text=new FormattedText(label,CultureInfo.InvariantCulture,FlowDirection.LeftToRight,new Typeface(ConstructDesign.Body,FontStyles.Normal,FontWeights.SemiBold,FontStretches.Normal),13,ConstructDesign.Paper,1);
        double scale=Math.Min(1,Math.Max(1,rect.Width-10)/Math.Max(1,text.Width));
        dc.PushTransform(new TranslateTransform(rect.Left+rect.Width/2,rect.Top+rect.Height/2));dc.PushTransform(new ScaleTransform(scale,scale));
        dc.DrawText(text,new Point(-text.Width/2,-text.Height/2));dc.Pop();dc.Pop();
        if(displaced) dc.DrawEllipse(ConstructDesign.Signal,new Pen(ConstructDesign.Paper,1),anchor,2,2);
    }
}

internal static class CommentEditorPlacement
{
    internal static Rect Place(Rect badge,Size viewport)
    {
        const double margin=8,gap=16;
        double availableWidth=Math.Max(1,viewport.Width-margin*2),availableHeight=Math.Max(1,viewport.Height-margin*2);
        double width=Math.Min(380,availableWidth),height=Math.Min(246,availableHeight);
        Rect Fit(double x,double y,double w,double h) => new(Math.Clamp(x,Math.Min(margin,viewport.Width-w),Math.Max(0,viewport.Width-w-margin)),Math.Clamp(y,Math.Min(margin,viewport.Height-h),Math.Max(0,viewport.Height-h-margin)),w,h);
        double below=viewport.Height-margin-badge.Bottom-gap;
        if(below>=220) return Fit(badge.Left+24,badge.Bottom+gap,width,Math.Min(height,below));
        double right=viewport.Width-margin-badge.Right-gap;
        if(right>=280) return Fit(badge.Right+gap,badge.Top+8,Math.Min(width,right),height);
        double left=badge.Left-gap-margin;
        if(left>=280) return Fit(badge.Left-gap-Math.Min(width,left),badge.Top+8,Math.Min(width,left),height);
        double above=badge.Top-gap-margin;
        if(above>=220) return Fit(badge.Left+24,badge.Top-gap-Math.Min(height,above),width,Math.Min(height,above));
        return Fit(badge.Left+24,badge.Bottom+gap,width,height);
    }
}
