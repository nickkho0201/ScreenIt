using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ScreenIt.Core;

internal static class ToastChecks
{
    internal static readonly List<object> Stress = [];
    private static async Task Settle() { await Task.Delay(50); GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect(); await Dispatcher.Yield(DispatcherPriority.Background); }
    private static async Task WaitFor(Func<bool> predicate)
    {
        var deadline=Stopwatch.StartNew();
        while(!predicate() && deadline.Elapsed<TimeSpan.FromSeconds(6)) await Task.Delay(25);
    }
    // Offscreen WPF only: safe alongside the user's running production process.
    internal static void Static(Action<bool,string> check)
    {
        var previousTheme=Appearance.Current;var previousLanguage=L.Language;
        try
        {
            foreach(var scale in new[]{1.0,1.25,1.5,2.0})
            {
                var work=new PxRect(-2560,-100,2560,1400);var bounds=ToastNative.Bounds(work,scale);
                check(bounds.X==work.X+(work.Width-bounds.Width)/2 && work.Y+work.Height-bounds.Y-bounds.Height==(int)Math.Round(16*scale),"Offscreen bottom-center placement "+scale);
                var tiny=ToastNative.Bounds(new(-50,-80,180,90),scale,344,160);
                check(tiny.X>=-50 && tiny.Y>=-80 && tiny.X+tiny.Width<=130 && tiny.Y+tiny.Height<=10,"Offscreen small work area clamp "+scale);
            }
            var preview=new DrawingVisual();
            var sizes=new Dictionary<string,double>();
            using(var dc=preview.RenderOpen())
            {
                int row=0;
                foreach(var theme in new[]{UiTheme.Dark,UiTheme.Light})
                foreach(var language in new[]{"en","ru"})
                {
                    Appearance.Select(theme);L.Select(language);int column=0;
                    foreach(var message in new[]{ToastMessage.Added("C",3),ToastMessage.Cleared(),ToastMessage.Paste(new(PasteOutcome.Completed,3,3,""),true)!,ToastMessage.Paste(new(PasteOutcome.Aborted,2,3,""),true)!,ToastMessage.Paste(new(PasteOutcome.Failed,0,3,""),false)!,ToastMessage.Paste(new(PasteOutcome.Aborted,0,3,"") { PasteAttempted=true },false)!,ToastMessage.Started(),new ToastMessage("Could not open confirmation","Session unchanged",true),new ToastMessage("Paste interrupted",language=="ru" ? "Вставка прервана после отправки части снимков. Проверьте принимающее приложение перед повторной попыткой. Снимки и комментарии остаются в текущей сессии." : "The paste was interrupted after sending some screenshots. Check the receiving application before retrying. Your screenshots and comments remain available in the current session.",true)})
                    {
                        var toast=new ToastWindow(message);
                        try
                        {
                            var surface=(Border)toast.Content;var size=toast.MeasureContent(ToastWindow.MaxLayoutWidth);size=toast.MeasureContent(size.Width);surface.Arrange(new Rect(new Point(),size));surface.UpdateLayout();
                            var grid=(Grid)surface.Child;var badge=grid.Children.OfType<Border>().Single();var blocks=grid.Children.OfType<StackPanel>().Single().Children.OfType<TextBlock>().ToArray();
                            var text=grid.Children.OfType<StackPanel>().Single();
                            sizes[theme+language+message.Title+message.Detail]=surface.ActualWidth;
                            check(toast.Handle==IntPtr.Zero && !toast.ShowActivated && !toast.IsHitTestVisible && !toast.Focusable,"Offscreen noninteractive toast "+theme+language+message.Title);
                            check(surface.ActualWidth>=surface.MinWidth && surface.ActualWidth<=surface.MaxWidth && surface.ActualHeight>=68 && surface.ActualHeight<=130 && surface.CornerRadius==new CornerRadius(15) && surface.Padding==new Thickness(16,10,16,10),"Compact adaptive surface "+theme+language+message.Title+" size="+surface.ActualWidth+"x"+surface.ActualHeight+" desired="+surface.DesiredSize);
                            check(double.IsNaN(toast.Width) && double.IsNaN(surface.Width) && surface.MinWidth==0 && double.IsNaN(grid.Width) && grid.MinWidth==0 && grid.ColumnDefinitions.All(c=>c.Width.IsAuto) && double.IsNaN(text.Width) && text.MinWidth==0,"No width floor or fixed presenter/grid/text width "+theme+language+message.Title);
                            check(Math.Abs(surface.ActualWidth-(surface.Padding.Left+surface.Padding.Right+2+badge.Width+badge.Margin.Right+text.ActualWidth))<0.01,"Surface ends at text plus right padding "+theme+language+message.Title);
                            if(message==ToastMessage.Added("C",3) || message==ToastMessage.Cleared())
                                check(surface.ActualHeight==68 && text.ActualWidth==text.DesiredSize.Width,"Short content keeps natural width and original height "+theme+language+message.Title);
                            if(message.Detail.StartsWith("The paste was interrupted",StringComparison.Ordinal) || message.Detail.StartsWith("Вставка прервана после",StringComparison.Ordinal))
                                check(blocks[1].ActualHeight>40,"Long localized partial warning wraps "+theme+language);
                            foreach(var scale in new[]{1.0,1.25,1.5,2.0})
                            {
                                var work=new PxRect(-2560,-100,2560,1400);var bounds=ToastNative.Bounds(work,scale,size.Width,size.Height);
                                check(bounds.X==work.X+(work.Width-bounds.Width)/2,"Center follows measured toast width "+theme+language+message.Title+scale);
                            }
                            check(badge.Width==26 && badge.Height==26 && ((SolidColorBrush)badge.Background).Color.A==26 && ((SolidColorBrush)surface.BorderBrush).Color.A==18,"Subtle status accent and border "+theme+language+message.Title);
                            check(blocks[0].FontWeight==FontWeights.SemiBold && blocks[0].FontSize==13 && blocks.All(b=>b.TextWrapping==TextWrapping.Wrap && b.ActualWidth>0 && b.ActualHeight>0),"Readable wrapping and primary hierarchy "+theme+language+message.Title);
                            if(blocks.Length>1) check(blocks[1].FontSize==12 && blocks[1].Margin.Top==4 && blocks[1].Foreground==Appearance.Palette.Muted,"Secondary hierarchy "+theme+language+message.Title);
                            if(message==ToastMessage.Started()) check(blocks[0].Text==(language=="ru" ? "ScreenIt запущен в фоне" : "ScreenIt is running in the background"),"Startup translation "+language);
                            var bitmap=new RenderTargetBitmap((int)Math.Ceiling(size.Width),(int)Math.Ceiling(size.Height),96,96,PixelFormats.Pbgra32);bitmap.Render(surface);bitmap.Freeze();
                            if(language=="ru" && message==ToastMessage.Added("C",3))
                            {
                                var single=new PngBitmapEncoder();single.Frames.Add(BitmapFrame.Create(bitmap));
                                var folder=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../artifacts"));Directory.CreateDirectory(folder);
                                using var stream=File.Create(Path.Combine(folder,"toast-ru-short-"+theme.ToString().ToLowerInvariant()+".png"));single.Save(stream);
                            }
                            dc.DrawImage(bitmap,new Rect(column++*396,row*150,bitmap.PixelWidth,bitmap.PixelHeight));
                        }
                        finally { toast.Close(); }
                    }
                    row++;
                }
            }
            foreach(var theme in new[]{UiTheme.Dark,UiTheme.Light})
            foreach(var language in new[]{"en","ru"})
            {
                string prefix=theme+language;
                check(sizes[prefix+ToastMessage.Cleared().Title]<sizes[prefix+ToastMessage.Added("C",3).Title+ToastMessage.Added("C",3).Detail] && sizes[prefix+ToastMessage.Added("C",3).Title+ToastMessage.Added("C",3).Detail]<sizes[prefix+ToastMessage.Started().Title],"Clear < screenshot added < startup natural content widths "+prefix);
                check(sizes[prefix+"Could not open confirmationSession unchanged"]<sizes.Last(p=>p.Key.StartsWith(prefix,StringComparison.Ordinal)).Value,"Short warning narrower than long warning "+prefix);
            }
            L.Select("ru");
            foreach(var (n,word) in new[]{(1,"снимок"),(3,"снимка"),(5,"снимков"),(11,"снимков"),(21,"снимок"),(22,"снимка"),(114,"снимков")})
                check(L.T(ToastMessage.Added("C",n).Detail)==$"{n} {word} в сессии","Offscreen RU session plural "+n);
            L.Select("en");check(ToastMessage.Added("A",1).Detail=="1 screenshot in session" && ToastMessage.Added("C",3).Detail=="3 screenshots in session","Natural EN singular/plural");
            var image=new RenderTargetBitmap(396*9,150*4,96,96,PixelFormats.Pbgra32);image.Render(preview);
            var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));
            var root=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../artifacts"));Directory.CreateDirectory(root);
            using var output=File.Create(Path.Combine(root,"toast-offscreen-preview.png"));encoder.Save(output);
        }
        finally { Appearance.Select(previousTheme);L.Select(previousLanguage); }
    }
    public static async Task Run(Action<bool,string> check,Coordinator coordinator)
    {
        coordinator.Toasts.Hide();
        var constructionCount=coordinator.Toasts.ShownCount;coordinator.NotifyStarted();
        check(coordinator.Toasts.ShownCount==constructionCount && coordinator.Toasts.Current==null,"Tray-disabled verification runtime cannot emit startup success");
        check(ToastMessage.Added("A",1)==new ToastMessage("Screenshot A added","1 screenshot in session"),"Commit toast singular/letter wording");
        check(ToastMessage.Added("C",3).Detail=="3 screenshots in session","Commit toast plural count wording");
        L.Select("ru");
        foreach(var (n,word) in new[]{(1,"снимок"),(3,"снимка"),(5,"снимков"),(11,"снимков"),(21,"снимок"),(22,"снимка"),(114,"снимков")})
            check(L.T(ToastMessage.Added("C",n).Detail)==$"{n} {word} в сессии","Natural RU session plural "+n);
        check(L.T(ToastMessage.Started().Title)=="ScreenIt запущен в фоне","Startup RU resource");L.Select("en");
        using(var startup=new Coordinator(showTray:true,registerHotkey:false))
        {
            check(startup.Toasts.ShownCount==0,"Runtime constructor completes before startup feedback");
            startup.NotifyStarted();var window=startup.Toasts.Current!;var area=ToastNative.Area(null,IntPtr.Zero,true);var actual=Native.WindowRect(window.Handle);
            check(startup.Toasts.ShownCount==1 && window.Message==ToastMessage.Started() && startup.Settings==null && startup.OverlayCount==0,"Ready background runtime emits startup once without Settings/capture");
            var expected=ToastNative.Bounds(area,Native.GetDpiForWindow(window.Handle)/96.0,window.Width,window.Height);
            check(actual.Left==expected.X && actual.Top==expected.Y,"Startup toast bottom-center on primary work area");
            startup.NotifyStarted();check(startup.Toasts.ShownCount==1,"Repeated startup callback stays silent");
            startup.Dispose();startup.NotifyStarted();check(startup.Toasts.Current==null && !ToastNative.IsWindow(window.Handle),"Disposed startup runtime cannot notify");
        }
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
            check(b.X==work.X+(work.Width-b.Width)/2 && work.Y+work.Height-b.Y-b.Height==(int)Math.Round(16*scale),"Toast bottom-center and per-monitor bottom margin "+scale);
        }
        var tiny=ToastNative.Bounds(new(-50,-80,180,90),2);check(tiny.X>=-50 && tiny.Y>=-80 && tiny.X+tiny.Width<=130 && tiny.Y+tiny.Height<=10,"Toast clamps to unusually small work area");
        using var receiver=new TestReceiver();receiver.ShowActivated=false;receiver.Show();await Dispatcher.Yield(DispatcherPriority.Input);
        var target=PasteInput.Foreground();check(target.Hwnd!=IntPtr.Zero,"Existing foreground captured without activation request");
        using var service=new ToastService();
        foreach(var monitor in Native.Monitors())
        {
            service.Show(ToastMessage.Added("A",1),monitor);await Dispatcher.Yield(DispatcherPriority.Render);
            check(service.Current is { IsVisible:true },"Toast visible on actual monitor "+monitor.Device);
            var toast=service.Current!;var area=ToastNative.Area(monitor,IntPtr.Zero);var scale=Native.GetDpiForWindow(toast.Handle)/96.0;var bounds=ToastNative.Bounds(area,scale,toast.Width,toast.Height);var actual=Native.WindowRect(toast.Handle);
            check(actual.Left==bounds.X && actual.Top==bounds.Y && actual.Right-actual.Left==bounds.Width && actual.Bottom-actual.Top==bounds.Height,"Actual toast HWND work-area/DPI placement "+monitor.Device);
            check(PasteInput.Foreground()==target && !toast.IsKeyboardFocusWithin && !toast.ShowActivated,"Toast does not change foreground/activate "+monitor.Device+" before="+target+" after="+PasteInput.Foreground()+" toast="+toast.Handle+" active="+toast.IsActive);
            check((ToastNative.GetWindowLongPtrW(toast.Handle,-20).ToInt64() & 0x080800A0)==0x080800A0,"Toast layered/transparent/toolwindow/noactivate styles "+monitor.Device);
            check(!toast.IsHitTestVisible && !toast.Focusable && SendMessage(toast.Handle,0x84,IntPtr.Zero,IntPtr.Zero)==new IntPtr(-1) && SendMessage(toast.Handle,0x21,IntPtr.Zero,IntPtr.Zero)==new IntPtr(3),"Toast native click-through/noactivate hooks "+monitor.Device);
            check(ToastNative.Area(null,toast.Handle)==area,"Receiver HWND selects corresponding monitor "+monitor.Device);
            GetWindowThreadProcessId(toast.Handle,out uint pid); var owned=new PasteTarget(toast.Handle,pid);check(pid==(uint)Environment.ProcessId && PasteInput.IsOwn(owned),"Toast HWND rejected as ScreenIt-owned paste target "+monitor.Device);
        }
        Static(check);
        var old=service.Current!.Handle;service.Show(ToastMessage.Cleared());
        check(!ToastNative.IsWindow(old) && Application.Current.Windows.OfType<ToastWindow>().Count()==1,"Toast replacement destroys previous HWND without stacking");
        service.Hide();check(!ToastNative.IsWindow(old) && service.Current==null,"Explicit hide destroys toast");
        service.Show(ToastMessage.Cleared());var expiry=service.Current!.Handle;
        await WaitFor(()=>service.Current==null);
        check(service.Current==null && !ToastNative.IsWindow(expiry),"Hold timer and fade-out eventually destroy HWND");
        service.Show(ToastMessage.Cleared());var fading=service.Current!;service.Expire(fading);
        check(fading.IsFadingOut && ToastNative.IsWindow(fading.Handle) && service.Current==fading,"Expiry starts fade-out before closing");
        service.Show(new("Paste interrupted","Session unchanged",true));var replacement=service.Current!;
        service.Expire(fading);await Task.Delay(ToastService.FadeOutDuration+TimeSpan.FromMilliseconds(300));
        check(service.Current==replacement && !replacement.IsFadingOut && !ToastNative.IsWindow(fading.Handle),"Old expiry/animation cannot close replacement");
        service.Hide();service.Show(ToastMessage.Cleared());var hidden=service.Current!;service.Expire(hidden);service.Hide();
        check(service.Current==null && !ToastNative.IsWindow(hidden.Handle),"Hide during fade-out immediately destroys HWND");
        using(var shutdownService=new ToastService())
        {
            shutdownService.Show(ToastMessage.Cleared());var shutdown=shutdownService.Current!;shutdownService.Expire(shutdown);shutdownService.Dispose();shutdownService.Show(ToastMessage.Started());
            check(shutdownService.Current==null && !ToastNative.IsWindow(shutdown.Handle),"Dispose during animation immediately destroys HWND");
            await Task.Delay(ToastService.FadeOutDuration+TimeSpan.FromMilliseconds(300));
            check(shutdownService.Current==null,"Disposed service and stale animation cannot create feedback");
        }
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
    [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr hwnd,uint msg,IntPtr wp,IntPtr lp);
    private sealed class TestReceiver : Window, IDisposable
    {
        public TestReceiver() { Title="ScreenIt verification receiver";Width=100;Height=80;ShowInTaskbar=false; }
        public void Dispose()=>Close();
    }
}
