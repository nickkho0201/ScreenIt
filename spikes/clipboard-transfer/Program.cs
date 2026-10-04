using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        try
        {
            if (args.Contains("--help"))
            {
                Console.WriteLine("dotnet run -- [--self-test | --publish N | --cleanup RUN-DIRECTORY] [--report FILE] [--no-privacy]");
                return 0;
            }
            string? Option(string name)
            {
                int i = Array.IndexOf(args, name);
                return i < 0 ? null : i + 1 < args.Length ? args[i + 1] : throw new ArgumentException("Missing value: " + name);
            }
            var known = new HashSet<string>(["--help", "--self-test", "--publish", "--cleanup", "--report", "--no-privacy"]);
            for (int i = 0; i < args.Length; i++)
            {
                if (!known.Contains(args[i])) throw new ArgumentException("Unknown option: " + args[i]);
                if (args[i] is "--publish" or "--cleanup" or "--report") i++;
            }
            string? cleanup = Option("--cleanup");
            if (cleanup is not null)
            {
                Console.WriteLine("Explicit cleanup may break an outstanding file-based paste. Never clean before the receiver is finished.");
                Samples.Cleanup(cleanup);
                return 0;
            }
            if (args.Contains("--self-test") && Option("--publish") is not null) throw new ArgumentException("Choose self-test OR publish");
            using var clipboard = new NativeClipboard();
            var samples = new Samples();
            samples.Describe();
            Console.WriteLine("Only synthetic data. Publishing REPLACES your clipboard; old contents are not read, saved, or restored.");
            bool privacy = !args.Contains("--no-privacy");
            var observations = new List<object>();
            string? report = Option("--report");
            void WriteReport()
            {
                if (report is null) return;
                string path = Path.GetFullPath(report);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, JsonSerializer.Serialize(new
                {
                    generatedAt = DateTimeOffset.Now, windows = Environment.OSVersion.VersionString,
                    runtime = Environment.Version.ToString(), x64 = Environment.Is64BitProcess,
                    runDirectory = samples.DirectoryPath,
                    fixtures = samples.Files.Select((p, i) => new { path = p, width = Samples.Sizes[i].Width, height = Samples.Sizes[i].Height, sha256 = Convert.ToHexString(SHA256.HashData(samples.Pngs[i])) }),
                    observations,
                    receiverTests = "NOT TESTED: no receiving application paste was performed by this harness"
                }, new JsonSerializerOptions { WriteIndented = true }));
                Console.WriteLine("Technical report: " + path);
            }
            void Publish(int variant, bool flags)
            {
                if (variant < 1 || variant > Samples.Names.Length) throw new ArgumentException("Variant must be 1..12");
                Console.WriteLine($"\nTEST {variant}: {Samples.Names[variant - 1]} | privacy={(flags ? "ON" : "OFF")}");
                var parts = samples.Build(variant, flags);
                samples.Validate(parts, flags);
                clipboard.Publish(parts);
                string[] formats = clipboard.CheckOwn();
                observations.Add(new { variant, payload = Samples.Names[variant - 1], privacy = flags, status = "PASS", formats, time = DateTimeOffset.Now });
                Console.WriteLine("SELF-CHECK PASS (publication/readback only, NOT receiver compatibility)");
            }
            if (args.Contains("--self-test"))
            {
                int failures = 0;
                for (int variant = 1; variant <= Samples.Names.Length; variant++)
                    foreach (bool flags in new[] { false, true })
                    {
                        try { Publish(variant, flags); }
                        catch (Exception e)
                        {
                            failures++;
                            observations.Add(new { variant, privacy = flags, status = "FAIL", error = e.Message });
                            Console.WriteLine("FAIL: " + e.Message);
                        }
                    }
                // Negative controls ensure validators are not just printing PASS.
                bool Rejects(Action action)
                {
                    try { action(); return false; }
                    catch (InvalidDataException) { return true; }
                }
                var wrongPath = samples.Build(6, false);
                wrongPath[0] = wrongPath[0] with { Bytes = (byte[])wrongPath[0].Bytes.Clone() };
                wrongPath[0].Bytes[20] ^= 1;
                var badHtml = samples.Build(8, false);
                badHtml[0] = badHtml[0] with { Bytes = (byte[])badHtml[0].Bytes.Clone() };
                int endOffset = Encoding.ASCII.GetString(badHtml[0].Bytes).IndexOf("EndHTML:", StringComparison.Ordinal) + 8;
                badHtml[0].Bytes[endOffset] = (byte)'9';
                var remoteHtml = samples.Build(8, false);
                remoteHtml[0] = remoteHtml[0] with { Bytes = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(remoteHtml[0].Bytes).Replace("data:image/", "http:image/", StringComparison.Ordinal)) };
                bool negatives = Rejects(() => samples.Validate(wrongPath, false)) && Rejects(() => samples.Validate(badHtml, false)) && Rejects(() => samples.Validate(remoteHtml, false));
                observations.Add(new { check = "Negative controls: external path, broken HTML offsets, nonlocal image URI", status = negatives ? "PASS" : "FAIL" });
                if (!negatives) failures++;
                clipboard.ClearOwn();
                samples.Validate(samples.Build(6, false), false); // Replacement does not delete files.
                observations.Add(new { check = "Synthetic PNG files survive clipboard replacement", status = "PASS" });
                WriteReport();
                Samples.Cleanup(samples.DirectoryPath); // No receiver tests during self-test.
                Console.WriteLine($"\n24 publication/readback cases + negative/lifetime checks; failures={failures}");
                return failures == 0 ? 0 : 1;
            }
            string? publish = Option("--publish");
            if (publish is not null)
            {
                Publish(int.Parse(publish), privacy);
                WriteReport();
                Console.WriteLine("Process exits; eager payload and synthetic PNG files are intentionally retained. Files are NOT automatically cleaned.");
                return 0;
            }
            while (true)
            {
                Console.WriteLine("\nChoose payload:");
                for (int i = 0; i < Samples.Names.Length; i++) Console.WriteLine($"[{i + 1}] {Samples.Names[i]}");
                Console.WriteLine($"[p] Toggle privacy ({(privacy ? "ON" : "OFF")}) | [v] Verify own clipboard | [r] Replace with synthetic text control");
                Console.WriteLine("[t] Show fixed comments | [f] File status | [d] Delete this run's files (explicit) | [q] Exit, RETAIN files");
                string? input = ReadLinePumping();
                if (input is null || input.Trim().Equals("q", StringComparison.OrdinalIgnoreCase)) break;
                try
                {
                    switch (input.Trim().ToLowerInvariant())
                    {
                        case "p": privacy = !privacy; break;
                        case "v": clipboard.CheckOwn(); break;
                        case "r": Publish(12, privacy); break;
                        case "t": Console.WriteLine(Samples.Comments); break;
                        case "f": foreach (string file in samples.Files) Console.WriteLine($"{file}: exists={File.Exists(file)}"); break;
                        case "d":
                            Console.WriteLine("May break pending asynchronous reads. Type DELETE to remove ONLY this run's fixtures:");
                            if (ReadLinePumping() == "DELETE") { clipboard.ClearOwn(); Samples.Cleanup(samples.DirectoryPath); WriteReport(); return 0; }
                            break;
                        default: Publish(int.Parse(input.Trim()), privacy); break;
                    }
                }
                catch (Exception e) { Console.WriteLine("ERROR: " + e.Message); }
                WriteReport();
            }
            WriteReport();
            Console.WriteLine("Files retained at " + samples.DirectoryPath);
            Console.WriteLine("After all paste tests finish: dotnet run -- --cleanup \"" + samples.DirectoryPath + "\"");
            return 0;
        }
        catch (Exception e) { Console.Error.WriteLine(e.ToString()); return 1; }
    }
    private static string? ReadLinePumping()
    {
        // A hidden clipboard owner still needs its message queue serviced while console waits.
        var line = Task.Run(Console.ReadLine);
        while (!line.IsCompleted) { Application.DoEvents(); Thread.Sleep(25); }
        return line.GetAwaiter().GetResult();
    }
}
