using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

internal sealed record ScreenshotRaster(BitmapSource Original, BitmapSource Annotated);
internal sealed class Coordinator : IDisposable
{
    public Session Session { get; } = new();
    public AnnotationOverlay? Active { get; private set; }
    internal readonly Dictionary<Guid, ScreenshotRaster> Rasters = [];
    private readonly List<AnnotationOverlay> overlays = [];
    private readonly Window control = new() { Title = "ScreenIt control", Width = 1, Height = 1, ShowInTaskbar = false };
    private readonly HwndSource source;
    private readonly Forms.NotifyIcon tray;
    private readonly Forms.ContextMenuStrip menu = new();
    private readonly Forms.ToolStripMenuItem count = new(), copyImages = new("Copy Session Images"), copyComments = new("Copy Session Comments"), capture = new("Capture") { ShortcutKeyDisplayString="Ctrl+Alt+S" };
    private readonly TemporaryImages temporary = new();
    private readonly PasteSequencer paste;
    private readonly CancellationTokenSource pasteCancellation = new();
    private readonly Forms.ToolStripMenuItem pasteCommand = new("Paste Session") { ShortcutKeyDisplayString="Ctrl+Alt+V" };
    private readonly Forms.ToolStripMenuItem darkTheme=new("Dark"), lightTheme=new("Light");
    private string topology = "";
    private bool busy, suspended, disposed;
    internal ClearSessionDialog? ClearDialog { get; private set; }
    internal bool ConfirmationOpen => ClearDialog != null;
    private readonly System.Drawing.Icon icon;
    private readonly Forms.ToolStripMenuItem clearCommand = new("Clear Session") { ShortcutKeyDisplayString="Ctrl+Alt+X" }, feedback = new() { Enabled = false };
    internal Func<int, bool>? ConfirmClear { get; set; }
    internal Func<int,ClearSessionDialog> ClearDialogFactory { get; set; } = count => new ClearSessionDialog(count);
    internal void RequestClear() => Application.Current.Dispatcher.BeginInvoke(()=>Clear());
    internal Forms.ToolStripMenuItem[] PrimaryCommands => [capture,pasteCommand,clearCommand];
    internal string SessionStatus => count.Text ?? "";
    internal ToastService Toasts { get; } = new();
    public int OverlayCount => overlays.Count;
    internal IntPtr ControlHandle => source.Handle;
    public Coordinator(bool showTray = true, bool registerHotkey = true)
    {
        var hwnd = new WindowInteropHelper(control).EnsureHandle(); source = HwndSource.FromHwnd(hwnd)!; source.AddHook(Hook);
        if (registerHotkey && !Native.RegisterHotKey(hwnd, 1, 0x4000 | 0x0001 | 0x0002, 0x53))
        { source.RemoveHook(Hook); control.Close(); source.Dispose(); throw new InvalidOperationException("Ctrl+Alt+S is already in use."); }
        if (registerHotkey && !Native.RegisterHotKey(hwnd, 2, 0x4000 | 0x0001 | 0x0002, 0x56))
        { Native.UnregisterHotKey(hwnd, 1); source.RemoveHook(Hook); control.Close(); source.Dispose(); throw new InvalidOperationException("Ctrl+Alt+V is already in use."); }
        if (registerHotkey && !Native.RegisterHotKey(hwnd, 3, 0x4003, 0x58))
        { Native.UnregisterHotKey(hwnd, 1); Native.UnregisterHotKey(hwnd, 2); source.RemoveHook(Hook); control.Close(); source.Dispose(); throw new InvalidOperationException("Ctrl+Alt+X is already in use."); }
        paste = new(new WindowsPasteDelivery(hwnd));
        paste.Progress += (state, index, total) => { if (!disposed) Update(state == PasteState.PastingComments ? "Pasting comments…" : $"Pasting session… {index}/{total}"); };
        Native.WTSRegisterSessionNotification(hwnd, 0);
        capture.Click += async (_, _) => await Capture();
        copyImages.Click += (_, _) => CopyImages(); copyComments.Click += (_, _) => CopyComments();
        // A tray menu owns foreground. Do not guess/activate an earlier receiver from there.
        pasteCommand.Click += (_, _) => Notify("Focus the target composer, then press Ctrl+Alt+V.");
        count.Enabled = false;
        clearCommand.Click += (_, _) => RequestClear();
        var more = new Forms.ToolStripMenuItem("More"); more.DropDownItems.Add(copyImages); more.DropDownItems.Add(copyComments);
        menu.Items.Add(capture); menu.Items.Add(pasteCommand); menu.Items.Add(new Forms.ToolStripSeparator()); menu.Items.Add(count); menu.Items.Add(feedback);
        var themes=new Forms.ToolStripMenuItem("Theme");themes.DropDownItems.Add(darkTheme);themes.DropDownItems.Add(lightTheme);
        darkTheme.Click+=(_,_)=>SelectTheme(UiTheme.Dark);lightTheme.Click+=(_,_)=>SelectTheme(UiTheme.Light);
        menu.Items.Add(clearCommand); menu.Items.Add(themes); menu.Items.Add(more); menu.Items.Add(new Forms.ToolStripSeparator()); menu.Items.Add("Exit", null, (_, _) => Exit());
        icon = UtilityUi.TrayIcon();
        tray = new Forms.NotifyIcon { Icon = icon, Text = "ScreenIt", ContextMenuStrip = menu, Visible = showTray };
        tray.DoubleClick += async (_, _) => await Capture();
        source.Disposed += (_, _) =>
        {
            if (!disposed && !Application.Current.Dispatcher.HasShutdownStarted)
                Application.Current.Dispatcher.BeginInvoke(Exit);
        };
        try { temporary.Cleanup(DateTimeOffset.UtcNow); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        Update();
    }
    private void SelectTheme(UiTheme theme)
    {
        try { Appearance.Save(Appearance.SettingsPath,theme);Appearance.Select(theme);Update(); }
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException) { Error("Appearance could not be saved. Please retry."); }
    }
    private IntPtr Hook(IntPtr hwnd, int msg, IntPtr wp, IntPtr lp, ref bool handled)
    {
        if (msg == 0x0010) { handled = true; Application.Current.Dispatcher.BeginInvoke(Exit); return IntPtr.Zero; }
        if (msg == 0x0312)
        {
            handled = true;
            if (wp.ToInt64() == 2) { var target = PasteInput.Foreground(); _ = PasteSession(target); }
            else if (wp.ToInt64() == 1) _ = Capture();
            else if (wp.ToInt64() == 3) RequestClear();
        }
        if (overlays.Count != 0 && (msg == 0x007E || msg == 0x001A))
        {
            try { if (Topology(Native.Monitors()) != topology) Suspend("Display configuration changed."); } catch (Exception) { Suspend("Display configuration is unavailable."); }
        }
        if (overlays.Count != 0 && (msg == 0x0218 && wp.ToInt64() is 4 or 7 or 18 || msg == 0x02B1 && wp.ToInt64() is 7 or 8)) Suspend("Windows session or power state changed.");
        return IntPtr.Zero;
    }
    private static string Topology(MonitorData[] monitors) => string.Join("|", monitors.Select(m => m.TopologyKey));
    public async Task Capture()
    {
        if (paste.IsActive) { Update("Paste Session is running"); return; }
        if (ConfirmationOpen || UtilityUi.PromptOpen) { ClearDialog?.BringForward();Update("Close the Clear Session confirmation first"); return; }
        if (disposed || busy) return;
        if (overlays.Count != 0) { ReturnToDraft(); return; }
        busy = true; Toasts.Hide(); Update(); menu.Close();
        try
        {
            await Dispatcher.Yield(DispatcherPriority.Render); Native.Flush();
            var monitors = Native.Monitors(); topology = Topology(monitors);
            var frames = await Task.Run(() => monitors.Select(Native.Capture).ToArray());
            if (Topology(Native.Monitors()) != topology) throw new InvalidOperationException("Displays changed during capture. Please retry.");
            suspended = false;
            foreach (var frame in frames) overlays.Add(new(this, frame));
            foreach (var overlay in overlays) { overlay.Show(); Native.Place(new WindowInteropHelper(overlay).Handle, overlay.Frame.Monitor); }
            await Task.WhenAll(overlays.Select(o => o.Rendered.Task)).WaitAsync(TimeSpan.FromSeconds(5)); Native.Flush();
            foreach (var overlay in overlays) overlay.Placement();
            overlays.FirstOrDefault(o => o.Frame.Monitor.Primary)?.ReturnFocus();
        }
        catch (Exception ex) { CloseOverlays(); Error("Capture could not complete. " + SafeError(ex)); }
        finally { busy = false; Update(); }
    }
    public bool IsCurrent(AnnotationOverlay overlay) => overlays.Contains(overlay);
    public void SelectMonitor(AnnotationOverlay selected)
    {
        foreach (var other in overlays.Where(o => o != selected)) { other.RegionSelection.Clear(); other.Refresh(); }
    }
    public void ActivateDraft(AnnotationOverlay selected) { Active = selected; foreach (var overlay in overlays) overlay.Refresh(); }
    public void ReturnToDraft()
    {
        if (suspended) { Error("Capture is suspended. Cancel it and start a fresh capture."); Cancel(); return; }
        (Active ?? overlays.FirstOrDefault(o => o.Frame.Monitor.Primary))?.ReturnFocus();
    }
    public void Commit(AnnotationOverlay overlay)
    {
        if (!IsCurrent(overlay) || overlay != Active || overlay.Editing || suspended) return;
        try
        {
            var raster = Painter.Render(overlay.Crop, Painter.Project(overlay.Model.Items));
            var pair = new ScreenshotRaster(overlay.Crop, raster);
            Rasters.EnsureCapacity(Rasters.Count + 1);
            var screenshot = Session.Commit(overlay.Model);
            Rasters.Add(screenshot.Id, pair);
            CloseOverlays(); Update("Screenshot " + screenshot.Letter + " added");
            Toasts.Show(ToastMessage.Added(screenshot.Letter,Session.Screenshots.Count),overlay.Frame.Monitor);
        }
        catch (Exception ex) { Error("Screenshot could not be committed. " + SafeError(ex)); }
    }
    public void Cancel()
    {
        if (Active is { } active && active.Model.Items.Count > 0 && !UtilityUi.Confirm(active.IsVisible ? active : control,"Discard this screenshot?","Its annotations and comments will be discarded.","Discard")) { if (!suspended) active.ReturnFocus(); return; }
        CloseOverlays(); Update();
    }
    public void Suspend(string reason)
    {
        if (suspended || overlays.Count == 0) return; suspended = true;
        foreach (var overlay in overlays) { overlay.InterruptGesture(); overlay.Hide(); }
        Update("Capture suspended");
        Application.Current.Dispatcher.BeginInvoke(() => Error(reason + " Your screenshot is still available. Use Capture to cancel and start again."));
    }
    private void CloseOverlays()
    {
        foreach (var overlay in overlays) { overlay.ClosingByApp = true; overlay.Close(); }
        overlays.Clear(); Active = null; suspended = false;
    }
    public void CopyImages()
    {
        if (paste.IsActive) { Update("Paste Session is running"); return; }
        if (Session.Screenshots.Count == 0) return;
        try
        {
            var files = temporary.Write(Session.Screenshots.Select(s => (s.Letter, Rasters[s.Id].Annotated)).ToArray());
            ClipboardTransport.Publish(source.Handle, ClipboardPayload.Images(files)); Update(Session.Screenshots.Count + " screenshots copied");
        }
        catch (Exception ex) { Error("Images could not be copied. Session retained; retry Copy. " + SafeError(ex)); }
    }
    public void CopyComments()
    {
        if (paste.IsActive) { Update("Paste Session is running"); return; }
        if (Session.Screenshots.Count == 0) return;
        try { ClipboardTransport.Publish(source.Handle, ClipboardPayload.Comments(CommentFormatter.Format(Session))); Update("Comments copied"); }
        catch (Exception ex) { Error("Comments could not be copied. Session retained; retry Copy. " + SafeError(ex)); }
    }
    public void Clear(bool confirmed = false)
    {
        if (paste.IsActive) { Update("Paste Session is running"); return; }
        if (busy || overlays.Count != 0)
        {
            const string message = "Finish or cancel the current screenshot before clearing the session";
            Update(message); (Active ?? overlays.FirstOrDefault(o => o.Frame.Monitor.Primary))?.ShowHint(message); return;
        }
        if (ConfirmationOpen || UtilityUi.PromptOpen) { ClearDialog?.BringForward();return; }
        if (Session.Screenshots.Count == 0) { Update("Session is already empty"); return; }
        if(confirmed || ConfirmClear?.Invoke(Session.Screenshots.Count)==true) { ClearCommittedSession();return; }
        if(ConfirmClear!=null) return; // Existing verification-only policy seam; runtime always owns a real window.
        menu.Close();Toasts.Hide();
        ClearSessionDialog? dialog=null;
        try
        {
            dialog=ClearDialogFactory(Session.Screenshots.Count);ClearDialog=dialog;
            dialog.Closed+=(_,_)=>FinishClear(dialog);
            dialog.Prepare(control);dialog.Show();dialog.Activate();Update();
        }
        catch(Exception)
        {
            try { dialog?.Close(); } finally { if(ReferenceEquals(ClearDialog,dialog)) ClearDialog=null;Update("Session unchanged"); }
            Toasts.Show(new("Could not open confirmation","Session unchanged",true));
        }
    }
    private void FinishClear(ClearSessionDialog dialog)
    {
        if(!ReferenceEquals(ClearDialog,dialog)) return;
        ClearDialog=null; // The owning window closed: Cancel/Escape/X/owner shutdown all converge here.
        if(disposed) return;
        if(dialog.Accepted) ClearCommittedSession();else Update();
    }
    private void ClearCommittedSession()
    {
        Session.Clear();Rasters.Clear();Update("Session cleared");Toasts.Show(ToastMessage.Cleared());
    }
    private void Exit()
    {
        ClearDialog?.Close();
        if (Session.Screenshots.Count > 0 && !UtilityUi.Confirm(control,"Exit ScreenIt?","The current session will be discarded.","Exit")) return;
        Dispose(); Application.Current.Shutdown();
    }
    private void Update(string? hint = null)
    {
        darkTheme.Checked=Appearance.Current==UiTheme.Dark;lightTheme.Checked=Appearance.Current==UiTheme.Light;
        count.Text = $"Session: {Session.Screenshots.Count} {(Session.Screenshots.Count == 1 ? "screenshot" : "screenshots")}";
        feedback.Text = hint ?? ""; feedback.Visible = !string.IsNullOrEmpty(hint);
        clearCommand.Enabled = !busy && overlays.Count == 0 && !paste.IsActive && !ConfirmationOpen;
        capture.Enabled = !busy && !paste.IsActive && !ConfirmationOpen; copyImages.Enabled = copyComments.Enabled = pasteCommand.Enabled = Session.Screenshots.Count > 0 && !paste.IsActive && !ConfirmationOpen;
        var tooltip = "ScreenIt — " + (hint ?? count.Text);
        tray.Text = tooltip[..Math.Min(63, tooltip.Length)];
    }
    private static string SafeError(Exception ex) => ex is OutOfMemoryException ? "Not enough memory." : ex is InvalidOperationException && ex.Message.StartsWith("Clipboard is busy", StringComparison.Ordinal) ? "Clipboard is busy." : "Operation failed (" + ex.GetType().Name + ").";
    private static void Error(string message) => UtilityUi.Inform(message);
    private void Notify(string message)
    {
        Update(message);
        // Feedback stays in the tray menu/tooltip; no notification-center infrastructure.
    }
    internal async Task<PastePlan> PreparePaste()
    {
        var snapshot = Session.Screenshots.ToArray();
        var images = snapshot.Select(s => (s.Letter, Rasters[s.Id].Annotated)).ToArray();
        bool hasComments = snapshot.Any(s => s.Annotations.OfType<Marker>().Any(m => !string.IsNullOrWhiteSpace(m.Comment)));
        var comments = hasComments ? string.Join(Environment.NewLine + Environment.NewLine, snapshot.Select(s => CommentFormatter.Format(s.Letter, s.Annotations))) : "";
        var files = await Task.Run(() => temporary.Write(images));
        foreach (var file in files)
        {
            using var readable = File.Open(file, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (readable.Length == 0) throw new IOException("Prepared PNG is empty.");
        }
        return PastePlan.Create(files, comments);
    }
    internal async Task PasteSession(PasteTarget target)
    {
        if (disposed) return;
        if (ConfirmationOpen || UtilityUi.PromptOpen) { ClearDialog?.BringForward();Notify("Close the Clear Session confirmation first"); return; }
        if (busy || overlays.Count != 0) { Notify("Finish the capture, focus the receiver, then press Ctrl+Alt+V."); return; }
        bool hasComments = Session.Screenshots.Any(s => s.Annotations.OfType<Marker>().Any(m => !string.IsNullOrWhiteSpace(m.Comment)));
        var result = await paste.Run(target, Session.Screenshots.Count, PreparePaste, pasteCancellation.Token);
        if (disposed) return;
        Update(result.Status + (result.Outcome == PasteOutcome.Completed && Session.Screenshots.Any(s => s.Annotations.OfType<Marker>().Any(m => !string.IsNullOrWhiteSpace(m.Comment))) ? " + comments" : ""));
        if (result.Outcome is not PasteOutcome.Completed and not PasteOutcome.Busy) Notify(result.Status);
        if (ToastMessage.Paste(result,hasComments) is { } message) Toasts.Show(message, target: target.Hwnd);
    }
    public void Dispose()
    {
        if (disposed) return; disposed = true; ClearDialog?.Close(); ClearDialog=null; pasteCancellation.Cancel(); Toasts.Dispose(); CloseOverlays(); Rasters.Clear(); Session.Clear();
        tray.Visible = false; tray.Dispose(); icon.Dispose(); menu.Dispose();
        Native.UnregisterHotKey(source.Handle, 1); Native.UnregisterHotKey(source.Handle, 2); Native.UnregisterHotKey(source.Handle, 3); Native.WTSUnRegisterSessionNotification(source.Handle); source.RemoveHook(Hook);
        control.Close(); source.Dispose();
    }
}
