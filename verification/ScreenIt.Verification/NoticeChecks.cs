using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ScreenIt.Core;

internal static class NoticeChecks
{
    private const string LongRussian="Вставка была прервана после отправки части снимков. Проверьте принимающее приложение перед повторной попыткой. Снимки и комментарии остаются в текущей сессии, пока ScreenIt не будет закрыт, поэтому повторная попытка может создать дубликаты.";
    private const string LongEnglish="The paste was interrupted after sending some screenshots. Check the receiving application before retrying. Your screenshots and comments remain available in the current session until ScreenIt exits, so a second attempt may create duplicates.";
    private static PxRect Rect(Window window)
    {
        var r=Native.WindowRect(new System.Windows.Interop.WindowInteropHelper(window).Handle);
        return new(r.Left,r.Top,r.Right-r.Left,r.Bottom-r.Top);
    }
    // Records the window rectangle of every rendered frame so a pre-centering corner frame cannot hide behind ContentRendered.
    private static async Task<PxRect> FirstShowPlacement(Action<bool,string> check,Window owner,Func<PromptWindow> create,string label)
    {
        var foreground=PasteInput.Foreground().Hwnd;
        var work=ToastNative.Area(null,foreground);
        var prompt=create();
        try
        {
            prompt.Prepare(owner);
            var hwnd=new System.Windows.Interop.WindowInteropHelper(prompt).Handle;
            uint dpi=Native.GetDpiForWindow(hwnd);double scale=dpi/96.0;
            check(!prompt.IsVisible,"Prepared prompt stays hidden before Show "+label);
            check(PasteInput.Foreground().Hwnd==foreground,"Preparing a prompt does not steal foreground from the receiver "+label);
            var frames=new List<PxRect>();
            void Rendered(object? sender,EventArgs e) { var r=Rect(prompt); if(frames.Count==0 || frames[^1]!=r) frames.Add(r); }
            CompositionTarget.Rendering+=Rendered;
            try
            {
                prompt.Show();
                var shown=Rect(prompt);
                check(prompt.IsVisible && shown.Width>1 && shown.Height>1,"First visible frame has final size "+label);
                check(shown.X==work.X+(work.Width-shown.Width)/2 && shown.Y==work.Y+(work.Height-shown.Height)/2,"First visible frame is already centered "+label+" rect="+shown+" work="+work);
                check(Math.Abs(shown.Width-(UiDesign.IsEmber ? 520 : 390)*scale)<=1,"First visible width follows actual HWND DPI "+label+" dpi="+dpi+" width="+shown.Width);
                check(shown.X>=work.X && shown.Y>=work.Y && shown.X+shown.Width<=work.X+work.Width && shown.Y+shown.Height<=work.Y+work.Height,"First visible frame fits work area "+label+" rect="+shown);
                for(int attempt=0;attempt<100 && frames.Count==0;attempt++) await Task.Delay(10);
                check(frames.Count>0 && frames[0]==shown,"First rendered frame already final "+label+" frame="+(frames.Count>0 ? frames[0].ToString() : "none")+" shown="+shown);
                await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                var settled=Rect(prompt);
                check(settled==shown && frames.All(f=>f==shown),"No visible reposition after first frame "+label+" settled="+settled+" frames="+frames.Count);
                return settled;
            }
            finally { CompositionTarget.Rendering-=Rendered; }
        }
        finally { prompt.Close(); }
    }
    // Modal prompts block the caller, so frames are collected across ShowDialog and every one must be final.
    private static async Task<PxRect> FirstShowModal(Action<bool,string> check,Window owner,Func<PromptWindow> create,string label)
    {
        var work=ToastNative.Area(null,PasteInput.Foreground().Hwnd);
        var prompt=create();
        try
        {
            prompt.Prepare(owner);
            var frames=new List<PxRect>();
            void Rendered(object? sender,EventArgs e) { var r=Rect(prompt); if(frames.Count==0 || frames[^1]!=r) frames.Add(r); }
            CompositionTarget.Rendering+=Rendered;
            var close=new DispatcherTimer { Interval=TimeSpan.FromMilliseconds(150) };
            close.Tick+=(_,_)=> { close.Stop();prompt.Complete(true); };
            try
            {
                close.Start();bool? accepted=prompt.ShowDialog();
                check(accepted==true && !prompt.IsVisible,"Modal prompt completes through accepted default "+label);
                check(frames.Count>0,"Modal prompt rendered a visible frame "+label);
                var final=frames.Count>0 ? frames[^1] : default;
                check(frames.All(f=>f==final),"Every modal visible frame is final "+label+" frames="+frames.Count);
                check(final.X==work.X+(work.Width-final.Width)/2 && final.Y==work.Y+(work.Height-final.Height)/2,"Modal first frame centered "+label+" rect="+final+" work="+work);
                return final;
            }
            finally { CompositionTarget.Rendering-=Rendered; }
        }
        finally { prompt.Close(); }
    }
    // The live first frame follows the actual foreground receiver; other monitors are covered by the shared frame math
    // and by moving a hidden prompt HWND onto each monitor, because Windows foreground activation is user-owned.
    internal static void PlacementGeometry(Action<bool,string> check)
    {
        foreach(var monitor in Native.Monitors())
        {
            var work=ToastNative.Area(monitor,IntPtr.Zero);
            foreach(double scale in new[]{1.0,1.25,1.5,2.0})
            {
                int width=(int)Math.Round(520*scale),height=(int)Math.Round(276*scale);
                var centered=PromptWindow.Centered(work,width,height);
                check(centered.Width==width && centered.Height==height && centered.X==work.X+(work.Width-width)/2 && centered.Y==work.Y+(work.Height-height)/2,"Centered prompt frame "+monitor.Device+" scale="+scale);
                check(centered.X>work.X && centered.Y>work.Y && centered.X+centered.Width<=work.X+work.Width && centered.Y+centered.Height<=work.Y+work.Height,"Prompt frame never starts at the work-area origin "+monitor.Device+" scale="+scale);
            }
            var probe=new PromptWindow("ScreenIt","DPI probe","OK",modal:false,cancel:false);
            try
            {
                var hwnd=new System.Windows.Interop.WindowInteropHelper(probe).EnsureHandle();
                ToastNative.Place(hwnd,new(work.X,work.Y,1,1));
                check(Native.GetDpiForWindow(hwnd)==monitor.EffectiveDpi && !probe.IsVisible,"Hidden prompt HWND reports actual monitor DPI "+monitor.Device+" expected="+monitor.EffectiveDpi);
            }
            finally { probe.Close(); }
        }
        var tiny=new PxRect(-200,-80,180,90);
        var single=PromptWindow.Centered(tiny,1,1);
        check(single.Width==1 && single.Height==1 && single.X==tiny.X+(tiny.Width-1)/2 && single.Y==tiny.Y+(tiny.Height-1)/2,"Single-pixel frame stays inside its work area "+single);
        var fitted=PromptWindow.Centered(tiny,(int)Math.Round(520*1.25),(int)Math.Round(300*1.25));
        check(fitted.Width==tiny.Width && fitted.Height==tiny.Height && fitted.X==tiny.X && fitted.Y==tiny.Y,"Small work area clamps the prompt instead of overflowing "+tiny);
        var negative=new PxRect(-3840,-360,3840,2120);
        var virtualDesktop=PromptWindow.Centered(negative,650,345);
        check(virtualDesktop.X==-3840+(3840-650)/2 && virtualDesktop.Y==-360+(2120-345)/2,"Negative-origin monitor centers on its own work area "+virtualDesktop);
    }
    internal static async Task Run(Action<bool,string> check)
    {
        var theme=Appearance.Preference;string language=L.Language;
        string folder=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../artifacts/notices"));Directory.CreateDirectory(folder);
        var backdrop=new Window { Width=1000,Height=700,WindowStyle=WindowStyle.None,ResizeMode=ResizeMode.NoResize,ShowInTaskbar=false,Background=UtilityUi.Brush(48,48,48),WindowStartupLocation=WindowStartupLocation.CenterScreen };
        try
        {
            backdrop.Show();var primary=Native.Monitors().First(m=>m.Primary);
            var backdropHwnd=new System.Windows.Interop.WindowInteropHelper(backdrop).Handle;
            ToastNative.Place(backdropHwnd,new(primary.Left,primary.Top,primary.Width,primary.Height));
            // Production policy stays "work area of the foreground receiver": the live checks use that actual work area.
            var liveWork=ToastNative.Area(null,PasteInput.Foreground().Hwnd);
            var liveMonitor=Native.Monitors().First(m=>liveWork.X>=m.Left && liveWork.X<m.Left+m.Width && liveWork.Y>=m.Top && liveWork.Y<m.Top+m.Height);
            string live=liveMonitor.Device+"@"+liveMonitor.EffectiveDpi;
            L.Select("en");Appearance.Select(UiTheme.Dark);
            var brief=await FirstShowPlacement(check,backdrop,()=>new PromptWindow("ScreenIt","ScreenIt is running in the background","OK",modal:false,cancel:false),"information "+live);
            L.Select("ru");var longRu=await FirstShowPlacement(check,backdrop,()=>new PromptWindow("ScreenIt",LongRussian,"OK",modal:false,cancel:false),"long RU information "+live);
            L.Select("en");var longEn=await FirstShowPlacement(check,backdrop,()=>new PromptWindow("ScreenIt",LongEnglish,"OK",modal:false,cancel:false),"long EN information "+live);
            var clear=await FirstShowPlacement(check,backdrop,()=>new ClearSessionDialog(3),"clear confirmation "+live);
            var modal=await FirstShowModal(check,backdrop,()=>new PromptWindow("ScreenIt","Discard this screenshot?","Discard"),"modal discard "+live);
            check(longRu.Height>brief.Height && longEn.Height==longRu.Height,"Long RU/EN text wraps to the same taller first frame "+live+" brief="+brief.Height+" ru="+longRu.Height+" en="+longEn.Height);
            check(clear.Height>brief.Height && modal.Height>=brief.Height,"Clear checkbox and modal content keep final measured height "+live+" brief="+brief.Height+" clear="+clear.Height+" modal="+modal.Height);
            check(new[]{brief,longRu,longEn,clear,modal}.All(r=>r.Width==brief.Width) && Math.Abs(brief.Width-(UiDesign.IsEmber ? 520 : 390)*liveMonitor.EffectiveDpi/96.0)<=1,"First frame width honours work-area monitor DPI "+live+" width="+brief.Width);
            PlacementGeometry(check);
            foreach(string lang in new[]{"ru","en"}) foreach(var selected in new[]{ThemePreference.Dark,ThemePreference.Light})
            {
                L.Select(lang);Appearance.Choose(selected);
                foreach(string route in new[]{"enter","escape","close","cancel","accept"})
                {
                    var dialog=new ClearSessionDialog(1);dialog.Prepare(backdrop);
                    try
                    {
                        dialog.Show();await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                        check(dialog.CancelButton.IsDefault && dialog.CancelButton.IsCancel && !dialog.AcceptButton.IsDefault,"Safe Cancel default "+lang+selected+route);
                        if(route=="enter")
                        {
                            Native.Flush();var monitor=Native.Monitors().First(m=>m.Primary);var screen=Native.Capture(monitor).Image;
                            var r=Native.WindowRect(new System.Windows.Interop.WindowInteropHelper(dialog).Handle);
                            var crop=new CroppedBitmap(screen,new Int32Rect(r.Left-monitor.Left,r.Top-monitor.Top,r.Right-r.Left,r.Bottom-r.Top));
                            using var output=File.Create(Path.Combine(folder,"clear-"+lang+selected+".png"));var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(crop));png.Save(output);
                        }
                        dialog.DoNotAskAgain.IsChecked=true;
                        if(route=="accept") dialog.Complete(true);
                        else if(route=="close") dialog.Close();
                        else if(route=="cancel") dialog.CancelButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
                        else dialog.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(dialog),0,route=="enter" ? Key.Enter : Key.Escape) { RoutedEvent=Keyboard.PreviewKeyDownEvent });
                        check(dialog.Accepted==(route=="accept") && !dialog.IsVisible,"Prompt outcome preserved "+lang+selected+route);
                    }
                    finally { dialog.Close(); }
                }
                var toast=new ToastWindow(new ToastMessage("Could not open confirmation","Session unchanged",true));
                try
                {
                    toast.ShowAt(ToastNative.Area(primary,IntPtr.Zero));await Task.Delay(160);
                    Native.Flush();var r=Native.WindowRect(toast.Handle);var screen=Native.Capture(primary).Image;
                    var crop=new CroppedBitmap(screen,new Int32Rect(r.Left-primary.Left,r.Top-primary.Top,r.Right-r.Left,r.Bottom-r.Top));
                    using var output=File.Create(Path.Combine(folder,"toast-"+lang+selected+".png"));var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(crop));png.Save(output);
                    check(!toast.IsActive && !toast.IsHitTestVisible,"Passive toast stays noninteractive "+lang+selected);
                }
                finally { toast.Close(); }
                var info=new PromptWindow("ScreenIt","ScreenIt could not complete the interaction. Existing session data remain in memory.","OK",modal:false,cancel:false);info.Prepare(backdrop);
                try { info.Show();await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);check(info.AcceptButton.IsDefault && info.CancelButton.Visibility==Visibility.Collapsed,"Information has single default OK "+lang+selected);info.Complete(true); }
                finally { info.Close(); }
            }
            ToastChecks.Static(check);
        }
        finally { backdrop.Close();Appearance.Choose(theme);L.Select(language); }
    }
}
