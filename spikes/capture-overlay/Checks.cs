using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

internal static class Checks
{
    public static async Task<object> Run(Harness harness)
    {
        var passed = new List<string>();
        void Check(bool value, string name)
        {
            if (!value) throw new InvalidOperationException("Self-check failed: " + name); passed.Add(name);
            Harness.SaveReport("self-test-progress", new { status = "IN PROGRESS", last = name, count = passed.Count });
        }
        foreach (double scale in new[] { 1.0, 1.25, 1.5, 2.0 })
        foreach (var origin in new[] { new PxPoint(0, 0), new PxPoint(-1920, 0), new PxPoint(-1080, -400) })
        foreach (var size in new[] { (1920, 1080), (1080, 1920) })
        {
            var p = new PxPoint(151, 203); var dip = Geometry.ToDip(p, scale, scale); var px = Geometry.FromDip(dip, scale, scale);
            Check(Math.Abs(px.X - p.X) < 1e-9 && Math.Abs(px.Y - p.Y) < 1e-9, $"DIP roundtrip {scale}/{origin}/{size}");
            Check(Geometry.Local(new(origin.X + p.X, origin.Y + p.Y), (int)origin.X, (int)origin.Y) == p, $"Negative origin mapping {scale}/{origin}/{size}");
            Check(Geometry.Bounds(new(-12.2, -5), new(size.Item1 + 30, size.Item2 + 50), size.Item1, size.Item2) == new PxRect(0, 0, size.Item1, size.Item2), $"Crop clamps {scale}/{origin}/{size}");
        }
        Check(Geometry.Bounds(new(20.8, 30.2), new(10.1, 4.9), 100, 100) == new PxRect(10, 4, 11, 27), "Reverse drag uses floor start / ceil end");
        bool invalid = false; try { Geometry.Bounds(new(double.NaN, 1), new(1, 1), 100, 100); } catch (ArgumentException) { invalid = true; }
        Check(invalid, "Non-finite crop rejected");
        var selection = new Selection(); selection.Full(100, 100); var before = selection.Completed;
        selection.Begin(new(10, 10)); selection.Move(new(30, 30), 100, 100); selection.Interrupt(false);
        Check(!selection.Active && selection.Completed == before && selection.Preview == null, "B focus interruption rolls back only gesture");
        selection.Begin(new(10, 10)); selection.Move(new(40, 40), 100, 100); var preview = selection.Preview; selection.Interrupt(true);
        Check(selection.Active && selection.Paused && selection.Preview == preview && selection.Completed == before, "A preserves paused preview and completed selection");
        selection.Begin(new(90, 90)); selection.End(new(50, 50), 100, 100);
        Check(selection.Completed == new PxRect(10, 10, 40, 40), "A resumes original anchor on next click");
        before = selection.Completed; selection.Begin(new(1, 1)); selection.End(new(1, 1), 100, 100);
        Check(selection.Completed == before, "Accidental tiny gesture preserves prior region");
        selection.Begin(new(2, 2)); selection.Move(new(20, 20), 100, 100); selection.CancelGesture();
        Check(selection.Completed == before && !selection.Active, "Escape gesture rollback preserves capture geometry");
        selection.Interrupt(false); Check(selection.Completed == before, "Idle focus loss retains completed selection");
        // Synthetic pixel parity: cropping source pixels cannot include the selection/HUD drawing layer.
        byte[] bytes = new byte[16 * 12 * 4]; for (int i = 0; i < bytes.Length; i++) bytes[i] = (byte)(i % 251);
        var image = BitmapSource.Create(16, 12, 96, 96, PixelFormats.Bgr32, null, bytes, 16 * 4); image.Freeze();
        var crop = new CroppedBitmap(image, new Int32Rect(3, 4, 7, 5)); crop.Freeze(); byte[] actual = new byte[7 * 5 * 4]; crop.CopyPixels(actual, 7 * 4, 0);
        Check(crop.PixelWidth == 7 && crop.PixelHeight == 5, "Synthetic crop physical dimensions");
        Check(Enumerable.Range(0, 5).All(y => actual.AsSpan(y * 28, 28).SequenceEqual(bytes.AsSpan(((y + 4) * 16 + 3) * 4, 28))), "Synthetic crop exact pixel parity");
        var monitors = Native.Monitors();
        foreach (var monitor in monitors)
        {
            var frame = Native.Capture(monitor);
            Check(frame.Image.PixelWidth == monitor.Width && frame.Image.PixelHeight == monitor.Height && frame.Image.IsFrozen, "Native capture bounds/frozen " + monitor.Device);
            Check(Native.LiveDcs == 0 && Native.LiveBitmaps == 0, "Native resources disposed " + monitor.Device);
        }
        // Own synthetic WPF surface and cover: verify hide-before-capture with known colors, no user pixels written to disk.
        var primary = monitors.First(m => m.Primary);
        int width = Math.Min(400, primary.Width), height = Math.Min(300, primary.Height);
        var region = primary with { Left = primary.Left + 40, Top = primary.Top + 80, Width = width, Height = height };
        Window TestWindow(Brush color)
        {
            var w = new Window { WindowStyle = WindowStyle.None, ResizeMode = ResizeMode.NoResize, ShowInTaskbar = false, Topmost = true,
                Width = width, Height = height, Background = color, Content = new Border { Background = color } };
            w.SourceInitialized += (_, _) => Native.Place(new WindowInteropHelper(w).Handle, region); w.Show(); Native.Place(new WindowInteropHelper(w).Handle, region); return w;
        }
        var reference = TestWindow(Brushes.Red); Window? ownUi = null;
        byte[] Center(BitmapSource b) { var pixel = new byte[4]; b.CopyPixels(new Int32Rect(b.PixelWidth / 2, b.PixelHeight / 2, 1, 1), pixel, 4, 0); return pixel; }
        try
        {
            await Dispatcher.Yield(DispatcherPriority.Render); Native.Flush();
            ownUi = TestWindow(Brushes.Lime); await Task.Delay(150); Native.Flush();
            Check(Center(Native.Capture(region).Image).Take(3).SequenceEqual(new byte[] { 0, 255, 0 }), "Synthetic own UI visibly covers reference (control)");
            ownUi.Hide(); await Dispatcher.Yield(DispatcherPriority.Render); Native.Flush();
            Check(Center(Native.Capture(region).Image).Take(3).SequenceEqual(new byte[] { 0, 0, 255 }), "Hide + DwmFlush removes own UI before BitBlt");
        }
        finally { ownUi?.Close(); reference.Close(); }
        var overlaySample = await harness.Start(); Check(harness.HasCapture, "All actual monitor overlays rendered and placement validated");
        var activeOverlay = harness.TestOverlays.First(o => o.Frame.Monitor.Primary);
        activeOverlay.Selection.Full(100, 100); var prior = activeOverlay.Selection.Completed;
        activeOverlay.Selection.Begin(new(10, 10)); activeOverlay.Selection.Move(new(30, 30), 100, 100);
        var focusTarget = TestWindow(Brushes.Blue);
        try
        {
            focusTarget.Activate(); await Task.Delay(80);
            Check(!activeOverlay.IsActive && harness.HasCapture && activeOverlay.Selection.Completed == prior && !activeOverlay.Selection.Active,
                "Actual own-window deactivation: B restores completed selection and retains capture");
            activeOverlay.Activate(); await Task.Delay(80);
            Check(activeOverlay.IsActive && activeOverlay.Selection.Completed == prior, "Own-window return restores overlay interaction with retained selection");
            harness.PreserveGesture = true;
            activeOverlay.Selection.Begin(new(10, 10)); activeOverlay.Selection.Move(new(30, 30), 100, 100); var savedPreview = activeOverlay.Selection.Preview;
            focusTarget.Activate(); await Task.Delay(80);
            Check(harness.HasCapture && activeOverlay.Selection.Active && activeOverlay.Selection.Paused && activeOverlay.Selection.Preview == savedPreview,
                "Actual own-window deactivation: A preserves paused gesture");
            activeOverlay.Activate(); harness.PreserveGesture = false;
            void Escape()
            {
                activeOverlay.RaiseEvent(new System.Windows.Input.KeyEventArgs(System.Windows.Input.Keyboard.PrimaryDevice,
                    PresentationSource.FromVisual(activeOverlay), 0, System.Windows.Input.Key.Escape) { RoutedEvent = System.Windows.Input.Keyboard.PreviewKeyDownEvent });
            }
            Escape(); Check(harness.HasCapture && !activeOverlay.Selection.Active && activeOverlay.Selection.Completed == prior, "WPF Escape cancels gesture only");
            Escape(); Check(!harness.HasCapture, "WPF neutral Escape explicitly cancels capture, including completed selection");
        }
        finally { focusTarget.Close(); harness.PreserveGesture = false; }
        await harness.Start();
        activeOverlay = harness.TestOverlays.First(o => o.Frame.Monitor.Primary);
        void Press(System.Windows.Input.Key key) => activeOverlay.RaiseEvent(new System.Windows.Input.KeyEventArgs(
            System.Windows.Input.Keyboard.PrimaryDevice, PresentationSource.FromVisual(activeOverlay), 0, key)
            { RoutedEvent = System.Windows.Input.Keyboard.PreviewKeyDownEvent });
        var fullFrame = activeOverlay.Frame;
        Press(System.Windows.Input.Key.Space);
        Check(activeOverlay.Selection.Completed == new PxRect(0, 0, fullFrame.Monitor.Width, fullFrame.Monitor.Height), "WPF Space selects complete physical monitor");
        Press(System.Windows.Input.Key.Enter);
        Check(!harness.HasCapture && harness.LastCrop is { } full && full.PixelWidth == fullFrame.Monitor.Width && full.PixelHeight == fullFrame.Monitor.Height,
            "WPF Enter commits full monitor and closes overlays");
        Check(Center(harness.LastCrop!).SequenceEqual(Center(fullFrame.Image)), "Committed full-monitor crop equals source pixel, not overlay HUD/dimming");
        await harness.Start();
        harness.Suspend("Synthetic system-event path test (not an actual lock/disconnect)."); Check(harness.HasCapture, "Suspend retains frozen capture");
        harness.Cancel(false); Check(!harness.HasCapture, "Explicit cancel closes all overlays");
        Check(Native.LiveDcs == 0 && Native.LiveBitmaps == 0, "End-of-test native counters zero");
        Harness.SaveReport("self-test-progress", new { status = "PASS", count = passed.Count });
        return new { status = "PASS", count = passed.Count, machine = Native.Machine(), monitors, checks = passed, overlaySample,
            resources = Native.Resources(), limitations = "Geometry simulations are not hardware acceptance. Synthetic suspend is not actual lock/disconnect. Application capture correctness and focus gestures require manual observation." };
    }
}
