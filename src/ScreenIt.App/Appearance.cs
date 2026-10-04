using System.Text.Json;
using System.Windows;
using System.Windows.Media;

internal enum UiTheme { Dark, Light }
internal sealed record UiPalette(SolidColorBrush Surface,SolidColorBrush Text,SolidColorBrush Muted,SolidColorBrush Border,SolidColorBrush Editor,SolidColorBrush AccentText,SolidColorBrush Warning);
internal static class Appearance
{
    private static SolidColorBrush B(byte r,byte g,byte b) { var brush=new SolidColorBrush(Color.FromRgb(r,g,b));brush.Freeze();return brush; }
    private static readonly UiPalette dark=new(B(32,39,51),B(243,245,248),B(176,184,198),B(59,71,87),B(20,25,34),B(166,181,255),B(242,178,133));
    private static readonly UiPalette light=new(B(255,255,255),B(30,41,59),B(100,116,139),B(203,213,225),B(255,255,255),B(66,85,197),B(151,85,20));
    internal static UiTheme Current { get; private set; }=UiTheme.Dark;
    internal static UiPalette Palette => Current==UiTheme.Dark ? dark : light;
    internal static string SettingsPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"ScreenIt","settings.json");
    internal static UiTheme Load(string path)
    {
        try
        {
            if(!File.Exists(path) || new FileInfo(path).Length>256) return UiTheme.Dark;
            using var document=JsonDocument.Parse(File.ReadAllText(path),new JsonDocumentOptions { MaxDepth=4 });
            return document.RootElement.ValueKind==JsonValueKind.Object && document.RootElement.TryGetProperty("theme",out var value) && value.ValueKind==JsonValueKind.String && value.GetString()=="light" ? UiTheme.Light : UiTheme.Dark;
        }
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or JsonException) { return UiTheme.Dark; }
    }
    internal static void Save(string path,UiTheme theme)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);var temporary=path+"."+Guid.NewGuid().ToString("N")+".tmp";
        try { File.WriteAllText(temporary,theme==UiTheme.Dark ? "{\"theme\":\"dark\"}\n" : "{\"theme\":\"light\"}\n");File.Move(temporary,path,true); }
        finally { if(File.Exists(temporary)) File.Delete(temporary); }
    }
    internal static void Select(UiTheme theme)
    {
        Current=theme;
        if(Application.Current==null) return;
        foreach(var window in Application.Current.Windows.Cast<Window>().ToArray())
        {
            if(window is AnnotationOverlay overlay) overlay.ApplyTheme();
            else if(window is ToastWindow toast) toast.ApplyTheme();
            else if(window is PromptWindow prompt) prompt.ApplyTheme();
        }
    }
}
