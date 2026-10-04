using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

internal sealed class AnnotationOverlay : Window
{
    public Frame Frame { get; }
    public ScreenshotDraft Model { get; private set; } = null!;
    public bool Editable => owner.Active == this && Model != null;
    public Tool Tool { get; private set; } = Tool.Marker;
    public Guid? Selected { get; private set; }
    public bool Editing => editorHost.Visibility == Visibility.Visible;
    public TextBox CommentInput => editor;
    public bool ClosingByApp;
    public readonly TaskCompletionSource Rendered = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Coordinator owner;
    private readonly Surface surface;
    private readonly Canvas ui = new();
    private readonly TextBlock status = new() { Foreground = UtilityUi.Muted, FontSize = 12, Margin = new Thickness(10,5,10,5), TextWrapping = TextWrapping.Wrap };
    private readonly Border editorHost, bar;
    private readonly TextBlock editorTitle = new() { Foreground = UtilityUi.Accent, FontWeight = FontWeights.SemiBold, FontSize = 14 };
    private readonly TextBox editor = new() { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        FontSize = 14, Height = 96, Padding = new Thickness(8), Margin = new Thickness(0,8,0,8), BorderBrush = UtilityUi.Brush(203,213,225), BorderThickness = new Thickness(1) };
    private readonly TextBlock error = new() { Foreground = Brushes.DarkRed, TextWrapping = TextWrapping.Wrap, MaxHeight = 34, FontSize = 12 };
    private readonly List<Button> annotationButtons = [];
    private readonly Dictionary<Tool,Button> toolButtons = [];
    private readonly Button editButton, deleteButton, undoButton, redoButton;
    private readonly Button doneButton;
    internal UiTheme AppliedTheme { get; private set; }
    private HwndSource? source;
    private P editingAnchor, down;
    private Guid? editingId;
    private Marker? moving;
    private Glyph? gesture;
    private bool dragging, ready;
    public Matrix DeviceTransform => source?.CompositionTarget?.TransformToDevice ?? Matrix.Identity;
    public Bounds EditorBounds { get; private set; }
    public Selection RegionSelection { get; } = new();
    public PxRect Region { get; private set; }
    public BitmapSource Crop { get; private set; } = null!;
    public bool AnnotationMode => owner.Active != null;
    internal IReadOnlyList<Button> ToolbarButtons => annotationButtons;
    internal bool ToolHighlighted(Tool tool) => toolButtons[tool].FontWeight == FontWeights.SemiBold;
    public AnnotationOverlay(Coordinator owner, Frame frame)
    {
        this.owner = owner; Frame = frame; Icon = UtilityUi.WindowIcon();
        WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize; ShowInTaskbar = false; Topmost = true; ShowActivated = false;
        Width = frame.Monitor.Width * 96.0 / frame.Monitor.EffectiveDpi; Height = frame.Monitor.Height * 96.0 / frame.Monitor.EffectiveDpi;
        var root = new Grid { Background = Brushes.Black }; root.Children.Add(surface = new Surface(this) { Focusable = true }); root.Children.Add(ui); Content = root;
        RenderOptions.SetBitmapScalingMode(surface, BitmapScalingMode.NearestNeighbor);
        var tools = new StackPanel { Orientation = Orientation.Horizontal };
        var panel = new StackPanel(); panel.Children.Add(tools); panel.Children.Add(status);
        bar = new Border { Background = Brushes.White, BorderBrush = UtilityUi.Brush(203,213,225), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10), Padding = new Thickness(5), Child = panel,
            Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 16, ShadowDepth = 2, Opacity = .25 } };
        Button Add(string text, string hint, Action action) { var b = UtilityUi.Button(text,hint,action); tools.Children.Add(b); annotationButtons.Add(b); return b; }
        toolButtons.Add(Tool.Marker,Add("● Marker","Marker (M)",()=>Switch(Tool.Marker)));
        toolButtons.Add(Tool.Arrow,Add("↗ Arrow","Arrow (A)",()=>Switch(Tool.Arrow)));
        toolButtons.Add(Tool.Rectangle,Add("▭ Rectangle","Rectangle (R)",()=>Switch(Tool.Rectangle)));
        editButton = Add("Edit","Edit comment (E)",EditSelected);
        deleteButton = Add("Delete","Delete selected (Delete)",Delete);
        undoButton = Add("↶","Undo (Ctrl+Z)",()=>History(false)); redoButton = Add("↷","Redo (Ctrl+Y)",()=>History(true));
        var done = doneButton = Add("Done","Done (Ctrl+Enter)",()=> { if (!Editing && !dragging) owner.Commit(this); else Message("Save or cancel the comment first."); });
        done.Background = UtilityUi.Ink; done.Foreground = Brushes.White;
        ui.Children.Add(bar); Canvas.SetTop(bar,16); bar.SizeChanged += (_,_)=>LayoutToolbar();
        var editPanel = new StackPanel { Margin = new Thickness(12) };
        editPanel.Children.Add(editorTitle); editPanel.Children.Add(editor); editPanel.Children.Add(error);
        editPanel.Children.Add(new TextBlock { Text = "Enter save · Shift+Enter newline · Esc cancel", FontSize = 11, Foreground = UtilityUi.Muted, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,6,0,0) });
        editorHost = new Border { Background = Brushes.White, BorderBrush = UtilityUi.Brush(203,213,225), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10), Child = editPanel, Visibility = Visibility.Collapsed,
            Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 16, ShadowDepth = 2, Opacity = .25 } };
        ui.Children.Add(editorHost); Panel.SetZIndex(editorHost,2);
        SourceInitialized += (_,_)=> { source = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle)!; source.AddHook(Hook); Native.Place(source.Handle,Frame.Monitor); };
        ContentRendered += (_,_)=> { ready = true; LayoutToolbar(); Rendered.TrySetResult(); };
        Deactivated += (_,_)=>InterruptGesture();
        Closing += (_,e)=> { if (!ClosingByApp) { e.Cancel = true; Dispatcher.BeginInvoke(()=>owner.Cancel()); } };
        Closed += (_,_)=> { if (source != null) { source.RemoveHook(Hook); source.Dispose(); source = null; } editor.Text = ""; Content = null; };
        PreviewKeyDown += Keys; surface.MouseLeftButtonDown += Down;
        surface.MouseMove += (_,e)=>MovePointer(InputPoint(e)); surface.MouseLeftButtonUp += (_,e)=>EndPointer(InputPoint(e));
        surface.LostMouseCapture += (_,_)=>InterruptGesture(); ApplyTheme();
    }
    internal void ApplyTheme()
    {
        AppliedTheme=Appearance.Current;
        bar.Background=editorHost.Background=Appearance.Palette.Surface;
        bar.BorderBrush=editorHost.BorderBrush=editor.BorderBrush=Appearance.Palette.Border;
        editor.Background=Appearance.Palette.Editor;editor.Foreground=UtilityUi.Ink;editor.CaretBrush=UtilityUi.Ink;
        status.Foreground=UtilityUi.Muted;editorTitle.Foreground=Appearance.Palette.AccentText;error.Foreground=Appearance.Palette.Warning;
        foreach(var button in annotationButtons)
        {
            button.Foreground=UtilityUi.Ink;
            if(button.ToolTip is ToolTip tip) { tip.Background=Appearance.Palette.Surface;tip.Foreground=UtilityUi.Ink;tip.BorderBrush=Appearance.Palette.Border; }
        }
        doneButton.Background=Appearance.Current==UiTheme.Dark ? UtilityUi.Accent : UtilityUi.Brush(30,41,59);doneButton.Foreground=Brushes.White;doneButton.Content=UtilityUi.ShortcutCaption("Done","Ctrl+Enter",true);
        if(editorHost.Child is StackPanel panel) panel.Children.OfType<TextBlock>().Last().Foreground=UtilityUi.Muted;
        Refresh();
    }
    private IntPtr Hook(IntPtr hwnd,int message,IntPtr wp,IntPtr lp,ref bool handled)
    {
        if (message == 0x02E0 && ready && (wp.ToInt64() & 0xffff) != Frame.Monitor.EffectiveDpi)
            Dispatcher.BeginInvoke(()=> { if (owner.IsCurrent(this)) owner.Suspend("Display scaling changed. Cancel this capture and start again."); });
        return IntPtr.Zero;
    }
    public void ReturnFocus() { Activate(); if (Editing) editor.Focus(); else surface.Focus(); }
    public object Placement()
    {
        var r = Native.WindowRect(source!.Handle); var t = DeviceTransform;
        bool exact = r.Left == Frame.Monitor.Left && r.Top == Frame.Monitor.Top && r.Right-r.Left == Frame.Monitor.Width && r.Bottom-r.Top == Frame.Monitor.Height;
        bool canvas = Math.Abs(surface.ActualWidth*t.M11-Frame.Monitor.Width)<1 && Math.Abs(surface.ActualHeight*t.M22-Frame.Monitor.Height)<1;
        if (!exact || !canvas) throw new InvalidOperationException("Physical overlay placement mismatch.");
        return new { Frame.Monitor.Device, editable = Editable, left = r.Left, top = r.Top, width = Frame.Monitor.Width, height = Frame.Monitor.Height, scaleX = t.M11, scaleY = t.M22, hwndDpi = Native.GetDpiForWindow(source.Handle), exact, canvas };
    }
    private P InputPoint(MouseEventArgs e)
    {
        var p = e.GetPosition(surface); var t = DeviceTransform; var physical = Geo.FromDip(new(p.X,p.Y),t.M11,t.M22);
        return !AnnotationMode ? physical : new(physical.X-Region.X,physical.Y-Region.Y);
    }
    private void Down(object sender,MouseButtonEventArgs e) { e.Handled = true; BeginPointer(InputPoint(e),e.ClickCount); }
    public void BeginPointer(P p,int clickCount = 1)
    {
        if (!AnnotationMode) { Activate(); surface.Focus(); owner.SelectMonitor(this); RegionSelection.Begin(p); surface.CaptureMouse(); Refresh(); return; }
        if (!Editable) { owner.ReturnToDraft(); return; }
        if (p.X<0 || p.Y<0 || p.X>=Model.Width || p.Y>=Model.Height) return;
        Activate(); if (Editing) { editor.Focus(); Message("Enter saves the comment · Esc cancels this edit"); return; }
        p = Geo.Clamp(p,Model.Width,Model.Height); var hit = Geo.Hit(Model.Items,p,Model.Width,Model.Height); Selected = hit?.Id;
        if (hit is Marker marker)
        {
            if (clickCount>=2) { BeginEdit(marker.Anchor,marker.Id); return; }
            down=p; moving=marker; dragging=true; surface.CaptureMouse();
        }
        else if (hit != null) surface.Focus();
        else if (Tool == Tool.Marker) BeginEdit(p);
        else { down=p; dragging=true; surface.CaptureMouse(); }
        Refresh();
    }
    public void MovePointer(P p)
    {
        if (!AnnotationMode) { RegionSelection.Move(p,Frame.Monitor.Width,Frame.Monitor.Height); Refresh(); return; }
        if (!dragging) return; p=Geo.Clamp(p,Model.Width,Model.Height);
        if (moving != null) gesture = new BadgeGlyph(moving.Id,moving.Label,Geo.Clamp(new(moving.Anchor.X+p.X-down.X,moving.Anchor.Y+p.Y-down.Y),Model.Width,Model.Height));
        else if (Tool == Tool.Arrow) gesture = new ArrowGlyph(Guid.Empty,down,p);
        else if (Tool == Tool.Rectangle) gesture = new BoxGlyph(Guid.Empty,Geo.Normalize(down,p,Model.Width,Model.Height));
        Refresh();
    }
    public void EndPointer(P p)
    {
        if (!AnnotationMode)
        {
            if (!RegionSelection.Active) return;
            RegionSelection.Move(p,Frame.Monitor.Width,Frame.Monitor.Height);
            bool valid = RegionSelection.Preview is { Width: >= 2, Height: >= 2 };
            RegionSelection.End(p,Frame.Monitor.Width,Frame.Monitor.Height); surface.ReleaseMouseCapture();
            if (valid && RegionSelection.Completed is { } region) ChooseRegion(region); else Refresh();
            return;
        }
        if (!dragging) return; MovePointer(p);
        if (moving != null && gesture is BadgeGlyph m && down.Distance(p)>=3) Model.Move(moving.Id,m.Anchor);
        else if (moving == null && Tool == Tool.Arrow) Model.AddArrow(down,p);
        else if (moving == null && Tool == Tool.Rectangle) Model.AddBox(down,p);
        dragging=false; moving=null; gesture=null; surface.ReleaseMouseCapture(); surface.Focus(); Refresh();
    }
    public void InterruptGesture()
    {
        RegionSelection.CancelGesture(); dragging=false; moving=null; gesture=null;
        if (surface.IsMouseCaptured) surface.ReleaseMouseCapture(); if (ready) Refresh();
    }
    public void BeginEdit(P anchor,Guid? id = null)
    {
        if (!Editable || Editing) return; InterruptGesture(); editingId=id; editingAnchor=Geo.Clamp(anchor,Model.Width,Model.Height);
        var m=id.HasValue ? Model.Items.OfType<Marker>().FirstOrDefault(a=>a.Id==id) : null;
        editor.Text=m?.Comment ?? ""; error.Text=""; editorTitle.Text=m?.Label ?? Model.Letter+Model.NextMarker;
        editorHost.Visibility=Visibility.Visible; LayoutEditor(); ReturnFocus(); editor.CaretIndex=editor.Text.Length;
        Dispatcher.BeginInvoke(()=> { if (Editing && IsVisible && IsActive) editor.Focus(); },System.Windows.Threading.DispatcherPriority.Input); Refresh();
    }
    private void LayoutEditor()
    {
        var t=DeviceTransform;
        EditorBounds=Geo.Editor(new(editingAnchor.X+Region.X,editingAnchor.Y+Region.Y),320*t.M11,205*t.M22,Frame.Monitor.Width,Frame.Monitor.Height);
        Canvas.SetLeft(editorHost,EditorBounds.X/t.M11); Canvas.SetTop(editorHost,EditorBounds.Y/t.M22);
        editorHost.Width=EditorBounds.Width/t.M11; editorHost.Height=EditorBounds.Height/t.M22;
    }
    public bool CommitComment()
    {
        if (!Editing) return false;
        if (string.IsNullOrWhiteSpace(editor.Text)) { error.Text="Type a comment, or press Esc to cancel."; editor.Focus(); return false; }
        if (editingId.HasValue) { Model.Edit(editingId.Value,editor.Text); Selected=editingId; } else Selected=Model.CreateMarker(editingAnchor,editor.Text).Id;
        CloseEditor(); Tool=Tool.Marker; Refresh(); return true;
    }
    private void CloseEditor() { editorHost.Visibility=Visibility.Collapsed; editor.Text=""; editingId=null; surface.Focus(); }
    public void CancelEdit() { if (!Editing) return; CloseEditor(); Refresh(); }
    public void EditSelected()
    {
        if (Selected.HasValue && Model.Items.OfType<Marker>().FirstOrDefault(m=>m.Id==Selected) is Marker marker) BeginEdit(marker.Anchor,marker.Id);
        else Message("Select a marker, then press E or double-click it.");
    }
    private void Delete() { if (Editing) return; InterruptGesture(); if (Selected.HasValue) Model.Delete(Selected.Value); Selected=null; Refresh(); }
    public void History(bool redo)
    {
        if (Editing) { Message("Save or cancel the comment before undoing annotations."); return; }
        InterruptGesture(); if (redo) Model.Redo(); else Model.Undo(); if (!Model.Items.Any(a=>a.Id==Selected)) Selected=null; Refresh();
    }
    public void Switch(Tool tool) { if (Editing) { Message("Save or cancel the comment first."); return; } InterruptGesture(); Tool=tool; Selected=null; surface.Focus(); Refresh(); }
    private void Keys(object sender,KeyEventArgs e)
    {
        if (!AnnotationMode)
        {
            if (e.Key==Key.Escape) { if (RegionSelection.Active) InterruptGesture(); else owner.Cancel(); }
            else if (e.Key==Key.Space) { InterruptGesture(); owner.SelectMonitor(this); RegionSelection.Full(Frame.Monitor.Width,Frame.Monitor.Height); ChooseRegion(RegionSelection.Completed!.Value); }
            else return;
            e.Handled=true; return;
        }
        if (!Editable) { owner.ReturnToDraft(); if (owner.Active is { } active && active!=this) active.Keys(active,e); e.Handled=true; return; }
        bool ctrl=(Keyboard.Modifiers & ModifierKeys.Control)!=0, shift=(Keyboard.Modifiers & ModifierKeys.Shift)!=0;
        if (Editing)
        {
            if (e.Key==Key.Escape) CancelEdit();
            else if (e.Key==Key.Enter)
            {
                var action=ScreenshotDraft.EnterAction(shift,ctrl);
                if (action==EditorEnter.Commit) CommitComment();
                else if (action==EditorEnter.Newline) { int caret=editor.SelectionStart; editor.SelectedText=Environment.NewLine; editor.CaretIndex=caret+Environment.NewLine.Length; }
                else error.Text="Save or cancel the comment before finishing the screenshot.";
            }
            else return;
            e.Handled=true; return;
        }
        if (e.Key==Key.Escape) { if (dragging) InterruptGesture(); else owner.Cancel(); }
        else if (ctrl && e.Key==Key.Enter) { if (!dragging) owner.Commit(this); }
        else if (ctrl && e.Key==Key.Z) History(shift);
        else if (ctrl && e.Key==Key.Y) History(true);
        else if (e.Key==Key.Delete) Delete();
        else if (e.Key==Key.E || e.Key==Key.Enter) EditSelected();
        else if (e.Key==Key.M) Switch(Tool.Marker);
        else if (e.Key==Key.A) Switch(Tool.Arrow);
        else if (e.Key==Key.R) Switch(Tool.Rectangle);
        else return;
        e.Handled=true;
    }
    internal void ShowHint(string message) => Message(message);
    private void Message(string message) { status.Text=message; status.Visibility=Visibility.Visible; LayoutToolbar(); }
    private void LayoutToolbar()
    {
        var t=DeviceTransform; bar.Measure(new Size(double.PositiveInfinity,double.PositiveInfinity));
        Canvas.SetLeft(bar,Math.Max(8,(Frame.Monitor.Width/t.M11-bar.DesiredSize.Width)/2));
    }
    public void Refresh()
    {
        foreach (var button in annotationButtons) button.Visibility=Editable ? Visibility.Visible : Visibility.Collapsed;
        foreach (var (tool,button) in toolButtons)
        {
            bool active=Tool==tool; button.Background=active ? UtilityUi.Accent : Brushes.Transparent; button.Foreground=active ? Brushes.White : UtilityUi.Ink;
            button.FontWeight=active ? FontWeights.SemiBold : FontWeights.Normal;
            button.Content=UtilityUi.ShortcutCaption((active ? "✓ " : "")+(tool==Tool.Marker ? "● Marker" : tool==Tool.Arrow ? "↗ Arrow" : "▭ Rectangle"),tool==Tool.Marker ? "M" : tool==Tool.Arrow ? "A" : "R",active);
        }
        if (Editable)
        {
            editButton.Visibility=Model.Items.OfType<Marker>().Any(m=>m.Id==Selected) ? Visibility.Visible : Visibility.Collapsed;
            deleteButton.Visibility=Selected.HasValue ? Visibility.Visible : Visibility.Collapsed;
            undoButton.IsEnabled=Model.CanUndo && !Editing; redoButton.IsEnabled=Model.CanRedo && !Editing;
        }
        status.Text=!AnnotationMode ? "Drag a region · Space full monitor · Esc cancel" : Editable ? "" : "Continue on the selected monitor";
        status.Visibility=string.IsNullOrEmpty(status.Text) ? Visibility.Collapsed : Visibility.Visible;
        LayoutToolbar(); surface.Cursor=Cursors.Cross; surface.InvalidateVisual();
    }
    public void ChooseRegion(PxRect region)
    {
        if (AnnotationMode) return;
        if (region.X<0 || region.Y<0 || region.Width<2 || region.Height<2 || region.X+region.Width>Frame.Monitor.Width || region.Y+region.Height>Frame.Monitor.Height) throw new ArgumentException("Invalid crop.");
        Region=region;
        var crop=new CroppedBitmap(Frame.Image,new Int32Rect(region.X,region.Y,region.Width,region.Height)); int stride=checked(region.Width*4); var pixels=new byte[checked(stride*region.Height)]; crop.CopyPixels(pixels,stride,0);
        var detached=BitmapSource.Create(region.Width,region.Height,96,96,crop.Format,null,pixels,stride); detached.Freeze(); Crop=detached;
        Model=owner.Session.CreateDraft(region.Width,region.Height); owner.ActivateDraft(this); ReturnFocus(); Refresh();
    }
    public Glyph[] VisibleGlyphs()
    {
        if (!Editable) return []; var list=Painter.Project(Model.Items).ToList();
        if (gesture is BadgeGlyph movingGlyph) list.RemoveAll(g=>g.Id==movingGlyph.Id);
        if (gesture!=null) list.Add(gesture);
        if (Editing && editingId==null) list.Add(new BadgeGlyph(Guid.Empty,Model.Letter+Model.NextMarker,editingAnchor)); return list.ToArray();
    }
    private sealed class Surface(AnnotationOverlay window) : FrameworkElement
    {
        protected override void OnRender(DrawingContext dc)
        {
            dc.DrawImage(window.Frame.Image,new Rect(0,0,ActualWidth,ActualHeight)); var t=window.DeviceTransform;
            dc.PushTransform(new ScaleTransform(1/t.M11,1/t.M22)); dc.PushClip(new RectangleGeometry(new Rect(0,0,window.Frame.Monitor.Width,window.Frame.Monitor.Height)));
            PxRect? region=window.Editable ? window.Region : !window.AnnotationMode ? window.RegionSelection.Preview ?? window.RegionSelection.Completed : null;
            var full=new Rect(0,0,window.Frame.Monitor.Width,window.Frame.Monitor.Height); var dim=new SolidColorBrush(Color.FromArgb(105,0,0,0));
            if (region is { } r)
            {
                var hole=new Rect(r.X,r.Y,r.Width,r.Height);
                dc.DrawGeometry(dim,null,new CombinedGeometry(GeometryCombineMode.Exclude,new RectangleGeometry(full),new RectangleGeometry(hole)));
                dc.DrawRectangle(null,new Pen(UtilityUi.Accent,1),hole);
            }
            else dc.DrawRectangle(dim,null,full);
            dc.PushTransform(new TranslateTransform(window.Region.X,window.Region.Y));
            if (window.Editable) dc.PushClip(new RectangleGeometry(new Rect(0,0,window.Model.Width,window.Model.Height)));
            if (window.Editable) Painter.Draw(dc,window.VisibleGlyphs(),window.Model.Width,window.Model.Height);
            if (window.Editable && window.Selected.HasValue && window.Model.Items.FirstOrDefault(a=>a.Id==window.Selected) is { } selected)
            {
                var pen=new Pen(UtilityUi.Accent,1.5);
                if (selected is Marker m)
                {
                    var b=Geo.Badge(m.Anchor,m.Label,window.Model.Width,window.Model.Height); var rect=new Rect(b.X-3,b.Y-3,b.Width+6,b.Height+6);
                    dc.DrawRoundedRectangle(null,new Pen(Brushes.White,3.5),rect,9,9); dc.DrawRoundedRectangle(null,pen,rect,9,9);
                }
                else if (selected is Box b) dc.DrawRectangle(null,pen,new Rect(b.Rect.X-3,b.Rect.Y-3,b.Rect.Width+6,b.Rect.Height+6));
                else if (selected is Arrow a) { dc.DrawEllipse(UtilityUi.Accent,new Pen(Brushes.White,1.5),new Point(a.Start.X,a.Start.Y),4,4); dc.DrawEllipse(UtilityUi.Accent,new Pen(Brushes.White,1.5),new Point(a.End.X,a.End.Y),4,4); }
            }
            if (window.Editable) dc.Pop(); dc.Pop(); dc.Pop(); dc.Pop();
        }
    }
}
