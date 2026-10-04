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
            Appearance.Select(Appearance.Load(Appearance.SettingsPath));
            var application = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            using var coordinator = new Coordinator();
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
            MessageBox.Show("ScreenIt could not start. Check the desktop runtime and whether the capture hotkey is available.", "ScreenIt", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally { instance.ReleaseMutex(); }
    }
}
