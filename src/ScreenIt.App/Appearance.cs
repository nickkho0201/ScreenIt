using System.Windows;
using System.Windows.Media;

internal enum UiTheme { Dark, Light }
internal sealed record UiPalette(SolidColorBrush Surface,SolidColorBrush Text,SolidColorBrush Muted,SolidColorBrush Border,SolidColorBrush Editor,SolidColorBrush AccentText,SolidColorBrush Warning);
internal static class Appearance
{
    private static SolidColorBrush B(byte r,byte g,byte b) { var brush=new SolidColorBrush(Color.FromRgb(r,g,b));brush.Freeze();return brush; }
    // Classic reproduces the blue 0.2.3 release palette; Ember is the CONSTRUCT palette.
    private static readonly UiPalette classicDark=new(B(32,39,51),B(243,245,248),B(176,184,198),B(59,71,87),B(20,25,34),B(166,181,255),B(242,178,133));
    private static readonly UiPalette classicLight=new(B(255,255,255),B(30,41,59),B(100,116,139),B(203,213,225),B(255,255,255),B(66,85,197),B(151,85,20));
    private static readonly UiPalette emberDark=new(B(35,35,35),B(251,254,248),B(182,182,176),B(69,69,69),B(26,26,26),B(197,217,71),B(255,164,141));
    private static readonly UiPalette emberLight=new(B(241,242,236),B(26,26,26),B(79,87,81),B(201,205,196),B(251,254,248),B(26,26,26),B(148,42,23));
    internal static ThemePreference Preference { get; private set; }=ThemePreference.System;
    internal static DesignPreference Design { get; private set; }=DesignPreference.Classic;
    internal static Func<UiTheme> ReadSystem { get; set; }=()=> { try { return Microsoft.Win32.Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize","AppsUseLightTheme",1) is int value && value==0 ? UiTheme.Dark : UiTheme.Light; } catch(System.Security.SecurityException) { return UiTheme.Light; } };
    internal static void Choose(ThemePreference preference) { Preference=preference;SystemChanged(); }
    internal static void ChooseDesign(DesignPreference design) { Design=design;SystemChanged(); }
    internal static void SystemChanged() => Select(Preference==ThemePreference.System ? ReadSystem() : Preference==ThemePreference.Dark ? UiTheme.Dark : UiTheme.Light);
    internal static UiTheme Current { get; private set; }=UiTheme.Dark;
    internal static UiPalette Palette => SystemParameters.HighContrast
        ? new(SystemColors.ControlBrush,SystemColors.ControlTextBrush,SystemColors.ControlTextBrush,SystemColors.WindowTextBrush,SystemColors.WindowBrush,SystemColors.WindowTextBrush,SystemColors.WindowTextBrush)
        : Design==DesignPreference.Ember
            ? Current==UiTheme.Dark ? emberDark : emberLight
            : Current==UiTheme.Dark ? classicDark : classicLight;
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
// Design-sensitive shared tokens. Classic keeps the 0.2.3 blue UI and system type;
// Ember keeps the CONSTRUCT palette, Signal/Acid accents and bundled type.
internal static class UiDesign
{
    internal static bool IsEmber => Appearance.Design==DesignPreference.Ember;
    internal static bool IsClassic => !IsEmber;
    internal static SolidColorBrush Accent => SystemParameters.HighContrast ? SystemColors.HighlightBrush : ConstructDesign.Signal;
    internal static SolidColorBrush OnAccent => SystemParameters.HighContrast ? SystemColors.HighlightTextBrush : IsEmber ? ConstructDesign.Ink : Brushes.White;
    internal static SolidColorBrush Focus => SystemParameters.HighContrast ? SystemColors.WindowTextBrush : IsEmber ? (Appearance.Current==UiTheme.Dark ? ConstructDesign.Acid : ConstructDesign.Ink) : ConstructDesign.ClassicAccent;
}
