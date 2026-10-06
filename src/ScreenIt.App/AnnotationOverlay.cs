using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Documents;

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
    private readonly SelectionGlow glow;
    private readonly TextBlock modifierHint=new() { FontSize=12 };
    private readonly InlineUIContainer modifierInline;
    private readonly DropShadowEffect hintGlow=new() { ShadowDepth=0,BlurRadius=10,Opacity=0 };
    internal Func<bool> AnnotationHeld { get; set; }
    internal bool ModifierActive { get; private set; }
    internal string ModifierHint => modifierHint.Text;
    private static readonly DependencyProperty GlowOpacityProperty=DependencyProperty.Register("GlowOpacity",typeof(double),typeof(AnnotationOverlay),new PropertyMetadata(0.0,GlowChanged));
    private static readonly DependencyProperty GlowStrengthProperty=DependencyProperty.Register("GlowStrength",typeof(double),typeof(AnnotationOverlay),new PropertyMetadata(1.0,GlowChanged));
    private static void GlowChanged(DependencyObject source,DependencyPropertyChangedEventArgs e)
    {
        var window=(AnnotationOverlay)source;window.glow?.InvalidateVisual();
        window.hintGlow.Opacity=.65*(double)window.GetValue(GlowOpacityProperty)*(double)window.GetValue(GlowStrengthProperty);
    }
    internal void UpdateModifierVisual()
    {
        bool active=!AnnotationMode && IsActive && AnnotationHeld();
        if(active==ModifierActive) return;ModifierActive=active;
        modifierHint.Foreground=active ? Appearance.Palette.AccentText : UtilityUi.Muted;
        bool animate=SystemParameters.ClientAreaAnimation;
        double current=(double)GetValue(GlowOpacityProperty);
        BeginAnimation(GlowOpacityProperty,null);SetValue(GlowOpacityProperty,active ? 1.0 : 0.0);
        BeginAnimation(GlowStrengthProperty,null);SetValue(GlowStrengthProperty,1.0);
        if(animate)
        {
            BeginAnimation(GlowOpacityProperty,new DoubleAnimation(current,active ? 1 : 0,TimeSpan.FromMilliseconds(140)) { FillBehavior=FillBehavior.Stop });
            if(active) BeginAnimation(GlowStrengthProperty,new DoubleAnimation(.55,1,TimeSpan.FromMilliseconds(750)) { AutoReverse=true,RepeatBehavior=RepeatBehavior.Forever,EasingFunction=new SineEase { EasingMode=EasingMode.EaseInOut } });
        }
    }
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
    internal double SourceScale { get; private set; }=1;
    internal PxRect? WindowHighlight => owner.WindowMode && owner.WindowHover is { } target ? new(target.Bounds.X-Frame.Monitor.Left,target.Bounds.Y-Frame.Monitor.Top,target.Bounds.Width,target.Bounds.Height) : null;
    public bool AnnotationMode => owner.Active != null;
    internal IReadOnlyList<Button> ToolbarButtons => annotationButtons;
    internal bool ToolHighlighted(Tool tool) => toolButtons[tool].FontWeight == FontWeights.SemiBold;
    public AnnotationOverlay(Coordinator owner, Frame frame)
    {
        this.owner = owner; Frame = frame; Icon = UtilityUi.WindowIcon();
        AnnotationHeld=()=>ModifierDown(owner.Preferences.AnnotationModifier);
        WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize; ShowInTaskbar = false; Topmost = true; ShowActivated = false;
        Width = frame.Monitor.Width * 96.0 / frame.Monitor.EffectiveDpi; Height = frame.Monitor.Height * 96.0 / frame.Monitor.EffectiveDpi;
        var root = new Grid { Background = Brushes.Black }; root.Children.Add(surface = new Surface(this) { Focusable = true });root.Children.Add(glow=new SelectionGlow(this) { IsHitTestVisible=false }); root.Children.Add(ui); Content = root;
        modifierHint.Effect=hintGlow;
        modifierInline=new InlineUIContainer(modifierHint) { BaselineAlignment=BaselineAlignment.Center };
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
        Deactivated += (_,_)=> { InterruptGesture();UpdateModifierVisual(); };
        Activated += (_,_)=>UpdateModifierVisual();
        Closing += (_,e)=> { if (!ClosingByApp) { e.Cancel = true; Dispatcher.BeginInvoke(()=>owner.Cancel()); } };
        Closed += (_,_)=> { BeginAnimation(GlowOpacityProperty,null);BeginAnimation(GlowStrengthProperty,null);if (source != null) { source.RemoveHook(Hook); source.Dispose(); source = null; } editor.Text = ""; Content = null; };
        PreviewKeyDown += Keys; surface.MouseLeftButtonDown += Down;
        PreviewKeyUp+=(_,_)=>UpdateModifierVisual();
        surface.MouseMove += (_,e)=>MovePointer(InputPoint(e)); surface.MouseLeftButtonUp += (_,e)=>EndPointer(InputPoint(e));
        surface.LostMouseCapture += (_,_)=>InterruptGesture(); ApplyTheme();
    }
    private static bool ModifierDown(AnnotationModifier modifier) => (GetAsyncKeyState(modifier switch { AnnotationModifier.Ctrl=>0x11,AnnotationModifier.Shift=>0x10,_=>0x12 })&0x8000)!=0;
    [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
    internal void ApplyTheme()
    {
        AppliedTheme=Appearance.Current;
        bar.Background=editorHost.Background=Appearance.Palette.Surface;
        bar.BorderBrush=editorHost.BorderBrush=editor.BorderBrush=Appearance.Palette.Border;
        editor.Background=Appearance.Palette.Editor;editor.Foreground=UtilityUi.Ink;editor.CaretBrush=UtilityUi.Ink;
        status.Foreground=UtilityUi.Muted;editorTitle.Foreground=Appearance.Palette.AccentText;error.Foreground=Appearance.Palette.Warning;
        hintGlow.Color=((SolidColorBrush)UtilityUi.Accent).Color;
        modifierHint.Foreground=ModifierActive ? Appearance.Palette.AccentText : UtilityUi.Muted;
        foreach(var button in annotationButtons)
        {
            button.Foreground=UtilityUi.Ink;
            if(button.ToolTip is ToolTip tip) { tip.Background=Appearance.Palette.Surface;tip.Foreground=UtilityUi.Ink;tip.BorderBrush=Appearance.Palette.Border; }
        }
        doneButton.Background=UtilityUi.Accent;doneButton.Foreground=Brushes.White;doneButton.Content=UtilityUi.ShortcutCaption("Done","Ctrl+Enter",true);
        if(editorHost.Child is StackPanel panel) panel.Children.OfType<TextBlock>().Last().Foreground=UtilityUi.Muted;
        Refresh();L.Tree(editorHost);
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
        return !AnnotationMode ? physical : new((physical.X-Region.X)/SourceScale,(physical.Y-Region.Y)/SourceScale);
    }
    private void Down(object sender,MouseButtonEventArgs e) { e.Handled = true; BeginPointer(InputPoint(e),e.ClickCount); }
    public void BeginPointer(P p,int clickCount = 1)
    {
        if(owner.WindowAcquiring) return;
        if(!AnnotationMode && owner.WindowMode) { Activate();surface.Focus();_=owner.CaptureWindow(this,AnnotationHeld(),new(p.X+Frame.Monitor.Left,p.Y+Frame.Monitor.Top));return; }
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
        if(!AnnotationMode && owner.WindowMode) { owner.HoverWindow(new(p.X+Frame.Monitor.Left,p.Y+Frame.Monitor.Top));return; }
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
            bool annotate=AnnotationHeld();
            RegionSelection.End(p,Frame.Monitor.Width,Frame.Monitor.Height); surface.ReleaseMouseCapture();
            if (valid && RegionSelection.Completed is { } region) CompleteSelection(region,annotate); else Refresh();
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
        EditorBounds=Geo.Editor(new(editingAnchor.X*SourceScale+Region.X,editingAnchor.Y*SourceScale+Region.Y),320*t.M11,205*t.M22,Frame.Monitor.Width,Frame.Monitor.Height);
        Canvas.SetLeft(editorHost,EditorBounds.X/t.M11); Canvas.SetTop(editorHost,EditorBounds.Y/t.M22);
        editorHost.Width=EditorBounds.Width/t.M11; editorHost.Height=EditorBounds.Height/t.M22;
    }
    public bool CommitComment()
    {
        if (!Editing) return false;
        if (string.IsNullOrWhiteSpace(editor.Text)) { error.Text=L.T("Type a comment, or press Esc to cancel."); editor.Focus(); return false; }
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
        UpdateModifierVisual();
        if (!AnnotationMode)
        {
            var key=e.Key==Key.System ? e.SystemKey : e.Key;
            if(owner.WindowAcquiring) { if(key==Key.Escape) owner.Cancel();e.Handled=true;return; }
            if (key==Key.Escape) { if (RegionSelection.Active) InterruptGesture(); else owner.Cancel(); }
            else if(key==Key.W && !e.IsRepeat) owner.ToggleWindowMode();
            else if (key==Key.Space) { bool annotate=AnnotationHeld();InterruptGesture(); owner.SelectMonitor(this); RegionSelection.Full(Frame.Monitor.Width,Frame.Monitor.Height); CompleteSelection(RegionSelection.Completed!.Value,annotate); }
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
                else error.Text=L.T("Save or cancel the comment before finishing the screenshot.");
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
    private void Message(string message) { status.Text=L.T(message); status.Visibility=Visibility.Visible; LayoutToolbar(); }
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
        status.Inlines.Clear();
        if(!AnnotationMode)
        {
            status.Inlines.Add(new Run(L.T(owner.WindowMode ? "Select a window · W — region" : "Drag a region · W — window")+" · "));
            modifierHint.Text=string.Format(L.T("{0} — annotations"),owner.Preferences.AnnotationModifier);
            status.Inlines.Add(modifierInline);
            status.Inlines.Add(new Run(" · "+L.T("Space full monitor · Esc cancel")));
        }
        else status.Text=Editable ? "" : L.T("Continue on the selected monitor");
        status.Visibility=!AnnotationMode || !Editable ? Visibility.Visible : Visibility.Collapsed;
        UpdateModifierVisual();glow.InvalidateVisual();
        foreach(var button in annotationButtons) L.Tree(button);LayoutToolbar(); surface.Cursor=Cursors.Cross; surface.InvalidateVisual();
    }
    private void CompleteSelection(PxRect region,bool annotate)
    {
        ChooseRegion(region);
        if(!annotate) owner.Commit(this);
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
    internal void ChooseBitmap(BitmapSource image)
    {
        if(AnnotationMode || !image.IsFrozen) throw new InvalidOperationException("Window source must be detached and frozen.");
        Crop=image;SourceScale=Math.Min(1,Math.Min((Frame.Monitor.Width-32d)/image.PixelWidth,(Frame.Monitor.Height-112d)/image.PixelHeight));
        int width=(int)Math.Ceiling(image.PixelWidth*SourceScale),height=(int)Math.Ceiling(image.PixelHeight*SourceScale);
        Region=new((Frame.Monitor.Width-width)/2,80+(Frame.Monitor.Height-112-height)/2,width,height);
        Model=owner.Session.CreateDraft(image.PixelWidth,image.PixelHeight);owner.ActivateDraft(this);ReturnFocus();Refresh();
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
            PxRect? region=window.Editable ? window.Region : !window.AnnotationMode ? window.WindowHighlight ?? window.RegionSelection.Preview ?? window.RegionSelection.Completed : null;
            var full=new Rect(0,0,window.Frame.Monitor.Width,window.Frame.Monitor.Height); var dim=new SolidColorBrush(Color.FromArgb(105,0,0,0));
            if (region is { } r)
            {
                var hole=new Rect(r.X,r.Y,r.Width,r.Height);
                dc.DrawGeometry(dim,null,new CombinedGeometry(GeometryCombineMode.Exclude,new RectangleGeometry(full),new RectangleGeometry(hole)));
                dc.DrawRectangle(null,new Pen(UtilityUi.Accent,1),hole);
                if(!window.AnnotationMode && window.owner.WindowMode) dc.DrawRectangle(new SolidColorBrush(((SolidColorBrush)UtilityUi.Accent).Color) { Opacity=.09 },null,hole);
            }
            else dc.DrawRectangle(dim,null,full);
            dc.PushTransform(new TranslateTransform(window.Region.X,window.Region.Y));
            dc.PushTransform(new ScaleTransform(window.SourceScale,window.SourceScale));
            if (window.Editable) dc.PushClip(new RectangleGeometry(new Rect(0,0,window.Model.Width,window.Model.Height)));
            if(window.Editable) dc.DrawImage(window.Crop,new Rect(0,0,window.Model.Width,window.Model.Height));
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
            if (window.Editable) dc.Pop(); dc.Pop(); dc.Pop(); dc.Pop(); dc.Pop();
        }
    }
    // UI-only soft rings, clipped to the outside: no tint or blur touches selected pixels.
    private sealed class SelectionGlow(AnnotationOverlay window) : FrameworkElement
    {
        protected override void OnRender(DrawingContext dc)
        {
            if(window.AnnotationMode || (window.WindowHighlight ?? window.RegionSelection.Preview) is not { } region) return;
            double opacity=(double)window.GetValue(GlowOpacityProperty)*(double)window.GetValue(GlowStrengthProperty);
            if(opacity<=0) return;
            var t=window.DeviceTransform;var hole=new Rect(region.X/t.M11,region.Y/t.M22,region.Width/t.M11,region.Height/t.M22);
            dc.PushClip(new CombinedGeometry(GeometryCombineMode.Exclude,new RectangleGeometry(new Rect(0,0,ActualWidth,ActualHeight)),new RectangleGeometry(hole)));
            double intensity=Appearance.Current==UiTheme.Light ? .31 : .28;
            for(int i=14;i>=1;i--)
            {
                var brush=new SolidColorBrush(((SolidColorBrush)UtilityUi.Accent).Color) { Opacity=opacity*intensity*Math.Exp(-i*i/48.0) };
                var ring=hole;ring.Inflate(i*.65,i*.65);dc.DrawRectangle(null,new Pen(brush,1.4),ring);
            }
            dc.Pop();
        }
    }
}
