using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

internal sealed class AnnotationOverlay : Window
{
    public Frame Frame { get; }
    public Draft Model { get; }
    public bool Editable { get; }
    public Tool Tool { get; private set; } = Tool.Marker;
    public Guid? Selected { get; private set; }
    public bool Editing => editorHost.Visibility == Visibility.Visible;
    public TextBox CommentInput => editor;
    public bool ClosingByHarness;
    public readonly TaskCompletionSource Rendered = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Launcher owner;
    private readonly Surface surface;
    private readonly Canvas ui = new();
    private readonly TextBlock status = new() { Foreground = Brushes.White, Margin = new Thickness(8, 4, 8, 4) };
    private readonly Border editorHost;
    private readonly TextBox editor = new() { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        FontSize = 16, Height = 105, Padding = new Thickness(6) };
    private readonly TextBlock error = new() { Foreground = Brushes.DarkRed, TextWrapping = TextWrapping.Wrap, MaxHeight = 34 };
    private HwndSource? source;
    private P editingAnchor, down;
    private Guid? editingId;
    private Marker? moving;
    private Glyph? gesture;
    private bool dragging, ready;
    public Matrix DeviceTransform => source?.CompositionTarget?.TransformToDevice ?? Matrix.Identity;
    public Bounds EditorBounds { get; private set; }
    public AnnotationOverlay(Launcher owner, Frame frame, Draft model, bool editable)
    {
        this.owner = owner; Frame = frame; Model = model; Editable = editable;
        WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize; ShowInTaskbar = false; Topmost = true; ShowActivated = false;
        Width = frame.Monitor.Width * 96.0 / frame.Monitor.EffectiveDpi; Height = frame.Monitor.Height * 96.0 / frame.Monitor.EffectiveDpi;
        var root = new Grid { Background = Brushes.Black }; root.Children.Add(surface = new Surface(this) { Focusable = true }); root.Children.Add(ui); Content = root;
        RenderOptions.SetBitmapScalingMode(surface, BitmapScalingMode.NearestNeighbor);
        var bar = new StackPanel { Background = Brushes.Black }; var tools = new WrapPanel(); bar.Children.Add(tools); bar.Children.Add(status);
        void Button(string text, Action action) { var b = new Button { Content = text, Margin = new Thickness(2), Padding = new Thickness(6, 3, 6, 3) }; b.Click += (_, _) => action(); tools.Children.Add(b); }
        if (Editable)
        {
            Button("M Marker", () => Switch(Tool.Marker)); Button("A Arrow", () => Switch(Tool.Arrow)); Button("R Rectangle", () => Switch(Tool.Rectangle));
            Button("E Edit comment", EditSelected); Button("Delete", Delete); Button("Undo", () => History(false)); Button("Redo", () => History(true));
            Button("Commit Ctrl+Enter", () => { if (!Editing && !dragging) owner.Commit(this); else Message("Finish or cancel the current interaction first."); });
        }
        else Button("Return to draft", owner.ReturnToDraft);
        ui.Children.Add(bar); Canvas.SetLeft(bar, 10); Canvas.SetTop(bar, 10);
        var editPanel = new StackPanel { Margin = new Thickness(8) };
        editPanel.Children.Add(new TextBlock { Text = "Enter: save · Shift+Enter: newline · Esc: discard this edit", TextWrapping = TextWrapping.Wrap });
        editPanel.Children.Add(editor); editPanel.Children.Add(error);
        var buttons = new WrapPanel();
        var save = new Button { Content = "Save comment", Margin = new Thickness(3) }; save.Click += (_, _) => CommitComment(); buttons.Children.Add(save);
        var cancel = new Button { Content = "Cancel edit", Margin = new Thickness(3) }; cancel.Click += (_, _) => CancelEdit(); buttons.Children.Add(cancel); editPanel.Children.Add(buttons);
        editorHost = new Border { Background = Brushes.WhiteSmoke, BorderBrush = Brushes.DarkBlue, BorderThickness = new Thickness(2), Child = editPanel, Visibility = Visibility.Collapsed };
        ui.Children.Add(editorHost);
        SourceInitialized += (_, _) => { source = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle)!; source.AddHook(Hook); Native.Place(source.Handle, Frame.Monitor); };
        ContentRendered += (_, _) => { ready = true; Rendered.TrySetResult(); };
        Deactivated += (_, _) => InterruptGesture(); // Do not close editor or lose typed text.
        Closing += (_, e) => { if (!ClosingByHarness) { e.Cancel = true; Dispatcher.BeginInvoke(() => owner.Cancel()); } };
        Closed += (_, _) => { if (source != null) { source.RemoveHook(Hook); source.Dispose(); source = null; } editor.Text = ""; Content = null; };
        PreviewKeyDown += Keys;
        surface.MouseLeftButtonDown += Down;
        surface.MouseMove += (_, e) => MovePointer(InputPoint(e));
        surface.MouseLeftButtonUp += (_, e) => EndPointer(InputPoint(e));
        surface.LostMouseCapture += (_, _) => InterruptGesture();
        Refresh();
    }
    private IntPtr Hook(IntPtr hwnd, int message, IntPtr wp, IntPtr lp, ref bool handled)
    {
        if (message == 0x02E0 && ready && (wp.ToInt64() & 0xffff) != Frame.Monitor.EffectiveDpi)
            Dispatcher.BeginInvoke(() => { if (owner.IsCurrent(this)) owner.Suspend("Overlay DPI changed; cancel/restart explicitly."); });
        return IntPtr.Zero;
    }
    public void ReturnFocus() { Activate(); if (Editing) editor.Focus(); else surface.Focus(); }
    public object Placement()
    {
        var r = Native.WindowRect(source!.Handle); var t = DeviceTransform;
        bool exact = r.Left == Frame.Monitor.Left && r.Top == Frame.Monitor.Top && r.Right - r.Left == Frame.Monitor.Width && r.Bottom - r.Top == Frame.Monitor.Height;
        bool canvas = Math.Abs(surface.ActualWidth * t.M11 - Frame.Monitor.Width) < 1 && Math.Abs(surface.ActualHeight * t.M22 - Frame.Monitor.Height) < 1;
        if (!exact || !canvas) throw new InvalidOperationException("Physical overlay placement mismatch.");
        return new { Frame.Monitor.Device, editable = Editable, left = r.Left, top = r.Top, width = Frame.Monitor.Width, height = Frame.Monitor.Height,
            scaleX = t.M11, scaleY = t.M22, hwndDpi = Native.GetDpiForWindow(source.Handle), exact, canvas };
    }
    private P InputPoint(MouseEventArgs e)
    {
        var p = e.GetPosition(surface); var t = DeviceTransform;
        return Geo.Clamp(Geo.FromDip(new(p.X, p.Y), t.M11, t.M22), Model.Width, Model.Height);
    }
    private void Down(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true; BeginPointer(InputPoint(e), e.ClickCount);
    }
    public void BeginPointer(P p, int clickCount = 1)
    {
        if (!Editable) { owner.ReturnToDraft(); return; }
        Activate(); if (Editing) { editor.Focus(); Message("Enter saves comment; Esc cancels this edit."); return; }
        p = Geo.Clamp(p, Model.Width, Model.Height); var hit = Geo.Hit(Model.Items, p, Model.Width, Model.Height);
        Selected = hit?.Id;
        if (hit is Marker marker)
        {
            if (clickCount >= 2) { BeginEdit(marker.Anchor, marker.Id); return; }
            down = p; moving = marker; dragging = true; surface.CaptureMouse();
        }
        else if (hit != null) { surface.Focus(); }
        else if (Tool == Tool.Marker) BeginEdit(p);
        else { down = p; dragging = true; surface.CaptureMouse(); }
        Refresh();
    }
    public void MovePointer(P p)
    {
        if (!dragging) return; p = Geo.Clamp(p, Model.Width, Model.Height);
        if (moving != null)
        {
            var position = Geo.Clamp(new(moving.Anchor.X + p.X - down.X, moving.Anchor.Y + p.Y - down.Y), Model.Width, Model.Height);
            gesture = new BadgeGlyph(moving.Id, moving.Label, position);
        }
        else if (Tool == Tool.Arrow) gesture = new ArrowGlyph(Guid.Empty, down, p);
        else if (Tool == Tool.Rectangle) gesture = new BoxGlyph(Guid.Empty, Geo.Normalize(down, p, Model.Width, Model.Height));
        Refresh();
    }
    public void EndPointer(P p)
    {
        if (!dragging) return; MovePointer(p);
        if (moving != null && gesture is BadgeGlyph m && down.Distance(p) >= 3) Model.Move(moving.Id, m.Anchor);
        else if (moving == null && Tool == Tool.Arrow) Model.AddArrow(down, p);
        else if (moving == null && Tool == Tool.Rectangle) Model.AddBox(down, p);
        dragging = false; moving = null; gesture = null; surface.ReleaseMouseCapture(); surface.Focus(); Refresh();
    }
    public void InterruptGesture()
    {
        dragging = false; moving = null; gesture = null;
        if (surface.IsMouseCaptured) surface.ReleaseMouseCapture();
        if (ready) Refresh(); // B: committed model untouched; editor stays open and retains text.
    }
    public void BeginEdit(P anchor, Guid? id = null)
    {
        if (!Editable || Editing) return; InterruptGesture(); editingId = id; editingAnchor = Geo.Clamp(anchor, Model.Width, Model.Height);
        var m = id.HasValue ? Model.Items.OfType<Marker>().FirstOrDefault(a => a.Id == id) : null;
        editor.Text = m?.Comment ?? ""; error.Text = "";
        editorHost.Visibility = Visibility.Visible; LayoutEditor(); ReturnFocus(); editor.CaretIndex = editor.Text.Length;
        Dispatcher.BeginInvoke(() => { if (Editing && IsVisible && IsActive) editor.Focus(); }, System.Windows.Threading.DispatcherPriority.Input);
        Refresh();
    }
    private void LayoutEditor()
    {
        var t = DeviceTransform;
        EditorBounds = Geo.Editor(editingAnchor, 360 * t.M11, 235 * t.M22, Model.Width, Model.Height);
        Canvas.SetLeft(editorHost, EditorBounds.X / t.M11); Canvas.SetTop(editorHost, EditorBounds.Y / t.M22);
        editorHost.Width = EditorBounds.Width / t.M11; editorHost.Height = EditorBounds.Height / t.M22;
    }
    public bool CommitComment()
    {
        if (!Editing) return false;
        if (string.IsNullOrWhiteSpace(editor.Text)) { error.Text = "Comment cannot be empty. Type text or Esc to cancel."; editor.Focus(); return false; }
        if (editingId.HasValue) { Model.Edit(editingId.Value, editor.Text); Selected = editingId; }
        else Selected = Model.CreateMarker(editingAnchor, editor.Text).Id;
        CloseEditor(); Tool = Tool.Marker; Refresh(); return true;
    }
    private void CloseEditor() { editorHost.Visibility = Visibility.Collapsed; editor.Text = ""; editingId = null; surface.Focus(); }
    public void CancelEdit() { if (!Editing) return; CloseEditor(); Refresh(); }
    public void EditSelected()
    {
        if (Selected.HasValue && Model.Items.OfType<Marker>().FirstOrDefault(m => m.Id == Selected) is Marker marker) BeginEdit(marker.Anchor, marker.Id);
        else Message("Select a marker, then E or double-click it.");
    }
    private void Delete() { if (Editing) return; InterruptGesture(); if (Selected.HasValue) Model.Delete(Selected.Value); Selected = null; Refresh(); }
    public void History(bool redo)
    {
        if (Editing) { Message("TextBox undo applies while editing; finish/cancel before document undo."); return; }
        InterruptGesture(); if (redo) Model.Redo(); else Model.Undo();
        if (!Model.Items.Any(a => a.Id == Selected)) Selected = null; Refresh();
    }
    public void Switch(Tool tool) { if (Editing) { Message("Finish/cancel comment first."); return; } InterruptGesture(); Tool = tool; Selected = null; surface.Focus(); Refresh(); }
    private void Keys(object sender, KeyEventArgs e)
    {
        if (!Editable)
        {
            owner.ReturnToDraft();
            if (owner.Active is { } active && active != this) active.Keys(active, e);
            e.Handled = true; return;
        }
        bool ctrl = (Keyboard.Modifiers & ModifierKeys.Control) != 0, shift = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;
        if (Editing)
        {
            if (e.Key == Key.Escape) CancelEdit();
            else if (e.Key == Key.Enter)
            {
                var action = Draft.EnterAction(shift, ctrl);
                if (action == EditorEnter.Commit) CommitComment();
                else if (action == EditorEnter.Newline) { int caret = editor.SelectionStart; editor.SelectedText = Environment.NewLine; editor.CaretIndex = caret + Environment.NewLine.Length; }
                else error.Text = "Save or cancel comment first; Ctrl+Enter commits only outside the editor.";
            }
            else return; // M/A/R/Delete/Ctrl+Z/Y remain normal TextBox input/undo.
            e.Handled = true; return;
        }
        if (e.Key == Key.Escape)
        {
            if (dragging) InterruptGesture(); else if (Selected.HasValue) { Selected = null; Refresh(); } else owner.Cancel();
        }
        else if (ctrl && e.Key == Key.Enter) { if (!dragging) owner.Commit(this); }
        else if (ctrl && e.Key == Key.Z) History(shift);
        else if (ctrl && e.Key == Key.Y) History(true);
        else if (e.Key == Key.Delete) Delete();
        else if (e.Key == Key.E || e.Key == Key.Enter) EditSelected();
        else if (e.Key == Key.M) Switch(Tool.Marker);
        else if (e.Key == Key.A) Switch(Tool.Arrow);
        else if (e.Key == Key.R) Switch(Tool.Rectangle);
        else return;
        e.Handled = true;
    }
    private void Message(string message) => status.Text = message;
    public void Refresh()
    {
        status.Text = Editable ? $"Tool: {Tool} · next A{Model.NextMarker} · {Model.Items.Count} annotations · selected {(Selected.HasValue ? "yes" : "no")} · Ctrl+Alt+S returns focus" : "Frozen companion monitor. Annotation draft is on the selected monitor.";
        surface.InvalidateVisual();
    }
    public Glyph[] VisibleGlyphs()
    {
        if (!Editable) return [];
        var list = Painter.Project(Model.Items).ToList();
        if (gesture is BadgeGlyph movingGlyph) list.RemoveAll(g => g.Id == movingGlyph.Id);
        if (gesture != null) list.Add(gesture);
        if (Editing && editingId == null) list.Add(new BadgeGlyph(Guid.Empty, "A" + Model.NextMarker, editingAnchor));
        return list.ToArray();
    }
    private sealed class Surface(AnnotationOverlay window) : FrameworkElement
    {
        protected override void OnRender(DrawingContext dc)
        {
            dc.DrawImage(window.Frame.Image, new Rect(0, 0, ActualWidth, ActualHeight));
            var t = window.DeviceTransform; dc.PushTransform(new ScaleTransform(1 / t.M11, 1 / t.M22));
            dc.PushClip(new RectangleGeometry(new Rect(0, 0, window.Frame.Monitor.Width, window.Frame.Monitor.Height)));
            Painter.Draw(dc, window.VisibleGlyphs(), window.Frame.Monitor.Width, window.Frame.Monitor.Height);
            if (window.Editable && window.Selected.HasValue && window.Model.Items.FirstOrDefault(a => a.Id == window.Selected) is { } selected)
            {
                var pen = new Pen(Brushes.Yellow, 2) { DashStyle = DashStyles.Dash };
                if (selected is Marker m) { var b = Geo.Badge(m.Anchor, m.Label, window.Model.Width, window.Model.Height); dc.DrawRectangle(null, pen, new Rect(b.X - 3, b.Y - 3, b.Width + 6, b.Height + 6)); }
                else if (selected is Box b) dc.DrawRectangle(null, pen, new Rect(b.Rect.X - 3, b.Rect.Y - 3, b.Rect.Width + 6, b.Rect.Height + 6));
                else if (selected is Arrow a) { dc.DrawEllipse(Brushes.Yellow, null, new Point(a.Start.X, a.Start.Y), 4, 4); dc.DrawEllipse(Brushes.Yellow, null, new Point(a.End.X, a.End.Y), 4, 4); }
            }
            dc.Pop(); dc.Pop();
        }
    }
}
