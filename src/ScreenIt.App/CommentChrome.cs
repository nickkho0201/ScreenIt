using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

// Local material/geometry for comment chrome. No bitmap effects on editable text.
internal static class CommentChrome
{
    internal static bool Dark => Appearance.Current==UiTheme.Dark;
    internal static Brush Tint(Color color,byte alpha) => new SolidColorBrush(Color.FromArgb(alpha,color.R,color.G,color.B));
    internal static Color Paper => ConstructDesign.Paper.Color;
    internal static Color Signal => ConstructDesign.Signal.Color;
    internal static StreamGeometry Cut(Rect r,double cut)
    {
        double c=Math.Min(cut,Math.Min(r.Width,r.Height)/3);
        var g=new StreamGeometry();using(var p=g.Open())
        {
            p.BeginFigure(new(r.Left+c,r.Top),true,true);
            foreach(var v in new[]{new Point(r.Right-c,r.Top),new Point(r.Right,r.Top+c),new Point(r.Right,r.Bottom-c),new Point(r.Right-c,r.Bottom),new Point(r.Left+c,r.Bottom),new Point(r.Left,r.Bottom-c),new Point(r.Left,r.Top+c)}) p.LineTo(v,true,false);
        }
        g.Freeze();return g;
    }
    internal static Brush Surface => new LinearGradientBrush(Dark ? Color.FromRgb(35,35,35) : Color.FromRgb(251,254,248),Dark ? Color.FromRgb(26,26,26) : Color.FromRgb(241,242,236),new Point(0,0),new Point(.8,1));
    internal static void Panel(DrawingContext dc,Size size,Brush fallback,Brush border)
    {
        var rect=new Rect(size);var shape=Cut(rect,8);
        if(SystemParameters.HighContrast) { dc.DrawGeometry(fallback,new Pen(border,1),shape);return; }
        // A small, stationary vector penumbra follows the actual silhouette.
        // The opaque face covers its interior; text never passes through a blur.
        for(int i=16;i>=1;i--)
        {
            var shadow=rect;shadow.Inflate(i*.8,i*.8);shadow.Offset(0,4);
            dc.DrawGeometry(new SolidColorBrush(Color.FromArgb(5,0,0,0)),null,Cut(shadow,8+i*.4));
        }
        dc.DrawGeometry(Surface,null,shape);dc.PushClip(shape);
        var reflection=new RadialGradientBrush { Center=new Point(.92,1.05),GradientOrigin=new Point(.92,1.05),RadiusX=.8,RadiusY=.65 };
        reflection.GradientStops.Add(new(Color.FromArgb(Dark ? (byte)17 : (byte)7,240,77,50),0));reflection.GradientStops.Add(new(Color.FromArgb(0,240,77,50),1));
        dc.DrawRectangle(reflection,null,rect);dc.Pop();
        var edge=new LinearGradientBrush();
        var ink=Dark ? Paper : ConstructDesign.Ink.Color;
        edge.StartPoint=new Point(0,0);edge.EndPoint=new Point(.7,1);
        edge.GradientStops.Add(new(Color.FromArgb(100,ink.R,ink.G,ink.B),0));edge.GradientStops.Add(new(Color.FromArgb(35,ink.R,ink.G,ink.B),.48));edge.GradientStops.Add(new(Color.FromArgb(64,ink.R,ink.G,ink.B),1));
        dc.DrawGeometry(null,new Pen(edge,1),shape);
        var inset=rect;inset.Inflate(-1,-1);dc.DrawGeometry(null,new Pen(Tint(ink,10),1),Cut(inset,7));
    }
}

namespace ScreenIt.Presentation
{
    // Border keeps the native TextBox host's measure/arrange and focus triggers.
    public sealed class CommentInputFrame : Border
    {
        protected override void OnRender(DrawingContext dc)
        {
            double w=ActualWidth,h=ActualHeight;if(w<=0 || h<=0) return;
            var g=new StreamGeometry();using(var p=g.Open())
            {
                p.BeginFigure(new Point(5,1),true,true);p.LineTo(new(w-5,1),true,false);p.QuadraticBezierTo(new(w-1,1),new(w-1,5),true,false);
                p.LineTo(new(w-1,h-9),true,false);p.LineTo(new(w-9,h-1),true,false);p.LineTo(new(5,h-1),true,false);
                p.QuadraticBezierTo(new(1,h-1),new(1,h-5),true,false);p.LineTo(new(1,5),true,false);p.QuadraticBezierTo(new(1,1),new(5,1),true,false);
            }
            dc.DrawGeometry(Background,new Pen(BorderBrush,BorderThickness.Left),g);
            if(!SystemParameters.HighContrast)
            {
                dc.PushClip(g);
                var inset=new LinearGradientBrush(Color.FromArgb(20,0,0,0),Colors.Transparent,90);
                dc.DrawRectangle(inset,null,new Rect(0,0,w,12));dc.Pop();
            }
        }
    }
}
