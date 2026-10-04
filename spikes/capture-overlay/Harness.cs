using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

internal sealed class Harness : Window
{
    private readonly List<Overlay> overlays = [];
    private readonly TextBox log = new() { IsReadOnly = true, AcceptsReturn = true, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, TextWrapping = TextWrapping.Wrap };
    private readonly CheckBox mode = new() { Content = "A: preserve interrupted preview (default unchecked = B: rollback gesture)", Margin = new Thickness(4) };
    private readonly Image preview = new() { Stretch = System.Windows.Media.Stretch.Uniform, MaxHeight = 170, ToolTip = "Read-only last crop; no editing/export. Cleared on next capture." };
    private HwndSource? source;
    private bool busy, suspended, batch, stop;
    private int generation;
    private MonitorData[] topology = [];
    public bool PreserveGesture { get => mode.IsChecked == true; set => mode.IsChecked = value; }
    public bool HasCapture => overlays.Count > 0;
    internal IReadOnlyList<Overlay> TestOverlays => overlays;
    public string Messages => log.Text;
    public BitmapSource? LastCrop => preview.Source as BitmapSource;
    public bool IsCurrent(Overlay overlay) => overlays.Contains(overlay);
    public Harness()
    {
        Title = "Disposable Capture / Overlay Spike"; Width = 760; Height = 520;
        var panel = new DockPanel { Margin = new Thickness(10) }; Content = panel;
        var controls = new StackPanel(); DockPanel.SetDock(controls, Dock.Top); panel.Children.Add(controls);
        var row = new WrapPanel(); controls.Children.Add(row);
        void Button(string name, Action action) { var b = new Button { Content = name, Margin = new Thickness(3), Padding = new Thickness(8, 4, 8, 4) }; b.Click += (_, _) => action(); row.Children.Add(b); }
        Button("Start / return (Ctrl+Alt+S)", () => SafeStart());
        Button("Show topology", () => Log(JsonSerializer.Serialize(new { machine = Native.Machine(), monitors = Native.Monitors() }, Program.Json)));
        Button("Latency / resources: 100 cycles", async () => { if (busy || HasCapture || batch) return; try { var report = await Measure(100); SaveReport("measurements", report); } catch (Exception e) { Log(e.ToString()); } });
        Button("Cancel retained capture", () => Cancel(true));
        Button("Restart retained capture", () => { if (busy || batch) return; Cancel(false); SafeStart(); });
        controls.Children.Add(mode); controls.Children.Add(preview); panel.Children.Add(log);
        Log("Synthetic tests: --self-test. Measurements: --measure 100. No screenshot files or clipboard operations. Enter shows a read-only crop preview in RAM; next capture clears it.");
        SourceInitialized += (_, _) =>
        {
            source = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle)!; source.AddHook(Hook);
            Log(Native.RegisterHotKey(source.Handle, 1, 0x4000 | 0x0001 | 0x0002, 0x53) ? "Global Ctrl+Alt+S registered." : "Hotkey registration failed; use Start button.");
            Log(Native.WTSRegisterSessionNotification(source.Handle, 0) ? "Session notifications registered." : "WARNING: WTS notification registration failed.");
        };
        Closed += (_, _) => { Cancel(false, false); if (source != null) { Native.UnregisterHotKey(source.Handle, 1); Native.WTSUnRegisterSessionNotification(source.Handle); source.RemoveHook(Hook); } Application.Current.Shutdown(); };
        new WindowInteropHelper(this).EnsureHandle(); // Receive system/hotkey notifications even in automated mode.
    }
    private IntPtr Hook(IntPtr hwnd, int message, IntPtr wp, IntPtr lp, ref bool handled)
    {
        if (message == 0x0312) { SafeStart(); handled = true; }
        else if (message == 0x007E) Suspend("Display topology changed; restart required.");
        else if (message == 0x001A && (HasCapture || busy))
        {
            Dispatcher.BeginInvoke(() => { try { if (!SameTopology(topology, Native.Monitors())) Suspend("Display configuration changed; restart required."); } catch (Exception e) { Suspend(e.Message); } });
        }
        else if (message == 0x0218 && (wp.ToInt64() == 4 || wp.ToInt64() == 7 || wp.ToInt64() == 18)) Suspend("Power transition; restart required.");
        else if (message == 0x02B1 && (wp.ToInt64() == 7 || wp.ToInt64() == 8)) Suspend("Session lock/unlock; restart required.");
        else if (message == 0x0011) Suspend("Windows session shutdown requested; RAM capture cannot survive process termination.");
        return IntPtr.Zero;
    }
    public void Log(string text) { log.AppendText(text + Environment.NewLine); log.ScrollToEnd(); }
    private async void SafeStart()
    {
        if (busy || batch) return;
        if (HasCapture)
        {
            if (suspended) { Show(); Activate(); Log("Retained capture suspended. Explicit Cancel or Restart required."); }
            else { foreach (var o in overlays) { o.Show(); o.Topmost = true; } overlays[0].Activate(); }
            return;
        }
        try { var sample = await Start(); Log(JsonSerializer.Serialize(sample, Program.Json)); }
        catch (Exception e) { if (!suspended) Cancel(false); else { Show(); Activate(); } Log(e.ToString()); }
    }
    private static bool SameTopology(MonitorData[] a, MonitorData[] b) => a.Select(m => m.TopologyKey).SequenceEqual(b.Select(m => m.TopologyKey));
    public async Task<object> Start()
    {
        if (busy || HasCapture) throw new InvalidOperationException("Capture already active.");
        busy = true; suspended = false; int current = ++generation;
        var timer = Stopwatch.StartNew();
        try
        {
            preview.Source = null; Hide(); await Dispatcher.Yield(DispatcherPriority.Render); Native.Flush();
            topology = Native.Monitors(); if (topology.Length == 0) throw new InvalidOperationException("No monitors.");
            if (HasCapture) throw new InvalidOperationException("Overlay present before capture.");
            var frames = await Task.Run(() => topology.Select(Native.Capture).ToArray());
            double capturedMs = timer.Elapsed.TotalMilliseconds;
            if (current != generation || suspended) throw new InvalidOperationException("System transition interrupted capture; restart required.");
            if (!SameTopology(topology, Native.Monitors())) throw new InvalidOperationException("Topology changed during capture; restart required.");
            foreach (var frame in frames) { var overlay = new Overlay(this, frame); overlays.Add(overlay); overlay.Show(); Native.Place(new WindowInteropHelper(overlay).Handle, frame.Monitor); }
            await Task.WhenAll(overlays.Select(o => o.Rendered.Task)).WaitAsync(TimeSpan.FromSeconds(5));
            if (current != generation || suspended) throw new InvalidOperationException("Capture interrupted before render.");
            Native.Flush(); double visibleMs = timer.Elapsed.TotalMilliseconds;
            var placement = overlays.Select(o => o.Placement()).ToArray();
            overlays.First(o => o.Frame.Monitor.Primary).Activate();
            return new { triggerToCapturedMs = capturedMs, triggerToRenderedAndDwmFlushMs = visibleMs, placement };
        }
        finally { busy = false; }
    }
    public void Choose(Overlay selected)
    {
        foreach (var o in overlays.Where(o => o != selected)) { o.Selection.Clear(); o.Refresh(); }
    }
    public void Commit(Overlay selected)
    {
        if (suspended || selected.Selection.Completed is not PxRect rect) return;
        var crop = new CroppedBitmap(selected.Frame.Image, new Int32Rect(rect.X, rect.Y, rect.Width, rect.Height)); crop.Freeze();
        if (crop.PixelWidth != rect.Width || crop.PixelHeight != rect.Height) throw new InvalidOperationException("Crop dimensions mismatch.");
        preview.Source = crop;
        Log($"Committed {selected.Frame.Monitor.Device}: {rect}; crop {crop.PixelWidth}×{crop.PixelHeight} physical pixels. Read-only preview in RAM; no export.");
        Cancel(false);
    }
    public void Suspend(string reason)
    {
        if (!HasCapture && !busy) return;
        suspended = true; stop = true; generation++;
        foreach (var o in overlays) { o.InterruptGesture(); o.Hide(); }
        Show(); Activate(); Log("SUSPENDED (frame/selection retained): " + reason);
    }
    public void Cancel(bool explicitUser, bool show = true)
    {
        if (explicitUser) { stop = true; generation++; }
        foreach (var o in overlays.ToArray()) { o.ClosingByHarness = true; o.Close(); }
        overlays.Clear(); suspended = false;
        if (show && !batch) { Show(); Activate(); }
        if (explicitUser) Log("Capture cancelled explicitly.");
    }
    private static async Task Settle()
    {
        await Task.Delay(80); GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect(); await Task.Delay(80);
    }
    public async Task<object> Measure(int count)
    {
        if (count < 1 || count > 500) throw new ArgumentOutOfRangeException(nameof(count), "Use 1..500 cycles for this disposable harness.");
        batch = true; stop = false;
        var samples = new List<object>(); var resources = new List<object>();
        try
        {
            for (int i = 0; i < 5; i++) { await Start(); Cancel(false, false); await Settle(); }
            resources.Add(new { cycle = 0, counters = Native.Resources() });
            for (int i = 1; i <= count; i++)
            {
                if (stop) throw new OperationCanceledException("Measurement cancelled by user/system event.");
                samples.Add(await Start()); Cancel(false, false); await Settle();
                if (i % 10 == 0 || i == count) resources.Add(new { cycle = i, counters = Native.Resources() });
            }
            var times = samples.Select(s => JsonSerializer.SerializeToElement(s).GetProperty("triggerToRenderedAndDwmFlushMs").GetDouble()).Order().ToArray();
            double Percentile(double fraction) => times[Math.Clamp((int)Math.Ceiling(times.Length * fraction) - 1, 0, times.Length - 1)];
            return new { recordedAt = DateTimeOffset.Now, machine = Native.Machine(), monitors = Native.Monitors(), warmup = 5, cycles = count,
                latency = new { averageMs = times.Average(), p50Ms = Percentile(.5), p95Ms = Percentile(.95), minMs = times[0], maxMs = times[^1], targetP95Ms = 250,
                    metric = "trigger to all WPF ContentRendered + DwmFlush completion; software proxy, not physical display latency" }, resources, samples };
        }
        finally { if (!suspended) Cancel(false, false); batch = false; Show(); }
    }
    public static void SaveReport(string name, object report, string? path = null)
    {
        path ??= Path.Combine(AppContext.BaseDirectory, "artifacts", name + ".json");
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllText(path, JsonSerializer.Serialize(report, Program.Json));
    }
}
