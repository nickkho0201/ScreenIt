using System.Windows;
using System.Windows.Controls;
using System.Windows.Automation;
using System.Windows.Media;
using System.Windows.Media.Imaging;

internal static class UtilityUi
{
    public static readonly SolidColorBrush Accent = Brush(66, 85, 197);
    public static SolidColorBrush Ink => Appearance.Palette.Text;
    public static SolidColorBrush Muted => Appearance.Palette.Muted;
    public static SolidColorBrush Brush(byte r, byte g, byte b) { var value = new SolidColorBrush(Color.FromRgb(r,g,b)); value.Freeze(); return value; }
    public static Button Button(string label, string hint, Action action)
    {
        var border = new FrameworkElementFactory(typeof(Border));
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
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
        focus.Setters.Add(new Setter(Control.BorderBrushProperty, Accent)); template.Triggers.Add(focus);
        var disabled = new Trigger { Property = UIElement.IsEnabledProperty, Value = false };
        disabled.Setters.Add(new Setter(UIElement.OpacityProperty, .4)); template.Triggers.Add(disabled);
        var tooltip = new ToolTip { Content=hint,Background=Appearance.Palette.Surface,Foreground=Ink,BorderBrush=Appearance.Palette.Border };
        var button = new Button { Content = label, ToolTip = tooltip, Template = template, MinHeight = 34, Margin = new Thickness(2), FontSize = 13,
            Background = Brushes.Transparent, Foreground = Ink, BorderBrush = Brushes.Transparent, BorderThickness = new Thickness(1), Cursor = System.Windows.Input.Cursors.Arrow };
        ToolTipService.SetShowOnDisabled(button,true);
        AutomationProperties.SetName(button, hint); button.Click += (_,_) => action(); return button;
    }
    internal static StackPanel ShortcutCaption(string label,string key,bool emphasized=false)
    {
        var row=new StackPanel { Orientation=Orientation.Horizontal };
        row.Children.Add(new TextBlock { Text=label,VerticalAlignment=VerticalAlignment.Center });
        row.Children.Add(new Border { CornerRadius=new CornerRadius(3),BorderThickness=new Thickness(1),
            BorderBrush=emphasized ? Brush(180,188,224) : Appearance.Palette.Border,
            Padding=new Thickness(4,1,4,1),Margin=new Thickness(7,0,0,0),VerticalAlignment=VerticalAlignment.Center,
            Child=new TextBlock { Text=key,FontSize=10,FontWeight=FontWeights.Normal,Foreground=emphasized ? Brushes.White : Muted } });
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
        var prompt=new PromptWindow("ScreenIt",text,"OK",cancel:false);prompt.Prepare(owner);prompt.ShowDialog();
    }
    public static byte[] IconBytes { get; } = ReadIcon();
    private static byte[] ReadIcon() { using var stream = typeof(UtilityUi).Assembly.GetManifestResourceStream("ScreenIt.Icon")!; using var bytes = new MemoryStream(); stream.CopyTo(bytes); return bytes.ToArray(); }
    public static System.Drawing.Icon TrayIcon()
    {
        using var stream = new MemoryStream(IconBytes); using var icon = new System.Drawing.Icon(stream); return (System.Drawing.Icon)icon.Clone();
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
    public PromptWindow(string title,string description,string accept,bool modal = true,bool cancel = true)
    {
        this.modal=modal;Title="ScreenIt";Icon=UtilityUi.WindowIcon();Width=390;SizeToContent=SizeToContent.Height;
        ResizeMode=ResizeMode.NoResize;ShowInTaskbar=false;Topmost=true;ShowActivated=true;WindowStartupLocation=WindowStartupLocation.Manual;
        Description=description;
        var panel=new StackPanel { Margin=new Thickness(22) };
        panel.Children.Add(new TextBlock { Text=title,FontSize=18,FontWeight=FontWeights.SemiBold,Foreground=UtilityUi.Ink });
        panel.Children.Add(new TextBlock { Text=description,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,12,0,18),Foreground=UtilityUi.Muted });
        var buttons=new StackPanel { Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right };
        AcceptButton=UtilityUi.Button(accept,accept=="Clear" ? "Clear session" : accept,()=>Complete(true));AcceptButton.Foreground=Brushes.White;AcceptButton.Background=UtilityUi.Accent;
        CancelButton=UtilityUi.Button("Cancel","Keep session",()=>Complete(false));CancelButton.IsDefault=cancel;CancelButton.IsCancel=cancel;
        if(!cancel) { CancelButton.Visibility=Visibility.Collapsed;AcceptButton.IsDefault=true; }
        buttons.Children.Add(AcceptButton);buttons.Children.Add(CancelButton);panel.Children.Add(buttons);Content=panel;
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
        AppliedTheme=Appearance.Current;Background=Appearance.Palette.Surface;
        if(Content is StackPanel panel)
        {
            panel.Children.OfType<TextBlock>().First().Foreground=UtilityUi.Ink;
            panel.Children.OfType<TextBlock>().Last().Foreground=UtilityUi.Muted;
        }
        CancelButton.Foreground=UtilityUi.Ink;CancelButton.BorderBrush=Appearance.Palette.Border;
        foreach(var button in new[]{AcceptButton,CancelButton})
            if(button.ToolTip is ToolTip tip) { tip.Background=Appearance.Palette.Surface;tip.Foreground=UtilityUi.Ink;tip.BorderBrush=Appearance.Palette.Border; }

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
        ToastNative.Place(hwnd,new(work.X,work.Y,1,1));
        Width=390;
    }
    private void Position()
    {
        if(work.Width==0) work=ToastNative.Area(null,PasteInput.Foreground().Hwnd);
        var hwnd=new System.Windows.Interop.WindowInteropHelper(this).Handle;var r=Native.WindowRect(hwnd);
        int width=Math.Min(r.Right-r.Left,work.Width),height=Math.Min(r.Bottom-r.Top,work.Height);
        ToastNative.Place(hwnd,new(work.X+(work.Width-width)/2,work.Y+(work.Height-height)/2,width,height));
    }
}
internal sealed class ClearSessionDialog : PromptWindow
{
    public ClearSessionDialog(int count) : base("Clear current session?",$"{count} {(count==1 ? "screenshot" : "screenshots")} and their comments will be discarded.","Clear",modal:false)
    { Title="Clear session — ScreenIt"; }
}
