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
    public bool Editing => commentEditing;
    public TextBox CommentInput => editor;
    public bool ClosingByApp;
    public readonly TaskCompletionSource Rendered = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Coordinator owner;
    private readonly Surface surface;
    private MarkerSurface? markerSurface;
    private Guid? hoveredMarker;
    private readonly TranslateTransform editorOffset=new();
    private readonly SelectionGlow glow;
    private static SolidColorBrush AnnotationSelectionAccent => UiDesign.IsEmber ? ConstructDesign.Acid : UtilityUi.Brush(160,67,201); // Selection UI only.
    private readonly TextBlock modifierHint=new() { FontSize=12 };
    private CaptureSelectionBar? captureBar;
    internal CaptureSelectionBar? CaptureChrome => captureBar;
    private InlineUIContainer? modifierInline;
    private DropShadowEffect? hintGlow;
    internal Func<bool> AnnotationHeld { get; set; }
    internal bool ModifierActive { get; private set; }
    internal string ModifierHint => modifierHint.Text;
    private static readonly DependencyProperty GlowOpacityProperty=DependencyProperty.Register("GlowOpacity",typeof(double),typeof(AnnotationOverlay),new PropertyMetadata(0.0,GlowChanged));
    private static readonly DependencyProperty GlowStrengthProperty=DependencyProperty.Register("GlowStrength",typeof(double),typeof(AnnotationOverlay),new PropertyMetadata(1.0,GlowChanged));
    private static void GlowChanged(DependencyObject source,DependencyPropertyChangedEventArgs e)
    {
        var window=(AnnotationOverlay)source;window.glow?.InvalidateVisual();
        if(window.hintGlow!=null) window.hintGlow.Opacity=.65*(double)window.GetValue(GlowOpacityProperty)*(double)window.GetValue(GlowStrengthProperty);
    }
    internal void UpdateModifierVisual()
    {
        bool active=!AnnotationMode && IsActive && AnnotationHeld();
        if(active==ModifierActive) return;ModifierActive=active;
        surface.InvalidateVisual();
        captureBar?.Refresh(owner.WindowMode,owner.Preferences.AnnotationModifier,active);
        if(hintGlow!=null) modifierHint.Foreground=active ? Appearance.Palette.AccentText : UtilityUi.Muted;
        bool animate=SystemParameters.ClientAreaAnimation;
        double current=(double)GetValue(GlowOpacityProperty);
        BeginAnimation(GlowOpacityProperty,null);SetValue(GlowOpacityProperty,active ? 1.0 : 0.0);
        BeginAnimation(GlowStrengthProperty,null);SetValue(GlowStrengthProperty,1.0);
        if(animate)
        {
            BeginAnimation(GlowOpacityProperty,new DoubleAnimation(current,active ? 1 : 0,TimeSpan.FromMilliseconds(140)) { FillBehavior=FillBehavior.Stop });

        }
    }
    private readonly Canvas ui = new();
    private readonly TextBlock status = new() { Foreground = UtilityUi.Muted, FontSize = 12, Margin = new Thickness(10,5,10,5), TextWrapping = TextWrapping.Wrap };
    private readonly Border editorHost, bar;
    private Border? contextDock, doneDock;
    private Canvas? editorLayer;
    private CommentEditorLink? editorLink;
    private bool commentEditing;
    private AnimationClock? editorFade;
    private EventHandler? editorFadeCompleted;
    private readonly TextBlock editorTitle = new() { Foreground = UtilityUi.Accent, FontWeight = FontWeights.SemiBold, FontSize = 14 };
    private readonly TextBox editor = new() { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        FontSize = 14, Height = 96, Padding = new Thickness(8), Margin = new Thickness(0,8,0,8), BorderBrush = UtilityUi.Brush(203,213,225), BorderThickness = new Thickness(1) };
    private readonly TextBlock error = new() { Foreground = Brushes.DarkRed, TextWrapping = TextWrapping.Wrap, MaxHeight = 34, FontSize = 12 };
    private readonly List<Button> annotationButtons = [];
    private readonly Dictionary<Tool,Button> toolButtons = [];
    private StackPanel? toolbarBrand;
    private WrapPanel? classicTools;
    private readonly Button editButton, deleteButton, undoButton, redoButton;
    private readonly Button doneButton;
    internal UiTheme AppliedTheme { get; private set; }
    private HwndSource? source;
    private P editingAnchor, down;
    private Guid? editingId;
    private Marker? moving;
    private Glyph? gesture;
    private readonly List<P> pencilPoints = [];
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
        FontFamily=ConstructDesign.Body;this.owner = owner; Frame = frame; Icon = UtilityUi.WindowIcon();
        AnnotationHeld=()=>ModifierDown(owner.Preferences.AnnotationModifier);
        WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize; ShowInTaskbar = false; Topmost = true; ShowActivated = false;
        Width = frame.Monitor.Width * 96.0 / frame.Monitor.EffectiveDpi; Height = frame.Monitor.Height * 96.0 / frame.Monitor.EffectiveDpi;
        var root = new Grid { Background = Brushes.Black }; root.Children.Add(surface = new Surface(this) { Focusable = true });
        if(UiDesign.IsEmber) root.Children.Add(markerSurface=new MarkerSurface(this) { IsHitTestVisible=false });
        root.Children.Add(glow=new SelectionGlow(this) { IsHitTestVisible=false }); root.Children.Add(ui); Content = root;
        RenderOptions.SetBitmapScalingMode(surface, BitmapScalingMode.NearestNeighbor);
        var tools = new WrapPanel { Orientation = Orientation.Horizontal };
        var panel = new StackPanel(); panel.Children.Add(tools);
        Button Add(string text, string hint, Action action) { var b = UtilityUi.Button(text,hint,action); tools.Children.Add(b); annotationButtons.Add(b); return b; }
        if(UiDesign.IsEmber)
        {
            captureBar=new CaptureSelectionBar(modifierHint,()=>
            {
                if(AnnotationMode || owner.WindowAcquiring) return;
                owner.ToggleWindowMode();Refresh();
                if(IsVisible) ReturnFocus();
            });
            toolbarBrand=new StackPanel { Orientation=Orientation.Horizontal,Margin=new Thickness(8,0,16,0),VerticalAlignment=VerticalAlignment.Center };
            toolbarBrand.Children.Add(new Image { Source=UtilityUi.WindowIcon(),Width=28,Height=28,Margin=new Thickness(0,0,8,0) });
            toolbarBrand.Children.Add(new TextBlock { Text="ScreenIt",FontWeight=FontWeights.Bold,VerticalAlignment=VerticalAlignment.Center });
            tools.Children.Add(toolbarBrand);
            bar = new Border { Background = Brushes.White, BorderBrush = UtilityUi.Brush(203,213,225), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(4), Padding = new Thickness(5), Child = panel,
                Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 6, ShadowDepth = 1, Opacity = .12 } };
            toolButtons.Add(Tool.Marker,Add("Marker","Marker (M)",()=>Switch(Tool.Marker)));
            toolButtons.Add(Tool.Pencil,Add("Pencil","Pencil (P)",()=>Switch(Tool.Pencil)));
            toolButtons.Add(Tool.Arrow,Add("Arrow","Arrow (A)",()=>Switch(Tool.Arrow)));
            toolButtons.Add(Tool.Rectangle,Add("Rectangle","Rectangle (R)",()=>Switch(Tool.Rectangle)));
            editButton = Add("Edit","Edit comment (E)",EditSelected);
            deleteButton = Add("Delete","Delete selected (Delete)",Delete);
            undoButton = Add("Undo","Undo (Ctrl+Z)",()=>History(false)); redoButton = Add("Redo","Redo (Ctrl+Y)",()=>History(true));
            Add("×","Cancel screenshot (Esc)",()=> { if(Editing) CancelEdit();else if(dragging) InterruptGesture();else owner.Cancel(); });
            doneButton=UtilityUi.Button("Done","Done (Ctrl+Enter)",()=> { if (!Editing && !dragging) owner.Commit(this); else { Message("Save or cancel the comment first.");ReturnFocus(); } });
            annotationButtons.Add(doneButton);doneButton.MinHeight=38;
            contextDock=new Border { Child=status,Padding=new Thickness(4),CornerRadius=new CornerRadius(4),BorderThickness=new Thickness(1),IsHitTestVisible=false };
            doneDock=new Border { Child=doneButton,Padding=new Thickness(4),CornerRadius=new CornerRadius(4),BorderThickness=new Thickness(1) };
            ui.Children.Add(bar);ui.Children.Add(captureBar);ui.Children.Add(contextDock);ui.Children.Add(doneDock);
            Canvas.SetTop(captureBar,16);
            Canvas.SetTop(bar,16);bar.SizeChanged += (_,_)=>LayoutToolbar();
            editorHost=new CommentEditorCard(editorTitle,editor,error);
            editorLayer=new Canvas { Visibility=Visibility.Collapsed };editorLink=new CommentEditorLink { IsHitTestVisible=false };
            editorLayer.Children.Add(editorLink);editorLayer.Children.Add(editorHost);
            ui.Children.Add(editorLayer);Panel.SetZIndex(editorLayer,2);
        }
        else
        {
            // 0.2.3 overlay chrome: one compact toolbar with the blue accent and the comment card below the marker.
            classicTools=tools;
            bar = new Border { Background = Brushes.White, BorderBrush = UtilityUi.Brush(203,213,225), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10), Padding = new Thickness(5), Child = panel,
                Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 16, ShadowDepth = 2, Opacity = .25 } };
            toolButtons.Add(Tool.Marker,Add("● Marker","Marker (M)",()=>Switch(Tool.Marker)));
            toolButtons.Add(Tool.Arrow,Add("↗ Arrow","Arrow (A)",()=>Switch(Tool.Arrow)));
            toolButtons.Add(Tool.Rectangle,Add("▭ Rectangle","Rectangle (R)",()=>Switch(Tool.Rectangle)));
            toolButtons.Add(Tool.Pencil,Add("✎ Pencil","Pencil (P)",()=>Switch(Tool.Pencil)));
            editButton = Add("Edit","Edit comment (E)",EditSelected);
            deleteButton = Add("Delete","Delete selected (Delete)",Delete);
            undoButton = Add("↶","Undo (Ctrl+Z)",()=>History(false)); redoButton = Add("↷","Redo (Ctrl+Y)",()=>History(true));
            doneButton = Add("Done","Done (Ctrl+Enter)",()=> { if (!Editing && !dragging) owner.Commit(this); else { Message("Save or cancel the comment first.");ReturnFocus(); } });
            doneButton.Background = UtilityUi.Ink; doneButton.Foreground = Brushes.White;
            ui.Children.Add(bar); Canvas.SetTop(bar,16); bar.SizeChanged += (_,_)=>LayoutToolbar();
            var editPanel = new StackPanel { Margin = new Thickness(12) };
            editPanel.Children.Add(editorTitle); editPanel.Children.Add(editor); editPanel.Children.Add(error);
            editPanel.Children.Add(new TextBlock { Text = "Enter save · Shift+Enter newline · Esc cancel", FontSize = 11, Foreground = UtilityUi.Muted, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,6,0,0) });
            editorHost = new Border { Background = Brushes.White, BorderBrush = UtilityUi.Brush(203,213,225), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10), Child = editPanel, Visibility = Visibility.Collapsed,
                Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 16, ShadowDepth = 2, Opacity = .25 } };
            ui.Children.Add(editorHost); Panel.SetZIndex(editorHost,2);
            hintGlow=new DropShadowEffect { ShadowDepth=0,BlurRadius=10,Opacity=0 };
            modifierHint.Effect=hintGlow;
            modifierInline=new InlineUIContainer(modifierHint) { BaselineAlignment=BaselineAlignment.Center };
        }
        SourceInitialized += (_,_)=> { source = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle)!; source.AddHook(Hook); Native.Place(source.Handle,Frame.Monitor); };
        ContentRendered += (_,_)=> { ready = true; LayoutToolbar(); Rendered.TrySetResult(); };
        Deactivated += (_,_)=> { InterruptGesture();UpdateModifierVisual(); };
        Activated += (_,_)=>UpdateModifierVisual();
        Closing += (_,e)=> { if (!ClosingByApp) { e.Cancel = true; Dispatcher.BeginInvoke(()=>owner.Cancel()); } };
        Closed += (_,_)=> { StopEditorMotion();markerSurface?.Stop();commentEditing=false;BeginAnimation(GlowOpacityProperty,null);BeginAnimation(GlowStrengthProperty,null);if (source != null) { source.RemoveHook(Hook); source.Dispose(); source = null; } editor.Text = ""; Content = null; };
        PreviewKeyDown += Keys; surface.MouseLeftButtonDown += Down;
        PreviewKeyUp+=(_,_)=>UpdateModifierVisual();
        surface.MouseMove += (_,e)=>MovePointer(InputPoint(e)); surface.MouseLeftButtonUp += (_,e)=>EndPointer(InputPoint(e));
        surface.LostMouseCapture += (_,_)=>InterruptGesture();surface.MouseLeave+=(_,_)=> { hoveredMarker=null;Refresh(); };ApplyTheme();
    }
    private static bool ModifierDown(AnnotationModifier modifier) => (GetAsyncKeyState(modifier switch { AnnotationModifier.Ctrl=>0x11,AnnotationModifier.Shift=>0x10,_=>0x12 })&0x8000)!=0;
    [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
    internal void ApplyTheme()
    {
        AppliedTheme=Appearance.Current;
        if(UiDesign.IsEmber)
        {
            toolbarBrand!.Children.OfType<TextBlock>().Single().Foreground=UtilityUi.Ink;
            bar.Background=editorHost.Background=contextDock!.Background=doneDock!.Background=Appearance.Palette.Surface;
            contextDock.BorderBrush=doneDock.BorderBrush=Appearance.Palette.Border;
            bar.BorderBrush=editorHost.BorderBrush=editor.BorderBrush=Appearance.Palette.Border;
            editor.Background=Appearance.Palette.Editor;editor.Foreground=UtilityUi.Ink;editor.CaretBrush=UtilityUi.Ink;
            status.Foreground=UtilityUi.Muted;editorTitle.Foreground=Appearance.Palette.AccentText;error.Foreground=Appearance.Palette.Warning;
            foreach(var button in annotationButtons)
            {
                button.Resources["UiFocus"]=ConstructDesign.Focus;button.Foreground=UtilityUi.Ink;
                if(button.ToolTip is ToolTip tip) { tip.Background=Appearance.Palette.Surface;tip.Foreground=UtilityUi.Ink;tip.BorderBrush=Appearance.Palette.Border; }
            }
            undoButton.Content=ConstructDesign.Icon("Undo",UtilityUi.Ink);redoButton.Content=ConstructDesign.Icon("Redo",UtilityUi.Ink);
            doneButton.Background=UtilityUi.Accent;doneButton.Foreground=ConstructDesign.OnAccent;doneButton.Content=UtilityUi.ShortcutCaption("Done","Ctrl+Enter",true);
            ((CommentEditorCard)editorHost).ApplyTheme();editorLink!.InvalidateVisual();
        }
        else
        {
            bar.Background=editorHost.Background=Appearance.Palette.Surface;
            bar.BorderBrush=editorHost.BorderBrush=editor.BorderBrush=Appearance.Palette.Border;
            editor.Background=Appearance.Palette.Editor;editor.Foreground=UtilityUi.Ink;editor.CaretBrush=UtilityUi.Ink;
            status.Foreground=UtilityUi.Muted;editorTitle.Foreground=Appearance.Palette.AccentText;error.Foreground=Appearance.Palette.Warning;
            if(hintGlow!=null) hintGlow.Color=((SolidColorBrush)UtilityUi.Accent).Color;
            modifierHint.Foreground=ModifierActive ? Appearance.Palette.AccentText : UtilityUi.Muted;
            foreach(var button in annotationButtons)
            {
                button.Resources["UiFocus"]=UtilityUi.Accent;button.Foreground=UtilityUi.Ink;
                if(button.ToolTip is ToolTip tip) { tip.Background=Appearance.Palette.Surface;tip.Foreground=UtilityUi.Ink;tip.BorderBrush=Appearance.Palette.Border; }
            }
            doneButton.Background=UtilityUi.Accent;doneButton.Foreground=Brushes.White;doneButton.Content=UtilityUi.ShortcutCaption("Done","Ctrl+Enter",true);
            if(editorHost.Child is StackPanel editPanel) editPanel.Children.OfType<TextBlock>().Last().Foreground=UtilityUi.Muted;
        }
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
        var uiMarker=UiDesign.IsEmber ? HitUiMarker(p) : null;
        if ((p.X<0 || p.Y<0 || p.X>=Model.Width || p.Y>=Model.Height) && uiMarker==null) return;
        Activate(); if (Editing) { editor.Focus(); Message("Enter saves the comment · Esc cancels this edit"); return; }
        var rawPointer=p;
        p = Geo.Clamp(p,Model.Width,Model.Height); var hit = (Annotation?)uiMarker ?? Geo.Hit(UiDesign.IsEmber ? Model.Items.Where(a=>a is not Marker) : Model.Items,p,Model.Width,Model.Height); Selected = hit?.Id;
        if (hit is Marker marker)
        {
            if (clickCount>=2) { BeginEdit(marker.Anchor,marker.Id); return; }
            down=rawPointer; moving=marker; dragging=true; surface.CaptureMouse();
        }
        else if (hit != null) surface.Focus();
        else if (Tool == Tool.Marker) BeginEdit(p);
        else { down=p; dragging=true; if (Tool == Tool.Pencil) { pencilPoints.Clear(); pencilPoints.Add(p); } surface.CaptureMouse(); }
        Refresh();
    }
    public void MovePointer(P p)
    {
        if(!AnnotationMode && owner.WindowMode) { owner.HoverWindow(new(p.X+Frame.Monitor.Left,p.Y+Frame.Monitor.Top));return; }
        if (!AnnotationMode) { RegionSelection.Move(p,Frame.Monitor.Width,Frame.Monitor.Height); Refresh(); return; }
        if (!dragging) { if(UiDesign.IsEmber && Editable && !Editing) { var next=HitUiMarker(p)?.Id;if(next!=hoveredMarker) { hoveredMarker=next;Refresh(); } } return; } var rawPointer=p;p=Geo.Clamp(p,Model.Width,Model.Height);
        if (moving != null) gesture = new BadgeGlyph(moving.Id,moving.Label,Geo.Clamp(new(moving.Anchor.X+rawPointer.X-down.X,moving.Anchor.Y+rawPointer.Y-down.Y),Model.Width,Model.Height));
        else if (Tool == Tool.Arrow) gesture = new ArrowGlyph(Guid.Empty,down,p);
        else if (Tool == Tool.Rectangle) gesture = new BoxGlyph(Guid.Empty,Geo.Normalize(down,p,Model.Width,Model.Height));
        else if (Tool == Tool.Pencil)
        {
            if (pencilPoints[^1].Distance(p) >= 1) pencilPoints.Add(p);
            gesture = new PencilGlyph(Guid.Empty,pencilPoints.AsReadOnly());
        }
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
        Marker? clickedMarker=moving!=null && down.Distance(p)<3 ? moving : null;
        if (moving != null && gesture is BadgeGlyph m && down.Distance(p)>=3) Model.Move(moving.Id,m.Anchor);
        else if (moving == null && Tool == Tool.Arrow) Model.AddArrow(down,p);
        else if (moving == null && Tool == Tool.Rectangle) Model.AddBox(down,p);
        else if (moving == null && Tool == Tool.Pencil)
        {
            var end = Geo.Clamp(p,Model.Width,Model.Height);
            if (pencilPoints[^1] != end) pencilPoints.Add(end);
            Model.AddPencil(pencilPoints);
        }
        dragging=false; moving=null; gesture=null; pencilPoints.Clear(); surface.ReleaseMouseCapture(); surface.Focus();
        if(clickedMarker!=null) BeginEdit(clickedMarker.Anchor,clickedMarker.Id);
        Refresh();
    }
    public void InterruptGesture()
    {
        RegionSelection.CancelGesture(); dragging=false; moving=null; gesture=null; pencilPoints.Clear();
        if (surface.IsMouseCaptured) surface.ReleaseMouseCapture(); if (ready) Refresh();
    }
    public void BeginEdit(P anchor,Guid? id = null)
    {
        if (!Editable || Editing) return; InterruptGesture(); editingId=id; editingAnchor=Geo.Clamp(anchor,Model.Width,Model.Height);
        var m=id.HasValue ? Model.Items.OfType<Marker>().FirstOrDefault(a=>a.Id==id) : null;
        editor.Text=m?.Comment ?? ""; error.Text="";error.Visibility=Visibility.Collapsed; editorTitle.Text=m?.Label ?? Model.Letter+Model.NextMarker;
        commentEditing=true;if(UiDesign.IsEmber) ShowEditorVisuals(); else editorHost.Visibility=Visibility.Visible;LayoutEditor();ReturnFocus();editor.CaretIndex=editor.Text.Length;
        Dispatcher.BeginInvoke(()=> { if (Editing && IsVisible && IsActive) editor.Focus(); },System.Windows.Threading.DispatcherPriority.Input); Refresh();
    }
    internal Point MarkerUiAnchor(P anchor)
    {
        var t=DeviceTransform;return new Point((Region.X+anchor.X*SourceScale)/t.M11,(Region.Y+anchor.Y*SourceScale)/t.M22);
    }
    internal Rect MarkerUiBounds(P anchor,string label)
    {
        var t=DeviceTransform;var original=Geo.Badge(anchor,label,Model.Width,Model.Height);
        var projected=new Rect((Region.X+original.X*SourceScale)/t.M11,(Region.Y+original.Y*SourceScale)/t.M22,original.Width*SourceScale/t.M11,original.Height*SourceScale/t.M22);
        var sourceRect=new Rect(Region.X/t.M11,Region.Y/t.M22,Model.Width*SourceScale/t.M11,Model.Height*SourceScale/t.M22);
        return MarkerBadgeUi.Bounds(projected,label,sourceRect,new Size(Frame.Monitor.Width/t.M11,Frame.Monitor.Height/t.M22));
    }
    private Marker? HitUiMarker(P point)
    {
        var dip=MarkerUiAnchor(point);
        return Model.Items.OfType<Marker>().Reverse().FirstOrDefault(m=>MarkerUiBounds(m.Anchor,m.Label).Contains(dip) || (MarkerUiAnchor(m.Anchor)-dip).Length<=6);
    }
    private void LayoutEditor()
    {
        var t=DeviceTransform;
        if(!UiDesign.IsEmber)
        {
            EditorBounds=Geo.Editor(new(editingAnchor.X*SourceScale+Region.X,editingAnchor.Y*SourceScale+Region.Y),320*t.M11,205*t.M22,Frame.Monitor.Width,Frame.Monitor.Height);
            Canvas.SetLeft(editorHost,EditorBounds.X/t.M11); Canvas.SetTop(editorHost,EditorBounds.Y/t.M22);
            editorHost.Width=EditorBounds.Width/t.M11; editorHost.Height=EditorBounds.Height/t.M22;
            return;
        }
        var anchor=MarkerUiAnchor(editingAnchor);
        var badge=MarkerUiBounds(editingAnchor,editorTitle.Text);
        var card=CommentEditorPlacement.Place(badge,new Size(Frame.Monitor.Width/t.M11,Frame.Monitor.Height/t.M22));
        EditorBounds=new Bounds(card.X*t.M11,card.Y*t.M22,card.Width*t.M11,card.Height*t.M22);
        Canvas.SetLeft(editorHost,card.X);Canvas.SetTop(editorHost,card.Y);editorHost.Width=card.Width;editorHost.Height=card.Height;
        editorLayer!.Width=editorLink!.Width=Frame.Monitor.Width/t.M11;editorLayer.Height=editorLink.Height=Frame.Monitor.Height/t.M22;
        Point from,to;
        if(card.Left>=badge.Right) { from=new Point(badge.Right,badge.Top+badge.Height/2);to=new Point(card.Left,Math.Clamp(from.Y,card.Top+12,card.Bottom-12)); }
        else if(card.Right<=badge.Left) { from=new Point(badge.Left,badge.Top+badge.Height/2);to=new Point(card.Right,Math.Clamp(from.Y,card.Top+12,card.Bottom-12)); }
        else { bool below=card.Top>=badge.Bottom;from=new Point(badge.Left+badge.Width/2,below ? badge.Bottom : badge.Top);to=new Point(Math.Clamp(from.X,card.Left+4,card.Right-4),below ? card.Top+4 : card.Bottom-4); }
        editorLink.Place(from,to,badge);editorLink.Reveal();
    }
    public bool CommitComment()
    {
        if (!Editing) return false;
        if (string.IsNullOrWhiteSpace(editor.Text)) { error.Text=L.T("Type a comment, or press Esc to cancel.");error.Visibility=Visibility.Visible; editor.Focus(); return false; }
        if (editingId.HasValue) { Model.Edit(editingId.Value,editor.Text); Selected=editingId; } else Selected=Model.CreateMarker(editingAnchor,editor.Text).Id;
        CloseEditor(); Tool=Tool.Marker; Refresh(); return true;
    }
    private void StopEditorMotion()
    {
        if(editorFade!=null && editorFadeCompleted!=null) editorFade.Completed-=editorFadeCompleted;
        editorFade?.Controller?.Remove();editorFade=null;editorFadeCompleted=null;
        editorLayer?.ApplyAnimationClock(OpacityProperty,null);editorOffset.BeginAnimation(TranslateTransform.YProperty,null);editorLink?.Stop();
    }
    private void ShowEditorVisuals()
    {
        if(editorLayer==null) { editorHost.Visibility=Visibility.Visible;return; }
        double from=editorLayer.Visibility==Visibility.Visible ? editorLayer.Opacity : 0;
        StopEditorMotion();editorLayer.Visibility=Visibility.Visible;editorLayer.IsHitTestVisible=true;editorLayer.Opacity=1;
        editorHost.RenderTransform=editorOffset;editorOffset.Y=0;
        if(IsVisible && SystemParameters.ClientAreaAnimation)
        {
            editorLayer.BeginAnimation(OpacityProperty,new DoubleAnimation(from,1,TimeSpan.FromMilliseconds(140)) { FillBehavior=FillBehavior.Stop,EasingFunction=new QuadraticEase { EasingMode=EasingMode.EaseOut } });
            UiMotion.Animate(editorOffset,TranslateTransform.YProperty,4,0,140);
        }
    }
    private void CloseEditor()
    {
        if(!UiDesign.IsEmber)
        {
            commentEditing=false;editingId=null;editorHost.Visibility=Visibility.Collapsed;editor.Text="";surface.Focus();return;
        }
        double from=editorLayer!.Opacity;commentEditing=false;editingId=null;StopEditorMotion();editorLayer.IsHitTestVisible=false;surface.Focus();
        if(!IsVisible || !SystemParameters.ClientAreaAnimation) { editorLayer.Visibility=Visibility.Collapsed;editor.Clear();return; }
        editorFade=(AnimationClock)new DoubleAnimation(from,0,TimeSpan.FromMilliseconds(100)).CreateClock(true);
        editorFadeCompleted=(_,_)=> { if(!Editing) { editorLayer.Visibility=Visibility.Collapsed;editor.Clear(); }StopEditorMotion(); };
        editorFade.Completed+=editorFadeCompleted;editorLayer.ApplyAnimationClock(OpacityProperty,editorFade);
    }
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
                else { error.Text=L.T("Save or cancel the comment before finishing the screenshot.");error.Visibility=Visibility.Visible; }
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
        else if (e.Key==Key.P) Switch(Tool.Pencil);
        else return;
        e.Handled=true;
    }
    internal void ShowHint(string message) => Message(message);
    private void Message(string message) { status.Text=L.T(message); status.Visibility=Visibility.Visible;if(contextDock!=null) contextDock.Visibility=Visibility.Visible; LayoutToolbar(); }
    private void LayoutToolbar()
    {
        var t=DeviceTransform;double width=Frame.Monitor.Width/t.M11,height=Frame.Monitor.Height/t.M22;
        if(!UiDesign.IsEmber)
        {
            bar.MaxWidth=Math.Max(1,width-16); classicTools?.InvalidateMeasure(); if(bar.Child is UIElement child) child.InvalidateMeasure(); bar.InvalidateMeasure(); bar.Measure(new Size(double.PositiveInfinity,double.PositiveInfinity));
            Canvas.SetLeft(bar,Math.Max(8,(width-bar.DesiredSize.Width)/2));
            return;
        }
        toolbarBrand!.Visibility=Editable && width>=1100 ? Visibility.Visible : Visibility.Collapsed;
        captureBar!.SetAvailableWidth(Math.Max(1,width-32));captureBar.Measure(new Size(captureBar.MaxWidth,double.PositiveInfinity));
        Canvas.SetLeft(captureBar,Math.Max(8,(width-captureBar.DesiredSize.Width)/2));
        // Two independent top blocks share a centered row; reserve Done before
        // measuring the wrapping tools so neither block overlaps the other.
        doneDock!.Measure(new Size(Math.Max(1,width-32),double.PositiveInfinity));
        const double gap=12;
        bar.MaxWidth=Math.Max(1,width-32-doneDock.DesiredSize.Width-gap);
        bar.Measure(new Size(bar.MaxWidth,double.PositiveInfinity));
        double left=Math.Max(8,(width-bar.DesiredSize.Width-gap-doneDock.DesiredSize.Width)/2);
        Canvas.SetLeft(bar,left);
        Canvas.SetLeft(doneDock,left+bar.DesiredSize.Width+gap);
        Canvas.SetTop(doneDock,16);
        contextDock!.MaxWidth=Math.Max(1,width-32);
        contextDock.Measure(new Size(contextDock.MaxWidth,double.PositiveInfinity));
        Canvas.SetLeft(contextDock,16);
        Canvas.SetTop(contextDock,Math.Max(16,height-contextDock.DesiredSize.Height-16));
    }
    public void Refresh()
    {
        foreach (var button in annotationButtons) button.Visibility=Editable ? Visibility.Visible : Visibility.Collapsed;
        foreach (var (tool,button) in toolButtons)
        {
            bool active=Tool==tool; button.Background=active ? UtilityUi.Accent : Brushes.Transparent; button.Foreground=active ? UiDesign.OnAccent : UtilityUi.Ink;
            button.FontWeight=active ? FontWeights.SemiBold : FontWeights.Normal;
            if(UiDesign.IsEmber)
            {
                var (caption,key)=tool switch { Tool.Marker => ("Marker","M"), Tool.Arrow => ("Arrow","A"), Tool.Rectangle => ("Rectangle","R"), _ => ("Pencil","P") };
                button.Content=ConstructDesign.ToolCaption(caption,key,active);
            }
            else
            {
                var (caption,key)=tool switch { Tool.Marker => ("● Marker","M"), Tool.Arrow => ("↗ Arrow","A"), Tool.Rectangle => ("▭ Rectangle","R"), _ => ("✎ Pencil","P") };
                button.Content=UtilityUi.ShortcutCaption((active ? "✓ " : "")+caption,key,active);
            }
        }
        if (Editable)
        {
            editButton.Visibility=Model.Items.OfType<Marker>().Any(m=>m.Id==Selected) ? Visibility.Visible : Visibility.Collapsed;
            deleteButton.Visibility=Selected.HasValue ? Visibility.Visible : Visibility.Collapsed;
            undoButton.IsEnabled=Model.CanUndo && !Editing; redoButton.IsEnabled=Model.CanRedo && !Editing;
        }
        status.Inlines.Clear();
        if(!UiDesign.IsEmber)
        {
            if(!AnnotationMode)
            {
                status.Inlines.Add(new Run(L.T(owner.WindowMode ? "Select a window · W — region" : "Drag a region · W — window")+" · "));
                modifierHint.Text=string.Format(L.T("{0} — annotations"),owner.Preferences.AnnotationModifier);
                if(modifierInline!=null) status.Inlines.Add(modifierInline);
                status.Inlines.Add(new Run(" · "+L.T("Space full monitor · Esc cancel")));
            }
            else status.Text=Editable ? "" : L.T("Continue on the selected monitor");
            status.Visibility=!AnnotationMode || !Editable ? Visibility.Visible : Visibility.Collapsed;
            bar.Visibility=Visibility.Visible;
        }
        else
        {
            if(!AnnotationMode)
            {
                status.Text=L.T(owner.WindowAcquiring ? "Capturing window…" : owner.WindowMode ? "Click a window to capture" : "Drag to select a region");
            }
            else status.Text=L.T(!Editable ? "Continue on the selected monitor" : Editing ? "Type a comment" : Selected.HasValue ? "Annotation selected · Delete to remove" : Tool switch { Tool.Marker=>"Click a point to add a comment",Tool.Pencil=>"Draw directly over the screenshot",Tool.Arrow=>"Drag to draw an arrow",_=>"Drag to draw a rectangle" });
            status.Visibility=Visibility.Visible;
            bar.Visibility=doneDock!.Visibility=Editable ? Visibility.Visible : Visibility.Collapsed;
            captureBar!.Visibility=!AnnotationMode ? Visibility.Visible : Visibility.Collapsed;
            captureBar.Refresh(owner.WindowMode,owner.Preferences.AnnotationModifier,ModifierActive);
            contextDock!.Visibility=Visibility.Visible;
            doneButton.IsEnabled=!Editing && !dragging;
        }
        UpdateModifierVisual();glow.InvalidateVisual();
        foreach(var button in annotationButtons) L.Tree(button);LayoutToolbar(); surface.Cursor=Cursors.Cross; surface.InvalidateVisual();markerSurface?.UpdateInteraction();markerSurface?.InvalidateVisual();
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
                dc.DrawRectangle(null,new Pen(window.ModifierActive && !window.AnnotationMode ? AnnotationSelectionAccent : UtilityUi.Accent,1),hole);
                if(!window.AnnotationMode && window.owner.WindowMode) dc.DrawRectangle(new SolidColorBrush(((SolidColorBrush)UtilityUi.Accent).Color) { Opacity=.09 },null,hole);
            }
            else dc.DrawRectangle(dim,null,full);
            dc.PushTransform(new TranslateTransform(window.Region.X,window.Region.Y));
            dc.PushTransform(new ScaleTransform(window.SourceScale,window.SourceScale));
            if (window.Editable) dc.PushClip(new RectangleGeometry(new Rect(0,0,window.Model.Width,window.Model.Height)));
            if(window.Editable) dc.DrawImage(window.Crop,new Rect(0,0,window.Model.Width,window.Model.Height));
            if (window.Editable) Painter.Draw(dc,UiDesign.IsEmber ? window.VisibleGlyphs().Where(g=>g is not BadgeGlyph) : window.VisibleGlyphs(),window.Model.Width,window.Model.Height);
            if (window.Editable && window.Selected.HasValue && window.Model.Items.FirstOrDefault(a=>a.Id==window.Selected) is { } selected)
            {
                var pen=new Pen(UtilityUi.Accent,1.5);
                if (selected is Marker m && !UiDesign.IsEmber)
                {
                    var b=Geo.Badge(m.Anchor,m.Label,window.Model.Width,window.Model.Height); var rect=new Rect(b.X-3,b.Y-3,b.Width+6,b.Height+6);
                    dc.DrawRoundedRectangle(null,new Pen(Brushes.White,3.5),rect,9,9); dc.DrawRoundedRectangle(null,pen,rect,9,9);
                }
                else if (selected is Box b) dc.DrawRectangle(null,pen,new Rect(b.Rect.X-3,b.Rect.Y-3,b.Rect.Width+6,b.Rect.Height+6));
                else if (selected is Arrow a) { dc.DrawEllipse(UtilityUi.Accent,new Pen(Brushes.White,1.5),new Point(a.Start.X,a.Start.Y),4,4); dc.DrawEllipse(UtilityUi.Accent,new Pen(Brushes.White,1.5),new Point(a.End.X,a.End.Y),4,4); }
                else if (selected is PencilStroke stroke)
                    foreach (var p in new[] { stroke.Points[0],stroke.Points[^1] }) dc.DrawEllipse(UtilityUi.Accent,new Pen(Brushes.White,1.5),new Point(p.X,p.Y),4,4);
            }
            if (window.Editable) dc.Pop(); dc.Pop(); dc.Pop(); dc.Pop(); dc.Pop();
        }
    }
    private sealed class MarkerSurface(AnnotationOverlay window) : FrameworkElement
    {
        private static readonly DependencyProperty ProgressProperty=DependencyProperty.Register("Progress",typeof(double),typeof(MarkerSurface),new FrameworkPropertyMetadata(1d,FrameworkPropertyMetadataOptions.AffectsRender));
        private (Guid? Hover,Guid? Selected,Guid? Moving,Guid? Editing) current,previous;
        internal void UpdateInteraction()
        {
            var next=(window.hoveredMarker,window.Selected,window.dragging ? window.moving?.Id : null,window.Editing ? window.editingId ?? Guid.Empty : (Guid?)null);
            if(next==current) return;previous=current;current=next;
            BeginAnimation(ProgressProperty,null);SetValue(ProgressProperty,1d);
            if(IsVisible && SystemParameters.ClientAreaAnimation)
                BeginAnimation(ProgressProperty,new DoubleAnimation(0,1,TimeSpan.FromMilliseconds(previous.Selected!=current.Selected ? 120 : 90)) { FillBehavior=FillBehavior.Stop,EasingFunction=new QuadraticEase { EasingMode=EasingMode.EaseOut } });
        }
        internal void Stop() => BeginAnimation(ProgressProperty,null);
        private static double Weight(Guid id,(Guid? Hover,Guid? Selected,Guid? Moving,Guid? Editing) state) => state.Editing==id || state.Moving==id ? 1 : state.Selected==id ? .75 : state.Hover==id ? .5 : 0;
        protected override void OnRender(DrawingContext dc)
        {
            if(!window.Editable) return;double progress=(double)GetValue(ProgressProperty);
            foreach(var badge in window.VisibleGlyphs().OfType<BadgeGlyph>())
            {
                double emphasis=Weight(badge.Id,previous)*(1-progress)+Weight(badge.Id,current)*progress;
                MarkerBadgeUi.Draw(dc,window.MarkerUiAnchor(badge.Anchor),window.MarkerUiBounds(badge.Anchor,badge.Label),badge.Label,emphasis);
            }
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
                var brush=new SolidColorBrush(AnnotationSelectionAccent.Color) { Opacity=opacity*intensity*Math.Exp(-i*i/48.0) };
                var ring=hole;ring.Inflate(i*.65,i*.65);dc.DrawRectangle(null,new Pen(brush,1.4),ring);
            }
            dc.Pop();
        }
    }
}
