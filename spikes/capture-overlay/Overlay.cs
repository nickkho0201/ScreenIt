using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

internal sealed class Overlay : Window
{
    public readonly Frame Frame;
    public readonly Selection Selection = new();
    private readonly Harness owner;
    private readonly Surface surface;
    private HwndSource? source;
    public readonly TaskCompletionSource Rendered = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public bool ClosingByHarness, Ready;
    public Overlay(Harness owner, Frame frame)
    {
        this.owner = owner; Frame = frame;
        WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize; ShowInTaskbar = false;
        Topmost = true; ShowActivated = false; Background = Brushes.Black;
        Width = frame.Monitor.Width * 96.0 / frame.Monitor.EffectiveDpi;
        Height = frame.Monitor.Height * 96.0 / frame.Monitor.EffectiveDpi;
        Content = surface = new Surface(this);
        RenderOptions.SetBitmapScalingMode(surface, BitmapScalingMode.NearestNeighbor);
        SourceInitialized += (_, _) =>
        {
            source = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle)!; source.AddHook(Hook);
            Native.Place(source.Handle, Frame.Monitor);
        };
        ContentRendered += (_, _) => { Ready = true; Rendered.TrySetResult(); };
        Deactivated += (_, _) => InterruptGesture();
        Closing += (_, e) => { if (!ClosingByHarness) { e.Cancel = true; Dispatcher.BeginInvoke(() => owner.Cancel(true)); } };
        Closed += (_, _) => { if (source != null) { source.RemoveHook(Hook); source.Dispose(); source = null; } Content = null; };
        PreviewKeyDown += Key;
        surface.MouseLeftButtonDown += Down;
        surface.MouseMove += (_, e) => { if (Selection.Active && !Selection.Paused) { Selection.Move(Point(e), Frame.Monitor.Width, Frame.Monitor.Height); Refresh(); } };
        surface.MouseLeftButtonUp += (_, e) =>
        {
            if (!Selection.Active || Selection.Paused) return;
            Selection.End(Point(e), Frame.Monitor.Width, Frame.Monitor.Height); surface.ReleaseMouseCapture(); Refresh();
        };
        surface.LostMouseCapture += (_, _) => InterruptGesture();
    }
    private IntPtr Hook(IntPtr hwnd, int message, IntPtr wp, IntPtr lp, ref bool handled)
    {
        if (message == 0x02E0 && Ready && (wp.ToInt64() & 0xffff) != Frame.Monitor.EffectiveDpi)
            Dispatcher.BeginInvoke(() => { if (owner.IsCurrent(this)) owner.Suspend("Overlay DPI changed; restart required."); });
        return IntPtr.Zero;
    }
    public Matrix Transform => source?.CompositionTarget?.TransformToDevice ?? Matrix.Identity;
    private PxPoint Point(MouseEventArgs e)
    {
        var p = e.GetPosition(surface); var t = Transform;
        var px = Geometry.FromDip(new(p.X, p.Y), t.M11, t.M22);
        return new(Math.Clamp(px.X, 0, Frame.Monitor.Width), Math.Clamp(px.Y, 0, Frame.Monitor.Height));
    }
    private void Down(object sender, MouseButtonEventArgs e)
    {
        Activate(); owner.Choose(this); Selection.Begin(Point(e)); surface.CaptureMouse(); Refresh(); e.Handled = true;
    }
    public void InterruptGesture()
    {
        Selection.Interrupt(owner.PreserveGesture);
        if (surface.IsMouseCaptured) surface.ReleaseMouseCapture();
        Refresh();
    }
    private void Key(object sender, KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Escape)
        {
            if (Selection.Active) { Selection.CancelGesture(); surface.ReleaseMouseCapture(); Refresh(); }
            else owner.Cancel(true);
        }
        else if (e.Key == System.Windows.Input.Key.Space) { owner.Choose(this); Selection.Full(Frame.Monitor.Width, Frame.Monitor.Height); Refresh(); }
        else if (e.Key == System.Windows.Input.Key.Enter) { if (Selection.Completed.HasValue) owner.Commit(this); }
        else return;
        e.Handled = true;
    }
    public object Placement()
    {
        var r = Native.WindowRect(source!.Handle); var t = Transform;
        bool exact = r.Left == Frame.Monitor.Left && r.Top == Frame.Monitor.Top && r.Right - r.Left == Frame.Monitor.Width && r.Bottom - r.Top == Frame.Monitor.Height;
        bool canvas = Math.Abs(surface.ActualWidth * t.M11 - Frame.Monitor.Width) < 1 && Math.Abs(surface.ActualHeight * t.M22 - Frame.Monitor.Height) < 1;
        if (!exact || !canvas) throw new InvalidOperationException("Overlay physical placement/transform mismatch: " + Frame.Monitor.Device);
        return new { Frame.Monitor.Device, left = r.Left, top = r.Top, width = r.Right - r.Left, height = r.Bottom - r.Top,
            dipWidth = surface.ActualWidth, dipHeight = surface.ActualHeight, scaleX = t.M11, scaleY = t.M22, hwndDpi = Native.GetDpiForWindow(source.Handle), exact, canvas };
    }
    public void Refresh() => surface.InvalidateVisual();
    private sealed class Surface(Overlay window) : FrameworkElement
    {
        protected override void OnRender(DrawingContext dc)
        {
            var all = new Rect(0, 0, ActualWidth, ActualHeight); dc.DrawImage(window.Frame.Image, all);
            var px = window.Selection.Preview ?? window.Selection.Completed;
            var dim = new GeometryGroup { FillRule = FillRule.EvenOdd }; dim.Children.Add(new RectangleGeometry(all));
            Rect? selected = null;
            if (px is PxRect r)
            {
                var t = window.Transform; selected = new Rect(r.X / t.M11, r.Y / t.M22, r.Width / t.M11, r.Height / t.M22);
                dim.Children.Add(new RectangleGeometry(selected.Value));
            }
            dc.DrawGeometry(new SolidColorBrush(Color.FromArgb(100, 0, 0, 0)), null, dim);
            if (selected.HasValue) dc.DrawRectangle(null, new Pen(Brushes.White, 1), selected.Value);
            string description = px is PxRect s ? $"{s.X},{s.Y}  {s.Width}×{s.Height} px" : "Drag a region";
            string text = $"{window.Frame.Monitor.Device}  {description}\nSpace: full monitor · Enter: commit · Esc: cancel gesture / capture\nCtrl+Alt+S: return · focus loss preserves capture · gesture {(window.owner.PreserveGesture ? "A: paused preview" : "B: rollback")}";
            var label = new FormattedText(text, System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                new Typeface("Segoe UI"), 14, Brushes.White, VisualTreeHelper.GetDpi(this).PixelsPerDip);
            dc.DrawRectangle(Brushes.Black, null, new Rect(12, 12, Math.Min(ActualWidth - 24, label.Width + 16), label.Height + 16));
            dc.DrawText(label, new Point(20, 20));
        }
    }
}
