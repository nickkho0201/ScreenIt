using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ScreenIt.Core;

internal static class AppearanceChecks
{
    internal static async Task Run(Action<bool,string> check,Coordinator c)
    {
        var root=Path.Combine(Path.GetTempPath(),"ScreenItAppearanceVerification",Guid.NewGuid().ToString("N"));var path=Path.Combine(root,"settings.json");
        try
        {
            check(Preferences.Load(path).Theme==ThemePreference.System,"Missing theme defaults System");
            var preferences=new Preferences { Theme=ThemePreference.Light };preferences.Save(path);check(Preferences.Load(path).Theme==ThemePreference.Light && System.Text.Json.JsonDocument.Parse(File.ReadAllText(path)).RootElement.GetProperty("schemaVersion").GetInt32()==2,"Light theme versioned schema roundtrip");
            preferences.Theme=ThemePreference.Dark;preferences.Save(path);check(Preferences.Load(path).Theme==ThemePreference.Dark && Directory.GetFiles(root).Length==1,"Atomic theme replacement leaves only settings file");
            File.WriteAllText(path,"bad-json");check(Preferences.Load(path).Theme==ThemePreference.System,"Malformed theme falls back System");File.WriteAllText(path,"{\"theme\":\"unknown\"}");check(Preferences.Load(path).Theme==ThemePreference.System,"Unknown theme fallback");
            File.WriteAllText(path,"[]");check(Preferences.Load(path).Theme==ThemePreference.System,"Non-object settings falls back System");
            File.WriteAllText(path,new string('x',65537));check(Preferences.Load(path).Theme==ThemePreference.System,"Oversized settings rejected");
        }
        finally { if(File.Exists(path)) File.Delete(path);if(Directory.Exists(root)) Directory.Delete(root); }
        await c.Capture();var overlay=Application.Current.Windows.OfType<AnnotationOverlay>().First(w=>w.Frame.Monitor.Primary);overlay.ChooseRegion(new(20,20,600,400));DiscoverabilityChecks.Run(check,c,overlay);overlay.BeginEdit(new(100,100));overlay.CommentInput.Text="synthetic";
        foreach(var theme in new[]{UiTheme.Light,UiTheme.Dark})
        {
            Appearance.Select(theme);check(overlay.AppliedTheme==theme && overlay.Editing && overlay.CommentInput.Text=="synthetic","Live overlay theme preserves edit "+theme);
            check(overlay.CommentInput.Background==Appearance.Palette.Editor && overlay.CommentInput.Foreground==Appearance.Palette.Text && overlay.CommentInput.CaretBrush==Appearance.Palette.Text,"Editor readable background/text/caret "+theme);
            using var service=new ToastService();service.Show(ToastMessage.Cleared());check(service.Current?.AppliedTheme==theme,"Toast uses selected theme "+theme);service.Hide();
        }
        overlay.CancelEdit();c.Cancel();c.Session.Commit(c.Session.CreateDraft(100,100));c.ConfirmClear=null;c.Clear();await Dispatcher.Yield(DispatcherPriority.Render);
        foreach(var theme in new[]{UiTheme.Light,UiTheme.Dark})
        {
            Appearance.Select(theme);check(c.ClearDialog?.AppliedTheme==theme && c.ClearDialog.Background==Appearance.Palette.Surface,"Live confirmation surface theme "+theme);
            var panel=(FrameworkElement)c.ClearDialog!.Content;panel.UpdateLayout();
            var visual=new DrawingVisual();using(var dc=visual.RenderOpen()) dc.DrawRectangle(new VisualBrush(panel),null,new Rect(0,0,panel.ActualWidth,panel.ActualHeight));
            var image=new RenderTargetBitmap((int)Math.Ceiling(panel.ActualWidth),(int)Math.Ceiling(panel.ActualHeight),96,96,PixelFormats.Pbgra32);image.Render(visual);
            var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));
            var preview=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../artifacts/clear-"+theme.ToString().ToLowerInvariant()+".png"));using(var stream=File.Create(preview)) encoder.Save(stream);
        }
        c.ClearDialog!.Complete(false);c.Clear(true);c.Toasts.Hide();
    }
}
