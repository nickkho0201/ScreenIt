using System;
using ScreenIt.Core;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

internal static class SettingsChecks
{
    internal static object? Stress;
    internal static async Task Run(Action<bool,string> check)
    {
        string folder=Path.Combine(Path.GetTempPath(),"ScreenItSettingsChecks",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);string path=Path.Combine(folder,"settings.json");
        try
        {
            check(Preferences.DefaultLanguage(new CultureInfo("ru-RU"))=="ru" && Preferences.DefaultLanguage(new CultureInfo("fr-FR"))=="en","Windows language fallback RU/EN");
            var swapped=new Preferences { Language="en",Hotkeys=new() { [GlobalAction.Capture]=new(3,0x56),[GlobalAction.Paste]=new(3,0x53),[GlobalAction.Clear]=new(3,0x58) } };swapped.Save(path);
            check(Preferences.Load(path).Hotkeys[GlobalAction.Capture]==new Hotkey(3,0x56) && Preferences.Load(path).Hotkeys[GlobalAction.Paste]==new Hotkey(3,0x53),"Persisted swapped bindings load as a complete set");
            foreach(var theme in new[]{"dark","light"})
            { File.WriteAllText(path,"{\"theme\":\""+theme+"\"}");var p=Preferences.Load(path);check(p.Theme.ToString().Equals(theme,StringComparison.OrdinalIgnoreCase) && p.Hotkeys.Count==3,"0.1.0 theme-only migration "+theme);p.Save(path);check(Preferences.Load(path).Theme==p.Theme,"Migrated theme persists "+theme); }
            File.WriteAllText(path,"{\"theme\":\"light\",\"language\":\"en\",\"future\":42,\"hotkeys\":{\"capture\":{\"key\":1,\"modifiers\":0}}}");var prefs=Preferences.Load(path);
            check(prefs.Theme==ThemePreference.Light && prefs.Language=="en" && prefs.Hotkeys[GlobalAction.Capture]==new Hotkey(3,0x53),"Per-field invalid hotkey preserves valid theme/language");prefs.Save(path);check(JsonDocument.Parse(File.ReadAllText(path)).RootElement.GetProperty("future").GetInt32()==42,"Unknown settings fields preserved");
            File.WriteAllText(path,"{\"theme\":\"dark\",\"language\":\"xx\"}");check(Preferences.Load(path).Language=="en","Unsupported persisted language falls back English");
            var registrations=new HashSet<int>();int attempts=0;using(var keys=new HotkeyRegistration(IntPtr.Zero,false))
            {
                keys.Register=(id,k)=> { attempts++;return k.Key!=0x54 && registrations.Add(id); };keys.Unregister=id=>registrations.Remove(id);
                var defaults=Hotkey.Defaults();check(keys.Replace(defaults) && registrations.Count==3,"Default hotkeys registered");
                var custom=new Dictionary<GlobalAction,Hotkey>(defaults) { [GlobalAction.Capture]=new(6,0x51) };check(keys.Replace(custom) && registrations.Count==3,"Custom hotkey atomic replacement");
                var bad=new Dictionary<GlobalAction,Hotkey>(custom) { [GlobalAction.Paste]=new(3,0x54) };check(!keys.Replace(bad) && keys.ActionFor(2)==GlobalAction.Paste && registrations.Count==3,"Unavailable Windows registration retains previous bindings");
                bad[GlobalAction.Paste]=custom[GlobalAction.Capture];int before=attempts;check(!keys.Replace(bad) && attempts==before,"Duplicate hotkey rejected before native registration");
                check(!new Hotkey(0,0x53).Valid && !new Hotkey(3,0x11).Valid && !new Hotkey(8,0x4C).Valid,"Bare/modifier-only/reserved shortcuts rejected");
                check(!keys.Replace(defaults,()=>false) && registrations.Count==3,"Persistence failure rolls back staged hotkey registration");
                keys.Register=(id,k)=>k.Key!=0x53 && registrations.Add(id);check(!keys.Replace(defaults) && registrations.Count==3,"Reset failure retains all previous registrations");
                keys.Register=(id,k)=>registrations.Add(id);check(keys.Replace(defaults) && registrations.Count==3,"Atomic reset defaults succeeds");
            }
            check(registrations.Count==0,"Hotkey disposal releases every registration");
            foreach(uint vk in new uint[]{0x08,0x2E,0x2D,0x24,0x23,0x21,0x22,0x25,0x27,0x26,0x28,0x20,0x7B}) check(new Hotkey(6,vk).Valid,"Modified main key supported "+vk);
            check(new Hotkey(6,0x08).ToString()=="Ctrl+Shift+Backspace" && new Hotkey(2,0x7B).ToString()=="Ctrl+F12" && new Hotkey(12,0x25).ToString()=="Win+Shift+Left" && new Hotkey(3,0x2E).ToString()=="Ctrl+Alt+Delete","Canonical expanded key display");
            check(!new Hotkey(3,0x2E).Valid && !new Hotkey(6,0x14).Valid,"Secure attention shortcut and Caps Lock remain excluded");
            var nativeHost=new Window();var handle=new WindowInteropHelper(nativeHost).EnsureHandle();
            using(var real=new HotkeyRegistration(handle))
            {
                var defaults=Hotkey.Defaults();check(real.Replace(defaults),"Actual Win32 default hotkey registrations");
                var custom=new Dictionary<GlobalAction,Hotkey>(defaults) { [GlobalAction.Capture]=new(6,0x51) };
                check(real.Replace(custom),"Actual Win32 custom hotkey replacement");
                bool oldFree=Native.RegisterHotKey(IntPtr.Zero,900,0x4003,0x53);if(oldFree) Native.UnregisterHotKey(IntPtr.Zero,900);check(oldFree,"Old native capture binding released after success");
                bool newFree=Native.RegisterHotKey(IntPtr.Zero,901,0x4006,0x51);if(newFree) Native.UnregisterHotKey(IntPtr.Zero,901);check(!newFree,"Custom native binding remains registered");
                bool held=Native.RegisterHotKey(IntPtr.Zero,902,0x4003,0x54);check(held,"Controlled native external conflict registered");
                try { var unavailable=new Dictionary<GlobalAction,Hotkey>(custom) { [GlobalAction.Paste]=new(3,0x54) };check(!real.Replace(unavailable),"Actual Windows unavailable hotkey leaves old bindings intact"); }finally { if(held) Native.UnregisterHotKey(IntPtr.Zero,902); }
                custom[GlobalAction.Capture]=new(6,0x08);check(real.Replace(custom),"Actual Win32 Ctrl+Shift+Backspace registration");
                bool backspaceFree=Native.RegisterHotKey(IntPtr.Zero,903,0x4006,0x08);if(backspaceFree) Native.UnregisterHotKey(IntPtr.Zero,903);check(!backspaceFree,"Backspace native binding is held until reset");
                check(real.Replace(defaults),"Actual native reset defaults restores set");
            }
            nativeHost.Close();
            // Semantic numeric ordering, no prereleases/downgrades, strict URL/asset identity.
            check(GithubUpdateSource.StableVersion("v0.1.10")>GithubUpdateSource.StableVersion("v0.1.2") && GithubUpdateSource.StableVersion("v0.1.2-beta")==null,"Numeric stable semantic version comparison");
            foreach(var v in new[]{"0.1.0","0.1.1","0.1.2"}) check(GithubUpdateSource.Parse(Release(v))==null,"No downgrade or same-version update "+v);
            check(GithubUpdateSource.Parse(Release("0.1.3",draft:true))==null && GithubUpdateSource.Parse(Release("0.1.3",pre:true))==null,"Draft/prerelease excluded");
            foreach(var malformed in new[]{"{}",Release("bad"),Release("0.1.3").Replace("SHA256SUMS.txt","missing.txt"),Release("0.1.3").Replace("https://github.com","http://github.com")})
            { bool failed=false;try { GithubUpdateSource.Parse(malformed); }catch(Exception) { failed=true; }check(failed,"Malformed/missing/untrusted release rejected"); }
            check(!GithubUpdateSource.Allowed(new Uri("https://evil.example/a"),true) && GithubUpdateSource.Allowed(new Uri("https://release-assets.githubusercontent.com/a"),true),"Download redirect allowlist");
            var release=GithubUpdateSource.Parse(Release("0.1.3"))!;check(release.Version==new Version(0,1,3),"New stable release recognized");
            var downloads=new SyntheticDownloads();string prepared=await new UpdateTransfer(downloads).Prepare(release,CancellationToken.None);
            check(File.Exists(prepared) && downloads.Calls==2 && Path.GetFileName(prepared)=="ScreenIt-Setup-0.1.3.exe","Synthetic updater prepares both assets and verifies hash without executing");
            string generation=Path.GetDirectoryName(prepared)!;foreach(var f in Directory.GetFiles(generation)) File.Delete(f);Directory.Delete(generation);
            foreach(var failure in new[]{1,2,3})
            { downloads.Fail=failure;bool rejected=false;try { await new UpdateTransfer(downloads).Prepare(release,CancellationToken.None); }catch(Exception) { rejected=true; }check(rejected,"Failed download/hash never yields launchable updater result "+failure); }
            var fake=new FakeUpdates();string installer=Path.Combine(folder,"ScreenIt-Setup-0.1.3.exe"),sums=Path.Combine(folder,"SHA256SUMS.txt");File.WriteAllBytes(installer,[1,2,3,4]);string hash=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(installer)));
            File.WriteAllText(sums,hash+"  "+Path.GetFileName(installer)+"\n");await UpdateTransfer.Verify(installer,sums);check(true,"Exact installer SHA256 verified");
            foreach(var text in new[]{"",new string('0',64)+"  "+Path.GetFileName(installer),hash+"  "+Path.GetFileName(installer)+"\n"+hash+"  "+Path.GetFileName(installer)})
            { File.WriteAllText(sums,text);bool failed=false;try { await UpdateTransfer.Verify(installer,sums); }catch(InvalidDataException) { failed=true; }check(failed,"Missing/bad/ambiguous SHA256 rejects installer"); }
            bool launched=false,closed=false;check(!UpdateTransfer.LaunchVerified(installer,()=>false,_=>launched=true,()=>closed=true) && !launched && !closed,"Update RAM-loss warning Cancel does not launch/exit");
            bool launchFailure=false;try { UpdateTransfer.LaunchVerified(installer,()=>true,_=>throw new IOException(),()=>closed=true); }catch(IOException) { launchFailure=true; }check(launchFailure && !closed,"Installer launch failure keeps app/session alive");
            check(UpdateTransfer.LaunchVerified(installer,()=>true,_=>launched=true,()=>closed=true) && launched && closed,"Verified confirmed installer launch precedes graceful close");
            using var c=new Coordinator(false,false) { PreferencesPath=path };
            c.ShowSettings();var settings=c.Settings!;c.ShowSettings();check(ReferenceEquals(settings,c.Settings),"Settings single-window ownership");settings.Close();check(c.Settings==null && !c.ConfirmationOpen,"Settings Closed destroys reference/guard");
            c.ShowSettings();c.Settings!.SelectPage(1);var keyPanel=(DependencyObject)c.Settings.Content;
            var captureBinding=Buttons(keyPanel).Single(b=>b.Content is string text && text=="Ctrl+Alt+S");captureBinding.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
            check(c.Settings.Recording,"Hotkey recording entered from focused/clicked control");c.Settings.RecordShortcut(new(0,0x53));check(c.Settings.Recording && c.Preferences.Hotkeys[GlobalAction.Capture]==new Hotkey(3,0x53),"Invalid recording preserves original binding");
            c.Settings.RecordShortcut(new(6,0x51));check(!c.Settings.Recording && c.PrimaryCommands[0].ShortcutKeyDisplayString=="Ctrl+Shift+Q" && Preferences.Load(path).Hotkeys[GlobalAction.Capture]==new Hotkey(6,0x51),"Recorder commits custom binding and updates tray/persistence");
            Buttons((DependencyObject)c.Settings.Content).Single(b=>b.Content is string text && text=="Ctrl+Shift+Q").RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
            c.Settings.RecordShortcut(new(6,0x08));check(!c.Settings.Recording && c.Preferences.Hotkeys[GlobalAction.Capture]==new Hotkey(6,0x08) && Preferences.Load(path).Hotkeys[GlobalAction.Capture].ToString()=="Ctrl+Shift+Backspace","Backspace recording commits and persists canonical binding");
            check(c.ChangeHotkeys(Hotkey.Defaults()),"Coordinator reset default hotkeys");c.Settings.Close();
            c.ShowSettings();c.Session.Commit(c.Session.CreateDraft(100,100));c.Clear();await Dispatcher.Yield(DispatcherPriority.Render);
            check(c.ClearDialog?.IsVisible==true && c.Settings!.IsVisible,"Clear confirmation is visible while modeless Settings stays open");c.ClearDialog!.Complete(false);
            check(c.Session.Screenshots.Count==1 && !c.ConfirmationOpen,"Settings + Clear Cancel preserves session and releases guard");
            await c.Capture();check(c.OverlayCount==Native.Monitors().Length && !c.Settings!.IsVisible,"Capture works after Clear Cancel with Settings open");c.Cancel();c.Clear(true);c.Toasts.Hide();c.Settings!.Close();
            var beforeResources=Native.Resources();
            for(int i=0;i<100;i++) { c.ShowSettings();var hwnd=new WindowInteropHelper(c.Settings!).Handle;c.Settings!.Close();check(c.Settings==null && !ToastNative.IsWindow(hwnd),"Settings HWND destroyed cycle "+i); }
            GC.Collect();GC.WaitForPendingFinalizers();await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);var afterResources=Native.Resources();Stress=new { beforeResources,afterResources };
            var b=JsonSerializer.SerializeToElement(beforeResources);var a=JsonSerializer.SerializeToElement(afterResources);
            check(a.GetProperty("user").GetInt32()<=b.GetProperty("user").GetInt32()+10 && a.GetProperty("handles").GetInt32()<=b.GetProperty("handles").GetInt32()+30,"Settings 100 cycles HWND/USER/process handles bounded");
            check(c.Settings==null && !c.ConfirmationOpen,"100 settings cycles leave no stale state");
            var window=new SettingsWindow(c,fake);
            {
                window.Show();window.SelectPage(2);check(fake.Calls==0,"Settings/About/startup never perform update network check");await window.CheckUpdates();check(fake.Calls==1,"Update check only after explicit action");fake.Result=release;await window.CheckUpdates();check(fake.Calls==2,"New update fake provider state");await Dispatcher.Yield(DispatcherPriority.Render);SavePreview(window,"update-available");fake.Failure=true;await window.CheckUpdates();check(fake.Calls==3,"Update failure/retry fake provider state");await Dispatcher.Yield(DispatcherPriority.Render);SavePreview(window,"update-error");window.Close();
            }
            c.ShowSettings();check(c.Settings!.IsVisible,"Settings visible before capture");await c.Capture();check(!c.Settings.IsVisible && c.OverlayCount==Native.Monitors().Length,"Capture hides Settings before frozen overlays");
            var overlay=Application.Current.Windows.OfType<AnnotationOverlay>().First(w=>w.Frame.Monitor.Primary);overlay.ChooseRegion(new(20,20,500,350));overlay.BeginEdit(new(100,100));overlay.CommentInput.Text="Русский user content\nuntouched";
            var system=Appearance.ReadSystem;Appearance.ReadSystem=()=>UiTheme.Light;Appearance.Choose(ThemePreference.System);check(Appearance.Current==UiTheme.Light,"System resolves Light");Appearance.ReadSystem=()=>UiTheme.Dark;SendMessage(c.ControlHandle,0x1A,IntPtr.Zero,IntPtr.Zero);check(Appearance.Current==UiTheme.Dark && overlay.AppliedTheme==UiTheme.Dark,"Actual WM_SETTINGCHANGE updates existing System overlay");Appearance.Choose(ThemePreference.Light);SendMessage(c.ControlHandle,0x1A,IntPtr.Zero,IntPtr.Zero);check(Appearance.Current==UiTheme.Light,"Explicit Light ignores system event");Appearance.ReadSystem=system;
            foreach(var language in new[]{"en","ru"}) foreach(var (theme,effective) in new[]{(ThemePreference.System,UiTheme.Light),(ThemePreference.System,UiTheme.Dark),(ThemePreference.Dark,UiTheme.Dark),(ThemePreference.Light,UiTheme.Light)})
            {
                Appearance.ReadSystem=()=>effective;
                L.Select(language);Appearance.Choose(theme);check(overlay.Editing && overlay.CommentInput.Text=="Русский user content\nuntouched","Live language/theme preserves active comment "+language+theme);
                check(Appearance.Current==effective,"Effective theme choice "+language+theme+effective);
                check(overlay.ToolbarButtons.All(b=>b.ToolTip!=null) && overlay.ToolHighlighted(Tool.Marker),"Localized toolbar preserves tool state "+language+theme);
            }
            Appearance.ReadSystem=system;
            L.Select("ru");check(L.T("Session: 3 screenshots")=="Сессия: 3 снимка" && L.T("Screenshot A added")=="Снимок A добавлен","Localized parameterized session messages");check(L.T("3 of 4 screenshots pasted\nSession unchanged").Contains("3 из 4"),"Localized multiline paste warning");
            foreach(var key in L.EnglishKeys) { var translated=L.T(key);check(translated.Length>0 && (translated!=key || key=="ScreenIt"),"Russian resource present: "+key); }
            L.Select("en");check(L.T("Снимок A добавлен")=="Screenshot A added","Live text reverse translation to English");
            overlay.CancelEdit();c.Cancel();c.Settings!.Close();
            c.ShowSettings();foreach(var (lang,theme,page,name) in new[]{("ru",ThemePreference.Dark,0,"general-dark-ru"),("en",ThemePreference.Light,0,"general-light-en"),("ru",ThemePreference.Dark,1,"hotkeys"),("ru",ThemePreference.Dark,2,"about")})
            { c.Preferences.Language=lang;c.Preferences.Theme=theme;L.Select(lang);Appearance.Choose(theme);c.Settings!.SelectPage(page);await Dispatcher.Yield(DispatcherPriority.Render);SavePreview(c.Settings,name);check(c.Settings.AppliedTheme==Appearance.Current,"Settings palette preview "+name);
                var root=(DependencyObject)c.Settings.Content;var scroller=Descendants<ScrollViewer>(root).Single();var title=Descendants<TextBlock>(scroller).First();
                check(scroller.VerticalContentAlignment==VerticalAlignment.Top && title.TranslatePoint(new Point(),(FrameworkElement)root).Y<2,"Compact top-aligned content "+name);
                check(Buttons(root).All(button=>button.Height==42 && button.Background!=Brushes.Transparent && button.BorderBrush!=Brushes.Transparent),"Settings actions have consistent button surface "+name);
                check(((Grid)root).RowDefinitions.Count==0 && Grid.GetColumn(scroller)==1,"No shared header/spacer row above Settings content "+name);
                check(Buttons(root).All(button=>button.Cursor==System.Windows.Input.Cursors.Hand && button.HorizontalContentAlignment==HorizontalAlignment.Center && button.VerticalContentAlignment==VerticalAlignment.Center),"Settings buttons centered with Hand cursor "+name);
                check(Buttons(root).All(button=>button.Template.Triggers.OfType<Trigger>().Any(t=>t.Property==UIElement.IsMouseOverProperty && t.Setters.OfType<Setter>().Any(setter=>setter.Property==Border.BackgroundProperty && setter.TargetName=="ButtonSurface")) && button.Template.Triggers.OfType<Trigger>().Any(t=>t.Property==System.Windows.Controls.Primitives.ButtonBase.IsPressedProperty && t.Setters.OfType<Setter>().Any(setter=>setter.Property==Border.BackgroundProperty && setter.TargetName=="ButtonSurface"))),"Settings buttons have distinct hover/pressed surfaces "+name);
                check(Buttons(root).Count(button=>System.Windows.Automation.AutomationProperties.GetItemStatus(button)=="Current page" && button.FontWeight==FontWeights.SemiBold)==1,"Exactly one selected navigation state "+name);
                if(page==1) check(Descendants<TextBlock>(root).Any(text=>text.Text==L.T("Use Ctrl, Alt, Shift or Win with a letter, number, F1–F12 or navigation key.")),"Expanded hotkey description localized");
                if(page==0) { var choices=Descendants<ComboBox>(root).ToArray();check(choices.Length==2 && choices[0].Items.Count==3 && choices[1].Items.Count==2,"Theme/language dropdown options "+name);check(choices[1].Items.Cast<ComboBoxItem>().Any(item=>Equals(item.Content,"Русский")),"Russian language name is intact Unicode "+name);choices[0].IsDropDownOpen=true;await Dispatcher.Yield(DispatcherPriority.Render);check(choices[0].IsDropDownOpen,"Themed dropdown opens "+name);choices[0].IsDropDownOpen=false; }
 }
            c.Preferences.Language="en";L.Select("en");c.Settings!.SelectPage(0);
            var dropdowns=Descendants<ComboBox>((DependencyObject)c.Settings.Content).ToArray();dropdowns[0].SelectedIndex=2;
            check(c.Preferences.Theme==ThemePreference.Light && Preferences.Load(path).Theme==ThemePreference.Light,"Theme dropdown live selection persists");
            dropdowns=Descendants<ComboBox>((DependencyObject)c.Settings.Content).ToArray();dropdowns[1].SelectedIndex=1;
            check(c.Preferences.Language=="ru" && Preferences.Load(path).Language=="ru","Language dropdown live selection persists");
            c.Settings!.Close();L.Select("en");Appearance.Choose(ThemePreference.Dark);
            c.ShowSettings();foreach(var monitor in Native.Monitors())
            {
                var hwnd=new WindowInteropHelper(c.Settings!).Handle;SetWindowPos(hwnd,IntPtr.Zero,monitor.Left+40,monitor.Top+40,0,0,0x0015);await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                check(Native.GetDpiForWindow(hwnd)==monitor.EffectiveDpi,"Actual Settings window per-monitor DPI "+monitor.EffectiveDpi);
                check(((FrameworkElement)c.Settings.Content).ActualWidth>600 && c.Settings.ActualWidth>=700,"Settings layout retains readable DIP bounds "+monitor.EffectiveDpi);
            }
            c.Settings!.Close();
        }
        finally { foreach(var file in Directory.GetFiles(folder)) File.Delete(file);Directory.Delete(folder);L.Select("en"); }
    }
    private static string Release(string v,bool draft=false,bool pre=false) => JsonSerializer.Serialize(new { tag_name="v"+v,draft,prerelease=pre,html_url=$"https://github.com/nickkho0201/ScreenIt/releases/tag/v{v}",assets=new[] { new { name=$"ScreenIt-Setup-{v}.exe",browser_download_url=$"https://github.com/nickkho0201/ScreenIt/releases/download/v{v}/ScreenIt-Setup-{v}.exe" },new { name="SHA256SUMS.txt",browser_download_url=$"https://github.com/nickkho0201/ScreenIt/releases/download/v{v}/SHA256SUMS.txt" } } });
    private sealed class FakeUpdates : IUpdateSource
    {
        internal int Calls;internal bool Failure;internal UpdateRelease? Result;
        public Task<UpdateRelease?> Check(CancellationToken token) { Calls++;return Failure ? Task.FromException<UpdateRelease?>(new IOException()) : Task.FromResult(Result); }
        public Task Download(Uri uri,string path,long limit,CancellationToken token)=>throw new NotSupportedException();
    }
    private sealed class SyntheticDownloads : IUpdateSource
    {
        internal int Calls,Fail;
        public Task<UpdateRelease?> Check(CancellationToken token)=>Task.FromResult<UpdateRelease?>(null);
        public Task Download(Uri uri,string path,long limit,CancellationToken token)
        {
            Calls++;if(Fail==1) throw new IOException("Synthetic download failure");
            if(Path.GetFileName(path)=="SHA256SUMS.txt") File.WriteAllText(path,(Fail==3 ? new string('0',64) : Convert.ToHexString(SHA256.HashData(new byte[]{1,2,3,4})))+"  ScreenIt-Setup-0.1.3.exe\n");
            else { if(Fail==2) throw new IOException("Synthetic installer download failure");File.WriteAllBytes(path,[1,2,3,4]); }
            return Task.CompletedTask;
        }
    }
    private static void SavePreview(Window window,string name)
    {
        var content=(FrameworkElement)window.Content;content.UpdateLayout();var visual=new DrawingVisual();using(var dc=visual.RenderOpen()) dc.DrawRectangle(new VisualBrush(content),null,new Rect(0,0,content.ActualWidth,content.ActualHeight));var bitmap=new RenderTargetBitmap((int)Math.Ceiling(content.ActualWidth),(int)Math.Ceiling(content.ActualHeight),96,96,PixelFormats.Pbgra32);bitmap.Render(visual);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));
        string path=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../artifacts/settings-"+name+".png"));using var output=File.Create(path);encoder.Save(output);
    }
    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T:DependencyObject
    {
        if(root is T item) yield return item;
        foreach(var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>()) foreach(var descendant in Descendants<T>(child)) yield return descendant;
    }
    private static IEnumerable<Button> Buttons(DependencyObject root)
    {
        if(root is Button button) yield return button;
        foreach(var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>()) foreach(var item in Buttons(child)) yield return item;
    }
    [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr hwnd,int message,IntPtr wp,IntPtr lp);
    [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr hwnd,IntPtr after,int x,int y,int width,int height,uint flags);
}
