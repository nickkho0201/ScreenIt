using System.Windows;
using System.Windows.Controls;
using System.Windows.Automation;
using System.Windows.Media;
using System.Windows.Media.Imaging;

internal static class UtilityUi
{
    public static SolidColorBrush Accent => UiDesign.Accent;
    public static SolidColorBrush Ink => Appearance.Palette.Text;
    public static SolidColorBrush Muted => Appearance.Palette.Muted;
    public static SolidColorBrush Brush(byte r, byte g, byte b) { var value = new SolidColorBrush(Color.FromRgb(r,g,b)); value.Freeze(); return value; }
    internal static CheckBox Checkbox(string label,bool value=false)
    {
        var checkbox=new CheckBox { Content=new TextBlock { Text=L.T(label),TextWrapping=TextWrapping.Wrap },IsChecked=value,
            FontSize=13,Foreground=Ink,Background=Appearance.Palette.Editor,BorderBrush=Appearance.Palette.Border,
            HorizontalContentAlignment=HorizontalAlignment.Stretch,VerticalContentAlignment=VerticalAlignment.Center,
            Cursor=System.Windows.Input.Cursors.Hand,Margin=new Thickness(0,0,0,18) };
        AutomationProperties.SetName(checkbox,L.T(label));
        checkbox.Resources["CheckAccent"]=Accent;checkbox.Resources["CheckFocus"]=ConstructDesign.Focus;checkbox.Resources["CheckInk"]=ConstructDesign.OnAccent;
        checkbox.Template=(ControlTemplate)System.Windows.Markup.XamlReader.Parse("""
            <ControlTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" TargetType="CheckBox">
              <Grid Background="Transparent" MinHeight="28">
                <Grid.ColumnDefinitions><ColumnDefinition Width="20"/><ColumnDefinition Width="*"/></Grid.ColumnDefinitions>
                <Border x:Name="Box" Width="18" Height="18" CornerRadius="4" Background="{TemplateBinding Background}" BorderBrush="{TemplateBinding BorderBrush}" BorderThickness="1" VerticalAlignment="Center">
                  <Path x:Name="Tick" Data="M 3,8 L 6,11 L 12,4" Stroke="{DynamicResource CheckInk}" StrokeThickness="2" Opacity="0"/>
                </Border>
                <ContentPresenter Grid.Column="1" Margin="9,2,0,2" VerticalAlignment="Center"/>
              </Grid>
              <ControlTemplate.Triggers>
                <Trigger Property="IsChecked" Value="True"><Setter TargetName="Box" Property="Background" Value="{DynamicResource CheckAccent}"/><Setter TargetName="Box" Property="BorderBrush" Value="{DynamicResource CheckAccent}"/><Setter TargetName="Tick" Property="Opacity" Value="1"/></Trigger>
                <Trigger Property="IsMouseOver" Value="True"><Setter TargetName="Box" Property="BorderBrush" Value="{DynamicResource CheckAccent}"/></Trigger>
                <Trigger Property="IsKeyboardFocused" Value="True"><Setter TargetName="Box" Property="BorderBrush" Value="{DynamicResource CheckFocus}"/><Setter TargetName="Box" Property="BorderThickness" Value="2"/></Trigger>
                <Trigger Property="IsPressed" Value="True"><Setter Property="Opacity" Value="0.75"/></Trigger>
                <Trigger Property="IsEnabled" Value="False"><Setter Property="Opacity" Value="0.4"/></Trigger>
              </ControlTemplate.Triggers>
            </ControlTemplate>
            """);
        bool wasChecked=value;
        void AnimateTick()
        {
            bool selected=checkbox.IsChecked==true;checkbox.ApplyTemplate();
            if(checkbox.IsVisible && checkbox.Template.FindName("Tick",checkbox) is System.Windows.Shapes.Path tick)
            {
                double from=tick.HasAnimatedProperties ? tick.Opacity : wasChecked ? 1 : 0;
                tick.BeginAnimation(UIElement.OpacityProperty,null);
                if(SystemParameters.ClientAreaAnimation) tick.BeginAnimation(UIElement.OpacityProperty,new System.Windows.Media.Animation.DoubleAnimation(from,selected ? 1 : 0,TimeSpan.FromMilliseconds(110)) { FillBehavior=System.Windows.Media.Animation.FillBehavior.Stop });
            }
            wasChecked=selected;
        }
        checkbox.Checked+=(_,_)=>AnimateTick();checkbox.Unchecked+=(_,_)=>AnimateTick();
        checkbox.Unloaded+=(_,_)=> { if(checkbox.Template.FindName("Tick",checkbox) is System.Windows.Shapes.Path tick) tick.BeginAnimation(UIElement.OpacityProperty,null); };
        return checkbox;
    }
    public static Button Button(string label, string hint, Action action)
    {
        var border = new FrameworkElementFactory(typeof(Border));
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(UiDesign.IsEmber ? 4 : 6));
        border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
        border.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Control.BorderBrushProperty));
        border.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Control.BorderThicknessProperty));
        border.SetValue(Border.PaddingProperty, new Thickness(10,6,10,6));
        var content = new FrameworkElementFactory(typeof(ContentPresenter));
        content.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        content.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center); border.AppendChild(content);
        var template = new ControlTemplate(typeof(Button)) { VisualTree = border };
        var hover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
        hover.Setters.Add(new Setter(Control.BorderBrushProperty, Accent)); template.Triggers.Add(hover);
        var focus = new Trigger { Property = UIElement.IsKeyboardFocusedProperty, Value = true };
        focus.Setters.Add(new Setter(Control.BorderBrushProperty, new DynamicResourceExtension("UiFocus")));
        focus.Setters.Add(new Setter(Control.BorderThicknessProperty,new Thickness(2))); template.Triggers.Add(focus);
        var disabled = new Trigger { Property = UIElement.IsEnabledProperty, Value = false };
        disabled.Setters.Add(new Setter(UIElement.OpacityProperty, .4)); template.Triggers.Add(disabled);
        var tooltip = new ToolTip { Content=L.T(hint),Background=Appearance.Palette.Surface,Foreground=Ink,FontFamily=ConstructDesign.Body,BorderBrush=Appearance.Palette.Border };
        var button = new Button { Content = L.T(label), ToolTip = tooltip, Template = template, MinHeight = 34, Margin = new Thickness(2), FontSize = 13, FontFamily=ConstructDesign.Body, FontWeight=UiDesign.IsEmber ? FontWeights.SemiBold : FontWeights.Normal,
            Background = Brushes.Transparent, Foreground = Ink, BorderBrush = Brushes.Transparent, BorderThickness = new Thickness(1), Cursor = System.Windows.Input.Cursors.Arrow };
        button.Resources["UiFocus"]=ConstructDesign.Focus;
        ToolTipService.SetShowOnDisabled(button,true);
        AutomationProperties.SetName(button, L.T(hint)); button.Click += (_,_) => action(); return button;
    }
    internal static StackPanel ShortcutCaption(string label,string key,bool emphasized=false)
    {
        var row=new StackPanel { Orientation=Orientation.Horizontal };
        row.Children.Add(new TextBlock { Text=L.T(label),VerticalAlignment=VerticalAlignment.Center });
        row.Children.Add(new Border { CornerRadius=new CornerRadius(UiDesign.IsEmber ? 2 : 3),BorderThickness=new Thickness(1),
            BorderBrush=emphasized ? (UiDesign.IsEmber ? ConstructDesign.OnAccent : Brush(180,188,224)) : Appearance.Palette.Border,
            Padding=new Thickness(4,1,4,1),Margin=new Thickness(7,0,0,0),VerticalAlignment=VerticalAlignment.Center,
            Child=new TextBlock { Text=key,FontSize=10,FontWeight=UiDesign.IsEmber ? FontWeights.SemiBold : FontWeights.Normal,Foreground=emphasized ? (UiDesign.IsEmber ? ConstructDesign.OnAccent : Brushes.White) : Muted } });
        return row;
    }
    internal static bool PromptOpen => Application.Current?.Windows.OfType<PromptWindow>().Any(w=>w.IsVisible)==true;
    internal static bool Confirm(Window owner,string title,string description,string accept)
    {
        var prompt=new PromptWindow(title,description,accept);prompt.Prepare(owner);return prompt.ShowDialog()==true;
    }
    internal static void Inform(string text)
    {
        var owner=Application.Current?.MainWindow;
        if(owner==null) return;
        var prompt=new PromptWindow("ScreenIt",L.T(text),"OK",cancel:false);prompt.Prepare(owner);prompt.ShowDialog();
    }
    public static byte[] IconBytes { get; } = ReadIcon();
    private static byte[] ReadIcon() { using var stream = typeof(UtilityUi).Assembly.GetManifestResourceStream("ScreenIt.Icon")!; using var bytes = new MemoryStream(); stream.CopyTo(bytes); return bytes.ToArray(); }
    public static System.Drawing.Icon TrayIcon()
    {
        using var stream = new MemoryStream(IconBytes); using var icon = new System.Drawing.Icon(stream,System.Windows.Forms.SystemInformation.SmallIconSize); return (System.Drawing.Icon)icon.Clone();
    }
    public static BitmapSource WindowIcon()
    {
        using var stream = new MemoryStream(IconBytes); var image = BitmapDecoder.Create(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad).Frames.OrderBy(f => Math.Abs(f.PixelWidth-32)).First(); image.Freeze(); return image;
    }
}

// Small application-owned prompt; Clear uses non-modal lifetime, other short prompts can be modal.
internal class PromptWindow : Window
{
    internal Button CancelButton { get; }
    internal Button AcceptButton { get; }
    internal string Description { get; }
    internal bool Accepted { get; private set; }
    internal UiTheme AppliedTheme { get; private set; }
    private readonly bool modal;
    protected StackPanel BodyPanel { get; }
    private readonly TextBlock? heading,detail,captionText;
    private readonly NoticeSurface? chrome;
    private readonly Button? closeButton;
    private readonly bool classic;
    public PromptWindow(string title,string description,string accept,bool modal = true,bool cancel = true)
    {
        FontFamily=ConstructDesign.Body;this.modal=modal;classic=UiDesign.IsClassic;Title="ScreenIt";Icon=UtilityUi.WindowIcon();Width=classic ? 390 : 520;SizeToContent=SizeToContent.Height;
        ResizeMode=ResizeMode.NoResize;ShowInTaskbar=false;Topmost=true;ShowActivated=true;WindowStartupLocation=WindowStartupLocation.Manual;
        Description=description;
        if(classic)
        {
            // 0.2.3 prompt: standard window chrome and a compact plain panel.
            Background=Appearance.Palette.Surface;
            var panel=BodyPanel=new StackPanel { Margin=new Thickness(22) };
            heading=new TextBlock { Text=title,FontSize=18,FontWeight=FontWeights.SemiBold,Foreground=UtilityUi.Ink };panel.Children.Add(heading);
            detail=new TextBlock { Text=description,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,12,0,18),Foreground=UtilityUi.Muted };panel.Children.Add(detail);
            var buttons=new StackPanel { Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right };
            AcceptButton=UtilityUi.Button(accept,accept=="Clear" ? "Clear session" : accept,()=>Complete(true));AcceptButton.Foreground=Brushes.White;AcceptButton.Background=UtilityUi.Accent;
            CancelButton=UtilityUi.Button("Cancel","Keep session",()=>Complete(false));CancelButton.IsDefault=cancel;CancelButton.IsCancel=cancel;
            if(!cancel) { CancelButton.Visibility=Visibility.Collapsed;AcceptButton.IsDefault=true; }
            buttons.Children.Add(AcceptButton);buttons.Children.Add(CancelButton);panel.Children.Add(buttons);Content=panel;
        }
        else
        {
            WindowStyle=WindowStyle.None;AllowsTransparency=true;Background=Brushes.Transparent;
            var shell=new StackPanel();
            var caption=new Grid { Height=48,Margin=new Thickness(20,0,12,0) };
            caption.ColumnDefinitions.Add(new() { Width=GridLength.Auto });caption.ColumnDefinitions.Add(new());caption.ColumnDefinitions.Add(new() { Width=GridLength.Auto });
            caption.Children.Add(new Image { Source=UtilityUi.WindowIcon(),Width=24,Height=24,Margin=new Thickness(0,0,12,0) });
            captionText=new TextBlock { FontSize=14,VerticalAlignment=VerticalAlignment.Center,TextTrimming=TextTrimming.CharacterEllipsis };
            captionText.SetBinding(TextBlock.TextProperty,new System.Windows.Data.Binding(nameof(Title)) { Source=this });Grid.SetColumn(captionText,1);caption.Children.Add(captionText);
            closeButton=UtilityUi.Button("×","Cancel",()=>Complete(false));closeButton.MinWidth=32;closeButton.FontSize=24;closeButton.Background=Brushes.Transparent;closeButton.BorderThickness=new Thickness(0);
            Grid.SetColumn(closeButton,2);caption.Children.Add(closeButton);
            caption.MouseLeftButtonDown+=(_,e)=> { if(e.LeftButton==System.Windows.Input.MouseButtonState.Pressed && !closeButton.IsMouseOver) DragMove(); };
            shell.Children.Add(caption);
            var panel=BodyPanel=new StackPanel { Margin=new Thickness(24,24,24,22) };
            var headline=new Grid();headline.ColumnDefinitions.Add(new() { Width=new GridLength(20) });headline.ColumnDefinitions.Add(new());
            headline.Children.Add(new Border { Background=UtilityUi.Accent,Width=3,Height=26,HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Top,Margin=new Thickness(0,3,0,0) });
            heading=new TextBlock { Text=title,FontFamily=ConstructDesign.Display,FontSize=22,FontWeight=FontWeights.Bold,TextWrapping=TextWrapping.Wrap,Foreground=UtilityUi.Ink };Grid.SetColumn(heading,1);headline.Children.Add(heading);panel.Children.Add(headline);
            detail=new TextBlock { Text=description,FontSize=15,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(20,10,0,18),Foreground=UtilityUi.Muted };panel.Children.Add(detail);
            var rule=new Grid { Height=24,Margin=new Thickness(0,0,0,12) };
            var line=ConstructDesign.Divider();line.Margin=new();line.VerticalAlignment=VerticalAlignment.Top;rule.Children.Add(line);
            panel.Children.Add(rule);
            var buttons=new StackPanel { Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right };
            AcceptButton=UtilityUi.Button(accept,accept=="Clear" ? "Clear session" : accept,()=>Complete(true));AcceptButton.Foreground=ConstructDesign.OnAccent;AcceptButton.Background=UtilityUi.Accent;
            CancelButton=UtilityUi.Button("Cancel","Keep session",()=>Complete(false));CancelButton.IsDefault=cancel;CancelButton.IsCancel=cancel;
            if(!cancel) { CancelButton.Visibility=Visibility.Collapsed;AcceptButton.IsDefault=true; }
            NoticeChrome.Action(AcceptButton);NoticeChrome.Action(CancelButton);
            buttons.Margin=new Thickness(0,12,0,0);
            buttons.Children.Add(AcceptButton);buttons.Children.Add(CancelButton);panel.Children.Add(buttons);shell.Children.Add(panel);
            chrome=new NoticeSurface { HeaderHeight=48,Child=shell,Margin=new Thickness(8),BorderBrush=Appearance.Palette.Border,Background=Appearance.Palette.Surface,
                Effect=new System.Windows.Media.Effects.DropShadowEffect { BlurRadius=10,ShadowDepth=2,Opacity=.16 } };
            Content=chrome;
        }
        ContentRendered+=(_,_)=> { if(!IsVisible) return;Position();BringForward(); };
        ApplyTheme();
        SourceInitialized+=(_,_)=>ApplyTheme();
        PreviewKeyDown+=(_,e)=>
        {
            if(e.Key==System.Windows.Input.Key.Escape) { e.Handled=true;Complete(false); }
            else if(cancel && e.Key==System.Windows.Input.Key.Enter && !AcceptButton.IsKeyboardFocusWithin) { e.Handled=true;Complete(false); }
        };
    }
    internal void BringForward()
    {
        if(!IsVisible) return;
        WindowState=WindowState.Normal;Topmost=true;Activate();
        PromptForeground(new System.Windows.Interop.WindowInteropHelper(this).Handle);
        if(CancelButton.IsDefault) CancelButton.Focus();else AcceptButton.Focus();
    }
    internal void ApplyTheme()
    {
        AppliedTheme=Appearance.Current;
        if(classic)
        {
            Background=Appearance.Palette.Surface;Foreground=UtilityUi.Ink;
            if(Content is StackPanel panel)
            {
                panel.Children.OfType<TextBlock>().First().Foreground=UtilityUi.Ink;
                panel.Children.OfType<TextBlock>().Last().Foreground=UtilityUi.Muted;
                foreach(var checkbox in panel.Children.OfType<CheckBox>())
                { checkbox.Foreground=UtilityUi.Ink;checkbox.Background=Appearance.Palette.Editor;checkbox.BorderBrush=Appearance.Palette.Border;checkbox.Resources["CheckFocus"]=UtilityUi.Accent;checkbox.Resources["CheckInk"]=Brushes.White; }
            }
            CancelButton.Foreground=UtilityUi.Ink;CancelButton.BorderBrush=Appearance.Palette.Border;
            AcceptButton.Background=UtilityUi.Accent;AcceptButton.Foreground=Brushes.White;
            foreach(var button in new[]{AcceptButton,CancelButton})
            {
                button.Resources["UiFocus"]=UtilityUi.Accent;
                if(button.ToolTip is ToolTip tip) { tip.Background=Appearance.Palette.Surface;tip.Foreground=UtilityUi.Ink;tip.BorderBrush=Appearance.Palette.Border; }
            }
        }
        else
        {
            Background=Brushes.Transparent;Foreground=UtilityUi.Ink;
            chrome!.Background=Appearance.Palette.Surface;chrome.BorderBrush=Appearance.Palette.Border;
            heading!.Foreground=UtilityUi.Ink;detail!.Foreground=UtilityUi.Muted;
            foreach(var checkbox in BodyPanel.Children.OfType<CheckBox>())
            { checkbox.Foreground=UtilityUi.Ink;checkbox.Background=Appearance.Palette.Editor;checkbox.BorderBrush=Appearance.Palette.Border;checkbox.Resources["CheckFocus"]=UtilityUi.Accent;checkbox.Resources["CheckInk"]=ConstructDesign.OnAccent; }
            closeButton!.Foreground=UtilityUi.Ink;chrome.InvalidateVisual();
            CancelButton.Foreground=UtilityUi.Ink;CancelButton.BorderBrush=Appearance.Palette.Border;
            AcceptButton.Background=UtilityUi.Accent;AcceptButton.Foreground=ConstructDesign.OnAccent;
            foreach(var button in new[]{AcceptButton,CancelButton})
            {
                button.Resources["UiFocus"]=ConstructDesign.Focus;
                if(button.ToolTip is ToolTip tip) { tip.Background=Appearance.Palette.Surface;tip.Foreground=UtilityUi.Ink;tip.BorderBrush=Appearance.Palette.Border; }
            }
            captionText!.SetBinding(TextBlock.TextProperty,new System.Windows.Data.Binding(nameof(Title)) { Source=this });
        }

        Title=L.T(Title);L.Tree((DependencyObject)Content);
        var hwnd=new System.Windows.Interop.WindowInteropHelper(this).Handle;
        if(hwnd!=IntPtr.Zero) { int dark=Appearance.Current==UiTheme.Dark ? 1 : 0;DwmSetWindowAttribute(hwnd,20,ref dark,4); }
    }
    [System.Runtime.InteropServices.DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr hwnd,int attribute,ref int value,int length);
    internal void Complete(bool accepted)
    {
        Accepted=accepted;
        if(modal) DialogResult=accepted;else Close();
    }
    [System.Runtime.InteropServices.DllImport("user32.dll",EntryPoint="SetForegroundWindow")] private static extern bool PromptForeground(IntPtr hwnd);
    private PxRect work;
    internal void Prepare(Window owner)
    {
        Owner=owner;work=ToastNative.Area(null,PasteInput.Foreground().Hwnd);
        var hwnd=new System.Windows.Interop.WindowInteropHelper(this).EnsureHandle();
        // The hidden HWND probes the target monitor first: its actual DPI decides the physical frame.
        ToastNative.Place(hwnd,new(work.X,work.Y,1,1));
        uint dpi=Native.GetDpiForWindow(hwnd);
        if(dpi==0) return; // No DPI answer: keep the rendered correction as the only placement.
        var scale=dpi/96.0;
        Width=Math.Min(classic ? 390 : 520,Math.Max(1,work.Width/scale));MaxHeight=Math.Max(1,work.Height/scale);
        PlaceBeforeShow(scale);
    }
    // Centers inside the work area without leaving it; shared by pre-show placement, the rendered correction and notices checks.
    internal static PxRect Centered(PxRect work,int width,int height)
    {
        width=Math.Max(1,Math.Min(width,work.Width));height=Math.Max(1,Math.Min(height,work.Height));
        return new(work.X+(work.Width-width)/2,work.Y+(work.Height-height)/2,width,height);
    }
    // Measures the same content WPF will lay out and moves the still-hidden HWND to its final frame,
    // so the first visible frame is already centered instead of appearing at the work-area origin.
    private void PlaceBeforeShow(double scale)
    {
        var hwnd=new System.Windows.Interop.WindowInteropHelper(this).Handle;
        if(hwnd==IntPtr.Zero || work.Width<=0 || Content is not FrameworkElement content) return;
        // A standard classic window includes its non-client frame in Width/Height;
        // measure the client content and add that frame back for the physical placement.
        double chromeWidth=0,chromeHeight=0;
        if(classic) { var frame=SystemParameters.WindowNonClientFrameThickness;chromeWidth=frame.Left+frame.Right;chromeHeight=frame.Top+frame.Bottom; }
        content.Measure(new Size(Math.Max(1,Width-chromeWidth),double.PositiveInfinity));
        if(!(content.DesiredSize.Height>0)) return;
        // WPF rounds SizeToContent up in DIPs before the DPI transform, so mirror that to keep the first frame final.
        double height=Math.Ceiling(content.DesiredSize.Height)+chromeHeight;
        if(classic) { Height=height;SizeToContent=SizeToContent.Manual; }
        ToastNative.Place(hwnd,Centered(work,(int)Math.Round(Width*scale),(int)Math.Round(height*scale)));
    }
    private void Position()
    {
        if(work.Width==0) work=ToastNative.Area(null,PasteInput.Foreground().Hwnd);
        var hwnd=new System.Windows.Interop.WindowInteropHelper(this).Handle;var r=Native.WindowRect(hwnd);
        ToastNative.Place(hwnd,Centered(work,r.Right-r.Left,r.Bottom-r.Top));
    }
}
internal sealed class ClearSessionDialog : PromptWindow
{
    internal CheckBox DoNotAskAgain { get; }=UtilityUi.Checkbox("Don't ask again");
    public ClearSessionDialog(int count) : base("Clear current session?",$"{count} {(count==1 ? "screenshot" : "screenshots")} and their comments will be discarded.","Clear",modal:false)
    {
        Title="Clear session — ScreenIt";
        DoNotAskAgain.Resources["CheckFocus"]=UtilityUi.Accent;
        if(UiDesign.IsEmber) { DoNotAskAgain.FontSize=15;DoNotAskAgain.Margin=new Thickness(8,0,0,8); }
        BodyPanel.Children.Insert(BodyPanel.Children.Count-1,DoNotAskAgain);
    }
}
