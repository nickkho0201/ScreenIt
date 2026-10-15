using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Reflection;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

// Hidden production UI; only WPF input read-only state is driven by the verification assembly.
// No copied templates, native input, foreground activation, tray or user preferences writes.
internal static class SettingsVisualChecks
{
    private static IEnumerable<T> Children<T>(DependencyObject root) where T:DependencyObject
    {
        if(root is T value) yield return value;
        for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)
            foreach(var child in Children<T>(VisualTreeHelper.GetChild(root,i))) yield return child;
    }
    private static void InputState(DependencyObject target,Type owner,string name,bool value)
    {
        var key=(DependencyPropertyKey)owner.GetField(name,BindingFlags.Static|BindingFlags.NonPublic)!.GetValue(null)!;
        target.SetValue(key,value);
    }
    private static bool Same(object? left,object? right) => left is SolidColorBrush a && right is SolidColorBrush b && a.Color==b.Color;
    private static byte[] Pixels(FrameworkElement element)
    {
        var bitmap=new RenderTargetBitmap((int)Math.Ceiling(element.ActualWidth),(int)Math.Ceiling(element.ActualHeight),96,96,PixelFormats.Pbgra32);
        var visual=new DrawingVisual();using(var dc=visual.RenderOpen()) dc.DrawRectangle(new VisualBrush(element),null,new Rect(0,0,element.ActualWidth,element.ActualHeight));
        bitmap.Render(visual);var bytes=new byte[bitmap.PixelWidth*bitmap.PixelHeight*4];bitmap.CopyPixels(bytes,bitmap.PixelWidth*4,0);return bytes;
    }
    private static RenderTargetBitmap Snapshot(FrameworkElement element)
    {
        var bitmap=new RenderTargetBitmap((int)Math.Ceiling(element.ActualWidth),(int)Math.Ceiling(element.ActualHeight),96,96,PixelFormats.Pbgra32);
        var visual=new DrawingVisual();using(var dc=visual.RenderOpen()) dc.DrawRectangle(new VisualBrush(element),null,new Rect(0,0,element.ActualWidth,element.ActualHeight));
        bitmap.Render(visual);bitmap.Freeze();return bitmap;
    }
    private static void Save(BitmapSource bitmap,string name)
    {
        var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));
        string path=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../artifacts/settings-"+name+".png"));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);using var output=File.Create(path);encoder.Save(output);
    }
    private static void Reset(ToggleButton toggle)
    {
        InputState(toggle,typeof(UIElement),"IsMouseOverPropertyKey",false);
        InputState(toggle,typeof(ButtonBase),"IsPressedPropertyKey",false);
        InputState(toggle,typeof(UIElement),"IsKeyboardFocusedPropertyKey",false);
    }
    internal static Task Run(Action<bool,string> check)
    {
        var language=L.Language;var theme=Appearance.Preference;
        using var owner=new Coordinator(showTray:false,registerHotkey:false);
        try
        {
            foreach(var lang in new[]{"ru","en"}) foreach(var selected in new[]{ThemePreference.Dark,ThemePreference.Light})
            {
                L.Select(lang);Appearance.Choose(selected);
                var window=new SettingsWindow(owner);
                try
                {
                    for(int page=0;page<3;page++)
                    {
                        window.SelectPage(page);var root=(FrameworkElement)window.Content;
                        root.Measure(new Size(736,544));root.Arrange(new Rect(0,0,736,544));root.UpdateLayout();
                        check(!window.IsVisible,"Deterministic Settings never shown "+lang+selected+page);
                        check(Children<TextBlock>(root).Any(t=>t.Text==L.T(new[]{"General","Hotkeys","About"}[page])),"Deterministic Settings localized page "+lang+selected+page);
                        var scroll=Children<ScrollViewer>(root).Single();
                        check(scroll.ScrollableHeight<.5,"Deterministic default Settings layout fits "+lang+selected+page);
                        foreach(var button in Children<Button>(root))
                        {
                            string label=AutomationProperties.GetName(button);var origin=button.TranslatePoint(new Point(),root);
                            check(button.ActualWidth>16 && button.ActualHeight==42 && origin.X>=0 && origin.Y>=0 && origin.X+button.ActualWidth<=root.ActualWidth+.5 && origin.Y+button.ActualHeight<=root.ActualHeight+.5,"Deterministic Settings button bounds "+lang+selected+label);
                            byte[]? normal=null,hover=null;
                            foreach(var phase in new[]{"normal","hover","pressed","disabled"})
                            {
                                InputState(button,typeof(UIElement),"IsMouseOverPropertyKey",phase is "hover" or "pressed");
                                InputState(button,typeof(ButtonBase),"IsPressedPropertyKey",phase=="pressed");
                                button.IsEnabled=phase!="disabled";root.UpdateLayout();
                                var surface=(Border)button.Template.FindName("ButtonSurface",button);
                                var expected=phase=="hover" ? (Brush)button.Resources["SettingsHover"] : phase=="pressed" ? (Brush)button.Resources["SettingsPressed"] : button.Background;
                                check(((SolidColorBrush)surface.Background).Color==((SolidColorBrush)expected).Color && (phase!="disabled" || button.Opacity==.4),"Production Settings template state "+lang+selected+label+phase);
                                var bitmap=new RenderTargetBitmap((int)Math.Ceiling(button.ActualWidth),(int)Math.Ceiling(button.ActualHeight),96,96,PixelFormats.Pbgra32);var visual=new DrawingVisual();using(var dc=visual.RenderOpen()) dc.DrawRectangle(new VisualBrush(button),null,new Rect(0,0,button.ActualWidth,button.ActualHeight));bitmap.Render(visual);
                                var pixels=new byte[bitmap.PixelWidth*bitmap.PixelHeight*4];bitmap.CopyPixels(pixels,bitmap.PixelWidth*4,0);
                                if(phase=="normal")normal=pixels;
                                if(phase=="hover") {hover=pixels;check(!normal!.SequenceEqual(pixels),"Rendered normal/hover distinct "+lang+selected+label);}
                                if(phase=="pressed")check(!hover!.SequenceEqual(pixels),"Rendered hover/pressed distinct "+lang+selected+label);
                            }
                            button.IsEnabled=true;InputState(button,typeof(UIElement),"IsMouseOverPropertyKey",false);InputState(button,typeof(ButtonBase),"IsPressedPropertyKey",false);
                        }
                        if(page==0) StartupSwitch(check,root,lang,selected);
                    }
                }
                finally {window.Close();}
            }
        }
        finally {L.Select(language);Appearance.Choose(theme);}
        return Task.CompletedTask;
    }
    // Verifies the real production startup toggle: template parts, states, accessibility and rendered output.
    private static void StartupSwitch(Action<bool,string> check,FrameworkElement root,string lang,ThemePreference selected)
    {
        string id=lang+"-"+selected.ToString().ToLowerInvariant();
        var toggle=Children<ToggleButton>(root).Single(t=>AutomationProperties.GetName(t)==L.T("Start with Windows"));
        toggle.ApplyTemplate();
        var track=(Border?)toggle.Template.FindName("SwitchTrack",toggle);
        var ring=(Border?)toggle.Template.FindName("SwitchFocus",toggle);
        var thumb=(Border?)toggle.Template.FindName("SwitchThumb",toggle);
        var card=VisualTreeHelper.GetParent(VisualTreeHelper.GetParent(toggle)) as Border;
        check(track!=null && ring!=null && thumb!=null && toggle.Template.TargetType==typeof(ToggleButton),"Production startup switch template "+id);
        check(card is { CornerRadius: { TopLeft: 6, TopRight: 6, BottomLeft: 6, BottomRight: 6 } } && Same(card.Background,Appearance.Palette.Surface) && Same(card.BorderBrush,Appearance.Palette.Border) && card.BorderThickness==new Thickness(1),"Startup switch sits on a rounded ScreenIt surface "+id);
        check(toggle.Focusable && KeyboardNavigation.GetIsTabStop(toggle) && toggle.Width==48 && toggle.Height==28 && toggle.HorizontalAlignment==HorizontalAlignment.Right,"Startup switch has compact focusable bounds on the right "+id);
        var provider=UIElementAutomationPeer.CreatePeerForElement(toggle)?.GetPattern(PatternInterface.Toggle) as IToggleProvider;
        check(provider is { ToggleState: ToggleState.Off } && AutomationProperties.GetHelpText(toggle)==L.T("Launch ScreenIt automatically when you sign in."),"Startup switch exposes an off toggle pattern and help text "+id);

        Reset(toggle);toggle.IsEnabled=true;toggle.IsChecked=false;root.UpdateLayout();
        check(Same(track!.Background,toggle.Resources["SwitchOff"]) && (thumb!.RenderTransform as TranslateTransform) is { X: 0 },"Startup switch off surface and thumb "+id);
        toggle.IsChecked=true;root.UpdateLayout();
        check(Same(track.Background,toggle.Resources["SwitchOn"]) && (thumb!.RenderTransform as TranslateTransform) is { X: 16 },"Startup switch on surface and thumb "+id);
        toggle.IsChecked=false;InputState(toggle,typeof(UIElement),"IsMouseOverPropertyKey",true);root.UpdateLayout();
        check(Same(track.Background,toggle.Resources["SwitchOffHover"]),"Startup switch off hover surface "+id);
        toggle.IsChecked=true;root.UpdateLayout();
        check(Same(track.Background,toggle.Resources["SwitchOnHover"]),"Startup switch on hover surface "+id);
        toggle.IsChecked=false;InputState(toggle,typeof(ButtonBase),"IsPressedPropertyKey",true);root.UpdateLayout();
        check(Same(track.Background,toggle.Resources["SwitchOffPressed"]),"Startup switch off pressed surface "+id);
        toggle.IsChecked=true;root.UpdateLayout();
        check(Same(track.Background,toggle.Resources["SwitchOnPressed"]),"Startup switch on pressed surface "+id);
        InputState(toggle,typeof(ButtonBase),"IsPressedPropertyKey",false);InputState(toggle,typeof(UIElement),"IsMouseOverPropertyKey",false);InputState(toggle,typeof(UIElement),"IsKeyboardFocusedPropertyKey",true);root.UpdateLayout();
        check(Same(ring!.BorderBrush,toggle.Resources["SwitchAccent"]),"Startup switch keyboard focus ring "+id);

        Reset(toggle);toggle.IsEnabled=true;toggle.IsChecked=false;root.UpdateLayout();var off=Pixels(toggle);
        toggle.IsChecked=true;root.UpdateLayout();var on=Pixels(toggle);
        toggle.IsChecked=false;InputState(toggle,typeof(UIElement),"IsMouseOverPropertyKey",true);root.UpdateLayout();var hover=Pixels(toggle);
        InputState(toggle,typeof(ButtonBase),"IsPressedPropertyKey",true);root.UpdateLayout();var pressed=Pixels(toggle);
        InputState(toggle,typeof(UIElement),"IsMouseOverPropertyKey",false);InputState(toggle,typeof(ButtonBase),"IsPressedPropertyKey",false);InputState(toggle,typeof(UIElement),"IsKeyboardFocusedPropertyKey",true);root.UpdateLayout();var focus=Pixels(toggle);
        check(!off.SequenceEqual(on) && !off.SequenceEqual(hover) && !hover.SequenceEqual(pressed) && !off.SequenceEqual(focus),"Startup switch states render distinctly "+id);

        Reset(toggle);toggle.IsEnabled=false;root.UpdateLayout();
        check(toggle.Opacity==.4,"Startup switch disabled appearance "+id);
        toggle.IsEnabled=true;

        root.UpdateLayout();
        Save(Snapshot(root),"page-general-"+id);
        if(card!=null)
        {
            ((Panel)card.Parent).Children.Remove(card);
            var host=new Border { Width=522,Background=Brushes.Transparent,Child=card };
            host.Measure(new Size(522,double.PositiveInfinity));
            host.Arrange(new Rect(0,0,522,host.DesiredSize.Height));
            foreach(var state in new[] { false,true })
            {
                toggle.IsChecked=state;host.UpdateLayout();
                Save(Snapshot(host),"startup-"+(state ? "on" : "off")+"-"+id);
            }
            toggle.IsChecked=false;
        }
    }
}
