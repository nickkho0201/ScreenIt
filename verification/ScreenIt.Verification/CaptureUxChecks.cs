using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using System.Windows.Threading;
using ScreenIt.Core;

internal static class CaptureUxChecks
{
    internal static object? Stress;
    internal static void State(Action<bool,string> check)
    {
        var state=new ReservedShortcutState();
        state.Process(0x5B,true,false,false);state.Process(0xA0,true,false,false);
        check(!state.Process(0x53,true,false,false).Suppress,"Unconfigured Win+Shift+S passes to Windows");
        state.Process(0x53,false,false,false);
        check(state.Process(0x53,true,false,true)==(true,true),"Configured exact reserved shortcut triggers and suppresses S");
        check(state.Process(0x53,true,false,true)==(true,false),"Reserved shortcut repeat suppressed without duplicate capture");
        state.Process(0xA0,false,false,true);
        check(state.Process(0x53,false,false,true)==(true,false),"Swallowed S release paired even after modifier release");
        state.Process(0xA1,true,false,true);state.Process(0x5C,true,false,true);
        check(!state.Process(0x54,true,false,true).Suppress,"Other main keys pass through reserved hook");
        state.Process(0xA2,true,false,true);check(!state.Process(0x53,true,false,true).Suppress,"Additional Ctrl is not the reserved combination");state.Process(0x53,false,false,true);state.Process(0xA2,false,false,true);
        state.Process(0xA5,true,false,true);check(!state.Process(0x53,true,false,true).Suppress,"Additional Alt is not the reserved combination");state.Process(0x53,false,false,true);state.Process(0xA5,false,false,true);
        check(!state.Process(0x53,true,true,true).Suppress,"Injected input passes through reserved hook");
        check(state.Process(0x53,true,false,true).Trigger,"Right modifier keys supported");state.Process(0x53,false,false,true);
        check(!state.Process(0x53,true,false,false).Suppress,"Disabled reserved shortcut immediately passes to Windows");
        state.Process(0x53,false,false,false);state.Process(0x53,true,false,false);
        check(!state.Process(0x53,true,false,true).Suppress,"Rebinding during an already held S does not capture a repeat");state.Process(0x53,false,false,true);
        state.Process(0x53,true,false,true);state.PublicationFailed();check(!state.Process(0x53,false,false,true).Suppress,"Failed command post leaves S release unsuppressed");
        int installs=0;using(var keys=new HotkeyRegistration(IntPtr.Zero,false))
        {
            keys.InstallReserved=()=> { installs++;return true; };
            var defaults=Hotkey.Defaults();check(keys.Replace(defaults) && installs==0,"Ordinary hotkeys require no low-level hook");
            var reserved=new Dictionary<GlobalAction,Hotkey>(defaults) { [GlobalAction.Capture]=new(12,0x53) };
            keys.InstallReserved=()=>false;check(!keys.Replace(reserved) && keys.ActionFor(1)==GlobalAction.Capture,"Hook installation failure retains ordinary binding");
            keys.InstallReserved=()=> { installs++;return true; };
            check(!keys.Replace(reserved,()=>false) && keys.ActionFor(1)==GlobalAction.Capture,"Reserved registration save failure preserves old binding");
            check(keys.Replace(reserved),"Reserved Capture accepted transactionally");
            check(keys.Replace(defaults),"Changing away restores ordinary Capture");
            reserved[GlobalAction.Paste]=reserved[GlobalAction.Capture];reserved[GlobalAction.Capture]=defaults[GlobalAction.Capture];
            int before=installs;check(keys.Replace(reserved) && installs==before,"Non-Capture Win+Shift+S uses ordinary registration without reserved interception");
        }
        string folder=Path.Combine(Path.GetTempPath(),"ScreenItCaptureUx",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);string path=Path.Combine(folder,"settings.json");
        try
        {
            File.WriteAllText(path,"{\"theme\":\"light\",\"future\":42}");var prefs=Preferences.Load(path);
            check(prefs.AnnotationModifier==AnnotationModifier.Ctrl,"Old settings default annotation modifier to Ctrl");
            foreach(var modifier in Enum.GetValues<AnnotationModifier>())
            {
                prefs.AnnotationModifier=modifier;prefs.Hotkeys[GlobalAction.Capture]=new(12,0x53);prefs.Save(path);var loaded=Preferences.Load(path);
                check(loaded.AnnotationModifier==modifier && loaded.Hotkeys[GlobalAction.Capture]==new Hotkey(12,0x53) && loaded.Theme==prefs.Theme,"Restart persistence for reserved hotkey and modifier "+modifier);
            }
            File.WriteAllText(path,"{\"annotationModifier\":\"win\"}");check(Preferences.Load(path).AnnotationModifier==AnnotationModifier.Ctrl,"Unsupported annotation modifier falls back safely");
        }
        finally { File.Delete(path);Directory.Delete(folder); }
    }
    internal static async Task Run(Action<bool,string> check)
    {
        var host=new Window();var hwnd=new WindowInteropHelper(host).EnsureHandle();
        try
        {
            using var keys=new HotkeyRegistration(hwnd);
            check(keys.Replace(Hotkey.Defaults()) && !keys.ReservedHookInstalled,"Native ordinary hotkeys install no keyboard hook");
            var reserved=Hotkey.Defaults();reserved[GlobalAction.Capture]=new(12,0x53);
            check(keys.Replace(reserved) && keys.ReservedHookInstalled,"Native reserved Capture installed before negative rebind check");
            bool conflict=Native.RegisterHotKey(IntPtr.Zero,930,0x4003,0x54);check(conflict,"Controlled external chord held for reserved rebind rollback");
            try
            {
                var unavailable=Hotkey.Defaults();unavailable[GlobalAction.Capture]=new(3,0x54);bool committed=false;
                check(!keys.Replace(unavailable,()=>committed=true) && !committed && keys.ReservedHookInstalled && keys.LastFailure?.Key==new Hotkey(3,0x54),"Failed ordinary rebind preserves native reserved hook and reports exact chord");
            }
            finally { if(conflict) Native.UnregisterHotKey(IntPtr.Zero,930); }
            check(!keys.Replace(Hotkey.Defaults(),()=>false) && keys.ReservedHookInstalled,"Native save failure preserves reserved hook");
            check(keys.Replace(Hotkey.Defaults()) && !keys.ReservedHookInstalled,"Successful ordinary rebind releases native reserved hook after negative checks");
            for(int warmup=0;warmup<3;warmup++) { keys.Replace(reserved);keys.Replace(Hotkey.Defaults()); }
            GC.Collect();GC.WaitForPendingFinalizers();
            var baseline=System.Text.Json.JsonSerializer.SerializeToElement(Native.Resources());
            for(int cycle=0;cycle<20;cycle++)
            {
                check(keys.Replace(reserved) && keys.ReservedHookInstalled,"Native reserved hook installed cycle "+cycle);
                check(keys.Replace(Hotkey.Defaults()) && !keys.ReservedHookInstalled,"Native rebind stops and joins hook thread cycle "+cycle);
            }
            keys.Replace(reserved);keys.Dispose();check(!keys.ReservedHookInstalled,"Shutdown releases reserved hook and thread");
            GC.Collect();GC.WaitForPendingFinalizers();
            var after=System.Text.Json.JsonSerializer.SerializeToElement(Native.Resources());
            Stress=new { before=baseline,after };
            check(after.GetProperty("handles").GetInt32()<=baseline.GetProperty("handles").GetInt32()+12,"Reserved hook native stress retains bounded handles");
        }
        finally { host.Close(); }
        using var owner=new Coordinator(showTray:false,registerHotkey:false);
        foreach(var modifier in Enum.GetValues<AnnotationModifier>())
        foreach(var scenario in new[]{"quick","held","pressed during drag","released before drop","space quick","space annotate"})
        {
            owner.Preferences.AnnotationModifier=modifier;await owner.Capture();
            var overlay=Application.Current.Windows.OfType<AnnotationOverlay>().First(w=>w.Frame.Monitor.Primary);
            bool held=scenario is "held" or "released before drop" or "space annotate";overlay.AnnotationHeld=()=>held;overlay.ReturnFocus();overlay.Refresh();
            check(overlay.ModifierHint.Contains(modifier.ToString(),StringComparison.Ordinal),"Hint shows configured modifier "+modifier);
            overlay.BeginPointer(new(80,90));overlay.MovePointer(new(380,290));
            held=true;overlay.UpdateModifierVisual();check(overlay.ModifierActive,"Modifier down activates glow state "+modifier);
            held=false;overlay.UpdateModifierVisual();check(!overlay.ModifierActive,"Modifier up deactivates glow state "+modifier);
            held=scenario is "held" or "pressed during drag" or "space annotate";
            var region=scenario.StartsWith("space",StringComparison.Ordinal) ? new PxRect(0,0,overlay.Frame.Monitor.Width,overlay.Frame.Monitor.Height) : new PxRect(80,90,300,200);
            var expected=new CroppedBitmap(overlay.Frame.Image,new Int32Rect(region.X,region.Y,region.Width,region.Height));
            if(scenario.StartsWith("space",StringComparison.Ordinal)) overlay.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(overlay),0,Key.Space) { RoutedEvent=Keyboard.PreviewKeyDownEvent });
            else overlay.EndPointer(new(380,290));
            check(held ? overlay.Editable && owner.Session.Screenshots.Count==0 : owner.OverlayCount==0 && owner.Session.Screenshots.Count==1,"Drop/Space modifier contract "+modifier+" "+scenario);
            if(held) { check(overlay.Crop.PixelWidth==region.Width,"Annotation crop physical dimensions");owner.Commit(overlay); }
            var shot=owner.Session.Screenshots.Single();var pair=owner.Rasters[shot.Id];
            check(EqualRgb(expected,pair.Annotated),"Frozen pixels exclude border/glow/hint in "+modifier+" "+scenario);
            check(owner.OverlayCount==0 && owner.Active==null && Mouse.Captured==null,"Capture completion releases draft/windows/mouse");owner.Clear(true);
        }
        owner.Toasts.Hide();owner.Preferences.AnnotationModifier=AnnotationModifier.Ctrl;
        await owner.Capture();var preview=Application.Current.Windows.OfType<AnnotationOverlay>().First(w=>w.Frame.Monitor.Primary);
        bool previewHeld=false;preview.AnnotationHeld=()=>previewHeld;preview.ReturnFocus();preview.BeginPointer(new(100,130));preview.MovePointer(new(800,530));
        var originalTheme=Appearance.Current;var originalLanguage=L.Language;
        try
        {
            foreach(var theme in Enum.GetValues<UiTheme>()) foreach(var language in new[]{"en","ru"})
            {
                Appearance.Select(theme);L.Select(language);
                foreach(bool held in new[]{false,true})
                {
                    previewHeld=held;preview.UpdateModifierVisual();await Task.Delay(held ? 750 : 180);await Dispatcher.Yield(DispatcherPriority.Render);
                    var root=(FrameworkElement)preview.Content;var transform=preview.DeviceTransform;
                    var image=new RenderTargetBitmap((int)Math.Round(root.ActualWidth*transform.M11),(int)Math.Round(root.ActualHeight*transform.M22),96*transform.M11,96*transform.M22,PixelFormats.Pbgra32);image.Render(root);
                    string folder=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../artifacts/capture-ux"));Directory.CreateDirectory(folder);
                    using var output=File.Create(Path.Combine(folder,$"selection-{theme}-{language}-{held}.png"));var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));encoder.Save(output);
                    check(preview.ModifierActive==held && preview.ModifierHint.Contains("Ctrl",StringComparison.Ordinal),"Themed localized modifier preview "+theme+language+held);
                }
            }
        }
        finally { owner.Cancel();L.Select(originalLanguage);Appearance.Select(originalTheme); }
    }
    private static bool EqualRgb(BitmapSource a,BitmapSource b)
    {
        if(a.PixelWidth!=b.PixelWidth || a.PixelHeight!=b.PixelHeight) return false;
        var x=new byte[a.PixelWidth*a.PixelHeight*4];var y=new byte[x.Length];a.CopyPixels(x,a.PixelWidth*4,0);b.CopyPixels(y,b.PixelWidth*4,0);
        for(int i=0;i<x.Length;i++) if(i%4!=3 && x[i]!=y[i]) return false;return true;
    }
}
