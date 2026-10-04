using System.Text.Json;
using System.Windows;

internal static class Program
{
    public static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    [STAThread] public static int Main(string[] args)
    {
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        var harness = new Harness(); int exit = 0;
        app.DispatcherUnhandledException += (_, e) => { harness.Log(e.Exception.ToString()); e.Handled = true; harness.Suspend("Unexpected error; explicit cancel/restart required."); harness.Show(); };
        app.Startup += async (_, _) =>
        {
            if (args.Length == 0) { harness.Show(); return; }
            string name = args[0].TrimStart('-'); object report;
            try
            {
                report = args[0] switch
                {
                    "--self-test" => await Checks.Run(harness),
                    "--measure" => await harness.Measure(args.Length > 1 && int.TryParse(args[1], out int count) ? count : 100),
                    "--topology" => new { machine = Native.Machine(), monitors = Native.Monitors() },
                    _ => throw new ArgumentException("Use --self-test, --topology, --measure [count], optionally --report <path>.")
                };
            }
            catch (Exception e) { report = new { status = "FAIL", error = e.ToString(), messages = harness.Messages, machine = Native.Machine(), resources = Native.Resources() }; exit = 1; }
            int p = Array.IndexOf(args, "--report");
            Harness.SaveReport(name, report, p >= 0 && p + 1 < args.Length ? args[p + 1] : null);
            harness.Cancel(false, false); app.Shutdown();
        };
        app.Run(); return exit;
    }
}
