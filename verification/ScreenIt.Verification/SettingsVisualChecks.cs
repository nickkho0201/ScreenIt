using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Reflection;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
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
                    }
                }
                finally {window.Close();}
            }
        }
        finally {L.Select(language);Appearance.Choose(theme);}
        return Task.CompletedTask;
    }
}
