using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

internal sealed class Launcher : Window
{
    private readonly List<AnnotationOverlay> overlays = [];
    private readonly ComboBox monitors = new() { MinWidth = 250, Margin = new Thickness(4) };
    private readonly TextBox log = new() { IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, MinHeight = 80 };
    private readonly Image preview = new() { MaxHeight = 230, Stretch = System.Windows.Media.Stretch.Uniform };
    private readonly TextBox comments = new() { IsReadOnly = true, AcceptsReturn = true, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, MinHeight = 130, MaxHeight = 250 };
    private HwndSource? source;
    private bool busy, suspended;
    private int generation;
    private MonitorData[] topology = [];
    public AnnotationOverlay? Active => overlays.FirstOrDefault(o => o.Editable);
    public IReadOnlyList<AnnotationOverlay> Overlays => overlays;
    public BitmapSource? LastBitmap { get; private set; }
    public IReadOnlyList<Annotation> LastAnnotations { get; private set; } = Array.Empty<Annotation>();
    public string LastComments => comments.Text;
    public string Messages => log.Text;
    public bool IsCurrent(AnnotationOverlay o) => overlays.Contains(o);
    public Launcher()
    {
        Title = "Disposable Annotation Interaction Spike"; Width = 920; Height = 820;
        var outer = new StackPanel { Margin = new Thickness(10) }; Content = new ScrollViewer { Content = outer, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        var controls = new WrapPanel(); outer.Children.Add(controls); controls.Children.Add(monitors);
        void Button(string title, Action action) { var button = new Button { Content = title, Margin = new Thickness(3), Padding = new Thickness(7, 4, 7, 4) }; button.Click += (_, _) => action(); controls.Children.Add(button); }
        Button("Capture / return Ctrl+Alt+N", () => StartFromUi(false));
        Button("Stress: 50M +25A +25R", () => StartFromUi(true));
        Button("Cancel retained draft", () => Cancel());
        Button("Restart", () => { if (busy) return; Cancel(false); StartFromUi(false); });
        outer.Children.Add(new TextBlock { Text = "One full-monitor Screenshot A. Other monitors are frozen companions. Annotation editing exists only in fullscreen overlay; below is read-only inspection.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(4) });
        outer.Children.Add(preview); outer.Children.Add(comments); outer.Children.Add(log);
        RefreshMonitors();
        SourceInitialized += (_, _) =>
        {
            source = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle)!; source.AddHook(Hook);
            Log(Native.RegisterHotKey(source.Handle, 1, 0x4000 | 0x0001 | 0x0002, 0x4E) ? "Ctrl+Alt+N registered." : "Hotkey unavailable; use Capture button.");
            Log(Native.WTSRegisterSessionNotification(source.Handle, 0) ? "WTS notifications registered." : "WARNING: WTS registration failed.");
        };
        Closed += (_, _) =>
        {
            Cancel(false); ClearInspection();
            if (source != null) { Native.UnregisterHotKey(source.Handle, 1); Native.WTSUnRegisterSessionNotification(source.Handle); source.RemoveHook(Hook); }
            Application.Current.Shutdown();
        };
        new WindowInteropHelper(this).EnsureHandle();
    }
    public void Log(string text) { log.AppendText(text + Environment.NewLine); log.ScrollToEnd(); }
    private void RefreshMonitors()
    {
        var current = monitors.SelectedItem as string; var all = Native.Monitors(); monitors.ItemsSource = all.Select(m => m.Device).ToArray();
        monitors.SelectedItem = all.Any(m => m.Device == current) ? current : all.First(m => m.Primary).Device;
    }
    private static bool Same(MonitorData[] a, MonitorData[] b) => a.Select(m => m.TopologyKey).SequenceEqual(b.Select(m => m.TopologyKey));
    private IntPtr Hook(IntPtr hwnd, int message, IntPtr wp, IntPtr lp, ref bool handled)
    {
        if (message == 0x0312) { StartFromUi(false); handled = true; }
        else if (message == 0x007E) Suspend("Display topology changed; cancel/restart required.");
        else if (message == 0x001A && (Active != null || busy)) Dispatcher.BeginInvoke(() =>
        { try { if (!Same(topology, Native.Monitors())) Suspend("Display settings changed; cancel/restart required."); } catch (Exception e) { Suspend(e.Message); } });
        else if (message == 0x0218 && (wp.ToInt64() == 4 || wp.ToInt64() == 7 || wp.ToInt64() == 18)) Suspend("Power transition; cancel/restart required.");
        else if (message == 0x02B1 && (wp.ToInt64() == 7 || wp.ToInt64() == 8)) Suspend("Session lock/unlock; cancel/restart required.");
        else if (message == 0x0011) Suspend("Shutdown requested; RAM draft cannot survive process termination.");
        return IntPtr.Zero;
    }
    private async void StartFromUi(bool stress)
    {
        if (busy) return;
        if (Active != null) { ReturnToDraft(); return; }
        try { await Start(monitors.SelectedItem as string); if (stress) { PopulateStress(Active!.Model); Active.Refresh(); } }
        catch (Exception e) { if (!suspended) Cancel(); else { Show(); Activate(); } Log(e.ToString()); }
    }
    public async Task<object> Start(string? selectedDevice = null)
    {
        if (busy || Active != null) throw new InvalidOperationException("Draft already active.");
        busy = true; suspended = false; int captureGeneration = ++generation;
        try
        {
            ClearInspection(); Hide(); await Dispatcher.Yield(DispatcherPriority.Render); Native.Flush(); topology = Native.Monitors();
            var chosen = selectedDevice == null ? topology.First(m => m.Primary) : topology.FirstOrDefault(m => m.Device == selectedDevice) ?? throw new InvalidOperationException("Selected monitor disconnected.");
            var frames = await Task.Run(() => topology.Select(Native.Capture).ToArray());
            if (captureGeneration != generation || suspended || !Same(topology, Native.Monitors())) throw new InvalidOperationException("Capture interrupted by system/display change; explicit restart required.");
            var model = new Draft(chosen.Width, chosen.Height);
            foreach (var frame in frames)
            {
                var o = new AnnotationOverlay(this, frame, model, frame.Monitor.Device == chosen.Device); overlays.Add(o); o.Show(); Native.Place(new WindowInteropHelper(o).Handle, frame.Monitor);
            }
            await Task.WhenAll(overlays.Select(o => o.Rendered.Task)).WaitAsync(TimeSpan.FromSeconds(5));
            if (captureGeneration != generation || suspended) throw new InvalidOperationException("Overlay render interrupted.");
            Native.Flush(); var placement = overlays.Select(o => o.Placement()).ToArray(); Active!.ReturnFocus();
            Log("Frozen draft active on " + chosen.Device + "; " + chosen.Width + "×" + chosen.Height + " px. No screenshots saved.");
            return new { selectedDevice = chosen.Device, placement };
        }
        finally { busy = false; }
    }
    public void ReturnToDraft()
    {
        if (suspended) { Show(); Activate(); Log("Draft retained but suspended. Cancel or Restart explicitly."); return; }
        foreach (var o in overlays) o.Show(); Active?.ReturnFocus();
    }
    public void Suspend(string reason)
    {
        if (Active == null && !busy) return;
        suspended = true; generation++;
        foreach (var o in overlays) { o.InterruptGesture(); o.Hide(); }
        Show(); Activate(); Log("SUSPENDED: frozen frame, annotations and open comment text retained. " + reason);
    }
    public double Commit(AnnotationOverlay overlay)
    {
        if (suspended || overlay.Editing || !overlay.Editable) throw new InvalidOperationException("Finish edit / restart invalid capture first.");
        var timer = Stopwatch.StartNew(); LastBitmap = Painter.Render(overlay.Frame.Image, Painter.Project(overlay.Model.Items)); timer.Stop();
        LastAnnotations = Array.AsReadOnly(overlay.Model.Items.ToArray()); comments.Text = overlay.Model.Comments(); preview.Source = LastBitmap;
        double elapsed = timer.Elapsed.TotalMilliseconds;
        Log($"Committed Screenshot A: {LastBitmap.PixelWidth}×{LastBitmap.PixelHeight} px; {LastAnnotations.Count} annotations. Render {elapsed:F2} ms. Comments remain text; no export/clipboard.");
        Cancel(); return elapsed;
    }
    public void Cancel(bool show = true)
    {
        generation++;
        foreach (var o in overlays.ToArray()) { o.ClosingByHarness = true; o.Close(); }
        overlays.Clear(); suspended = false;
        if (show) { Show(); Activate(); }
    }
    public void ClearInspection() { preview.Source = null; LastBitmap = null; LastAnnotations = Array.Empty<Annotation>(); comments.Text = ""; }
    public static void PopulateStress(Draft model)
    {
        double sx = model.Width / 1100.0, sy = model.Height / 800.0;
        for (int i = 0; i < 50; i++) model.CreateMarker(new((70 + i % 10 * 100) * sx, (130 + i / 10 * 95) * sy), "Synthetic stress marker " + (i + 1));
        for (int i = 0; i < 25; i++) model.AddArrow(new((30 + i % 5 * 200) * sx, (80 + i / 5 * 125) * sy), new((110 + i % 5 * 200) * sx, (115 + i / 5 * 125) * sy));
        for (int i = 0; i < 25; i++) model.AddBox(new((40 + i % 5 * 200) * sx, (140 + i / 5 * 120) * sy), new((140 + i % 5 * 200) * sx, (210 + i / 5 * 120) * sy));
    }
    public static void SaveReport(string name, object value, string? path = null)
    {
        path ??= Path.Combine(AppContext.BaseDirectory, "artifacts", name + ".json"); Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllText(path, JsonSerializer.Serialize(value, Program.Json));
    }
}
