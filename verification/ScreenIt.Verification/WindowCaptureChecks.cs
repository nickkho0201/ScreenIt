using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ScreenIt.Core;

internal static class WindowCaptureChecks
{
    // Native integration fixture is a separate process, so production PID filtering
    // remains enabled even when tests drive the real Coordinator.
    internal static void Fixture(string path)
    {
        var app=new Application { ShutdownMode=ShutdownMode.OnLastWindowClose };
        var target=new Window { Title="ScreenIt verification target",Width=640,Height=440,Background=Brushes.Crimson,Topmost=true };
        var cover=new Window { Title="ScreenIt verification occluder",Width=180,Height=180,Background=Brushes.LimeGreen,Topmost=true };
        app.Startup+=async(_,_)=>
        {
            var monitor=Native.Monitors().First(m=>m.Primary);target.Show();cover.Show();
            nint hwnd=new WindowInteropHelper(target).Handle,occluder=new WindowInteropHelper(cover).Handle;
            Place(hwnd,monitor.Left+160,monitor.Top+170,640,440);Place(occluder,monitor.Left+390,monitor.Top+300,180,180);
            nint child=CreateWindowEx(0,"STATIC","",0x50000000,10,10,40,20,hwnd,0,0,0);
            await Dispatcher.Yield(DispatcherPriority.Render);Native.Flush();
            File.WriteAllText(path,JsonSerializer.Serialize(new { target=hwnd.ToInt64(),cover=occluder.ToInt64(),child=child.ToInt64() }));
        };
        app.Run();
    }
    internal static async Task Run(Action<bool,string> check)
    {
        var bounds=new PxRect(-400,120,600,400);
        check(WindowTargets.Eligible(12,true,false,false,false,false,bounds,99),"Normal top-level metadata eligible with negative origin");
        check(!WindowTargets.Eligible(99,true,false,false,false,false,bounds,99),"Own process excluded by identity");
        check(!WindowTargets.Eligible(12,false,false,false,false,false,bounds,99),"Invisible window excluded");
        check(!WindowTargets.Eligible(12,true,true,false,false,false,bounds,99),"Minimized window excluded");
        check(!WindowTargets.Eligible(12,true,false,true,false,false,bounds,99),"Cloaked window excluded");
        check(!WindowTargets.Eligible(12,true,false,false,true,false,bounds,99),"Tool/menu/taskbar window excluded");
        check(!WindowTargets.Eligible(12,true,false,false,false,false,new(0,0,1,0),99),"Zero/tiny bounds excluded");
        var upper=new WindowTarget(1,12,bounds);var lower=new WindowTarget(2,13,new(-500,0,1000,700));
        check(WindowTargets.At(new[]{upper,lower},new(-100,200))==upper && WindowTargets.At(new[]{upper,lower},new(300,200))==lower,"Topmost eligible target wins overlap; exposed lower region remains selectable");
        string folder=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../artifacts/window-capture"));Directory.CreateDirectory(folder);
        string ready=Path.Combine(folder,"fixture-"+Guid.NewGuid().ToString("N")+".json");
        var start=new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute=false };start.ArgumentList.Add("--window-fixture");start.ArgumentList.Add(ready);
        using var process=Process.Start(start)!;nint hwnd=0,cover=0;
        try
        {
            for(int i=0;i<100 && !File.Exists(ready) && !process.HasExited;i++) await Task.Delay(50);
            check(File.Exists(ready),"Separate native target/occluder fixture starts");
            nint child;
            using(var data=JsonDocument.Parse(File.ReadAllText(ready))) { hwnd=(nint)data.RootElement.GetProperty("target").GetInt64();cover=(nint)data.RootElement.GetProperty("cover").GetInt64();child=(nint)data.RootElement.GetProperty("child").GetInt64(); }
            var target=WindowTargets.Read(hwnd) ?? throw new InvalidOperationException("Missing fixture target");var occluder=WindowTargets.Read(cover) ?? throw new InvalidOperationException("Missing fixture occluder");
            check(target.Bounds.Width>0 && occluder.Bounds.Width>0,"Real visible WPF HWNDs pass central eligibility");
            var childBounds=Native.WindowRect(child);
            check(child!=0 && WindowTargets.Read(child)==null && WindowTargets.At(WindowTargets.Snapshot(),new(childBounds.Left+5,childBounds.Top+5))?.Hwnd==hwnd,"Native child control selects its eligible top-level parent, never child HWND");
            check(WindowTargets.At(WindowTargets.Snapshot(),new(occluder.Bounds.X+20,occluder.Bounds.Y+60))?.Hwnd==cover,"Real always-on-top occluder wins native z-order");
            using(var selection=new WindowSelection())
            {
                var point=new P(target.Bounds.X+25,target.Bounds.Y+65);
                check(selection.At(point)?.Hwnd==hwnd,"Picker snapshot contains exposed native target");
                var original=Native.WindowRect(hwnd);
                try
                {
                    Place(hwnd,original.Left+900,original.Top,original.Right-original.Left,original.Bottom-original.Top);
                    check(selection.At(point)?.Hwnd==hwnd && selection.At(point,true)?.Hwnd!=hwnd,"Click refresh rejects moved target before coalesced WinEvent delivery");
                }
                finally { Place(hwnd,original.Left,original.Top,original.Right-original.Left,original.Bottom-original.Top); }
            }
            var image=await WindowCapture.Capture(target,CancellationToken.None);CheckImage(image,check,"Native occluded WGC");
            check(Math.Abs(image.PixelWidth-target.Bounds.Width)<=2 && Math.Abs(image.PixelHeight-target.Bounds.Height)<=2,"WGC physical dimensions match visible window frame");
            Save(image,Path.Combine(folder,"occluded-window.png"));
            using var self=Process.GetCurrentProcess();self.Refresh();int handles=self.HandleCount;
            for(int cycle=0;cycle<20;cycle++) { var captured=await WindowCapture.Capture(target,CancellationToken.None);check(IsCrimson(captured) && WindowCapture.LiveSessions==0,"WGC releases pool/session/GPU frame after cycle "+cycle); }
            self.Refresh();check(self.HandleCount<=handles+32,"Native WGC stress has bounded OS handles after 20 acquisitions");
            using(var cancel=new CancellationTokenSource()) { cancel.Cancel();bool stopped=false;try { await WindowCapture.Capture(target,cancel.Token); }catch(OperationCanceledException) { stopped=true; }check(stopped && WindowCapture.LiveSessions==0,"Cancelled acquisition owns no native capture resources"); }
            using var owner=new Coordinator(false,false);
            foreach(bool annotate in new[]{false,true})
            {
                var before=Evidence(owner);await owner.Capture();var after=Evidence(owner);
                File.WriteAllText(Path.Combine(folder,"overlay-transition.json"),JsonSerializer.Serialize(new { annotate,before,after },new JsonSerializerOptions { WriteIndented=true }));
                check(owner.OverlayCount==Native.Monitors().Length && owner.Active==null,"Capture must finish with all monitor overlays before window selection; evidence: "+JsonSerializer.Serialize(new { before,after }));
                var overlay=Application.Current.Windows.OfType<AnnotationOverlay>().First(w=>owner.IsCurrent(w) && w.Frame.Monitor.Primary);overlay.AnnotationHeld=()=>annotate;
                Press(overlay,Key.W);check(owner.WindowMode && owner.Active==null,"W enters Window mode");
                check(WindowTargets.Read(new WindowInteropHelper(overlay).Handle)==null && WindowTargets.Snapshot().All(t=>t.ProcessId!=Environment.ProcessId),"All actual ScreenIt HWNDs excluded despite topmost overlay");
                owner.HoverWindow(new(target.Bounds.X+25,target.Bounds.Y+65));check(owner.WindowHover?.Hwnd==hwnd,"ScreenIt overlays ignored in actual targeting");
                await owner.CaptureWindow(overlay,annotate);
                check(annotate ? overlay.Editable && owner.Session.Screenshots.Count==0 : owner.OverlayCount==0 && owner.Session.Screenshots.Count==1,"Window completion follows modifier contract "+annotate);
                if(annotate) { check(IsCrimson(overlay.Crop),"Annotation source excludes hover tint/border/occluder");owner.Commit(overlay); }
                var shot=owner.Session.Screenshots.Last();CheckImage(owner.Rasters[shot.Id].Annotated,check,"Session window source "+annotate);owner.Clear(true);
            }
            for(int i=0;i<3;i++)
            {
                await owner.Capture();var overlay=Application.Current.Windows.OfType<AnnotationOverlay>().First(w=>w.Frame.Monitor.Primary);owner.ToggleWindowMode();owner.HoverWindow(new(target.Bounds.X+25,target.Bounds.Y+65));await owner.CaptureWindow(overlay,false);
            }
            check(owner.Session.Screenshots.Select(s=>s.Letter).SequenceEqual(new[]{"A","B","C"}) && owner.Rasters.Count==3,"Window screenshots preserve A/B/C session order and raster GUID mapping");owner.Clear(true);
            await owner.Capture();var cancelling=Application.Current.Windows.OfType<AnnotationOverlay>().First(w=>w.Frame.Monitor.Primary);owner.ToggleWindowMode();owner.HoverWindow(new(target.Bounds.X+25,target.Bounds.Y+65));var run=owner.CaptureWindow(cancelling,false);owner.Cancel();await run;
            check(owner.OverlayCount==0 && owner.Session.Screenshots.Count==0 && WindowCapture.LiveSessions==0,"Esc/close-style cancellation during native acquisition cannot publish a stale draft");
            var oldLanguage=L.Language;var oldTheme=Appearance.Current;
            try
            {
                await owner.Capture();var overlay=Application.Current.Windows.OfType<AnnotationOverlay>().First(w=>w.Frame.Monitor.Primary);
                foreach(var theme in new[]{UiTheme.Light,UiTheme.Dark}) foreach(var lang in new[]{"ru","en"})
                {
                    Appearance.Select(theme);L.Select(lang);
                    foreach(bool windowMode in new[]{false,true})
                    {
                        if(owner.WindowMode!=windowMode) Press(overlay,Key.W);owner.HoverWindow(new(target.Bounds.X+25,target.Bounds.Y+65));
                        await Dispatcher.Yield(DispatcherPriority.Render);var root=(FrameworkElement)overlay.Content;var t=overlay.DeviceTransform;
                        var preview=new RenderTargetBitmap((int)(root.ActualWidth*t.M11),(int)(root.ActualHeight*t.M22),96*t.M11,96*t.M22,PixelFormats.Pbgra32);preview.Render(root);Save(preview,Path.Combine(folder,$"hint-{theme}-{lang}-{windowMode}.png"));
                        check(overlay.ModifierHint.Contains(owner.Preferences.AnnotationModifier.ToString()),"Region/window hint keeps configured modifier "+theme+lang+windowMode);
                    }
                }
                Press(overlay,Key.W);check(!owner.WindowMode,"W returns to Region mode");Press(overlay,Key.W);Press(overlay,Key.Escape);check(owner.OverlayCount==0 && !owner.WindowMode,"Esc cancels window selection and removes scoped events");
                await owner.Capture();overlay=Application.Current.Windows.OfType<AnnotationOverlay>().First(w=>w.Frame.Monitor.Primary);overlay.AnnotationHeld=()=>false;Press(overlay,Key.W);Press(overlay,Key.Space);check(owner.OverlayCount==0 && owner.Session.Screenshots.Count==1,"Space stays quick full-monitor in Window mode");owner.Clear(true);
                foreach(var monitor in Native.Monitors())
                {
                    Place(hwnd,monitor.Left+80,monitor.Top+100,520,360);await Task.Delay(100);target=WindowTargets.Read(hwnd)!;var actual=await WindowCapture.Capture(target,CancellationToken.None);CheckImage(actual,check,"Native monitor "+monitor.TopologyKey);
                    check(WindowTargets.At(WindowTargets.Snapshot(),new(target.Bounds.X+20,target.Bounds.Y+65))?.Hwnd==hwnd,"Physical hit test matches monitor/DPI origin "+monitor.TopologyKey);
                }
                var primary=Native.Monitors().First(m=>m.Primary);Place(hwnd,primary.Left-200,primary.Top+160,640,440);await Task.Delay(100);target=WindowTargets.Read(hwnd)!;image=await WindowCapture.Capture(target,CancellationToken.None);CheckImage(image,check,"Cross-monitor whole-window frame");
                check(image.PixelWidth>500,"Cross-monitor window is not cropped to monitor width");
                await owner.Capture();overlay=Application.Current.Windows.OfType<AnnotationOverlay>().First(w=>w.Frame.Monitor.Primary);overlay.AnnotationHeld=()=>true;Press(overlay,Key.W);owner.HoverWindow(new(target.Bounds.X+25,target.Bounds.Y+65));await owner.CaptureWindow(overlay,true);
                check(overlay.Editable && overlay.Model.Width==image.PixelWidth && overlay.Crop.PixelWidth==image.PixelWidth,"Cross-monitor annotation retains whole source width in existing draft");owner.Cancel();
                Place(hwnd,primary.Left-300,primary.Top+180,primary.Width+600,500);await Task.Delay(120);target=WindowTargets.Read(hwnd)!;
                await owner.Capture();overlay=Application.Current.Windows.OfType<AnnotationOverlay>().First(w=>w.Frame.Monitor.Primary);overlay.AnnotationHeld=()=>true;owner.ToggleWindowMode();owner.HoverWindow(new(target.Bounds.X+25,target.Bounds.Y+65));await owner.CaptureWindow(overlay,true);
                check(overlay.Editable && overlay.SourceScale<1 && overlay.Model.Width>primary.Width && overlay.Region.Width<=primary.Width && IsCrimson(overlay.Crop),"Large true window is fitted for annotation without cropping physical source");
                overlay.Switch(Tool.Arrow);overlay.BeginPointer(new(80,90));overlay.EndPointer(new(200,190));owner.Commit(overlay);
                check(owner.Session.Screenshots.Single().Width==target.Bounds.Width && owner.Rasters.Values.Single().Annotated.PixelWidth==target.Bounds.Width,"Scaled annotation commits full-resolution window with existing Painter");owner.Clear(true);
                for(int cycle=0;cycle<20;cycle++) { await owner.Capture();owner.ToggleWindowMode();owner.ToggleWindowMode();owner.Cancel();check(!owner.WindowMode && owner.OverlayCount==0,"Repeated picker event lifecycle "+cycle); }
            }
            finally { owner.Cancel();L.Select(oldLanguage);Appearance.Select(oldTheme); }
            SendMessage(hwnd,0x10,0,0);await Task.Delay(100);check(WindowTargets.Read(hwnd)==null,"Destroyed HWND becomes ineligible before click");
            bool vanished=false;try { await WindowCapture.Capture(target,CancellationToken.None); }catch(IOException) { vanished=true; }
            check(vanished && WindowCapture.LiveSessions==0,"Destroyed HWND acquisition fails safely without COM/GPU resources");
        }
        finally
        {
            if(hwnd!=0) SendMessage(hwnd,0x10,0,0);if(cover!=0) SendMessage(cover,0x10,0,0);
            if(!process.HasExited) { if(!process.WaitForExit(3000)) process.Kill(); } // Only this owned fixture process.
            if(File.Exists(ready)) File.Delete(ready);
        }
    }
    private static void Press(AnnotationOverlay overlay,Key key)=>overlay.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(overlay),0,key) { RoutedEvent=Keyboard.PreviewKeyDownEvent });
    private static object Evidence(Coordinator owner)
    {
        object? Field(string name)=>typeof(Coordinator).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(owner);
        return new { generation=owner.CaptureGeneration,failure=owner.LastCaptureFailure?.ToString(),busy=Field("busy"),disposed=Field("disposed"),suspended=Field("suspended"),prompt=UtilityUi.PromptOpen,confirmation=owner.ConfirmationOpen,overlays=owner.OverlayCount,active=owner.Active!=null,session=owner.Session.Screenshots.Count,windowMode=owner.WindowMode,acquiring=owner.WindowAcquiring,feedback=((System.Windows.Forms.ToolStripMenuItem)Field("feedback")!).Text,
            windows=Application.Current.Windows.Cast<Window>().Select(w=>new { type=w.GetType().Name,w.Title,w.IsVisible,w.IsActive,owned=w is AnnotationOverlay o && owner.IsCurrent(o),monitor=w is AnnotationOverlay a ? a.Frame.Monitor.Device : null,description=w is PromptWindow p ? p.Description : null }).ToArray(),monitors=Native.Monitors().Select(m=>new { m.Device,m.Primary }).ToArray() };
    }
    private static bool IsCrimson(BitmapSource image) { var pixel=new byte[4];image.CopyPixels(new Int32Rect(image.PixelWidth/2,image.PixelHeight/2,1,1),pixel,4,0);return pixel[2]>180 && pixel[1]<40 && pixel[0]<100; }
    private static void CheckImage(BitmapSource image,Action<bool,string> check,string name)=>check(image.IsFrozen && image.PixelWidth>200 && image.PixelHeight>150 && IsCrimson(image),name+" contains target pixels, never foreground occluder/ScreenIt chrome");
    private static void Save(BitmapSource image,string path) { using var file=File.Create(path);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(image));png.Save(file); }
    internal static void Place(nint h,int x,int y,int w,int height)=>SetWindowPos(h,0,x,y,w,height,0x14);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(nint h,nint after,int x,int y,int w,int height,uint flags);
    [DllImport("user32.dll")] private static extern nint SendMessage(nint h,uint message,nint w,nint l);
    [DllImport("user32.dll",EntryPoint="CreateWindowExW",CharSet=CharSet.Unicode)] private static extern nint CreateWindowEx(uint ex,string cls,string title,uint style,int x,int y,int width,int height,nint parent,nint menu,nint instance,nint parameter);
}
