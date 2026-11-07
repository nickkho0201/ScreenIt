using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ScreenIt.Core;

internal static class PencilChecks
{
    internal static void Run(Action<bool,string> check)
    {
        var session = new Session(); var draft = session.CreateDraft(800,600);
        check(!draft.AddPencil([]) && !draft.AddPencil([new(10,10),new(10,10)]) && !draft.CanUndo,"Pencil ignores empty/click gestures without history");
        P[] points = [new(100,100),new(200,100),new(200,200),new(100,100)];
        check(draft.AddPencil(points),"Closed pencil stroke keeps full path despite identical endpoints");
        var stroke = draft.Items.OfType<PencilStroke>().Single(); points[0] = new(0,0);
        check(stroke.Points[0] == new P(100,100) && stroke.Points is System.Collections.ObjectModel.ReadOnlyCollection<P>,"Pencil owns immutable point snapshot");
        check(draft.NextMarker == 1 && draft.Comments() == "Screenshot A","Pencil creates no marker number or comment");
        check(Geo.Hit(draft.Items,new(150,102),800,600)?.Id == stroke.Id && Geo.Hit(draft.Items,new(150,130),800,600) == null,"Pencil hit test follows segments, not bounding box");
        draft.Delete(stroke.Id); draft.Undo();
        check(draft.Items.Single() == stroke,"Delete/Undo restores whole stroke and GUID");
        draft.Undo(); draft.Redo();
        check(draft.Items.Single() == stroke,"One Undo/Redo per complete stroke");
        draft.AddPencil([new(-10,-20),new(1000,900)]);
        check(draft.Items.OfType<PencilStroke>().Last().Points.SequenceEqual(new P[] { new(0,0),new(799,599) }),"Pencil clips physical points to screenshot");
        var shot = session.Commit(draft);
        bool sealedDraft = false; try { draft.AddPencil([new(1,1),new(30,30)]); } catch (InvalidOperationException) { sealedDraft = true; }
        check(sealedDraft && shot.Annotations.Count == 2,"Committed pencil metadata is sealed");

        var bytes = Enumerable.Repeat((byte)245,800*600*4).ToArray();
        var image = BitmapSource.Create(800,600,96,96,PixelFormats.Bgr32,null,bytes,800*4); image.Freeze();
        var glyphs = Painter.Project(new[] { stroke }); var rendered = Painter.Render(image,glyphs);
        rendered.CopyPixels(bytes,800*4,0);
        bool Changed(int x,int y) => bytes[(y*800+x)*4] != 245 || bytes[(y*800+x)*4+1] != 245 || bytes[(y*800+x)*4+2] != 245;
        check(glyphs.Single() is PencilGlyph && Changed(150,100) && Changed(200,150) && !Changed(150,130),"Final bitmap draws bent pencil path without filling its interior");
        check(rendered.IsFrozen && rendered.PixelWidth == 800 && rendered.PixelHeight == 600,"Pencil raster retains frozen physical resolution");

        // Hidden synthetic overlay: no desktop acquisition, global input, clipboard writes or preferences saves.
        using var owner = new Coordinator(showTray:false,registerHotkey:false);
        var overlay = new AnnotationOverlay(owner,new Frame(new MonitorData("Synthetic",0,0,640,600,true,96),image));
        void Key(Key key) => overlay.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,HwndSource.FromHwnd(owner.ControlHandle),0,key) { RoutedEvent=Keyboard.PreviewKeyDownEvent });
        try
        {
            overlay.ChooseRegion(new(0,0,640,600)); Key(System.Windows.Input.Key.P);
            check(overlay.Tool == Tool.Pencil && overlay.ToolHighlighted(Tool.Pencil),"P selects and highlights pencil");
            overlay.BeginPointer(new(100,100)); overlay.MovePointer(new(200,100)); overlay.MovePointer(new(200,200));
            check(overlay.VisibleGlyphs().OfType<PencilGlyph>().Single().Points.Count == 3 && overlay.Model.Items.Count == 0,"Live pencil preview remains outside committed draft history");
            overlay.EndPointer(new(100,100));
            check(overlay.Model.Items.OfType<PencilStroke>().Single().Points.Count == 4 && !overlay.Editing,"Pointer release commits one stroke without editor");
            overlay.BeginPointer(new(300,300)); overlay.MovePointer(new(350,350)); Key(System.Windows.Input.Key.Escape); overlay.EndPointer(new(400,400));
            check(overlay.Model.Items.Count == 1 && overlay.VisibleGlyphs().Length == 1,"Escape cancels only unfinished pencil gesture");
            overlay.BeginPointer(new(300,300)); overlay.MovePointer(new(350,350)); overlay.InterruptGesture(); overlay.EndPointer(new(400,400));
            check(overlay.Model.Items.Count == 1,"Lost-capture/deactivation interruption discards unfinished pencil");
            overlay.BeginPointer(new(300,300)); overlay.MovePointer(new(350,350)); overlay.Switch(Tool.Arrow); overlay.EndPointer(new(400,400));
            check(overlay.Model.Items.Count == 1,"Switching tools cancels unfinished pencil");
            overlay.Switch(Tool.Pencil); overlay.BeginPointer(new(150,100)); Key(System.Windows.Input.Key.Delete);
            check(overlay.Model.Items.Count == 0,"Selected pencil stroke deletes as a single annotation");
            overlay.History(false); overlay.History(false); overlay.History(true);
            check(overlay.Model.Items.Count == 1 && overlay.Model.NextMarker == 1,"Overlay Undo/Redo restores stroke without consuming marker IDs");
            overlay.BeginPointer(new(300,300)); overlay.MovePointer(new(330,300)); overlay.EndPointer(new(330.25,300));
            check(overlay.Model.Items.OfType<PencilStroke>().Last().Points[^1] == new P(330.25,300),"Pencil retains subpixel release endpoint");
            overlay.History(false);
            overlay.Switch(Tool.Marker); overlay.BeginEdit(new(400,400)); Key(System.Windows.Input.Key.P);
            check(overlay.Editing && overlay.Tool == Tool.Marker && overlay.Model.NextMarker == 1,"P in comment editor does not switch tool or create annotation");
            overlay.CancelEdit();
            foreach (var language in new[] { "en","ru" })
                foreach (var theme in new[] { UiTheme.Dark,UiTheme.Light })
                {
                    L.Select(language); Appearance.Select(theme); overlay.ApplyTheme();
                    var button = overlay.ToolbarButtons.Single(b => ((ToolTip)b.ToolTip).Content.Equals(L.T("Pencil (P)")));
                    button.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                    check(overlay.Tool == Tool.Pencil && ((TextBlock)((StackPanel)button.Content).Children[0]).Text.Contains(language == "ru" ? "Карандаш" : "Pencil",StringComparison.Ordinal),"Pencil toolbar action/localization " + language + " " + theme);
                }
            foreach (var button in overlay.ToolbarButtons)
                for (FrameworkElement? node = button; node != null; node = node.Parent as FrameworkElement) node.InvalidateMeasure();
            var root = (FrameworkElement)overlay.Content; root.Measure(new Size(640,600)); root.Arrange(new Rect(0,0,640,600)); root.UpdateLayout();
            // Hidden windows do not deliver the visible HWND layout callback; run the same positioning method after measuring.
            typeof(AnnotationOverlay).GetMethod("LayoutToolbar",System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(overlay,null);
            root.Measure(new Size(640,600)); root.Arrange(new Rect(0,0,640,600)); root.UpdateLayout();
            foreach (var button in overlay.ToolbarButtons.Where(b => b.Visibility == Visibility.Visible))
            {
                var position = button.TranslatePoint(new Point(0,0),root);
                check(button.ActualWidth > 0 && position.X >= 0 && position.X + button.ActualWidth <= 640 && position.Y + button.ActualHeight <= 600,"Toolbar action fits synthetic monitor: " + ((ToolTip)button.ToolTip).Content + $" ({position.X}, {position.Y}, {button.ActualWidth}, {button.ActualHeight})");
            }
            var preview = new RenderTargetBitmap(640,600,96,96,PixelFormats.Pbgra32); preview.Render(root);
            var folder = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../artifacts/pencil")); Directory.CreateDirectory(folder);
            Save(rendered,Path.Combine(folder,"pencil-output.png")); Save(preview,Path.Combine(folder,"pencil-overlay.png"));
        }
        finally { overlay.ClosingByApp = true; overlay.Close(); L.Select("en"); Appearance.Select(UiTheme.Dark); }
    }
    private static void Save(BitmapSource image,string path)
    {
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image));
        using var output = File.Create(path); encoder.Save(output);
    }
}
