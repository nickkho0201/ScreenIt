using System.Windows;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        using var instance = new Mutex(true, @"Local\ScreenIt.MVP", out bool first);
        if (!first) return;
        try
        {
            var preferences=Preferences.Load(Appearance.SettingsPath);
            L.Select(preferences.Language);Appearance.Choose(preferences.Theme);
            var application = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            using var coordinator = new Coordinator(preferences:preferences);
            application.DispatcherUnhandledException += (_, e) =>
            {
                e.Handled = true;
                coordinator.Suspend("An interaction could not complete. Cancel/restart this capture.");
                UtilityUi.Inform("ScreenIt could not complete the interaction. Existing session data remain in memory.");
            };
            application.Run();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.GetType().Name + Environment.NewLine + ex.StackTrace);
            MessageBox.Show(L.T("ScreenIt could not start. Check the desktop runtime and whether the capture hotkey is available."), "ScreenIt", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally { instance.ReleaseMutex(); }
    }
}
