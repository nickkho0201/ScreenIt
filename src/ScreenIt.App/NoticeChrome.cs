using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

// Presentation shared only by application prompts and passive feedback cards.
internal sealed class NoticeSurface : Border
{
    internal double HeaderHeight { get; set; }
    protected override void OnRender(DrawingContext dc)
    {
        double w=ActualWidth,h=ActualHeight;if(w<=0 || h<=0) return;
        var shape=CommentChrome.Cut(new Rect(0,0,w,h),Tag is string ? 6 : 10);
        bool contrast=SystemParameters.HighContrast,action=Tag is string;
        Brush fill=Background;
        if(!contrast && !action) fill=CommentChrome.Surface;
        dc.DrawGeometry(fill,new Pen(BorderBrush,1),shape);
        if(contrast || action) return;
        dc.PushClip(shape);
        if(HeaderHeight>0)
        {
            var tint=new LinearGradientBrush(CommentChrome.Tint(CommentChrome.Signal,32).ColorValue(),Colors.Transparent,0);
            dc.DrawRectangle(tint,null,new Rect(0,0,w,HeaderHeight));
            double y=HeaderHeight;
            var divider=new StreamGeometry();using(var p=divider.Open())
            { p.BeginFigure(new(0,y),false,false);p.LineTo(new(w-70,y),true,false);p.LineTo(new(w-62,y-8),true,false);p.LineTo(new(w,y-8),true,false); }
            dc.DrawGeometry(null,new Pen(BorderBrush,1),divider);
        }
        dc.DrawLine(new Pen(UtilityUi.Accent,1.5),new Point(10,1),new Point(w-10,1));
        dc.Pop();
        dc.DrawGeometry(null,new Pen(CommentChrome.Tint(CommentChrome.Dark ? CommentChrome.Paper : ConstructDesign.Ink.Color,45),1),shape);
    }
}

internal static class NoticeChrome
{
    internal static Color ColorValue(this Brush brush) => ((SolidColorBrush)brush).Color;
    internal static void Action(Button button)
    {
        var face=new FrameworkElementFactory(typeof(NoticeSurface));
        face.SetValue(FrameworkElement.TagProperty,"action");
        face.SetValue(Border.BackgroundProperty,new TemplateBindingExtension(Control.BackgroundProperty));
        face.SetValue(Border.BorderBrushProperty,new TemplateBindingExtension(Control.BorderBrushProperty));
        face.SetValue(Border.PaddingProperty,new Thickness(20,9,20,9));
        var text=new FrameworkElementFactory(typeof(ContentPresenter));
        text.SetValue(FrameworkElement.HorizontalAlignmentProperty,HorizontalAlignment.Center);text.SetValue(FrameworkElement.VerticalAlignmentProperty,VerticalAlignment.Center);face.AppendChild(text);
        var template=new ControlTemplate(typeof(Button)) { VisualTree=face };
        foreach(var property in new[]{UIElement.IsMouseOverProperty,UIElement.IsKeyboardFocusedProperty})
        {
            var trigger=new Trigger { Property=property,Value=true };trigger.Setters.Add(new Setter(Control.BorderBrushProperty,new DynamicResourceExtension("UiFocus")));template.Triggers.Add(trigger);
        }
        var pressed=new Trigger { Property=System.Windows.Controls.Primitives.ButtonBase.IsPressedProperty,Value=true };pressed.Setters.Add(new Setter(UIElement.OpacityProperty,.8));template.Triggers.Add(pressed);
        button.Template=template;button.MinWidth=112;button.MinHeight=42;button.FontSize=15;button.Margin=new Thickness(8,0,0,0);
    }
}
