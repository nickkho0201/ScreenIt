using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;

// Annotation color popover. Presentation/input only: it never draws on the screenshot,
// never commits and owns no state outside the overlay that created it.
internal readonly record struct AnnotationColorPreset(string Name, AnnotationColor Color);

internal sealed class AnnotationColorPicker : Border
{
    internal static readonly AnnotationColorPreset[] Presets =
    [
        new("Signal", new AnnotationColor(240,77,50)),
        new("Yellow", new AnnotationColor(245,197,66)),
        new("Lime", new AnnotationColor(197,217,71)),
        new("Cyan", new AnnotationColor(46,196,198)),
        new("Blue", new AnnotationColor(61,123,255)),
        new("Purple", new AnnotationColor(139,92,246)),
        new("White", new AnnotationColor(255,255,255)),
        new("Black", new AnnotationColor(26,26,26))
    ];
    private readonly bool ember;
    private readonly Action<AnnotationColor> apply;
    private readonly TextBlock title,presetsLabel,customLabel;
    private readonly Border divider,preview;
    private readonly Button previewButton;
    private readonly WrapPanel presetRow;
    private readonly ColorPlane plane;
    private readonly HueBar hue;
    private readonly TextBox red,green,blue;
    private readonly List<TextBlock> channelLabels=[];
    private readonly NotchSurface notch;
    private readonly TranslateTransform revealOffset=new();
    private readonly List<(Button Button,AnnotationColorPreset Preset,Border Frame,System.Windows.Shapes.Path Check)> swatchViews=[];
    private AnnotationColor color;
    private bool updating;
    internal event Action? DismissRequested;
    internal AnnotationColor Color => color;
    internal bool HasActiveMotion => HasAnimatedProperties || revealOffset.HasAnimatedProperties;
    internal bool IsInteracting => plane.IsMouseCaptured || hue.IsMouseCaptured;
    internal TranslateTransform MotionOffset => revealOffset;
    // Keyboard containment: Tab/Ctrl+Tab cycle inside the popup only.
    internal IReadOnlyList<UIElement> FocusOrder => [..swatchViews.Select(v=>v.Button),plane,hue,red,green,blue,previewButton];
    internal UIElement NextFocus(UIElement? current,bool backwards)
    {
        var order=FocusOrder;
        int index=-1;
        if(current!=null) for(int i=0;i<order.Count;i++) if(ReferenceEquals(order[i],current) || order[i].IsAncestorOf(current)) { index=i;break; }
        int next=index<0 ? (backwards ? order.Count-1 : 0) : (index+(backwards ? -1 : 1)+order.Count)%order.Count;
        return order[next];
    }
    private UIElement MoveFocus(bool backwards)
    {
        var target=NextFocus(Keyboard.FocusedElement as UIElement,backwards);
        Keyboard.Focus(target);return target;
    }
    internal double NotchOffset { get => notch.Offset; set { notch.Offset=value; notch.InvalidateVisual(); } }
    internal FrameworkElement NotchElement => notch;
    internal IReadOnlyList<Button> Swatches => swatchViews.Select(v=>v.Button).ToArray();
    internal TextBox RedInput => red; internal TextBox GreenInput => green; internal TextBox BlueInput => blue;
    internal ColorPlane Plane => plane; internal HueBar Hue => hue; internal Border PreviewSwatch => preview;
    internal Button ApplyButton => previewButton;
    internal IReadOnlyList<TextBlock> ChannelLabels => channelLabels;
    internal AnnotationColorPicker(AnnotationColor initial,Action<AnnotationColor> apply,bool ember)
    {
        this.ember=ember;this.apply=apply;color=initial;
        Background=Appearance.Palette.Surface;BorderBrush=Appearance.Palette.Border;BorderThickness=new Thickness(1);
        CornerRadius=new CornerRadius(ember ? 4 : 10);Padding=new Thickness(16,14,16,16);
        Effect=new DropShadowEffect { BlurRadius=ember ? 12 : 18,ShadowDepth=2,Opacity=ember ? .18 : .22 };
        RenderTransform=revealOffset;
        var stack=new StackPanel();
        title=Label(L.T("Annotation color"),14,UtilityUi.Ink,FontWeights.SemiBold);
        stack.Children.Add(title);
        presetsLabel=Label(L.T("Presets"),11,UtilityUi.Muted,FontWeights.Normal);presetsLabel.Margin=new Thickness(0,10,0,6);
        stack.Children.Add(presetsLabel);
        presetRow=new WrapPanel { Orientation=Orientation.Horizontal };
        var template=SwatchTemplate();
        foreach(var preset in Presets)
        {
            var check=new System.Windows.Shapes.Path { Data=System.Windows.Media.Geometry.Parse("M2,7 L6,11 L12,3"),Stroke=Painter.MarkerInk(preset.Color),StrokeThickness=2,StrokeStartLineCap=PenLineCap.Round,StrokeEndLineCap=PenLineCap.Round,IsHitTestVisible=false,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center };
            var frame=new Border { Width=32,Height=32,CornerRadius=new CornerRadius(6),BorderThickness=new Thickness(1),BorderBrush=Brushes.Transparent,Background=Painter.Brush(preset.Color),Child=check };
            var button=new Button { Content=frame,Width=40,Height=40,Margin=new Thickness(2),Padding=new Thickness(0),Background=Brushes.Transparent,BorderThickness=new Thickness(0),Cursor=Cursors.Hand,Template=template,Focusable=true };
            button.Resources["SwatchRing"]=ConstructDesign.Focus;button.Resources["SwatchHover"]=Appearance.Palette.Border;
            AutomationProperties.SetName(button,L.T(preset.Name));
            button.Click+=(_,_)=> { SetColor(preset.Color,true);DismissRequested?.Invoke(); };
            presetRow.Children.Add(button);swatchViews.Add((button,preset,frame,check));
        }
        stack.Children.Add(presetRow);
        divider=new Border { Height=1,Background=Appearance.Palette.Border,Margin=new Thickness(0,12,0,10) };
        stack.Children.Add(divider);
        customLabel=Label(L.T("Custom color"),11,UtilityUi.Muted,FontWeights.Normal);customLabel.Margin=new Thickness(0,0,0,8);
        stack.Children.Add(customLabel);
        hue=new HueBar { Height=16,Margin=new Thickness(0,10,0,0) };
        plane=new ColorPlane { Height=104 };
        plane.Changed+=(s,v)=> { hsvSaturation=s;hsvValue=v;ApplyHsvState(); };
        AutomationProperties.SetName(plane,L.T("Custom color"));
        AutomationProperties.SetHelpText(plane,L.T("Arrow keys adjust saturation and brightness."));
        stack.Children.Add(plane);
        hue.Changed+=h=> { hsvHue=h;ApplyHsvState(); };
        AutomationProperties.SetName(hue,L.T("Hue"));
        AutomationProperties.SetHelpText(hue,L.T("Arrow keys adjust hue."));
        stack.Children.Add(hue);
        var rgb=new Grid { Margin=new Thickness(0,14,0,0) };
        rgb.ColumnDefinitions.Add(new ColumnDefinition { Width=GridLength.Auto });rgb.ColumnDefinitions.Add(new ColumnDefinition());
        var channels=new StackPanel { Orientation=Orientation.Horizontal };
        red=Channel("R",channels);green=Channel("G",channels);blue=Channel("B",channels);
        red.TextChanged+=(_,_)=>RgbChanged();green.TextChanged+=(_,_)=>RgbChanged();blue.TextChanged+=(_,_)=>RgbChanged();
        foreach(var (box,channel) in new[] { (red,0),(green,1),(blue,2) })
            box.LostKeyboardFocus+=(_,_)=>NormalizeChannel(box,channel);
        rgb.Children.Add(channels);
        preview=new Border { Width=40,Height=40,CornerRadius=new CornerRadius(ember ? 4 : 6),BorderThickness=new Thickness(1),BorderBrush=Appearance.Palette.Border,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center };
        previewButton=new Button { Content=preview,Width=44,Height=44,Padding=new Thickness(0),Background=Brushes.Transparent,BorderThickness=new Thickness(0),Cursor=Cursors.Hand,Template=SwatchTemplate(),Focusable=true,HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Center };
        previewButton.Resources["SwatchRing"]=ConstructDesign.Focus;previewButton.Resources["SwatchHover"]=Appearance.Palette.Border;
        previewButton.ToolTip=new ToolTip { Content=L.T("Apply color"),Background=Appearance.Palette.Surface,Foreground=UtilityUi.Ink,BorderBrush=Appearance.Palette.Border };
        AutomationProperties.SetName(previewButton,L.T("Apply color"));
        previewButton.Click+=(_,_)=> { apply(color);DismissRequested?.Invoke(); };
        Grid.SetColumn(previewButton,1);rgb.Children.Add(previewButton);
        stack.Children.Add(rgb);
        Child=stack;
        KeyboardNavigation.SetTabNavigation(this,KeyboardNavigationMode.Cycle);
        KeyboardNavigation.SetControlTabNavigation(this,KeyboardNavigationMode.Cycle);
        KeyboardNavigation.SetDirectionalNavigation(this,KeyboardNavigationMode.Contained);
        PreviewKeyDown+=(_,e)=> { if(e.Key!=Key.Tab) return;MoveFocus((Keyboard.Modifiers & ModifierKeys.Shift)!=0);e.Handled=true; };
        notch=new NotchSurface();
        var initialHsv=initial.ToHsv();hsvHue=initialHsv.Hue;hsvSaturation=initialHsv.Saturation;hsvValue=initialHsv.Value;
        ApplyHsvState();
    }
    private static TextBlock Label(string text,double size,Brush brush,FontWeight weight) => new() { Text=text,FontSize=size,Foreground=brush,FontWeight=weight,FontFamily=ConstructDesign.Body,TextWrapping=TextWrapping.NoWrap };
    private TextBox Channel(string name,StackPanel parent)
    {
        var group=new StackPanel { Margin=new Thickness(0,0,14,0) };
        var label=new TextBlock { Text=name,FontSize=10,Foreground=UtilityUi.Muted,FontFamily=ConstructDesign.Body,Margin=new Thickness(0,0,0,3) };
        channelLabels.Add(label);group.Children.Add(label);
        var box=new TextBox { Width=44,Height=28,FontSize=12,TextAlignment=TextAlignment.Center,VerticalContentAlignment=VerticalAlignment.Center,Padding=new Thickness(0),BorderThickness=new Thickness(1),
            Background=Appearance.Palette.Editor,Foreground=UtilityUi.Ink,BorderBrush=Appearance.Palette.Border,FontFamily=ConstructDesign.Body,CaretBrush=UtilityUi.Ink };
        box.PreviewTextInput+=(_,e)=>e.Handled=!e.Text.All(char.IsDigit);
        AutomationProperties.SetName(box,name);
        group.Children.Add(box);parent.Children.Add(group);return box;
    }
    private void RgbChanged()
    {
        if(updating) return;
        // Normalize each channel on its own; the candidate changes only when every channel is valid.
        bool redOk=TryChannel(red,out var r,out var redOverflow);
        bool greenOk=TryChannel(green,out var g,out var greenOverflow);
        bool blueOk=TryChannel(blue,out var b,out var blueOverflow);
        if(redOverflow) ClampField(red);if(greenOverflow) ClampField(green);if(blueOverflow) ClampField(blue);
        if(!redOk || !greenOk || !blueOk) return;
        SetColor(AnnotationColor.FromRgb(r,g,b));
    }
    // Digits above 255 are clamped, never truncated: long numeric strings and pasted overflow stay safe.
    private static bool TryChannel(TextBox box,out int value,out bool overflow)
    {
        value=0;overflow=false;
        var text=box.Text;
        if(text.Length==0 || !text.All(char.IsDigit)) return false;
        var digits=text.TrimStart('0');
        if(digits.Length==0) return true;
        if(digits.Length>3) { value=255;overflow=true;return true; }
        if(!int.TryParse(digits,NumberStyles.None,CultureInfo.InvariantCulture,out value)) return false;
        if(value>255) { value=255;overflow=true; }
        return true;
    }
    private void ClampField(TextBox box)
    {
        updating=true;box.Text="255";box.CaretIndex=3;updating=false;
    }
    private void NormalizeChannel(TextBox box,int channel)
    {
        if(TryChannel(box,out _,out var overflow)) { if(overflow) ClampField(box);return; }
        var value=channel switch { 0=>color.R,1=>color.G,_=>color.B };
        updating=true;box.Text=value.ToString(CultureInfo.InvariantCulture);updating=false;
    }
    // Editing HSV is kept separately from the RGB byte value: an achromatic RGB result must not reset
    // the hue (or the saturation of pure black) the user is currently dragging.
    private double hsvHue,hsvSaturation,hsvValue;
    private void SetColor(AnnotationColor next,bool notify) { AdoptExternal(next); if(notify) apply(color); }
    private void AdoptExternal(AnnotationColor next)
    {
        var hsv=next.ToHsv();
        // Hue is undefined for gray/black/white; keep the interaction hue so the sliders stay put.
        if(hsv.Saturation>0.0001) hsvHue=hsv.Hue;
        // Pure black (V=0) carries no saturation either; keep it for a later brightness raise.
        if(hsv.Value>0.0001) hsvSaturation=hsv.Saturation;
        hsvValue=hsv.Value;
        ApplyHsvState();
    }
    // Re-entrant-safe: programmatic RGB/plane updates never feed back into the current text field.
    private void ApplyHsvState()
    {
        var next=AnnotationColor.FromHsv(hsvHue,hsvSaturation,hsvValue);
        color=next;updating=true;
        plane.Hue=hsvHue;plane.SetSv(hsvSaturation,hsvValue);hue.Hue=hsvHue;
        if(!red.IsKeyboardFocusWithin) red.Text=next.R.ToString(CultureInfo.InvariantCulture);
        if(!green.IsKeyboardFocusWithin) green.Text=next.G.ToString(CultureInfo.InvariantCulture);
        if(!blue.IsKeyboardFocusWithin) blue.Text=next.B.ToString(CultureInfo.InvariantCulture);
        preview.Background=Painter.Brush(next);
        foreach(var (_,preset,frame,check) in swatchViews)
        {
            bool selected=preset.Color==next;
            frame.BorderBrush=selected ? UtilityUi.Ink : Appearance.Palette.Border;frame.BorderThickness=new Thickness(selected ? 2 : 1);
            check.Visibility=selected ? Visibility.Visible : Visibility.Collapsed;
        }
        updating=false;
    }
    internal void SetColor(AnnotationColor next) => AdoptExternal(next);
    internal void Reveal(bool animate) => UiMotion.Reveal(this,revealOffset,animate);
    internal void StopMotion() => UiMotion.Stop(this,revealOffset);
    internal void StopInteraction() { if(plane.IsMouseCaptured) plane.ReleaseMouseCapture();if(hue.IsMouseCaptured) hue.ReleaseMouseCapture(); }
    internal void ApplyTheme()
    {
        Background=Appearance.Palette.Surface;BorderBrush=Appearance.Palette.Border;
        title.Foreground=UtilityUi.Ink;presetsLabel.Foreground=UtilityUi.Muted;customLabel.Foreground=UtilityUi.Muted;
        foreach(var label in channelLabels) label.Foreground=UtilityUi.Muted;
        divider.Background=Appearance.Palette.Border;preview.BorderBrush=Appearance.Palette.Border;
        foreach(var box in new[]{red,green,blue})
        {
            box.Background=Appearance.Palette.Editor;box.Foreground=UtilityUi.Ink;box.BorderBrush=Appearance.Palette.Border;box.CaretBrush=UtilityUi.Ink;
        }
        foreach(var (button,_,_,_) in swatchViews)
        {
            button.Resources["SwatchRing"]=ConstructDesign.Focus;button.Resources["SwatchHover"]=Appearance.Palette.Border;
            if(button.ToolTip is ToolTip tip) { tip.Background=Appearance.Palette.Surface;tip.Foreground=UtilityUi.Ink; }
        }
        previewButton.Resources["SwatchRing"]=ConstructDesign.Focus;previewButton.Resources["SwatchHover"]=Appearance.Palette.Border;
        if(previewButton.ToolTip is ToolTip applyTip) { applyTip.Background=Appearance.Palette.Surface;applyTip.Foreground=UtilityUi.Ink;applyTip.BorderBrush=Appearance.Palette.Border; }
        notch.InvalidateVisual();plane.InvalidateVisual();hue.InvalidateVisual();
        ApplyHsvState();
        L.Tree(this);
    }
    private static ControlTemplate SwatchTemplate()
    {
        var root=new FrameworkElementFactory(typeof(Border));
        root.SetValue(Border.CornerRadiusProperty,new CornerRadius(8));
        root.SetValue(Border.BorderThicknessProperty,new Thickness(2));
        root.SetValue(Border.BorderBrushProperty,Brushes.Transparent);
        root.SetValue(Border.PaddingProperty,new Thickness(1));
        root.SetValue(Border.BackgroundProperty,Brushes.Transparent);
        var presenter=new FrameworkElementFactory(typeof(ContentPresenter));
        presenter.SetValue(FrameworkElement.HorizontalAlignmentProperty,HorizontalAlignment.Center);
        presenter.SetValue(FrameworkElement.VerticalAlignmentProperty,VerticalAlignment.Center);
        root.AppendChild(presenter);
        var template=new ControlTemplate(typeof(Button)) { VisualTree=root };
        var hover=new Trigger { Property=UIElement.IsMouseOverProperty,Value=true };
        hover.Setters.Add(new Setter(Border.BorderBrushProperty,new DynamicResourceExtension("SwatchHover")));
        template.Triggers.Add(hover);
        var focus=new Trigger { Property=UIElement.IsKeyboardFocusedProperty,Value=true };
        focus.Setters.Add(new Setter(Border.BorderBrushProperty,new DynamicResourceExtension("SwatchRing")));
        template.Triggers.Add(focus);
        return template;
    }
    internal sealed class NotchSurface : FrameworkElement
    {
        internal double Offset { get; set; }
        protected override void OnRender(DrawingContext dc)
        {
            const double width=13,height=8;
            double x=Math.Max(width/2,Offset);
            var fill=new StreamGeometry();
            using(var c=fill.Open()) { c.BeginFigure(new Point(x-width/2,height),true,true);c.LineTo(new Point(x,0),true,false);c.LineTo(new Point(x+width/2,height),true,false); }
            dc.DrawGeometry(Appearance.Palette.Surface,null,fill);
            var border=new StreamGeometry();
            using(var c=border.Open()) { c.BeginFigure(new Point(x-width/2,height),false,false);c.LineTo(new Point(x,0),true,false);c.LineTo(new Point(x+width/2,height),true,false); }
            dc.DrawGeometry(null,new Pen(Appearance.Palette.Border,1),border);
        }
    }
}

// Saturation (horizontal) / brightness (vertical) plane with keyboard support.
internal sealed class ColorPlane : FrameworkElement
{
    private double hue=240;
    internal double Saturation { get; private set; }=1;
    internal double Value { get; private set; }=1;
    internal event Action<double,double>? Changed;
    internal double Hue { set { hue=value;InvalidateVisual(); } }
    internal void SetSv(double saturation,double value) { Saturation=Math.Clamp(saturation,0,1);Value=Math.Clamp(value,0,1);InvalidateVisual(); }
    public ColorPlane() { Focusable=true;FocusVisualStyle=null; }
    private Point Thumb => new(Saturation*Math.Max(1,ActualWidth),(1-Value)*Math.Max(1,ActualHeight));
    protected override void OnRender(DrawingContext dc)
    {
        var rect=new Rect(0,0,Math.Max(1,ActualWidth),Math.Max(1,ActualHeight));
        var hueBrush=new SolidColorBrush(AnnotationColor.FromHsv(hue,1,1).ToBrushColor());hueBrush.Freeze();
        var horizontal=new LinearGradientBrush(Colors.White,hueBrush.Color,0);horizontal.Freeze();
        dc.DrawRoundedRectangle(horizontal,null,rect,4,4);
        var vertical=new LinearGradientBrush(Color.FromArgb(0,0,0,0),Color.FromArgb(255,0,0,0),90);vertical.Freeze();
        dc.DrawRoundedRectangle(vertical,null,rect,4,4);
        var point=Thumb;
        dc.DrawEllipse(null,new Pen(Brushes.White,2.5),point,7,7);
        dc.DrawEllipse(null,new Pen(ConstructDesign.Ink,1),point,8,8);
        if(IsKeyboardFocused) dc.DrawRoundedRectangle(null,new Pen(ConstructDesign.Focus,2),new Rect(1,1,Math.Max(1,rect.Width-2),Math.Max(1,rect.Height-2)),4,4);
    }
    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e) { Focus();CaptureMouse();SetFromPoint(e.GetPosition(this));e.Handled=true; }
    protected override void OnMouseMove(MouseEventArgs e) { if(IsMouseCaptured) SetFromPoint(e.GetPosition(this)); }
    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e) { if(!IsMouseCaptured) return;SetFromPoint(e.GetPosition(this));ReleaseMouseCapture();e.Handled=true; }
    protected override void OnLostMouseCapture(MouseEventArgs e) { base.OnLostMouseCapture(e); }
    private void SetFromPoint(Point point)
    {
        double width=Math.Max(1,ActualWidth),height=Math.Max(1,ActualHeight);
        double s=Math.Clamp(point.X/width,0,1),v=Math.Clamp(1-point.Y/height,0,1);
        SetSv(s,v);Changed?.Invoke(s,v);
    }
    internal void SetFromPointForTest(Point point) => SetFromPoint(point);
    protected override void OnKeyDown(KeyEventArgs e)
    {
        double step=e.KeyboardDevice.Modifiers.HasFlag(ModifierKeys.Shift) ? .1 : .02;
        double s=Saturation,v=Value;
        switch(e.Key)
        {
            case Key.Left: s-=step;break;case Key.Right: s+=step;break;case Key.Up: v+=step;break;case Key.Down: v-=step;break;
            default: base.OnKeyDown(e);return;
        }
        SetSv(s,v);Changed?.Invoke(Saturation,Value);e.Handled=true;
    }
    protected override void OnGotKeyboardFocus(KeyboardFocusChangedEventArgs e) { base.OnGotKeyboardFocus(e);InvalidateVisual(); }
    protected override void OnLostKeyboardFocus(KeyboardFocusChangedEventArgs e) { base.OnLostKeyboardFocus(e);InvalidateVisual(); }
}

// Rainbow hue strip with a round thumb and keyboard support.
internal sealed class HueBar : FrameworkElement
{
    private double hue=240;
    internal double Hue { get => hue; set { hue=((value%360)+360)%360;InvalidateVisual(); } }
    internal event Action<double>? Changed;
    public HueBar() { Focusable=true;FocusVisualStyle=null; }
    protected override void OnRender(DrawingContext dc)
    {
        var rect=new Rect(0,0,Math.Max(1,ActualWidth),Math.Max(1,ActualHeight));
        var brush=new LinearGradientBrush { StartPoint=new Point(0,0),EndPoint=new Point(1,0) };
        for(int i=0;i<=6;i++) brush.GradientStops.Add(new GradientStop(AnnotationColor.FromHsv(i*60,1,1).ToBrushColor(),i/6.0));
        brush.Freeze();var radius=rect.Height/2;
        dc.DrawRoundedRectangle(brush,null,rect,radius,radius);
        var point=new Point(hue/360*rect.Width,rect.Height/2);
        dc.DrawEllipse(null,new Pen(Brushes.White,2.5),point,Math.Max(5,radius-1),Math.Max(5,radius-1));
        dc.DrawEllipse(null,new Pen(ConstructDesign.Ink,1),point,Math.Max(6,radius),Math.Max(6,radius));
        if(IsKeyboardFocused) dc.DrawRoundedRectangle(null,new Pen(ConstructDesign.Focus,2),new Rect(-1,-1,rect.Width+2,rect.Height+2),radius+1,radius+1);
    }
    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e) { Focus();CaptureMouse();SetFromPoint(e.GetPosition(this));e.Handled=true; }
    protected override void OnMouseMove(MouseEventArgs e) { if(IsMouseCaptured) SetFromPoint(e.GetPosition(this)); }
    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e) { if(!IsMouseCaptured) return;SetFromPoint(e.GetPosition(this));ReleaseMouseCapture();e.Handled=true; }
    private void SetFromPoint(Point point)
    {
        Hue=Math.Clamp(point.X/Math.Max(1,ActualWidth),0,1)*360;
        Changed?.Invoke(Hue);
    }
    internal void SetFromPointForTest(Point point) => SetFromPoint(point);
    protected override void OnKeyDown(KeyEventArgs e)
    {
        double step=e.KeyboardDevice.Modifiers.HasFlag(ModifierKeys.Shift) ? 10 : 2;
        switch(e.Key)
        {
            case Key.Left: Hue-=step;break;case Key.Right: Hue+=step;break;
            case Key.Home: Hue=0;break;case Key.End: Hue=359;break;
            default: base.OnKeyDown(e);return;
        }
        Changed?.Invoke(Hue);e.Handled=true;
    }
    protected override void OnGotKeyboardFocus(KeyboardFocusChangedEventArgs e) { base.OnGotKeyboardFocus(e);InvalidateVisual(); }
    protected override void OnLostKeyboardFocus(KeyboardFocusChangedEventArgs e) { base.OnLostKeyboardFocus(e);InvalidateVisual(); }
}

internal static class AnnotationColorBrush
{
    internal static Color ToBrushColor(this AnnotationColor color) => Color.FromRgb(color.R,color.G,color.B);
}
