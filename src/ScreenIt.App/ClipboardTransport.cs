using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Media.Imaging;

internal readonly record struct ClipboardPart(uint Format, byte[] Bytes);
internal static class ClipboardPayload
{
    public static ClipboardPart[] Images(string[] paths) => [new(15, Drop(paths)), .. Privacy()];
    public static ClipboardPart[] Comments(string text) => [new(13, Encoding.Unicode.GetBytes(text + "\0")), .. Privacy()];
    private static ClipboardPart[] Privacy() => [new(Format("CanUploadToCloudClipboard"), new byte[4]), new(Format("CanIncludeInClipboardHistory"), new byte[4])];
    private static uint Format(string name) { uint id = RegisterClipboardFormatW(name); return id != 0 ? id : throw new Win32Exception(Marshal.GetLastWin32Error()); }
    private static byte[] Drop(string[] paths)
    {
        if (paths.Length == 0 || paths.Any(p => !Path.IsPathFullyQualified(p) || p.Contains('\0'))) throw new ArgumentException("Invalid file list.");
        var list = Encoding.Unicode.GetBytes(string.Join('\0', paths) + "\0\0");
        var bytes = new byte[20 + list.Length]; BitConverter.GetBytes(20).CopyTo(bytes, 0); BitConverter.GetBytes(1).CopyTo(bytes, 16); list.CopyTo(bytes, 20); return bytes;
    }
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] internal static extern uint RegisterClipboardFormatW(string name);
}

internal static class ClipboardTransport
{
    internal static async Task ProbeAsync(IntPtr owner, CancellationToken token, Action guard)
    {
        await OpenAsync(owner, token, guard);
        CloseClipboard(); // Availability check only. No read, empty, backup or publication.
    }
    private static async Task OpenAsync(IntPtr owner, CancellationToken token, Action guard)
    {
        for (int i = 0; i < 10; i++)
        {
            token.ThrowIfCancellationRequested(); guard();
            if (OpenClipboard(owner)) return;
            await Task.Delay(20, token);
        }
        throw new InvalidOperationException("Clipboard is busy.");
    }
    internal static async Task PublishAsync(IntPtr owner, ClipboardPart[] payload, CancellationToken token, Action guard)
    {
        var blocks = new List<(uint Format, IntPtr Handle)>(); bool opened = false;
        try
        {
            foreach (var part in payload)
            {
                var handle = GlobalAlloc(0x42, (nuint)part.Bytes.Length); if (handle == IntPtr.Zero) throw new OutOfMemoryException();
                blocks.Add((part.Format, handle)); var pointer = GlobalLock(handle);
                if (pointer == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error(), "GlobalLock");
                try { Marshal.Copy(part.Bytes, 0, pointer, part.Bytes.Length); } finally { GlobalUnlock(handle); }
            }
            await OpenAsync(owner, token, guard); opened = true;
            token.ThrowIfCancellationRequested(); guard();
            if (!EmptyClipboard()) throw new Win32Exception(Marshal.GetLastWin32Error(), "EmptyClipboard");
            for (int i = 0; i < blocks.Count; i++)
            {
                var block = blocks[i];
                if (SetClipboardData(block.Format, block.Handle) == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error(), "SetClipboardData");
                blocks[i] = (block.Format, IntPtr.Zero);
            }
        }
        finally
        {
            if (opened) CloseClipboard();
            foreach (var block in blocks) if (block.Handle != IntPtr.Zero) GlobalFree(block.Handle);
        }
    }
    public static void Publish(IntPtr owner, ClipboardPart[] payload)
    {
        var blocks = new List<(uint Format, IntPtr Handle)>(); bool opened = false;
        try
        {
            foreach (var part in payload)
            {
                var handle = GlobalAlloc(0x42, (nuint)part.Bytes.Length); if (handle == IntPtr.Zero) throw new OutOfMemoryException();
                blocks.Add((part.Format, handle)); var pointer = GlobalLock(handle);
                if (pointer == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error(), "GlobalLock");
                try { Marshal.Copy(part.Bytes, 0, pointer, part.Bytes.Length); } finally { GlobalUnlock(handle); }
            }
            for (int i = 0; i < 10 && !(opened = OpenClipboard(owner)); i++) Thread.Sleep(20);
            if (!opened) throw new InvalidOperationException("Clipboard is busy. Your session is unchanged; retry Copy.");
            if (!EmptyClipboard()) throw new Win32Exception(Marshal.GetLastWin32Error(), "EmptyClipboard");
            for (int i = 0; i < blocks.Count; i++)
            {
                var block = blocks[i];
                if (SetClipboardData(block.Format, block.Handle) == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error(), "SetClipboardData");
                blocks[i] = (block.Format, IntPtr.Zero); // Ownership transferred to Windows.
            }
        }
        finally
        {
            if (opened) CloseClipboard();
            foreach (var block in blocks) if (block.Handle != IntPtr.Zero) GlobalFree(block.Handle);
        }
    }
    [DllImport("user32.dll", SetLastError = true)] internal static extern bool OpenClipboard(IntPtr hwnd);
    [DllImport("user32.dll")] internal static extern bool CloseClipboard();
    [DllImport("user32.dll", SetLastError = true)] internal static extern bool EmptyClipboard();
    [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr SetClipboardData(uint format, IntPtr data);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern IntPtr GlobalAlloc(uint flags, nuint size);
    [DllImport("kernel32.dll", SetLastError = true)] internal static extern IntPtr GlobalLock(IntPtr handle);
    [DllImport("kernel32.dll")] internal static extern bool GlobalUnlock(IntPtr handle);
    [DllImport("kernel32.dll")] private static extern IntPtr GlobalFree(IntPtr handle);
}

internal sealed class TemporaryImages
{
    internal const string Ownership = "ScreenIt clipboard PNG generation v1";
    private readonly string root;
    public TemporaryImages(string? rootOverride = null) => root = Path.GetFullPath(rootOverride ?? Path.Combine(Path.GetTempPath(), "ScreenIt", "Clipboard-v1"));
    private static bool Safe(string path) => (File.GetAttributes(path) & FileAttributes.ReparsePoint) == 0;
    private void EnsureRoot()
    {
        Directory.CreateDirectory(root);
        for (var directory = new DirectoryInfo(root); directory != null; directory = directory.Parent)
            if (!Safe(directory.FullName)) throw new IOException("Temporary path includes a reparse point.");
    }
    public string[] Write(IReadOnlyList<(string Letter, BitmapSource Image)> images)
    {
        EnsureRoot(); var dir = Path.Combine(root, Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir);
        File.WriteAllLines(Path.Combine(dir, ".screenit-owned"), [Ownership, DateTimeOffset.UtcNow.ToString("O", System.Globalization.CultureInfo.InvariantCulture)]);
        var paths = new List<string>();
        foreach (var (letter, image) in images)
        {
            if (letter.Length == 0 || letter.Any(c => c < 'A' || c > 'Z')) throw new ArgumentException("Invalid screenshot letter.");
            var path = Path.Combine(dir, "ScreenIt-" + letter + ".png");
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image));
            using (var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read)) encoder.Save(file);
            paths.Add(path);
        }
        return paths.ToArray();
    }
    public int Cleanup(DateTimeOffset now)
    {
        if (!Directory.Exists(root)) return 0;
        EnsureRoot(); int removed = 0;
        foreach (var dir in Directory.EnumerateDirectories(root))
        {
            try
            {
                if (!Guid.TryParseExact(Path.GetFileName(dir), "N", out _) || !Safe(dir) || Directory.EnumerateDirectories(dir).Any()) continue;
                var marker = Path.Combine(dir, ".screenit-owned");
                if (!File.Exists(marker) || !Safe(marker)) continue;
                var stamp = File.ReadAllLines(marker);
                if (stamp.Length != 2 || stamp[0] != Ownership || !DateTimeOffset.TryParseExact(stamp[1], "O", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var created) || now - created < TimeSpan.FromDays(7)) continue;
                var files = Directory.GetFiles(dir);
                if (files.Any(p => !Safe(p) || p != marker && !ValidName(Path.GetFileName(p)))) continue;
                foreach (var file in files.Where(p => p != marker)) File.Delete(file);
                File.Delete(marker); Directory.Delete(dir, false); removed++;
            }
            catch (IOException) { /* Locked generations are retained for the next startup. */ }
            catch (UnauthorizedAccessException) { }
        }
        return removed;
    }
    private static bool ValidName(string name) => name.StartsWith("ScreenIt-", StringComparison.Ordinal) && name.EndsWith(".png", StringComparison.Ordinal) && name.Length > 13 && name[9..^4].All(c => c is >= 'A' and <= 'Z');
}
