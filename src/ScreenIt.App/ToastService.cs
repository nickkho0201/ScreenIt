using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

internal sealed record ToastMessage(string Title, string Detail = "", bool Warning = false)
{
    internal static string Screenshots(int count) => $"{count} {(count == 1 ? "screenshot" : "screenshots")}";
    internal static ToastMessage Added(string letter, int count) => new($"Screenshot {letter} added", "Session · " + Screenshots(count));
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
    private readonly DispatcherTimer timer = new();
    internal ToastWindow? Current { get; private set; }
    internal ToastMessage? LastShown { get; private set; }
    internal int ShownCount { get; private set; }
    internal static TimeSpan SuccessLifetime => TimeSpan.FromMilliseconds(1400);
    internal static TimeSpan WarningLifetime => TimeSpan.FromMilliseconds(3000);
    public ToastService() { timer.Tick += Expire; }
    private void Expire(object? sender, EventArgs args) => Hide();
    public void Show(ToastMessage message, MonitorData? monitor = null, IntPtr target = default)
    {
        Hide();
        try
        {
            var area = ToastNative.Area(monitor, target);
            var window = new ToastWindow(message); Current = window;
            window.ShowAt(area);
            LastShown = message; ShownCount++;
            timer.Interval = message.Warning ? WarningLifetime : SuccessLifetime; timer.Start();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Hide(); // Feedback failure must not undo a completed action or open an error dialog.
            System.Diagnostics.Debug.WriteLine("ScreenIt toast unavailable: " + ex.GetType().Name);
        }
    }
    public void Hide() { timer.Stop(); var old = Current; Current = null; old?.Close(); }
    public void Dispose() { Hide(); timer.Tick -= Expire; }
}

internal sealed class ToastWindow : Window
{
    private HwndSource? source;
    internal IntPtr Handle { get; private set; }
    internal ToastMessage Message { get; }
    internal UiTheme AppliedTheme { get; private set; }
    public ToastWindow(ToastMessage message)
    {
        Message = message; Title = "ScreenIt feedback"; ShowActivated = false; ShowInTaskbar = false; Focusable = false; IsHitTestVisible = false;
        WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize; AllowsTransparency = true; Background = Brushes.Transparent;
        Width = 364; Height = 112; Topmost = true; WindowStartupLocation = WindowStartupLocation.Manual;
        var grid = new Grid(); grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30) }); grid.ColumnDefinitions.Add(new ColumnDefinition());
        var symbol = new TextBlock { Text = message.Warning ? "!" : "✓", FontSize = 20, FontWeight = FontWeights.SemiBold, Foreground = message.Warning ? UtilityUi.Brush(151,85,20) : UtilityUi.Accent };
        grid.Children.Add(symbol);
        var text = new StackPanel(); Grid.SetColumn(text,1); grid.Children.Add(text);
        text.Children.Add(new TextBlock { Text = message.Title, FontSize = 14, FontWeight = FontWeights.SemiBold, Foreground = UtilityUi.Ink });
        if (message.Detail.Length != 0) text.Children.Add(new TextBlock { Text = message.Detail, FontSize = 12, TextWrapping = TextWrapping.Wrap, Foreground = UtilityUi.Muted, Margin = new Thickness(0,5,0,0) });
        Content = new Border { Margin = new Thickness(10), Padding = new Thickness(16,13,16,13), Background = Brushes.White, CornerRadius = new CornerRadius(10), BorderThickness = new Thickness(1), BorderBrush = UtilityUi.Brush(203,213,225), Child = grid,
            Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 14, ShadowDepth = 2, Opacity = .23 } };
        ApplyTheme();
        SourceInitialized += (_,_) =>
        {
            Handle = new WindowInteropHelper(this).Handle; source = HwndSource.FromHwnd(Handle)!;
            source.AddHook(Hook); ToastNative.Configure(Handle);
        };
        Closed += (_,_) => { if (source != null) { source.RemoveHook(Hook); source.Dispose(); source = null; } Content = null; };
    }
    internal void ApplyTheme()
    {
        AppliedTheme=Appearance.Current;
        if(Content is not Border border || border.Child is not Grid grid) return;
        border.Background=Appearance.Palette.Surface;border.BorderBrush=Appearance.Palette.Border;
        grid.Children.OfType<TextBlock>().First().Foreground=Message.Warning ? Appearance.Palette.Warning : Appearance.Palette.AccentText;
        var blocks=grid.Children.OfType<StackPanel>().Single().Children.OfType<TextBlock>().ToArray();
        blocks[0].Text=L.T(Message.Title);if(blocks.Length>1) blocks[1].Text=L.T(Message.Detail);
        blocks[0].Foreground=UtilityUi.Ink;foreach(var block in blocks.Skip(1)) block.Foreground=UtilityUi.Muted;
    }
    private IntPtr Hook(IntPtr hwnd,int msg,IntPtr wp,IntPtr lp,ref bool handled)
    {
        if (msg == 0x0021) { handled = true; return new IntPtr(3); } // MA_NOACTIVATE
        if (msg == 0x0084) { handled = true; return new IntPtr(-1); } // HTTRANSPARENT
        return IntPtr.Zero;
    }
    internal void ShowAt(PxRect work)
    {
        new WindowInteropHelper(this).EnsureHandle();
        // Move hidden HWND to the chosen physical monitor before reading its actual DPI.
        ToastNative.Place(Handle,new(work.X,work.Y,1,1));
        var scale = Native.GetDpiForWindow(Handle)/96.0;
        var bounds = ToastNative.Bounds(work,scale);
        Width = bounds.Width/scale; Height = bounds.Height/scale;
        Show(); ToastNative.Place(Handle,bounds);
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
    internal static PxRect Area(MonitorData? monitor,IntPtr target)
    {
        IntPtr handle;
        if (monitor != null) handle = MonitorFromPoint(new Point { X = monitor.Left+monitor.Width/2, Y = monitor.Top+monitor.Height/2 },2);
        else handle = MonitorFromWindow(target != IntPtr.Zero ? target : PasteInput.Foreground().Hwnd,1); // primary if no window
        var info = new Info { Size = Marshal.SizeOf<Info>(), Device = "" };
        if (handle == IntPtr.Zero || !GetMonitorInfoW(handle,ref info)) throw new InvalidOperationException("Toast work area unavailable.");
        return new(info.Work.Left,info.Work.Top,info.Work.Right-info.Work.Left,info.Work.Bottom-info.Work.Top);
    }
    internal static PxRect Bounds(PxRect work,double scale)
    {
        int margin = Math.Min((int)Math.Round(16*scale),Math.Max(0,Math.Min(work.Width,work.Height)/4));
        int width = Math.Max(1,Math.Min((int)Math.Round(364*scale),work.Width-2*margin));
        int height = Math.Max(1,Math.Min((int)Math.Round(112*scale),work.Height-2*margin));
        return new(work.X+work.Width-width-margin,work.Y+work.Height-height-margin,width,height);
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
