using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;

internal static class SettingsMotionChecks
{
    private static IEnumerable<T> Children<T>(DependencyObject root) where T:DependencyObject
    {
        if(root is T value) yield return value;
        foreach(var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
            foreach(var item in Children<T>(child)) yield return item;
    }
    internal static async Task Run(Action<bool,string> check)
    {
        string folder=Path.Combine(Path.GetTempPath(),"ScreenItMotionChecks",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
        try
        {
            using var owner=new Coordinator(showTray:false,registerHotkey:false);owner.PreferencesPath=Path.Combine(folder,"settings.json");
            owner.ShowSettings();var window=owner.Settings!;
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            window.SelectPage(1);await Dispatcher.Yield(DispatcherPriority.Loaded);var section=Children<ScrollViewer>((DependencyObject)window.Content).Single();
            check(!SystemParameters.ClientAreaAnimation || section.HasAnimatedProperties,"Visible page starts finite transition when Windows animations are enabled");
            for(int i=0;i<8;i++) window.SelectPage(i%3);
            window.SelectPage(0);await Task.Delay(200);
            section=Children<ScrollViewer>((DependencyObject)window.Content).Single();
            check(Math.Abs(section.Opacity-1)<.001 && section.RenderTransform.Value.IsIdentity,"Rapid page changes settle fully visible without layout offset");
            var combo=Children<ComboBox>((DependencyObject)window.Content).First();combo.ApplyTemplate();
            var popup=(Popup)combo.Template.FindName("PART_Popup",combo);
            check(popup.PopupAnimation==(SystemParameters.ClientAreaAnimation ? PopupAnimation.Fade : PopupAnimation.None),"Dropdown respects Windows reduced-motion preference");
            for(int i=0;i<4;i++) { combo.IsDropDownOpen=true;await Task.Delay(20);combo.IsDropDownOpen=false; }
            await Task.Delay(180);check(!popup.IsOpen && !combo.IsDropDownOpen,"Repeated dropdown open/close ends closed");
            ToggleButton Option()=>Children<ToggleButton>((DependencyObject)window.Content).Single(t=>AutomationProperties.GetName(t)==L.T("Disable session clear warning"));
            for(int i=0;i<5;i++)
            {
                var toggle=Option();toggle.IsChecked=toggle.IsChecked!=true;toggle.RaiseEvent(new RoutedEventArgs(ToggleButton.ClickEvent));
                await Task.Delay(25);
            }
            await Task.Delay(200);var settled=Option();settled.ApplyTemplate();
            var thumb=(Border)settled.Template.FindName("SwitchThumb",settled);
            check(owner.Preferences.DisableClearConfirmation && Preferences.Load(owner.PreferencesPath).DisableClearConfirmation && thumb.RenderTransform is TranslateTransform { X:16 },"Repeated switch toggles persist immediately and settle at correct thumb position after Refresh");
            var provider=(IToggleProvider)UIElementAutomationPeer.CreatePeerForElement(settled)!.GetPattern(PatternInterface.Toggle)!;
            provider.Toggle();await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            check(!owner.Preferences.DisableClearConfirmation && !Preferences.Load(owner.PreferencesPath).DisableClearConfirmation && Option().IsChecked==false,"UI Automation toggle updates real preference and persisted state");
            var dialog=new ClearSessionDialog(1);dialog.Show();await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            dialog.DoNotAskAgain.IsChecked=true;await Task.Delay(25);dialog.DoNotAskAgain.IsChecked=false;await Task.Delay(160);
            var tick=(System.Windows.Shapes.Path)dialog.DoNotAskAgain.Template.FindName("Tick",dialog.DoNotAskAgain);
            check(tick.Opacity==0 && dialog.DoNotAskAgain.IsChecked==false,"Interrupted checkbox fade settles unchecked");dialog.Close();
            window.SelectPage(1);window.Close();await Task.Delay(200);
            check(owner.Settings==null && !window.IsVisible && !window.RecorderInstalled,"Closing during page transition releases Settings and recorder");
        }
        finally { foreach(string file in Directory.EnumerateFiles(folder)) File.Delete(file);Directory.Delete(folder); }
    }
}
