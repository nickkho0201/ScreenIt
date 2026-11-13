using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

// Shared presentation resources; no capture, persistence or input ownership.
// Brand constants stay CONSTRUCT; the public tokens follow the selected design.
internal static class ConstructDesign
{
    private static readonly FontFamily EmberBody = new(new Uri("pack://application:,,,/ScreenIt.App;component/"), "./Assets/Fonts/Commissioner/#Commissioner");
    private static readonly FontFamily EmberDisplay = new(new Uri("pack://application:,,,/ScreenIt.App;component/"), "./Assets/Fonts/Unbounded/#Unbounded");
    internal static readonly SolidColorBrush ClassicAccent=UtilityUi.Brush(66,85,197); // 0.2.3 blue
    internal static readonly SolidColorBrush EmberSignal=UtilityUi.Brush(240,77,50);
    internal static readonly SolidColorBrush EmberAcid=UtilityUi.Brush(197,217,71);
    internal static readonly SolidColorBrush EmberInk=UtilityUi.Brush(26,26,26);
    internal static readonly SolidColorBrush EmberPaper=UtilityUi.Brush(251,254,248);
    internal static FontFamily Body => UiDesign.IsEmber ? EmberBody : SystemFonts.MessageFontFamily;
    internal static FontFamily Display => UiDesign.IsEmber ? EmberDisplay : Body;
    internal static SolidColorBrush Signal => UiDesign.IsEmber ? EmberSignal : ClassicAccent;
    internal static SolidColorBrush Acid => UiDesign.IsEmber ? EmberAcid : ClassicAccent;
    internal static SolidColorBrush Ink => EmberInk;
    internal static SolidColorBrush Paper => EmberPaper;
    internal static SolidColorBrush OnAccent => UiDesign.OnAccent;
    internal static SolidColorBrush Focus => UiDesign.Focus;
    internal static SolidColorBrush Sidebar => SystemParameters.HighContrast ? SystemColors.ControlBrush : UiDesign.IsEmber ? Appearance.Current==UiTheme.Dark ? UtilityUi.Brush(23,23,23) : UtilityUi.Brush(241,242,236) : Appearance.Palette.Editor;
    internal static SolidColorBrush Hover => SystemParameters.HighContrast ? SystemColors.ControlBrush : UiDesign.IsEmber ? Appearance.Current==UiTheme.Dark ? UtilityUi.Brush(48,48,48) : UtilityUi.Brush(231,234,227) : Appearance.Current==UiTheme.Dark ? UtilityUi.Brush(53,65,83) : UtilityUi.Brush(229,235,246);
    internal static SolidColorBrush Pressed => SystemParameters.HighContrast ? SystemColors.ControlDarkBrush : UiDesign.IsEmber ? Appearance.Current==UiTheme.Dark ? UtilityUi.Brush(57,57,57) : UtilityUi.Brush(216,220,211) : Appearance.Current==UiTheme.Dark ? UtilityUi.Brush(13,19,31) : UtilityUi.Brush(202,214,235);

    internal static FrameworkElement Divider() => new TechnicalDivider { Height=20,Margin=new Thickness(0,0,0,16),IsHitTestVisible=false };
    private sealed class TechnicalDivider : FrameworkElement
    {
        protected override void OnRender(DrawingContext dc)
        {
            double bend=Math.Max(0,ActualWidth-72);
            var path=new StreamGeometry();using(var c=path.Open())
            { c.BeginFigure(new Point(0,15),false,false);c.LineTo(new Point(bend,15),true,false);c.LineTo(new Point(bend+12,3),true,false);c.LineTo(new Point(ActualWidth,3),true,false); }
            dc.DrawGeometry(null,new Pen(Appearance.Palette.Border,1),path);
            dc.DrawRectangle(Signal,null,new Rect(0,12,24,3));
        }
    }
    internal static System.Windows.Shapes.Path Icon(string name,Brush color) => new()
    {
        Width=18,Height=18,Stretch=Stretch.Uniform,Stroke=color,StrokeThickness=1.8,
        StrokeStartLineCap=PenLineCap.Square,StrokeEndLineCap=PenLineCap.Square,
        Data=System.Windows.Media.Geometry.Parse(name switch
        {
            "Switch" => "M18,8 A8,8 0 0 0 4,4 L2,6 M2,1 L2,6 7,6 M2,12 A8,8 0 0 0 16,16 L18,14 M13,14 L18,14 18,19",
            "Region" => "M2,7 L2,2 7,2 M13,2 L18,2 18,7 M18,13 L18,18 13,18 M7,18 L2,18 2,13",
            "Window" => "M2,3 L18,3 18,17 2,17 Z M2,7 L18,7",
            "Full monitor" => "M1,2 L19,2 19,15 1,15 Z M10,15 L10,19 M6,19 L14,19",
            "Marker" => "M2,7 L2,2 7,2 M13,2 L18,2 18,7 M18,13 L18,18 13,18 M7,18 L2,18 2,13 M10,6 L10,14 M6,10 L14,10",
            "Pencil" => "M3,13 L13,3 17,7 7,17 2,18 Z M11,5 L15,9",
            "Arrow" => "M2,18 L18,2 M8,2 L18,2 18,12",
            "Rectangle" => "M2,2 L18,2 18,18 2,18 Z",
            "Undo" => "M7,3 L2,8 7,13 M2,8 L13,8 C20,8 20,18 13,18",
            "Redo" => "M13,3 L18,8 13,13 M18,8 L7,8 C0,8 0,18 7,18",
            "Done" => "M2,10 L8,16 19,3",
            _ => "M3,3 L17,17 M17,3 L3,17"
        })
    };
    internal static StackPanel ToolCaption(string label,string key,bool selected)
    {
        var row=new StackPanel { Orientation=Orientation.Horizontal };
        var icon=Icon(label,selected ? OnAccent : UtilityUi.Ink);icon.Margin=new Thickness(0,0,8,0);row.Children.Add(icon);
        row.Children.Add(UtilityUi.ShortcutCaption(label,key,selected));return row;
    }
}
