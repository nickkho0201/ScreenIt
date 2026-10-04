using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ScreenIt.Core;

internal static class ToastChecks
{
    internal static readonly List<object> Stress = [];
    private static async Task Settle() { await Task.Delay(50); GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect(); await Dispatcher.Yield(DispatcherPriority.Background); }
    public static async Task Run(Action<bool,string> check,Coordinator coordinator)
    {
        coordinator.Toasts.Hide();
        check(ToastMessage.Added("A",1)==new ToastMessage("Screenshot A added","Session · 1 screenshot"),"Commit toast singular/letter wording");
        check(ToastMessage.Added("C",3).Detail=="Session · 3 screenshots","Commit toast plural count wording");
        check(ToastMessage.Paste(new(PasteOutcome.Completed,1,1,""),true)==new ToastMessage("Session pasted","1 screenshot + comments"),"Paste success singular with real comments");
        check(ToastMessage.Paste(new(PasteOutcome.Completed,3,3,""),false)?.Detail=="3 screenshots","Text-less paste success has no comments claim");
        check(ToastMessage.Paste(new(PasteOutcome.Aborted,2,3,""),true)==new ToastMessage("Paste interrupted","2 of 3 screenshots pasted\nSession unchanged",true),"Partial paste toast preserves exact completed image request count");
        check(ToastMessage.Paste(new(PasteOutcome.Failed,0,3,""),true)?.Detail=="Nothing was pasted · Session unchanged","Preparation failure toast no false success");
        check(ToastMessage.Paste(new(PasteOutcome.Busy,0,3,""),true)==null && ToastMessage.Paste(new(PasteOutcome.Empty,0,0,""),false)==null,"Empty/busy result produces no noisy toast");
        check(ToastService.SuccessLifetime.TotalMilliseconds==1400 && ToastService.WarningLifetime.TotalMilliseconds==3000,"Bounded success/warning lifetime");
        foreach(var scale in new[]{1.0,1.25,1.5,2.0})
        {
            var work=new PxRect(-2560,-100,2560,1400);var b=ToastNative.Bounds(work,scale);
            check(b.X>=work.X && b.Y>=work.Y && b.X+b.Width<=work.X+work.Width && b.Y+b.Height<=work.Y+work.Height,"Toast physical work-area bounds/negative origin "+scale);
            check(work.X+work.Width-b.X-b.Width==(int)Math.Round(16*scale),"Toast per-monitor margin "+scale);
        }
        var tiny=ToastNative.Bounds(new(-50,-80,180,90),2);check(tiny.X>=-50 && tiny.Y>=-80 && tiny.X+tiny.Width<=130 && tiny.Y+tiny.Height<=10,"Toast clamps to unusually small work area");
        using var receiver=new TestReceiver();receiver.ShowActivated=false;receiver.Show();await Dispatcher.Yield(DispatcherPriority.Input);
        var target=PasteInput.Foreground();check(target.Hwnd!=IntPtr.Zero,"Existing foreground captured without activation request");
        using var service=new ToastService();
        foreach(var monitor in Native.Monitors())
        {
            service.Show(ToastMessage.Added("A",1),monitor);await Dispatcher.Yield(DispatcherPriority.Render);
            check(service.Current is { IsVisible:true },"Toast visible on actual monitor "+monitor.Device);
            var toast=service.Current!;var area=ToastNative.Area(monitor,IntPtr.Zero);var bounds=ToastNative.Bounds(area,Native.GetDpiForWindow(toast.Handle)/96.0);var actual=Native.WindowRect(toast.Handle);
            check(actual.Left==bounds.X && actual.Top==bounds.Y && actual.Right-actual.Left==bounds.Width && actual.Bottom-actual.Top==bounds.Height,"Actual toast HWND work-area/DPI placement "+monitor.Device);
            check(PasteInput.Foreground()==target && !toast.IsKeyboardFocusWithin && !toast.ShowActivated,"Toast does not change foreground/activate "+monitor.Device+" before="+target+" after="+PasteInput.Foreground()+" toast="+toast.Handle+" active="+toast.IsActive);
            check((ToastNative.GetWindowLongPtrW(toast.Handle,-20).ToInt64() & 0x080800A0)==0x080800A0,"Toast layered/transparent/toolwindow/noactivate styles "+monitor.Device);
            GetWindowThreadProcessId(toast.Handle,out uint pid); var owned=new PasteTarget(toast.Handle,pid);check(pid==(uint)Environment.ProcessId && PasteInput.IsOwn(owned),"Toast HWND rejected as ScreenIt-owned paste target "+monitor.Device);
        }
        var preview=new DrawingVisual();
        using(var dc=preview.RenderOpen())
        {
            int column=0;
            foreach(var message in new[]{ToastMessage.Added("A",1),new ToastMessage("Paste interrupted","2 of 3 screenshots pasted\nSession unchanged",true)})
            {
                service.Show(message);service.Current!.UpdateLayout();
                var part=new DrawingVisual();using(var inner=part.RenderOpen()) inner.DrawRectangle(new VisualBrush((Visual)service.Current.Content),null,new Rect(0,0,364,112));
                var bitmap=new RenderTargetBitmap(364,112,96,96,PixelFormats.Pbgra32);bitmap.Render(part);bitmap.Freeze();
                dc.DrawImage(bitmap,new Rect(column++*364,0,364,112));
            }
        }
        var image=new RenderTargetBitmap(728,112,96,96,PixelFormats.Pbgra32);image.Render(preview);
        var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));
        using(var output=File.Create(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../artifacts/toast-preview.png")))) encoder.Save(output);
        var old=service.Current!.Handle;service.Show(ToastMessage.Cleared());
        check(!ToastNative.IsWindow(old) && Application.Current.Windows.OfType<ToastWindow>().Count()==1,"Toast replacement destroys previous HWND without stacking");
        service.Hide();check(!ToastNative.IsWindow(old) && service.Current==null,"Explicit hide destroys toast");
        service.Show(ToastMessage.Cleared());var expiry=service.Current!.Handle;
        await Task.Delay(ToastService.SuccessLifetime+TimeSpan.FromMilliseconds(150));
        check(service.Current==null && !ToastNative.IsWindow(expiry),"DispatcherTimer auto-expiry destroys HWND");
        // Warm WPF caches first; measure real USER/process handles in batches, not managed allocation noise.
        for(int i=0;i<10;i++) { service.Show(ToastMessage.Cleared());service.Hide(); }
        await Settle();Stress.Add(new {cycle=0,counters=Native.Resources()});
        for(int i=0;i<100;i++)
        {
            service.Show(ToastMessage.Added("A",1));var first=service.Current!.Handle;
            service.Show(ToastMessage.Cleared());var second=service.Current!.Handle;
            check(!ToastNative.IsWindow(first) && Application.Current.Windows.OfType<ToastWindow>().Count()==1,"Toast replace window bounded "+i);
            service.Hide();check(!ToastNative.IsWindow(second) && !Application.Current.Windows.OfType<ToastWindow>().Any(),"Toast close destroys HWND "+i);
            if((i+1)%25==0) { await Settle();Stress.Add(new {cycle=i+1,counters=Native.Resources()}); }
        }
        var samples=Stress.Select(x=>JsonSerializer.SerializeToElement(x).GetProperty("counters")).ToArray();
        check(samples.Skip(1).All(x=>x.GetProperty("user").GetUInt32()<=samples[0].GetProperty("user").GetUInt32()+8),"100 toast cycles USER handles bounded after warmup (+8 allowance)");
        check(samples.Skip(1).All(x=>x.GetProperty("handles").GetInt32()<=samples[0].GetProperty("handles").GetInt32()+20),"100 toast cycles process handles bounded after warmup (+20 allowance)");
        check(PasteInput.Foreground()==target,"Repeated toast replacement retains receiver foreground");
        // Screenshot commit -> feedback only after actual RAM commit, then next capture hides feedback.
        coordinator.Toasts.Hide();await coordinator.Capture();
        var overlay=Application.Current.Windows.OfType<AnnotationOverlay>().First(w=>w.Frame.Monitor.Primary);overlay.ChooseRegion(new(80,80,600,400));
        int shown=coordinator.Toasts.ShownCount;overlay.BeginEdit(new(100,100));coordinator.Commit(overlay);
        check(coordinator.Session.Screenshots.Count==0 && coordinator.Toasts.ShownCount==shown,"Rejected commit while editing emits no success toast");
        overlay.CancelEdit();overlay.Model.CreateMarker(new(100,100),"synthetic");coordinator.Commit(overlay);
        check(coordinator.OverlayCount==0 && coordinator.Session.Screenshots.Count==1 && coordinator.Toasts.Current?.Message==ToastMessage.Added("A",1),"Real commit toast only after RAM screenshot and overlay closure");
        var capturedToast=coordinator.Toasts.Current!.Handle;await coordinator.Capture();
        check(coordinator.Toasts.Current==null && !ToastNative.IsWindow(capturedToast),"Capture preparation destroys active toast before frozen frames");coordinator.Cancel();
        coordinator.ConfirmClear=_=>false;shown=coordinator.Toasts.ShownCount;coordinator.Clear();
        check(coordinator.Session.Screenshots.Count==1 && coordinator.Toasts.ShownCount==shown,"Cancelled clear emits no success toast");
        coordinator.ConfirmClear=_=>true;coordinator.Clear();
        check(coordinator.Session.Screenshots.Count==0 && coordinator.Toasts.Current?.Message==ToastMessage.Cleared(),"Confirmed actual clear emits toast");coordinator.Toasts.Hide();
        shown=coordinator.Toasts.ShownCount;coordinator.Clear();check(coordinator.Toasts.ShownCount==shown,"Empty clear stays silent");
        receiver.Close();
    }
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hwnd,out uint pid);
    private sealed class TestReceiver : Window, IDisposable
    {
        public TestReceiver() { Title="ScreenIt verification receiver";Width=100;Height=80;ShowInTaskbar=false; }
        public void Dispose()=>Close();
    }
}
