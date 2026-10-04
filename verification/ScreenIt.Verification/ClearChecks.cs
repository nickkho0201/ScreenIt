using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using ScreenIt.Core;

internal static class ClearChecks
{
    internal static readonly List<object> Stress=[];
    [DllImport("user32.dll")] internal static extern bool PostMessage(IntPtr hwnd,uint msg,IntPtr wp,IntPtr lp);
    private static async Task Flush() { await Dispatcher.Yield(DispatcherPriority.ContextIdle);await Task.Delay(25); }
    private static void PressKey(Window w,Key key) => w.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(w),0,key) { RoutedEvent=Keyboard.PreviewKeyDownEvent });
    internal static async Task Run(Action<bool,string> check,Coordinator c)
    {
        c.ConfirmClear=null;c.Toasts.Hide();
        var source=c.Session.CreateDraft(500,300);source.CreateMarker(new(100,100),"synthetic");var committed=c.Session.Commit(source);
        foreach(var route in new[]{"menu","hotkey"})
        foreach(var close in new[]{"cancel","escape","x","enter"})
        {
            if(route=="menu") c.RequestClear();else PostMessage(c.ControlHandle,0x312,new IntPtr(3),IntPtr.Zero);
            await Flush();var dialog=c.ClearDialog!;var hwnd=new WindowInteropHelper(dialog).Handle;
            check(dialog.IsVisible && dialog.Topmost && dialog.Owner!=null && c.ConfirmationOpen,"Real confirmation visible/owned/tracked "+route+" "+close);
            check(dialog.CancelButton.IsDefault && !dialog.AcceptButton.IsDefault,"Cancel is sole safe default "+route+" "+close);
            c.RequestClear();await Flush();check(ReferenceEquals(dialog,c.ClearDialog) && Application.Current.Windows.OfType<ClearSessionDialog>().Count()==1,"Repeated Clear has single HWND "+route+" "+close);
            dialog.WindowState=WindowState.Minimized;await c.Capture();check(dialog.WindowState==WindowState.Normal,"Blocked command brings real confirmation back "+route+" "+close);await c.PasteSession(default);check(c.OverlayCount==0 && c.Session.Screenshots.Count==1,"Commands restricted only during actual confirmation "+route+" "+close);
            if(close=="cancel") dialog.CancelButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            else if(close=="escape") PressKey(dialog,Key.Escape);
            else if(close=="enter") { dialog.CancelButton.Focus();PressKey(dialog,Key.Enter); }
            else dialog.Close();
            await Flush();check(!c.ConfirmationOpen && !ToastNative.IsWindow(hwnd) && c.Session.Screenshots.Single()==committed,"Closing restores state and preserves session "+route+" "+close);
            await c.PasteSession(default);check(c.Toasts.LastShown?.Title=="Paste failed","Paste command restored after Cancel (own/empty target rejected by delivery, not confirmation) "+route+" "+close);
            await c.Capture();check(c.OverlayCount==Native.Monitors().Length,"Capture works after closing confirmation "+route+" "+close);c.Cancel();
        }
        c.RequestClear();await Flush();var confirmation=c.ClearDialog!;var acceptedHandle=new WindowInteropHelper(confirmation).Handle;
        confirmation.AcceptButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await Flush();
        check(!c.ConfirmationOpen && !ToastNative.IsWindow(acceptedHandle) && c.Session.Screenshots.Count==0 && c.Toasts.LastShown?.Title=="Session cleared","Actual confirm destroys HWND then clears and emits toast");
        await c.Capture();var overlay=Application.Current.Windows.OfType<AnnotationOverlay>().First(w=>w.Frame.Monitor.Primary);overlay.ChooseRegion(new(10,10,500,300));
        check(overlay.Model.Letter=="A" && overlay.Model.CreateMarker(new(50,50),"reset").Label=="A1","Capture after confirmed Clear restarts A/A1");
        overlay.Model.Delete(overlay.Model.Items.Single().Id);c.Cancel();
        c.Session.Commit(c.Session.CreateDraft(100,100));
        c.ClearDialogFactory=_=>throw new InvalidOperationException();c.Clear();check(!c.ConfirmationOpen && c.Session.Screenshots.Count==1,"Dialog creation exception cannot leave guard or clear session");c.ClearDialogFactory=n=> { var failed=new ClearSessionDialog(n);failed.Close();return failed; };
        c.Clear();check(!c.ConfirmationOpen && c.Session.Screenshots.Count==1,"Exception after tracking an unusable window cannot poison guard");
        c.ClearDialogFactory=n=>new ClearSessionDialog(n);
        // Warm real window lifecycle, then measure cache-independent USER/process handle bounds.
        for(int i=0;i<10;i++) { c.Clear();c.ClearDialog!.Complete(false); }await Settle();Stress.Add(new {cycle=0,counters=Native.Resources()});
        for(int i=0;i<100;i++)
        {
            c.Clear();var dialog=c.ClearDialog!;var hwnd=new WindowInteropHelper(dialog).Handle;dialog.Complete(false);
            check(!c.ConfirmationOpen && !ToastNative.IsWindow(hwnd) && !Application.Current.Windows.OfType<ClearSessionDialog>().Any(),"100 Clear cancel destroys window and guard "+i);
            if((i+1)%25==0) { await Settle();Stress.Add(new {cycle=i+1,counters=Native.Resources()}); }
        }
        var samples=Stress.Select(x=>JsonSerializer.SerializeToElement(x).GetProperty("counters")).ToArray();
        check(samples.Skip(1).All(x=>x.GetProperty("user").GetUInt32()<=samples[0].GetProperty("user").GetUInt32()+8),"Clear stress USER handles bounded");
        check(samples.Skip(1).All(x=>x.GetProperty("handles").GetInt32()<=samples[0].GetProperty("handles").GetInt32()+20),"Clear stress process handles bounded");
        c.Clear(true);c.Toasts.Hide();
        using var closing=new Coordinator(showTray:false,registerHotkey:false);closing.Session.Commit(closing.Session.CreateDraft(100,100));closing.Clear();var ownerWindow=new WindowInteropHelper(closing.ClearDialog!).Handle;closing.Dispose();
        check(!closing.ConfirmationOpen && !ToastNative.IsWindow(ownerWindow),"Owner/app disposal destroys pending confirmation without stale guard");
    }
    private static async Task Settle() { await Task.Delay(30);GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();await Dispatcher.Yield(DispatcherPriority.Background); }
}
