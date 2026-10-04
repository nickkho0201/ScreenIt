using System.Text.Json;
using System.Windows;

internal static class Program
{
    public static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    [STAThread] public static int Main(string[] args)
    {
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown }; var launcher = new Launcher(); int exit = 0;
        app.DispatcherUnhandledException += (_, e) => { launcher.Log(e.Exception.ToString()); launcher.Suspend("Unexpected error; explicit cancel/restart required."); launcher.Show(); e.Handled = true; };
        app.Startup += async (_, _) =>
        {
            if (args.Length == 0) { launcher.Show(); return; }
            object report;
            try { report = args[0] switch { "--self-test" => await Checks.Run(launcher), "--topology" => new { machine = Native.Machine(), monitors = Native.Monitors() }, _ => throw new ArgumentException("Use --self-test or --topology, optionally --report <path>.") }; }
            catch (Exception e) { exit = 1; report = new { status = "FAIL", error = e.ToString(), messages = launcher.Messages, resources = Native.Resources() }; }
            int i = Array.IndexOf(args, "--report"); Launcher.SaveReport(args[0].TrimStart('-'), report, i >= 0 && i + 1 < args.Length ? args[i + 1] : null);
            launcher.Cancel(false); launcher.ClearInspection(); app.Shutdown();
        };
        app.Run(); return exit;
    }
}
