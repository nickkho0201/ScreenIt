using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

internal static class ClearPreferenceChecks
{
    private static IEnumerable<T> Children<T>(DependencyObject root) where T:DependencyObject
    {
        if(root is T value) yield return value;
        foreach(var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
            foreach(var item in Children<T>(child)) yield return item;
    }
    private static void Add(Coordinator owner)
    {
        var shot=owner.Session.Commit(owner.Session.CreateDraft(16,16));
        var image=BitmapSource.Create(16,16,96,96,PixelFormats.Bgr32,null,new byte[16*16*4],16*4);image.Freeze();
        owner.Rasters.Add(shot.Id,new(image,image));
    }
    private static void Save(FrameworkElement view,string name)
    {
        view.UpdateLayout();
        var bitmap=new RenderTargetBitmap((int)Math.Ceiling(view.ActualWidth),(int)Math.Ceiling(view.ActualHeight),96,96,PixelFormats.Pbgra32);
        var visual=new DrawingVisual();using(var dc=visual.RenderOpen())
        { dc.DrawRectangle(Appearance.Palette.Surface,null,new Rect(0,0,view.ActualWidth,view.ActualHeight));dc.DrawRectangle(new VisualBrush(view),null,new Rect(0,0,view.ActualWidth,view.ActualHeight)); }
        bitmap.Render(visual);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));
        string folder=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../artifacts/clear-preferences"));Directory.CreateDirectory(folder);
        using var output=File.Create(Path.Combine(folder,name+".png"));encoder.Save(output);
    }
    internal static async Task Run(Action<bool,string> check)
    {
        string folder=Path.Combine(Path.GetTempPath(),"ScreenItClearChecks",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
        string path=Path.Combine(folder,"settings.json");var theme=Appearance.Preference;string language=L.Language;
        try
        {
            foreach(string json in new[]{"{}","{\"theme\":\"light\",\"future\":42}","{\"disableClearConfirmation\":\"true\"}","{\"disableClearConfirmation\":null}"})
            { File.WriteAllText(path,json);check(!Preferences.Load(path).DisableClearConfirmation,"Old/malformed field keeps confirmation: "+json); }
            File.WriteAllText(path,"{\"theme\":\"light\",\"future\":42}");var prefs=Preferences.Load(path);
            prefs.DisableClearConfirmation=true;prefs.Save(path);
            check(Preferences.Load(path).DisableClearConfirmation && File.ReadAllText(path).Contains("\"future\": 42"),"Opt-out persists and preserves old/unknown fields");
            prefs.DisableClearConfirmation=false;prefs.Save(path);
            using var owner=new Coordinator(showTray:false,registerHotkey:false,preferences:prefs);owner.PreferencesPath=path;
            Add(owner);
            foreach(string close in new[]{"cancel","escape","enter","x"})
            {
                owner.Clear();await Dispatcher.Yield(DispatcherPriority.ContextIdle);var dialog=owner.ClearDialog!;
                dialog.DoNotAskAgain.IsChecked=true;
                if(close=="cancel") dialog.Complete(false);
                else if(close=="x") dialog.Close();
                else dialog.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(dialog),0,close=="escape" ? Key.Escape : Key.Enter) { RoutedEvent=Keyboard.PreviewKeyDownEvent });
                check(!owner.ConfirmationOpen && owner.Session.Screenshots.Count==1 && owner.Rasters.Count==1 && !Preferences.Load(path).DisableClearConfirmation,"Checked + "+close+" preserves session and preference");
            }
            owner.Clear();owner.ClearDialog!.Complete(true);
            check(owner.Session.Screenshots.Count==0 && owner.Rasters.Count==0 && !prefs.DisableClearConfirmation,"Ordinary confirm clears without opting out");
            owner.ShowSettings();
            Add(owner);owner.Clear();var confirmation=owner.ClearDialog!;confirmation.DoNotAskAgain.IsChecked=true;confirmation.Complete(true);
            check(prefs.DisableClearConfirmation && Preferences.Load(path).DisableClearConfirmation && owner.Toasts.LastShown?.Title=="Session cleared","Accept opt-out saves and emits cleared toast");
            check(Children<ToggleButton>(owner.Settings!).Single(t=>AutomationProperties.GetName(t)==L.T("Disable session clear warning")).IsChecked==true,"Accepted opt-out synchronizes an already open Settings window");owner.Settings!.Close();
            Add(owner);owner.RequestClear();await Dispatcher.Yield(DispatcherPriority.ContextIdle);
            check(!owner.ConfirmationOpen && owner.Session.Screenshots.Count==0 && owner.Rasters.Count==0 && owner.Session.NextLetter=="A" && owner.Toasts.LastShown?.Title=="Session cleared","Subsequent queued Clear skips dialog, clears both stores, resets numbering and toasts");
            using(var reloaded=new Coordinator(showTray:false,registerHotkey:false,preferences:Preferences.Load(path)))
            { Add(reloaded);reloaded.Clear();check(!reloaded.ConfirmationOpen && reloaded.Session.Screenshots.Count==0,"Reloaded preference skips confirmation"); }
            check(owner.ChangeClearConfirmation(false),"Re-enable confirmation saves");
            Add(owner);owner.Clear();check(owner.ClearDialog!=null,"Re-enabled confirmation opens again");owner.ClearDialog!.Complete(false);
            var settings=new SettingsWindow(owner);
            try
            {
                foreach(var selectedTheme in new[]{UiTheme.Light,UiTheme.Dark})
                foreach(string lang in new[]{"en","ru"})
                {
                    Appearance.Select(selectedTheme);L.Select(lang);settings.Refresh();
                    var root=(FrameworkElement)settings.Content;root.Measure(new Size(736,544));root.Arrange(new Rect(0,0,736,544));root.UpdateLayout();
                    var checkbox=Children<ToggleButton>(settings).Single(t=>AutomationProperties.GetName(t)==L.T("Disable session clear warning"));
                    check(AutomationProperties.GetName(checkbox)==L.T("Disable session clear warning") && new ToggleButtonAutomationPeer(checkbox).GetPattern(PatternInterface.Toggle)!=null,"Accessible localized checkbox "+selectedTheme+" "+lang);
                    Save((FrameworkElement)settings.Content,"settings-"+selectedTheme+"-"+lang);
                    owner.Clear();await Dispatcher.Yield(DispatcherPriority.ContextIdle);
                    check(AutomationProperties.GetName(owner.ClearDialog!.DoNotAskAgain)==L.T("Don't ask again"),"Localized dialog checkbox "+selectedTheme+" "+lang);
                    Save((FrameworkElement)owner.ClearDialog.Content,"dialog-"+selectedTheme+"-"+lang);
                    owner.ClearDialog.DoNotAskAgain.IsChecked=true;
                    await Task.Delay(160);
                    Save((FrameworkElement)owner.ClearDialog.Content,"dialog-checked-"+selectedTheme+"-"+lang);
                    L.Select(lang=="ru" ? "en" : "ru");
                    check(owner.ClearDialog.DoNotAskAgain.IsChecked==true && AutomationProperties.GetName(owner.ClearDialog.DoNotAskAgain)==L.T("Don't ask again"),"Live language refresh preserves checked state "+selectedTheme+" "+lang);
                    L.Select(lang);owner.ClearDialog.Complete(false);
                }
                var option=Children<ToggleButton>(settings).Single(t=>AutomationProperties.GetName(t)==L.T("Disable session clear warning"));option.IsChecked=true;option.RaiseEvent(new RoutedEventArgs(ToggleButton.ClickEvent));
                check(prefs.DisableClearConfirmation && Preferences.Load(path).DisableClearConfirmation,"General checkbox disables confirmation");
                option=Children<ToggleButton>(settings).Single(t=>AutomationProperties.GetName(t)==L.T("Disable session clear warning"));option.IsChecked=false;option.RaiseEvent(new RoutedEventArgs(ToggleButton.ClickEvent));
                check(!prefs.DisableClearConfirmation && !Preferences.Load(path).DisableClearConfirmation,"General checkbox restores confirmation");
                owner.PreferencesPath=folder;option=Children<ToggleButton>(settings).Single(t=>AutomationProperties.GetName(t)==L.T("Disable session clear warning"));option.IsChecked=true;option.RaiseEvent(new RoutedEventArgs(ToggleButton.ClickEvent));
                check(!prefs.DisableClearConfirmation && Children<ToggleButton>(settings).Single(t=>AutomationProperties.GetName(t)==L.T("Disable session clear warning")).IsChecked==false && settings.ValidationError.Length>0,"Settings save failure rolls back checkbox");
            }
            finally { settings.Close(); }
            owner.Clear();owner.ClearDialog!.DoNotAskAgain.IsChecked=true;owner.ClearDialog.Complete(true);
            check(!prefs.DisableClearConfirmation && owner.Session.Screenshots.Count==0 && owner.Toasts.LastShown is { Title:"Session cleared",Warning:true },"Dialog save failure still clears explicitly accepted session and reports failure");
            Add(owner);owner.Clear();check(owner.ClearDialog!=null,"Save failure retains future confirmation");owner.ClearDialog!.Complete(false);
        }
        finally
        {
            Appearance.Choose(theme);L.Select(language);
            foreach(string file in Directory.EnumerateFiles(folder)) File.Delete(file);Directory.Delete(folder);
        }
    }
}
