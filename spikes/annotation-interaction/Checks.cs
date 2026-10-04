using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

internal static class Checks
{
    public static async Task<object> Run(Launcher launcher)
    {
        var passed = new List<string>();
        void Check(bool condition, string name)
        {
            if (!condition) throw new InvalidOperationException("Self-check failed: " + name);
            passed.Add(name); Launcher.SaveReport("self-test-progress", new { status = "IN PROGRESS", count = passed.Count, last = name });
        }
        string Fingerprint(Draft d) => JsonSerializer.Serialize(d.Items.Select(a => a switch
        { Marker m => $"{m.Id}:{m.Label}:{m.Anchor}:{m.Comment}", Arrow a2 => $"{a2.Id}:{a2.Start}:{a2.End}", Box b => $"{b.Id}:{b.Rect}", _ => "?" }));
        byte[] Pixels(BitmapSource image) { var data = new byte[image.PixelWidth * image.PixelHeight * 4]; image.CopyPixels(data, image.PixelWidth * 4, 0); return data; }
        BitmapSource Background(int w, int h)
        {
            var pixels = new byte[w * h * 4]; for (int i = 0; i < pixels.Length; i += 4) { pixels[i] = 40; pixels[i + 1] = 50; pixels[i + 2] = 60; pixels[i + 3] = 255; }
            var image = BitmapSource.Create(w, h, 96, 96, PixelFormats.Bgra32, null, pixels, w * 4); image.Freeze(); return image;
        }
        var d = new Draft(800, 600);
        foreach (string empty in new[] { "", " ", "\r\n\t" })
        {
            bool rejected = false; try { d.CreateMarker(new(1, 1), empty); } catch (ArgumentException) { rejected = true; }
            Check(rejected && d.Items.Count == 0 && d.NextMarker == 1, "Empty/whitespace marker rejected without ordinal consumption: " + empty.Length);
        }
        var a1 = d.CreateMarker(new(120, 100), "проверить эту кнопку 🌍");
        var a2 = d.CreateMarker(new(250, 200), "русский\r\nмногострочный комментарий");
        var a3 = d.CreateMarker(new(300, 300), "English");
        d.Delete(a2.Id); Check(d.Items.OfType<Marker>().Select(m => m.Label).SequenceEqual(new[] { "A1", "A3" }), "Delete A2 never renumbers A3");
        Check(d.NextMarker == 4, "Next marker remains A4 after deletion");
        var allocation = new Draft(100, 100); allocation.CreateMarker(new(30, 30), "one"); allocation.Undo();
        Check(allocation.CreateMarker(new(40, 40), "branch").Label == "A2", "Undo create never recycles a previously committed marker label");
        d.Undo(); Check(d.Items.OfType<Marker>().First(m => m.Id == a2.Id) == a2, "Undo restores exact A2 identity and comment");
        d.Redo(); var a4 = d.CreateMarker(new(400, 350), "A4 body"); Check(a4.Label == "A4" && a4.Id != a1.Id, "New A4 internal identity independent of display numbering");
        var unicode = d.Comments(); Check(unicode.Contains("A1 — проверить эту кнопку 🌍") && !unicode.Contains("A2 —") && unicode.Contains("A3 — English"), "Structured Unicode formatting and deleted-marker exclusion");
        d.Undo(); d.Undo(); Check(d.Comments().Contains("A2 — русский" + Environment.NewLine + "     многострочный комментарий"), "Restored multiline formatter preserves Cyrillic with continuation indentation");
        d.Edit(a1.Id, "new comment"); Check(d.Items.OfType<Marker>().First(m => m.Id == a1.Id).Anchor == a1.Anchor, "Comment edit leaves anchor and identity unchanged");
        Check(!d.CanRedo, "New edit after undo clears redo branch");
        var original = Fingerprint(d); d.Move(a1.Id, new(-50, 900)); Check(d.Items.OfType<Marker>().First(m => m.Id == a1.Id).Anchor == new P(0, 599), "Marker move clamps to physical pixel bounds");
        d.Undo(); Check(Fingerprint(d) == original, "Move undo restores exact draft"); d.Redo(); d.Undo();
        var mixed = new Draft(600, 500); var snapshots = new List<string> { Fingerprint(mixed) };
        var m1 = mixed.CreateMarker(new(100, 100), "one"); snapshots.Add(Fingerprint(mixed));
        var m2 = mixed.CreateMarker(new(200, 200), "two"); snapshots.Add(Fingerprint(mixed));
        mixed.AddArrow(new(10, 20), new(300, 240)); snapshots.Add(Fingerprint(mixed));
        mixed.Edit(m1.Id, "edited\nmultiline"); snapshots.Add(Fingerprint(mixed));
        mixed.Move(m1.Id, new(140, 160)); snapshots.Add(Fingerprint(mixed));
        mixed.AddBox(new(450, 400), new(300, 260)); snapshots.Add(Fingerprint(mixed));
        mixed.Delete(m2.Id); snapshots.Add(Fingerprint(mixed));
        mixed.Delete(mixed.Items.OfType<Arrow>().Single().Id); snapshots.Add(Fingerprint(mixed));
        mixed.Delete(mixed.Items.OfType<Box>().Single().Id); snapshots.Add(Fingerprint(mixed));
        for (int i = snapshots.Count - 2; i >= 0; i--) Check(mixed.Undo() && Fingerprint(mixed) == snapshots[i], "Mixed history undo exact state " + i);
        for (int i = 1; i < snapshots.Count; i++) Check(mixed.Redo() && Fingerprint(mixed) == snapshots[i], "Mixed history redo exact state " + i);
        mixed.Undo(); var branch = mixed.CreateMarker(new(40, 40), "branch"); Check(branch.Label == "A3" && !mixed.CanRedo, "Committed IDs not recycled on undo branch");
        var tiny = new Draft(500, 400); Check(!tiny.AddArrow(new(5, 5), new(6, 6)) && !tiny.AddBox(new(5, 5), new(6, 30)) && !tiny.CanUndo, "Accidental tiny supporting gestures make no history entry");
        foreach (var end in new[] { new P(200, 100), new P(0, 100), new P(100, 200), new P(100, 0), new P(200, 200), new P(0, 0), new P(0, 200), new P(200, 0) })
        {
            var start = new P(100, 100); var head = Geo.ArrowHead(start, end);
            Check((end.X - head.left.X) * (end.X - start.X) + (end.Y - head.left.Y) * (end.Y - start.Y) > 0, "Arrowhead behind tip in direction " + end);
        }
        Check(Geo.Normalize(new(100, 200), new(10, 20), 500, 400) == new Bounds(10, 20, 90, 180), "Reverse rectangle normalized");
        Check(Geo.Normalize(new(-5, 500), new(900, -2), 500, 400) == new Bounds(0, 0, 499, 399), "Rectangle clamps every edge");
        var hitDraft = new Draft(600, 500); var hitMarker = hitDraft.CreateMarker(new(100, 100), "hit"); hitDraft.AddArrow(new(200, 200), new(400, 200)); hitDraft.AddBox(new(20, 300), new(200, 400));
        Check(Geo.Hit(hitDraft.Items, new(100, 100), 600, 500)?.Id == hitMarker.Id, "Marker badge hit testing");
        Check(Geo.Hit(hitDraft.Items, new(300, 202), 600, 500) is Arrow, "Arrow line hit testing");
        Check(Geo.Hit(hitDraft.Items, new(20, 350), 600, 500) is Box, "Rectangle edge hit testing");
        Check(Geo.Hit(hitDraft.Items, new(100, 350), 600, 500) == null, "Rectangle interior remains available for marker creation");
        Check(Draft.EnterAction(false, false) == EditorEnter.Commit && Draft.EnterAction(true, false) == EditorEnter.Newline && Draft.EnterAction(false, true) == EditorEnter.Blocked, "Enter/Shift+Enter/Ctrl+Enter edit semantics");
        foreach (double scale in new[] { 1.0, 1.25, 1.5, 2.0 })
        foreach (var p in new[] { new P(0, 0), new P(799, 599), new P(400, 300), new P(799, 300), new P(400, 599) })
        {
            Check(Geo.FromDip(Geo.ToDip(p, scale, scale), scale, scale).Distance(p) < 1e-8, "Physical/DIP roundtrip " + scale + "/" + p);
            var editor = Geo.Editor(p, 360 * scale, 195 * scale, 800, 600);
            Check(editor.X >= 0 && editor.Y >= 0 && editor.X + editor.Width <= 800 && editor.Y + editor.Height <= 600, "Editor flip/clamp " + scale + "/" + p);
            Check(Geo.Badge(p, "A100", 800, 600).Contains(p), "Badge anchor remains inside edge-adjusted badge " + scale + "/" + p);
        }
        var renderDraft = new Draft(800, 600); var rm = renderDraft.CreateMarker(new(123, 211), "UNIQUE_BODY_MUST_NOT_BE_RASTERIZED");
        renderDraft.AddArrow(new(200, 250), new(450, 300)); renderDraft.AddBox(new(500, 100), new(700, 400));
        var background = Background(800, 600); var beforeRender = Painter.Render(background, Painter.Project(renderDraft.Items));
        renderDraft.Edit(rm.Id, "другой Unicode body\ncompletely different length"); var afterRender = Painter.Render(background, Painter.Project(renderDraft.Items));
        Check(Pixels(beforeRender).SequenceEqual(Pixels(afterRender)), "Changing only comment bodies leaves entire rendered bitmap byte-identical");
        Check(typeof(BadgeGlyph).GetProperties().All(p => p.Name != "Comment" && p.Name != "Body"), "Glyph renderer model contains no comment body field");
        Check(Painter.Project(renderDraft.Items).OfType<BadgeGlyph>().Single().Anchor == rm.Anchor, "Projection preserves exact marker anchor");
        Check(afterRender.PixelWidth == 800 && afterRender.PixelHeight == 600 && afterRender.IsFrozen, "Output physical dimensions and frozen lifetime");
        var centerPixel = new byte[4]; afterRender.CopyPixels(new Int32Rect(123, 211, 1, 1), centerPixel, 4, 0);
        Check(centerPixel[0] > 200 && centerPixel[1] > 200 && centerPixel[2] < 30, "Rendered cyan marker anchor exactly at document pixel");
        var placements = new List<object>(); var actualMonitors = Native.Monitors();
        foreach (var monitor in actualMonitors)
        {
            placements.Add(await launcher.Start(monitor.Device)); var overlay = launcher.Active!;
            P[] anchors = [new(monitor.Width / 2, monitor.Height / 2), new(monitor.Width - 1, monitor.Height / 2), new(monitor.Width / 2, monitor.Height - 1), new(monitor.Width - 1, monitor.Height - 1)];
            foreach (var anchor in anchors)
            {
                overlay.BeginPointer(anchor); await Dispatcher.Yield(DispatcherPriority.Input);
                Check(overlay.Editing && overlay.CommentInput.IsKeyboardFocused, "Immediate editor focus actual " + monitor.Device + "/" + anchor);
                Check(overlay.VisibleGlyphs().OfType<BadgeGlyph>().Any(g => g.Label == "A1" && g.Anchor == anchor), "Provisional A1 glyph at physical anchor " + monitor.Device + "/" + anchor);
                var b = overlay.EditorBounds; Check(b.X >= 0 && b.Y >= 0 && b.X + b.Width <= monitor.Width && b.Y + b.Height <= monitor.Height, "Actual HWND editor bounds " + monitor.Device + "/" + anchor);
                overlay.CommentInput.Text = " \r\n"; Check(!overlay.CommitComment() && overlay.Model.NextMarker == 1, "UI empty rejection retains provisional editor " + monitor.Device + "/" + anchor);
                overlay.CancelEdit(); Check(overlay.Model.Items.Count == 0 && overlay.Model.NextMarker == 1, "Cancelled provisional consumes no ID " + monitor.Device + "/" + anchor);
            }
            overlay.BeginEdit(new(200, 200)); overlay.CommentInput.Text = "uncommitted";
            var companion = launcher.Overlays.First(o => !o.Editable);
            companion.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(companion), 0, Key.Escape) { RoutedEvent = Keyboard.PreviewKeyDownEvent });
            Check(!overlay.Editing && launcher.Active == overlay && overlay.Model.NextMarker == 1, "Companion Escape cancels current comment, never whole draft " + monitor.Device);
            void Press(Key key) => overlay.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(overlay), 0, key) { RoutedEvent = Keyboard.PreviewKeyDownEvent });
            overlay.BeginEdit(new(300, 250)); overlay.CommentInput.Text = "проверить эту кнопку"; Press(Key.Enter);
            Check(!overlay.Editing && launcher.Active == overlay && overlay.Model.Items.Count == 1, "WPF Enter commits Unicode comment, never screenshot " + monitor.Device);
            var created = overlay.Model.Items.OfType<Marker>().Single(); Check(created.Label == "A1" && created.Comment == "проверить эту кнопку", "Actual marker/comment association " + monitor.Device);
            overlay.BeginPointer(created.Anchor, 2); overlay.CommentInput.Text = "русский\r\nмногострочный комментарий";
            Check(overlay.CommitComment() && overlay.Model.Items.OfType<Marker>().Single().Comment.Contains("\r\n"), "Existing comment multiline edit " + monitor.Device);
            overlay.BeginPointer(created.Anchor); overlay.MovePointer(new(350, 280)); overlay.EndPointer(new(350, 280));
            Check(overlay.Model.Items.OfType<Marker>().Single().Anchor == new P(350, 280), "Marker move interaction handler " + monitor.Device);
            overlay.History(false); Check(overlay.Model.Items.OfType<Marker>().Single().Anchor == created.Anchor, "Toolbar document undo marker move " + monitor.Device);
            overlay.BeginPointer(created.Anchor); overlay.MovePointer(new(390, 350)); overlay.InterruptGesture();
            Check(overlay.Model.Items.OfType<Marker>().Single().Anchor == created.Anchor && launcher.Active == overlay,
                "Interrupted move rolls back preview only, preserves draft " + monitor.Device);
            overlay.Switch(Tool.Arrow); overlay.BeginPointer(new(500, 450)); overlay.MovePointer(new(700, 550)); overlay.EndPointer(new(700, 550));
            Check(overlay.Model.Items.OfType<Arrow>().Count() == 1, "Arrow drag interaction handler " + monitor.Device);
            overlay.Switch(Tool.Rectangle); overlay.BeginPointer(new(900, 850)); overlay.MovePointer(new(750, 650)); overlay.EndPointer(new(750, 650));
            Check(overlay.Model.Items.OfType<Box>().Single().Rect == new Bounds(750, 650, 150, 200), "Reverse rectangle interaction handler " + monitor.Device);
            overlay.BeginPointer(created.Anchor); overlay.EndPointer(created.Anchor); Press(Key.Delete);
            Check(!overlay.Model.Items.OfType<Marker>().Any() && !overlay.Model.Comments().Contains("A1 —"), "WPF Delete removes selected marker and structured comment " + monitor.Device);
            overlay.History(false); Check(overlay.Model.Items.OfType<Marker>().Single().Id == created.Id, "Undo selected marker deletion restores internal identity " + monitor.Device);
            overlay.BeginEdit(created.Anchor, created.Id); overlay.CommentInput.Text = "pending typed text\nрусский";
            var probe = new Window { Width = 100, Height = 100, ShowInTaskbar = false, Topmost = true, Background = Brushes.Black }; probe.Show(); probe.Activate(); await Task.Delay(70);
            Check(overlay.Editing && overlay.CommentInput.Text == "pending typed text\nрусский" && overlay.Model.Items.Count == 3, "Actual own-window focus loss retains typed edit and annotations " + monitor.Device);
            probe.Close(); launcher.ReturnToDraft(); await Dispatcher.Yield(DispatcherPriority.Input);
            Check(overlay.CommentInput.IsKeyboardFocused && overlay.CommentInput.Text.Contains("русский"), "Focus return restores open editor " + monitor.Device); overlay.CancelEdit();
            double commitMs = launcher.Commit(overlay);
            Check(launcher.IsVisible && launcher.Active == null, "Commit returns to visible read-only launcher, no editor window " + monitor.Device);
            Check(launcher.LastBitmap!.PixelWidth == monitor.Width && launcher.LastBitmap.PixelHeight == monitor.Height, "Actual monitor output dimensions " + monitor.Device);
            Check(launcher.LastComments.Contains("A1 — русский") && launcher.LastComments.Contains("     многострочный комментарий") && launcher.LastAnnotations.Count == 3, "Separate structured inspection and annotated bitmap " + monitor.Device);
            Check(Native.LiveDcs == 0 && Native.LiveBitmaps == 0, "Native resources zero after annotation commit " + monitor.Device);
            launcher.ClearInspection();
        }
        // Single finite stress case; no fabricated responsiveness/latency threshold.
        var stress = new Draft(1100, 800); var timer = Stopwatch.StartNew(); Launcher.PopulateStress(stress); timer.Stop(); double populateMs = timer.Elapsed.TotalMilliseconds;
        Check(stress.Items.Count == 100 && stress.Items.OfType<Marker>().Count() == 50 && stress.Items.OfType<Arrow>().Count() == 25 && stress.Items.OfType<Box>().Count() == 25, "Stress 50 markers /25 arrows /25 rectangles");
        string stressState = Fingerprint(stress); timer.Restart(); for (int i = 0; i < 100; i++) stress.Undo(); for (int i = 0; i < 100; i++) stress.Redo(); timer.Stop(); double historyMs = timer.Elapsed.TotalMilliseconds;
        Check(Fingerprint(stress) == stressState, "Stress 100 undo +100 redo restores exact state");
        timer.Restart(); var stressOutput = Painter.Render(Background(1100, 800), Painter.Project(stress.Items)); timer.Stop(); double syntheticRenderMs = timer.Elapsed.TotalMilliseconds;
        Check(stressOutput.PixelWidth == 1100 && stressOutput.PixelHeight == 800, "Stress renderer completes with expected dimensions");
        await launcher.Start(); Launcher.PopulateStress(launcher.Active!.Model); launcher.Active.Refresh(); await Dispatcher.Yield(DispatcherPriority.Render); Native.Flush();
        double actualStressCommitMs = launcher.Commit(launcher.Active!);
        Check(launcher.LastAnnotations.Count == 100 && launcher.LastComments.Contains("A50 —"), "Actual full-monitor 100-annotation commit and structured comments"); launcher.ClearInspection();
        var resources = new List<object>();
        async Task Settle() { await Task.Delay(100); GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect(); await Task.Delay(100); }
        for (int i = 0; i < 3; i++) { await launcher.Start(); launcher.Cancel(false); await Settle(); }
        resources.Add(new { cycle = 0, counters = Native.Resources() });
        for (int i = 1; i <= 50; i++)
        {
            await launcher.Start(); var o = launcher.Active!;
            o.BeginEdit(new(200, 200)); o.CommentInput.Text = "synthetic lifetime comment"; o.CommitComment();
            if (i % 2 == 0) launcher.Commit(o); else launcher.Cancel(false);
            launcher.ClearInspection(); await Settle();
            Check(Native.LiveDcs == 0 && Native.LiveBitmaps == 0 && launcher.Active == null && launcher.LastBitmap == null, "Capture/editor/commit/cancel resource ownership cycle " + i);
            if (i % 5 == 0) resources.Add(new { cycle = i, counters = Native.Resources() });
        }
        await Task.Delay(1500); await Settle(); resources.Add(new { cycle = 50, phase = "quiet-after-series", counters = Native.Resources() });
        Launcher.SaveReport("self-test-progress", new { status = "PASS", count = passed.Count });
        return new { status = "PASS", recordedAt = DateTimeOffset.Now, count = passed.Count, machine = Native.Machine(), monitors = actualMonitors, checks = passed, placements,
            stress = new { populateMs, undoRedo200OperationsMs = historyMs, synthetic1100x800RenderMs = syntheticRenderMs, actualStressCommitMs, annotations = 100 }, resources,
            limitations = "Input handler tests/own-window focus tests are automated, not human interaction acceptance. No screenshot pixels or actual user comments stored in this report. Physical 150/200/portrait NOT TESTED." };
    }
}
