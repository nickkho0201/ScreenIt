using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

internal sealed record ToastMessage(string Title, string Detail = "", bool Warning = false)
{
    internal static string Screenshots(int count) => $"{count} {(count == 1 ? "screenshot" : "screenshots")}";
    internal static ToastMessage Added(string letter, int count) => new($"Screenshot {letter} added", Screenshots(count) + " in session");
    internal static ToastMessage Started() => new("ScreenIt is running in the background");
    internal static ToastMessage Cleared() => new("Session cleared");
    internal static ToastMessage? Paste(PasteResult result, bool comments) => result.Outcome switch
    {
        PasteOutcome.Completed => new("Session pasted", Screenshots(result.ImagesSent) + (comments ? " + comments" : "")),
        PasteOutcome.Failed or PasteOutcome.Aborted when result.ImagesSent == 0 && result.PasteAttempted => new("Paste interrupted", "Check the receiving app\nSession unchanged", true),
        PasteOutcome.Failed or PasteOutcome.Aborted when result.ImagesSent == 0 => new("Paste failed", "Nothing was pasted · Session unchanged", true),
        PasteOutcome.Failed or PasteOutcome.Aborted => new("Paste interrupted", $"{result.ImagesSent} of {result.TotalImages} screenshots pasted\nSession unchanged", true),
        _ => null
    };
}

// Exactly one short-lived, non-interactive window. No queue, sound, OS notifications or Core dependency.
internal sealed class ToastService : IDisposable
{
    private DispatcherTimer? timer;
    private EventHandler? tick;
    private bool disposed;
    internal ToastWindow? Current { get; private set; }
    internal ToastMessage? LastShown { get; private set; }
    internal int ShownCount { get; private set; }
    internal static TimeSpan SuccessLifetime => TimeSpan.FromMilliseconds(1400);
    internal static TimeSpan WarningLifetime => TimeSpan.FromMilliseconds(3000);
    internal static TimeSpan FadeInDuration => TimeSpan.FromMilliseconds(150);
    internal static TimeSpan FadeOutDuration => TimeSpan.FromMilliseconds(200);
    private void StopTimer()
    {
        if(timer == null) return;
        timer.Stop();timer.Tick -= tick;timer = null;tick = null;
    }
    internal void Expire(ToastWindow window)
    {
        if(!ReferenceEquals(Current,window) || disposed) return;
        StopTimer();
        try { window.FadeOut(() => { if(ReferenceEquals(Current,window)) Hide(); }); }
        catch(Exception ex) when(ex is not OutOfMemoryException) { Hide();System.Diagnostics.Debug.WriteLine("ScreenIt toast unavailable: " + ex.GetType().Name); }
    }
    public void Show(ToastMessage message, MonitorData? monitor = null, IntPtr target = default, bool primary = false)
    {
        if(disposed) return;
        Hide();
        try
        {
            var area = ToastNative.Area(monitor, target, primary);
            var window = new ToastWindow(message); Current = window;
            window.Closed += (_,_) => { if(ReferenceEquals(Current,window)) { StopTimer();Current=null; } };
            window.ShowAt(area);
            LastShown = message; ShownCount++;
            timer = new DispatcherTimer { Interval = FadeInDuration + (message.Warning ? WarningLifetime : SuccessLifetime) };
            tick = (_,_) => Expire(window);
            timer.Tick += tick;timer.Start();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Hide(); // Feedback failure must not undo a completed action or open an error dialog.
            System.Diagnostics.Debug.WriteLine("ScreenIt toast unavailable: " + ex.GetType().Name);
        }
    }
    public void Hide() { StopTimer(); var old = Current; Current = null; old?.Close(); }
    public void Dispose() { if(disposed) return;disposed=true;Hide(); }
}

internal sealed class ToastWindow : Window
{
    private HwndSource? source;
    private AnimationClock? fade;
    private EventHandler? fadeCompleted;
    private PxRect work;
    private bool placed;
    internal bool IsFadingOut { get; private set; }
    internal const double MaxLayoutWidth = 396; // 380 DIP surface + shadow gutter.
    internal const double LayoutHeight = 84; // 68 DIP minimum surface + shadow gutter.
    internal IntPtr Handle { get; private set; }
    internal ToastMessage Message { get; }
    internal UiTheme AppliedTheme { get; private set; }
    public ToastWindow(ToastMessage message)
    {
        Message = message; Title = "ScreenIt feedback"; ShowActivated = false; ShowInTaskbar = false; Focusable = false; IsHitTestVisible = false;
        WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize; AllowsTransparency = true; Background = Brushes.Transparent;
        Height = LayoutHeight; Topmost = true; WindowStartupLocation = WindowStartupLocation.Manual;
        Opacity=0;
        var grid = new Grid(); grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var symbol = new TextBlock { Text = message.Warning ? "!" : "✓", FontSize = 16, FontWeight = FontWeights.SemiBold, HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center };
        grid.Children.Add(new Border { Width=26,Height=26,Margin=new Thickness(0,0,12,0),CornerRadius=new CornerRadius(13),HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Center,Child=symbol });
        // Upper bound only: surface 380 minus padding/border (34), badge (26) and gap (12).
        var text = new StackPanel { MaxWidth=308, VerticalAlignment=VerticalAlignment.Center }; Grid.SetColumn(text,1); grid.Children.Add(text);
        text.Children.Add(new TextBlock { Text = message.Title, FontSize = 13, FontWeight = FontWeights.SemiBold, TextWrapping=TextWrapping.Wrap, Foreground = UtilityUi.Ink });
        if (message.Detail.Length != 0) text.Children.Add(new TextBlock { Text = message.Detail, FontSize = 12, TextWrapping = TextWrapping.Wrap, Foreground = UtilityUi.Muted, Margin = new Thickness(0,4,0,0) });
        Content = new Border { Margin = new Thickness(8), MaxWidth=380, MinHeight=68, Padding = new Thickness(16,10,16,10), Background = Brushes.White, CornerRadius = new CornerRadius(15), BorderThickness = new Thickness(1), Child = grid,
            Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 12, ShadowDepth = 2, Opacity = .18 } };
        ApplyTheme();
        SourceInitialized += (_,_) =>
        {
            Handle = new WindowInteropHelper(this).Handle; source = HwndSource.FromHwnd(Handle)!;
            source.AddHook(Hook); ToastNative.Configure(Handle);
        };
        Closed += (_,_) => { StopAnimation();if (source != null) { source.RemoveHook(Hook); source.Dispose(); source = null; } placed=false;Content = null; };
    }
    internal void ApplyTheme()
    {
        AppliedTheme=Appearance.Current;
        if(Content is not Border border || border.Child is not Grid grid) return;
        border.Background=AppliedTheme==UiTheme.Dark ? UtilityUi.Brush(34,35,39) : UtilityUi.Brush(250,250,251);
        border.BorderBrush=new SolidColorBrush(Color.FromArgb(18,Appearance.Palette.Text.Color.R,Appearance.Palette.Text.Color.G,Appearance.Palette.Text.Color.B));
        var badge=grid.Children.OfType<Border>().Single();var accent=Message.Warning ? Appearance.Palette.Warning : Appearance.Palette.AccentText;
        badge.Background=new SolidColorBrush(Color.FromArgb(26,accent.Color.R,accent.Color.G,accent.Color.B));
        ((TextBlock)badge.Child).Foreground=accent;
        var blocks=grid.Children.OfType<StackPanel>().Single().Children.OfType<TextBlock>().ToArray();
        blocks[0].Text=L.T(Message.Title);if(blocks.Length>1) blocks[1].Text=L.T(Message.Detail);
        blocks[0].Foreground=UtilityUi.Ink;foreach(var block in blocks.Skip(1)) block.Foreground=UtilityUi.Muted;
        if(placed) PlaceContent();
    }
    private void StopAnimation()
    {
        if(fade != null && fadeCompleted != null) fade.Completed -= fadeCompleted;
        fade?.Controller?.Remove();ApplyAnimationClock(OpacityProperty,null);fade=null;fadeCompleted=null;
    }
    internal void FadeOut(Action completed)
    {
        double opacity=Opacity;StopAnimation();Opacity=opacity;IsFadingOut=true;
        fade=(AnimationClock)new DoubleAnimation(opacity,0,ToastService.FadeOutDuration).CreateClock(true);
        fadeCompleted=(_,_)=>completed();fade.Completed+=fadeCompleted;
        ApplyAnimationClock(OpacityProperty,fade);
    }
    private IntPtr Hook(IntPtr hwnd,int msg,IntPtr wp,IntPtr lp,ref bool handled)
    {
        if (msg == 0x0021) { handled = true; return new IntPtr(3); } // MA_NOACTIVATE
        if (msg == 0x0084) { handled = true; return new IntPtr(-1); } // HTTRANSPARENT
        return IntPtr.Zero;
    }
    internal void ShowAt(PxRect work)
    {
        this.work=work;
        new WindowInteropHelper(this).EnsureHandle();
        // Move hidden HWND to the chosen physical monitor before reading its actual DPI.
        ToastNative.Place(Handle,new(work.X,work.Y,1,1));
        PlaceContent();Show();PlaceContent();placed=true;
        fade=(AnimationClock)new DoubleAnimation(0,1,ToastService.FadeInDuration).CreateClock(true);
        Opacity=1;ApplyAnimationClock(OpacityProperty,fade);
    }
    private void PlaceContent()
    {
        var scale=Native.GetDpiForWindow(Handle)/96.0;
        var available=ToastNative.Bounds(work,scale);
        var size=MeasureContent(available.Width/scale);
        var bounds=ToastNative.Bounds(work,scale,size.Width,size.Height);
        Width=bounds.Width/scale;
        // Re-measure wrapping at the actual pixel-rounded HWND width before centering.
        size=MeasureContent(Width);
        bounds=ToastNative.Bounds(work,scale,Width,size.Height);
        Height=bounds.Height/scale;ToastNative.Place(Handle,bounds);
    }
    internal Size MeasureContent(double availableWidth)
    {
        var content=(FrameworkElement)Content;
        content.Measure(new Size(Math.Min(MaxLayoutWidth,availableWidth),double.PositiveInfinity));
        return new Size(Math.Min(availableWidth,content.DesiredSize.Width),Math.Max(LayoutHeight,content.DesiredSize.Height));
    }
}

internal static class ToastNative
{
    [StructLayout(LayoutKind.Sequential)] private struct Point { public int X,Y; }
    [StructLayout(LayoutKind.Sequential,CharSet = CharSet.Unicode)] private struct Info
    {
        public int Size; public Native.RECT Monitor,Work; public uint Flags;
        [MarshalAs(UnmanagedType.ByValTStr,SizeConst = 32)] public string Device;
    }
    internal static PxRect Area(MonitorData? monitor,IntPtr target,bool primary=false)
    {
        IntPtr handle;
        if (monitor != null) handle = MonitorFromPoint(new Point { X = monitor.Left+monitor.Width/2, Y = monitor.Top+monitor.Height/2 },2);
        else handle = MonitorFromWindow(primary ? IntPtr.Zero : target != IntPtr.Zero ? target : PasteInput.Foreground().Hwnd,1); // primary if no window
        var info = new Info { Size = Marshal.SizeOf<Info>(), Device = "" };
        if (handle == IntPtr.Zero || !GetMonitorInfoW(handle,ref info)) throw new InvalidOperationException("Toast work area unavailable.");
        return new(info.Work.Left,info.Work.Top,info.Work.Right-info.Work.Left,info.Work.Bottom-info.Work.Top);
    }
    internal static PxRect Bounds(PxRect work,double scale,double widthDip=ToastWindow.MaxLayoutWidth,double heightDip=ToastWindow.LayoutHeight)
    {
        int margin = Math.Min((int)Math.Round(16*scale),Math.Max(0,Math.Min(work.Width,work.Height)/4));
        int width = Math.Max(1,Math.Min((int)Math.Round(widthDip*scale),work.Width-2*margin));
        int height = Math.Max(1,Math.Min((int)Math.Round(heightDip*scale),work.Height-2*margin));
        return new(work.X+(work.Width-width)/2,work.Y+work.Height-height-margin,width,height);
    }
    internal static void Configure(IntPtr hwnd)
    {
        long flags = GetWindowLongPtrW(hwnd,-20).ToInt64();
        SetWindowLongPtrW(hwnd,-20,new IntPtr(flags | 0x08000000 | 0x80 | 0x20)); // NOACTIVATE | TOOLWINDOW | TRANSPARENT (layered WPF window)
        if ((GetWindowLongPtrW(hwnd,-20).ToInt64() & 0x080000A0) != 0x080000A0) throw new InvalidOperationException("Toast input styles unavailable.");
    }
    internal static void Place(IntPtr hwnd,PxRect r)
    {
        if (!SetWindowPos(hwnd,new IntPtr(-1),r.X,r.Y,r.Width,r.Height,0x0010)) throw new InvalidOperationException("Toast placement unavailable.");
    }
    [DllImport("user32.dll")] internal static extern bool IsWindow(IntPtr hwnd);
    [DllImport("user32.dll")] internal static extern IntPtr GetWindowLongPtrW(IntPtr hwnd,int index);
    [DllImport("user32.dll")] private static extern IntPtr SetWindowLongPtrW(IntPtr hwnd,int index,IntPtr value);
    [DllImport("user32.dll")] private static extern IntPtr MonitorFromPoint(Point point,uint flags);
    [DllImport("user32.dll")] private static extern IntPtr MonitorFromWindow(IntPtr hwnd,uint flags);
    [DllImport("user32.dll",CharSet = CharSet.Unicode)] private static extern bool GetMonitorInfoW(IntPtr monitor,ref Info info);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr hwnd,IntPtr after,int x,int y,int w,int h,uint flags);
}
