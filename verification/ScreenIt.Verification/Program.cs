using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ScreenIt.Core;
using Geometry = ScreenIt.Core.Geometry;

internal static class Verification
{
    private static readonly List<string> checks = [];
    private static readonly List<object> resources = [];
    private static readonly List<double> latency = [];
    private static void Check(bool value, string name) { if (!value) throw new InvalidOperationException(name); checks.Add(name); }
    [STAThread]
    private static int Main(string[] args)
    {
        int exit = 0; var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.Startup += async (_, _) =>
        {
            object report;
            try { if(args.Contains("--toast-static",StringComparer.Ordinal)) ToastChecks.Static(Check);else await Run(); if(args.Contains("--check-updates",StringComparer.Ordinal)) { var actual=await new GithubUpdateSource().Check(System.Threading.CancellationToken.None);Check(actual==null,"Explicit live GitHub check: public latest is not newer than 0.1.2; no downgrade"); } report = new { status = "PASS", count = checks.Count, checks, machine = Native.Machine(), monitors = Native.Monitors(), latencyMs = latency.Count==0 ? null : Distribution(latency), resources, toastStress = ToastChecks.Stress, clearStress=ClearChecks.Stress,settingsStress=SettingsChecks.Stress }; }
            catch (Exception ex) { exit = 1; report = new { status = "FAIL", checks, error = ex.ToString(), resources }; }
            var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../artifacts")); Directory.CreateDirectory(root);
            File.WriteAllText(Path.Combine(root, args.Contains("--toast-static",StringComparer.Ordinal) ? "toast-static-verification.json" : "verification.json"), JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine(JsonSerializer.Serialize(report)); app.Shutdown();
        };
        app.Run(); return exit;
    }
    private static object Distribution(List<double> values)
    {
        var sorted = values.Order().ToArray();
        return new { samples = sorted.Length, average = values.Average(), p50 = sorted[(int)Math.Ceiling(sorted.Length * .5) - 1], p95 = sorted[(int)Math.Ceiling(sorted.Length * .95) - 1], min = sorted[0], max = sorted[^1] };
    }
    private static BitmapSource Synthetic(int w = 800, int h = 600)
    {
        var bytes = Enumerable.Repeat((byte)245, w * h * 4).ToArray();
        var bitmap = BitmapSource.Create(w, h, 96, 96, PixelFormats.Bgr32, null, bytes, w * 4); bitmap.Freeze(); return bitmap;
    }
    private static byte[] Pixels(BitmapSource image) { var bytes = new byte[image.PixelWidth * image.PixelHeight * 4]; image.CopyPixels(bytes, image.PixelWidth * 4, 0); return bytes; }
    private static void Core()
    {
        foreach (var (ordinal, label) in new[] { (1,"A"), (2,"B"), (26,"Z"), (27,"AA"), (28,"AB"), (52,"AZ"), (53,"BA"), (702,"ZZ"), (703,"AAA") }) Check(ScreenshotLetters.FromOrdinal(ordinal) == label, "Letter " + ordinal);
        var session = new Session(); var draft = session.CreateDraft(800, 600);
        Check(session.NextLetter == "A", "Cancelled draft does not consume screenshot letter");
        var one = draft.CreateMarker(new(20, 30), "проверить"); var two = draft.CreateMarker(new(70, 80), "русский\nмногострочный комментарий"); var three = draft.CreateMarker(new(150, 200), "third");
        draft.Delete(two.Id); Check(draft.Items.OfType<Marker>().Select(m => m.Label).SequenceEqual(new[] { "A1", "A3" }), "Delete preserves marker labels");
        draft.Undo(); Check(draft.Items.Contains(two), "Undo restores marker GUID and comment"); draft.Redo();
        var four = draft.CreateMarker(new(300, 200), "four"); Check(four.Number == 4, "High water after delete");
        draft.Undo(); draft.Undo(); draft.CreateMarker(new(400, 200), "branch"); Check(!draft.CanRedo && draft.Items.OfType<Marker>().Last().Number == 5, "Branch does not reuse committed ID");
        draft.Move(one.Id, new(60, 90)); Check(draft.Items.OfType<Marker>().First(m => m.Id == one.Id).Anchor == new P(60,90), "Move"); draft.Undo(); Check(draft.Items.Contains(one), "Undo move"); draft.Redo();
        draft.Edit(one.Id,"изменён"); draft.Undo(); Check(draft.Items.OfType<Marker>().First(m => m.Id == one.Id).Comment == "проверить", "Undo edit"); draft.Redo();
        Check(draft.AddArrow(new(20, 20), new(200, 100)), "Arrow creation"); Check(draft.AddBox(new(350, 300), new(200, 200)), "Reverse rectangle");
        var before = draft.Items.ToArray(); draft.Delete(before.OfType<Arrow>().Single().Id); draft.Undo(); Check(draft.Items.SequenceEqual(before), "Supporting delete undo");
        int history = 0; while (draft.Undo()) history++; Check(draft.Items.Count == 0, "Undo all records"); while(history > 0) { Check(draft.Redo(), "Redo history step " + history); history--; } Check(history == 0 && draft.Items.SequenceEqual(before), "Redo exact mixed state");
        bool emptyRejected = false; try { draft.CreateMarker(new(1, 1), "  "); } catch(ArgumentException) { emptyRejected = true; } Check(emptyRejected, "No empty/orphan marker");
        var committed = session.Commit(draft); bool frozen = false; try { draft.CreateMarker(new(1,1), "late"); } catch(InvalidOperationException) { frozen = true; } Check(frozen && committed.Annotations.SequenceEqual(before), "Commit immutability");
        var b = session.CreateDraft(800,600); Check(b.CreateMarker(new(30,30), "B body").Label == "B1", "Screenshot-scoped B1"); session.Commit(b);
        var c = session.CreateDraft(800,600); c.CreateMarker(new(30,30), "C body"); session.Commit(c);
        Check(session.Screenshots.Select(s=>s.Letter).SequenceEqual(new[]{"A","B","C"}), "Session creation order");
        var formatted = CommentFormatter.Format(session); Check(formatted.Contains("A2 — русский" + Environment.NewLine + "     многострочный комментарий") && formatted.IndexOf("Screenshot B",StringComparison.Ordinal) < formatted.IndexOf("Screenshot C",StringComparison.Ordinal), "Unicode multiline session formatter");
        Check(ScreenshotDraft.EnterAction(true,false) == EditorEnter.Newline && ScreenshotDraft.EnterAction(false,true)==EditorEnter.Blocked, "Contextual Enter");
        session.Clear(); Check(session.NextLetter=="A" && session.Screenshots.Count==0 && committed.Annotations.Count==before.Length, "Clear does not mutate committed snapshot");
        foreach(var scale in new[]{1.0,1.25,1.5,2.0})
        {
            var local = Geometry.Local(new(-2560+321,77),-2560,0); var round = Geo.FromDip(Geo.ToDip(local,scale,scale),scale,scale);
            Check(round.Distance(local)<.000001, "DPI roundtrip and negative origin " + scale);
        }
        Check(Geometry.Bounds(new(330.2,200.3),new(-20,50.8),800,600)==new PxRect(0,50,331,151), "Crop floor/ceil reverse clamp");
        var selection = new Selection(); selection.Full(800,600); selection.Begin(new(10,10)); selection.Move(new(100,100),800,600); selection.CancelGesture(); Check(selection.Completed==new PxRect(0,0,800,600), "Interrupted selection B rollback");
        selection.Begin(new(10,10)); selection.End(new(10,10),800,600); Check(selection.Completed==new PxRect(0,0,800,600), "Accidental tiny selection preserves prior region");
        var rendererDraft = new ScreenshotDraft(800,600,"AA"); var mark = rendererDraft.CreateMarker(new(100,100), "secret body"); rendererDraft.AddArrow(new(200,200),new(300,250)); rendererDraft.AddBox(new(400,300),new(550,450));
        var original=Synthetic(); var first=Painter.Render(original,Painter.Project(rendererDraft.Items)); rendererDraft.Edit(mark.Id,"different body"); var second=Painter.Render(original,Painter.Project(rendererDraft.Items));
        Check(Pixels(first).SequenceEqual(Pixels(second)), "Comment body absent from bitmap bytes"); Check(first.PixelWidth==800 && first.PixelHeight==600 && first.IsFrozen,"Output physical dimensions/frozen");
        var glyphs=Painter.Project(rendererDraft.Items); Check(glyphs.OfType<BadgeGlyph>().Single().Anchor==mark.Anchor && glyphs.OfType<BadgeGlyph>().Single().Label=="AA1", "Marker anchor and ID projection");
        Check(glyphs.OfType<ArrowGlyph>().Single().End==new P(300,250) && glyphs.OfType<BoxGlyph>().Single().Rect==new Bounds(400,300,150,150),"Arrow endpoints and rectangle bounds");
        var badge=Geo.Badge(mark.Anchor,mark.Label,800,600); Check(new P(badge.X+badge.Width/2,badge.Y+badge.Height/2)==mark.Anchor, "Renderer badge centered at exact physical anchor");
    }
    private static async Task Run()
    {
        Core(); using var coordinator = new Coordinator(showTray:false,registerHotkey:false);
        await coordinator.Capture(); Check(coordinator.OverlayCount==Native.Monitors().Length,"Frozen overlay on every actual monitor");
        var windows = Application.Current.Windows.OfType<AnnotationOverlay>().ToArray();
        foreach(var overlay in windows) { overlay.Placement(); Check(true,"Actual physical HWND/DPI placement " + overlay.Frame.Monitor.Device); }
        var selectedWindow=windows.First(w=>w.Frame.Monitor.Primary);
        selectedWindow.BeginPointer(new(100,100)); selectedWindow.MovePointer(new(500,400)); Key(selectedWindow, System.Windows.Input.Key.Escape);
        Check(!selectedWindow.RegionSelection.Active && coordinator.OverlayCount==windows.Length,"Selection Esc rolls back gesture, retains capture");
        selectedWindow.BeginPointer(new(120,120)); Key(selectedWindow, System.Windows.Input.Key.Space); Check(selectedWindow.RegionSelection.Completed==new PxRect(0,0,selectedWindow.Frame.Monitor.Width,selectedWindow.Frame.Monitor.Height),"Space full active monitor");
        Check(selectedWindow.Editable && selectedWindow.Model.Width==selectedWindow.Frame.Monitor.Width,"Space auto-transitions full monitor into annotation without Enter");
        Check(!selectedWindow.RegionSelection.Active && Mouse.Captured==null,"Space during selection releases the unfinished gesture");
        Check(selectedWindow.Tool==Tool.Marker && selectedWindow.ToolHighlighted(Tool.Marker),"Marker is default and visibly selected");
        selectedWindow.BeginPointer(new(300,300)); selectedWindow.CommentInput.Text="provisional"; Key(selectedWindow,System.Windows.Input.Key.Escape);
        Check(!selectedWindow.Editing && selectedWindow.Model.NextMarker==1 && coordinator.OverlayCount==windows.Length,"Editor Esc preserves capture, consumes no ID");
        Key(selectedWindow,System.Windows.Input.Key.Escape); Check(coordinator.OverlayCount==0,"Idle empty annotation Esc cancels capture");
        await coordinator.Capture(); selectedWindow=Application.Current.Windows.OfType<AnnotationOverlay>().First(w=>w.Frame.Monitor.Primary);
        selectedWindow.ChooseRegion(new(50,50,800,600)); selectedWindow.BeginPointer(new(100,100)); selectedWindow.CommentInput.Text="keyboard";
        Key(selectedWindow,System.Windows.Input.Key.Enter);Check(!selectedWindow.Editing && selectedWindow.Model.Items.OfType<Marker>().Single().Comment=="keyboard","Routed editor Enter saves without screenshot commit");
        selectedWindow.BeginPointer(new(100,100),2);selectedWindow.CommentInput.Text="unsaved text";
        var companion=Application.Current.Windows.OfType<AnnotationOverlay>().First(w=>w!=selectedWindow);companion.ReturnFocus();await Dispatcher.Yield(DispatcherPriority.Input);
        Check(selectedWindow.Editing && selectedWindow.CommentInput.Text=="unsaved text","Actual own-window Deactivated preserves edit");coordinator.ReturnToDraft();await Dispatcher.Yield(DispatcherPriority.Input);Check(selectedWindow.CommentInput.IsKeyboardFocused,"Hotkey-equivalent return restores editor input");
        selectedWindow.CancelEdit();coordinator.Commit(selectedWindow);coordinator.Clear(true);
        coordinator.Cancel();
        for(int cycle=0;cycle<33;cycle++)
        {
            var timer=Stopwatch.StartNew(); await coordinator.Capture(); timer.Stop(); if(cycle>=3) latency.Add(timer.Elapsed.TotalMilliseconds);
            var overlay=Application.Current.Windows.OfType<AnnotationOverlay>().OrderBy(w=>w.Frame.Monitor.Device).ElementAt(cycle % Native.Monitors().Length);
            var hwnd=new WindowInteropHelper(overlay).Handle;
            overlay.BeginPointer(new(110.2,120.3)); overlay.MovePointer(new(910.2,720.3)); overlay.EndPointer(new(910.2,720.3));
            Check(overlay.Editable,"Valid mouse-up auto-transitions without Enter " + cycle);
            Check(new WindowInteropHelper(overlay).Handle==hwnd && overlay.Crop.PixelWidth==801 && overlay.Crop.PixelHeight==601,"Same HWND selection → annotation / exact crop " + cycle);
            overlay.BeginPointer(new(150,150)); await Dispatcher.Yield(DispatcherPriority.Input);
            Check(overlay.Editing && overlay.CommentInput.IsKeyboardFocused,"Immediate focused editor " + cycle);
            overlay.CommentInput.Text="синтетический\nкомментарий"; overlay.InterruptGesture(); Check(overlay.Editing && overlay.CommentInput.Text.Contains("комментарий"),"Gesture interruption preserves typed draft " + cycle);
            Check(overlay.CommitComment(),"Enter-equivalent marker commit " + cycle);
            overlay.Model.AddArrow(new(300,200),new(500,400)); overlay.Model.AddBox(new(350,100),new(700,300));
            coordinator.Commit(overlay); Check(coordinator.OverlayCount==0 && coordinator.Session.Screenshots.Count==cycle%3+1,"Commit closes overlays and advances RAM session " + cycle);
            Check(Native.LiveDcs==0 && Native.LiveBitmaps==0,"Native capture resources released " + cycle);
            if(cycle%3==2) { Check(coordinator.Session.Screenshots.Select(s=>s.Letter).SequenceEqual(new[]{"A","B","C"}),"Actual A/B/C repeated session " + cycle); coordinator.Clear(true); }
            if(cycle==2 || cycle==12 || cycle==22 || cycle==32) { await Settle(); resources.Add(new {cycle, counters=Native.Resources()}); }
        }
        foreach(var label in new[]{"A","B","C"})
        {
            var draft=coordinator.Session.CreateDraft(800,600);draft.CreateMarker(new(100,100),"тест\nкомментарий");var committed=coordinator.Session.Commit(draft);var original=Synthetic();coordinator.Rasters.Add(committed.Id,new(original,Painter.Render(original,Painter.Project(committed.Annotations))));
        }
        coordinator.CopyImages();var copied=ReadFiles(coordinator.ControlHandle);
        Check(copied.Select(Path.GetFileName).SequenceEqual(new[]{"ScreenIt-A.png","ScreenIt-B.png","ScreenIt-C.png"}),"Production Copy Images names/session order");
        string expected=CommentFormatter.Format(coordinator.Session);coordinator.CopyComments();Check(ReadText(coordinator.ControlHandle)==expected,"Production Copy Comments exact formatter");
        await PasteChecks.Run(Check, coordinator);
        coordinator.Clear(true);Check(copied.All(File.Exists),"Production generations retained after comments replacement and Clear");
        await Polish(coordinator);
        await ToastChecks.Run(Check, coordinator);
        await ClearChecks.Run(Check, coordinator);
        await AppearanceChecks.Run(Check, coordinator);
        Appearance.Select(UiTheme.Light);await Preview("light");
        Appearance.Select(UiTheme.Dark);await Preview("dark");
        await Clipboard();
        await SettingsChecks.Run(Check);
    }
    private static async Task Polish(Coordinator coordinator)
    {
        using var trayIcon=UtilityUi.TrayIcon(); Check(trayIcon.Handle!=IntPtr.Zero && UtilityUi.WindowIcon().IsFrozen,"Embedded tray/window icon loads");
        var icon=UtilityUi.IconBytes; Check(BitConverter.ToUInt16(icon,4)==8,"ICO includes eight tray/app sizes");
        var dialog=new ClearSessionDialog(3); Check(dialog.CancelButton.IsDefault && dialog.CancelButton.IsCancel && dialog.Description.StartsWith("3 screenshots"),"Clear confirmation defaults to Cancel with actual count"); dialog.Close();
        coordinator.Clear(); Check(coordinator.Session.Screenshots.Count==0 && coordinator.SessionStatus=="Session: 0 screenshots","Empty Clear is harmless");
        var draft=coordinator.Session.CreateDraft(800,600); draft.CreateMarker(new(100,100),"synthetic"); var shot=coordinator.Session.Commit(draft); var image=Synthetic(); coordinator.Rasters.Add(shot.Id,new(image,image));
        coordinator.ConfirmClear=n=> { Check(n==1,"Clear confirmation receives actual count"); return false; };
        coordinator.Clear(); Check(coordinator.Session.Screenshots.Count==1 && coordinator.Session.NextLetter=="B","Cancel Clear preserves session and numbering");
        await coordinator.Capture(); var overlay=Application.Current.Windows.OfType<AnnotationOverlay>().First(w=>w.Frame.Monitor.Primary);
        coordinator.Clear(true); Check(coordinator.Session.Screenshots.Count==1,"Clear rejected during active capture");
        overlay.RegionSelection.Full(overlay.Frame.Monitor.Width,overlay.Frame.Monitor.Height);
        overlay.BeginPointer(new(100,100)); overlay.EndPointer(new(100,100));
        Check(!overlay.AnnotationMode && overlay.RegionSelection.Completed==new PxRect(0,0,overlay.Frame.Monitor.Width,overlay.Frame.Monitor.Height),"Tiny mouse-up stays Selection and preserves prior region");
        overlay.EndPointer(new(500,500)); Check(!overlay.AnnotationMode,"Late mouse-up after gesture cancellation cannot transition");
        Key(overlay,System.Windows.Input.Key.Space);
        foreach(var tool in new[]{Tool.Marker,Tool.Arrow,Tool.Rectangle}) { overlay.Switch(tool); Check(overlay.ToolHighlighted(tool) && Enum.GetValues<Tool>().Where(t=>t!=tool).All(t=>!overlay.ToolHighlighted(t)),"Only active tool highlighted "+tool); }
        overlay.Switch(Tool.Marker);
        var marker=overlay.Model.CreateMarker(new(160,180),"synthetic selected marker");
        overlay.Refresh(); var unselected=overlay.VisibleGlyphs().OfType<BadgeGlyph>().Single();
        overlay.BeginPointer(marker.Anchor); overlay.EndPointer(marker.Anchor);
        var selected=overlay.VisibleGlyphs().OfType<BadgeGlyph>().Single();
        Check(overlay.Selected==marker.Id && selected==unselected && overlay.Model.Items.OfType<Marker>().Single()==marker,"Marker selection leaves physical geometry and raster glyph unchanged");
        Key(overlay,System.Windows.Input.Key.Delete); Check(overlay.Model.Items.Count==0,"Delete selected marker remains keyboard accessible");
        overlay.History(false); Check(overlay.Model.Items.OfType<Marker>().Single()==marker,"Undo restores polished marker identity");
        overlay.History(true); Check(overlay.Model.Items.Count==0,"Redo removes marker before empty-draft cancellation");
        overlay.BeginEdit(new(overlay.Model.Width-2,overlay.Model.Height-2));
        var r=overlay.EditorBounds; Check(r.X>=0 && r.Y>=0 && r.X+r.Width<=overlay.Frame.Monitor.Width && r.Y+r.Height<=overlay.Frame.Monitor.Height,"Comment editor flips/clamps at monitor bottom-right");
        overlay.CancelEdit(); coordinator.Cancel();
        coordinator.ConfirmClear=_=>true; coordinator.Clear();
        var fresh=coordinator.Session.CreateDraft(800,600); Check(fresh.Letter=="A" && fresh.CreateMarker(new(20,20),"reset").Label=="A1","Confirmed Clear resets screenshot/marker A/A1");
        Check(coordinator.Rasters.Count==0 && coordinator.SessionStatus=="Session: 0 screenshots","Clear releases session rasters and updates tray count");
    }
    private static async Task Preview(string theme)
    {
        using var owner=new Coordinator(showTray:false,registerHotkey:false);
        var monitor=Native.Monitors().First(m=>m.Primary);
        var window=new AnnotationOverlay(owner,new Frame(monitor,Synthetic(monitor.Width,monitor.Height)));
        try
        {
            window.Show(); await window.Rendered.Task; Key(window,System.Windows.Input.Key.Space);
            var center=monitor.Width/2.0;
            window.Model.CreateMarker(new(center-280,220),"synthetic saved comment");
            window.Model.AddArrow(new(center-280,300),new(center-120,350));
            window.Model.AddBox(new(center-320,390),new(center-130,470));
            window.BeginEdit(new(center-40,200)); window.CommentInput.Text="Проверить расположение кнопки.\nМногострочный комментарий.";
            window.UpdateLayout(); await Dispatcher.Yield(DispatcherPriority.Render);
            var visual=new DrawingVisual();
            using(var dc=visual.RenderOpen()) dc.DrawRectangle(new VisualBrush((Visual)window.Content),null,new Rect(0,0,monitor.Width,monitor.Height));
            var raster=new RenderTargetBitmap(monitor.Width,monitor.Height,96,96,PixelFormats.Pbgra32); raster.Render(visual);
            var crop=new CroppedBitmap(raster,new Int32Rect((monitor.Width-1000)/2,0,1000,600));
            var encoder=new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(crop));
            var path=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../artifacts/polish-preview-"+theme+".png"));
            using(var output=File.Create(path)) encoder.Save(output);
            Check(window.Editing && window.CommentInput.IsKeyboardFocused,"Polished editor remains immediately focused");
            Check(Native.LiveDcs==0 && Native.LiveBitmaps==0,"Synthetic UI preview does not allocate native capture handles");
        }
        finally { window.ClosingByApp=true; window.Close(); }
    }
    private static void Key(AnnotationOverlay window, System.Windows.Input.Key key)
    {
        var args=new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(window),0,key){RoutedEvent=Keyboard.PreviewKeyDownEvent};window.RaiseEvent(args);
    }
    private static string[] ReadFiles(IntPtr owner)
    {
        Check(GetClipboardOwner()==owner && ClipboardTransport.OpenClipboard(owner),"Production images ownership guard");
        try{var drop=GetClipboardData(15);uint count=DragQueryFileW(drop,uint.MaxValue,null,0);var files=new List<string>();for(uint i=0;i<count;i++){var name=new StringBuilder((int)DragQueryFileW(drop,i,null,0)+1);DragQueryFileW(drop,i,name,(uint)name.Capacity);files.Add(name.ToString());}return files.ToArray();}finally{ClipboardTransport.CloseClipboard();}
    }
    private static string? ReadText(IntPtr owner)
    {
        Check(GetClipboardOwner()==owner && ClipboardTransport.OpenClipboard(owner),"Production comments ownership guard");
        try{var data=GetClipboardData(13);var ptr=ClipboardTransport.GlobalLock(data);try{return Marshal.PtrToStringUni(ptr);}finally{ClipboardTransport.GlobalUnlock(data);}}finally{ClipboardTransport.CloseClipboard();}
    }
    private static async Task Settle() { await Task.Delay(80); GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect(); await Dispatcher.Yield(DispatcherPriority.Background); }
    private static async Task Clipboard()
    {
        var window = new Window { ShowInTaskbar=false }; var handle=new WindowInteropHelper(window).EnsureHandle();
        var tempRoot=Path.Combine(Path.GetTempPath(),"ScreenItVerification",Guid.NewGuid().ToString("N")); var store=new TemporaryImages(tempRoot);
        var paths=store.Write(new[]{("A",Synthetic()),("B",Synthetic(640,480)),("C",Synthetic(1024,768))});
        ClipboardTransport.Publish(handle,ClipboardPayload.Images(paths)); uint sequence=GetClipboardSequenceNumber();
        Check(GetClipboardOwner()==handle && sequence!=0,"Own published clipboard state");
        Check(ClipboardTransport.OpenClipboard(handle),"Open own clipboard readback");
        try
        {
            Check(GetClipboardOwner()==handle && GetClipboardSequenceNumber()==sequence,"Guard foreign clipboard readback");
            var drop=GetClipboardData(15); uint count=DragQueryFileW(drop,uint.MaxValue,null,0); var actual=new List<string>();
            for(uint i=0;i<count;i++){uint length=DragQueryFileW(drop,i,null,0);var name=new StringBuilder((int)length+1);DragQueryFileW(drop,i,name,(uint)name.Capacity);actual.Add(name.ToString());}
            Check(actual.SequenceEqual(paths),"HDROP ordered A/B/C roundtrip"); Check(!IsClipboardFormatAvailable(13) && !IsClipboardFormatAvailable(8) && !IsClipboardFormatAvailable(17),"Files-only no text/DIB competition"); Privacy();
        }
        finally { ClipboardTransport.CloseClipboard(); }
        for(int i=0;i<paths.Length;i++)
        {
            using var file=File.OpenRead(paths[i]); var png=BitmapDecoder.Create(file,BitmapCreateOptions.None,BitmapCacheOption.OnLoad).Frames[0];
            Check(png.PixelWidth==new[]{800,640,1024}[i] && png.PixelHeight==new[]{600,480,768}[i],"Independent PNG dimensions " + i);
        }
        string text="Screenshot A\r\nA1 — русский\r\n     многострочный комментарий\r\n\r\nScreenshot B\r\nB1 — тест";
        ClipboardTransport.Publish(handle,ClipboardPayload.Comments(text)); sequence=GetClipboardSequenceNumber(); Check(await OpenOwnClipboard(handle),"Open comments readback");
        try
        {
            Check(GetClipboardOwner()==handle && GetClipboardSequenceNumber()==sequence,"Comments ownership guard"); var data=GetClipboardData(13); var pointer=ClipboardTransport.GlobalLock(data);
            try{Check(Marshal.PtrToStringUni(pointer)==text,"Exact Unicode multiline clipboard roundtrip");}finally{ClipboardTransport.GlobalUnlock(data);}
            Check(!IsClipboardFormatAvailable(15) && !IsClipboardFormatAvailable(8) && !IsClipboardFormatAvailable(17),"Comments-only no files/image competition"); Privacy();
        }
        finally{ClipboardTransport.CloseClipboard();}
        Check(paths.All(File.Exists),"PNG files survive comments clipboard replacement"); Check(store.Cleanup(DateTimeOffset.UtcNow)==0,"New generation not cleaned");
        var unrelated=Path.Combine(tempRoot,Guid.NewGuid().ToString("N"));Directory.CreateDirectory(unrelated);File.WriteAllText(Path.Combine(unrelated,"foreign.txt"),"synthetic");
        Check(store.Cleanup(DateTimeOffset.UtcNow.AddDays(8))==1 && File.Exists(Path.Combine(unrelated,"foreign.txt")),"TTL cleanup only owned generation, unrelated directory retained");
        if(GetClipboardOwner()==handle && ClipboardTransport.OpenClipboard(handle)){try{ClipboardTransport.EmptyClipboard();}finally{ClipboardTransport.CloseClipboard();}}
        File.Delete(Path.Combine(unrelated,"foreign.txt"));Directory.Delete(unrelated);Directory.Delete(tempRoot);window.Close();
        await Task.CompletedTask;
    }
    private static async Task<bool> OpenOwnClipboard(IntPtr handle)
    {
        // Other processes may briefly open even our synthetic payload after a publication.
        for(int attempt=0;attempt<20;attempt++)
        {
            if(GetClipboardOwner()!=handle) return false;
            if(ClipboardTransport.OpenClipboard(handle)) return true;
            await Task.Delay(10);
        }
        return false;
    }
    private static void Privacy()
    {
        foreach(var name in new[]{"CanUploadToCloudClipboard","CanIncludeInClipboardHistory"})
        {
            uint format=ClipboardPayload.RegisterClipboardFormatW(name); var data=GetClipboardData(format);Check(data!=IntPtr.Zero,"Privacy format present " + name);
            var pointer=ClipboardTransport.GlobalLock(data);try{Check(pointer!=IntPtr.Zero && Marshal.ReadInt32(pointer)==0,"Privacy DWORD zero " + name);}finally{ClipboardTransport.GlobalUnlock(data);}
        }
    }
    [DllImport("user32.dll")] private static extern IntPtr GetClipboardOwner();
    [DllImport("user32.dll")] private static extern uint GetClipboardSequenceNumber();
    [DllImport("user32.dll")] private static extern IntPtr GetClipboardData(uint format);
    [DllImport("user32.dll")] private static extern bool IsClipboardFormatAvailable(uint format);
    [DllImport("shell32.dll",CharSet=CharSet.Unicode)] private static extern uint DragQueryFileW(IntPtr drop,uint index,StringBuilder? name,uint count);
}
