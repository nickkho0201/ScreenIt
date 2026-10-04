using System.Windows;
using System.Windows.Media;

internal enum UiTheme { Dark, Light }
internal sealed record UiPalette(SolidColorBrush Surface,SolidColorBrush Text,SolidColorBrush Muted,SolidColorBrush Border,SolidColorBrush Editor,SolidColorBrush AccentText,SolidColorBrush Warning);
internal static class Appearance
{
    private static SolidColorBrush B(byte r,byte g,byte b) { var brush=new SolidColorBrush(Color.FromRgb(r,g,b));brush.Freeze();return brush; }
    private static readonly UiPalette dark=new(B(32,39,51),B(243,245,248),B(176,184,198),B(59,71,87),B(20,25,34),B(166,181,255),B(242,178,133));
    private static readonly UiPalette light=new(B(255,255,255),B(30,41,59),B(100,116,139),B(203,213,225),B(255,255,255),B(66,85,197),B(151,85,20));
    internal static ThemePreference Preference { get; private set; }=ThemePreference.System;
    internal static Func<UiTheme> ReadSystem { get; set; }=()=> { try { return Microsoft.Win32.Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize","AppsUseLightTheme",1) is int value && value==0 ? UiTheme.Dark : UiTheme.Light; } catch(System.Security.SecurityException) { return UiTheme.Light; } };
    internal static void Choose(ThemePreference preference) { Preference=preference;SystemChanged(); }
    internal static void SystemChanged() => Select(Preference==ThemePreference.System ? ReadSystem() : Preference==ThemePreference.Dark ? UiTheme.Dark : UiTheme.Light);
    internal static UiTheme Current { get; private set; }=UiTheme.Dark;
    internal static UiPalette Palette => Current==UiTheme.Dark ? dark : light;
    internal static string SettingsPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"ScreenIt","settings.json");
    internal static void Select(UiTheme theme)
    {
        Current=theme;
        if(Application.Current==null) return;
        foreach(var window in Application.Current.Windows.Cast<Window>().ToArray())
        {
            if(window is AnnotationOverlay overlay) overlay.ApplyTheme();
            else if(window is ToastWindow toast) toast.ApplyTheme();
            else if(window is PromptWindow prompt) prompt.ApplyTheme();
            else if(window is SettingsWindow settings) settings.Refresh();
        }
    }
}
