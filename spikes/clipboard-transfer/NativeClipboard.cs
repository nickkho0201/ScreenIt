using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

// Disposable, eager Win32 clipboard helper. No delayed rendering or OLE layer.
internal sealed class NativeClipboard : NativeWindow, IDisposable
{
    public const uint Text = 13, Drop = 15, DibV5 = 17;
    private uint sequence;
    private List<Part>? last;
    public NativeClipboard() => CreateHandle(new CreateParams { Caption = "ScreenIt clipboard SPIKE (hidden owner)" });
    public record Part(uint Id, string Name, byte[] Bytes);
    public static uint Format(string name)
    {
        uint id = RegisterClipboardFormatW(name);
        if (id == 0) throw Error("RegisterClipboardFormatW");
        return id;
    }
    public static Part Registered(string name, byte[] bytes) => new(Format(name), name, bytes);

    public void Publish(List<Part> parts)
    {
        // Allocate everything first; clipboard cannot be transactionally restored on failure.
        var handles = new List<(Part Part, IntPtr Handle)>();
        try
        {
            foreach (var part in parts)
            {
                IntPtr h = GlobalAlloc(0x42, (nuint)part.Bytes.Length); // MOVEABLE | ZEROINIT
                if (h == IntPtr.Zero) throw Error("GlobalAlloc");
                handles.Add((part, h));
                IntPtr p = GlobalLock(h);
                if (p == IntPtr.Zero) throw Error("GlobalLock");
                try { Marshal.Copy(part.Bytes, 0, p, part.Bytes.Length); }
                finally { GlobalUnlock(h); }
            }
            Open();
            try
            {
                if (!EmptyClipboard()) throw Error("EmptyClipboard");
                last = null;
                for (int i = 0; i < handles.Count; i++)
                {
                    var item = handles[i];
                    if (SetClipboardData(item.Part.Id, item.Handle) == IntPtr.Zero)
                        throw Error($"SetClipboardData({item.Part.Name}); clipboard may contain a partial payload");
                    handles[i] = (item.Part, IntPtr.Zero); // Ownership transferred to Windows.
                    Console.WriteLine($"SetClipboardData OK: {item.Part.Name} ({item.Part.Id}), {item.Part.Bytes.Length} bytes");
                }
                last = parts;
            }
            finally { if (!CloseClipboard()) throw Error("CloseClipboard"); }
            sequence = GetClipboardSequenceNumber();
        }
        finally
        {
            foreach (var item in handles) if (item.Handle != IntPtr.Zero) GlobalFree(item.Handle);
        }
    }

    public string[] CheckOwn()
    {
        if (last is null) throw new InvalidOperationException("No successfully published payload in this process.");
        Open();
        try
        {
            // Do not read third-party clipboard contents, even when another app copied during a test.
            if (GetClipboardOwner() != Handle || GetClipboardSequenceNumber() != sequence)
                throw new InvalidOperationException("Clipboard changed/owner changed: self-check skipped; no foreign data read.");
            var formats = new List<string>();
            uint id = 0;
            while (true)
            {
                SetLastError(0);
                id = EnumClipboardFormats(id);
                if (id == 0)
                {
                    if (Marshal.GetLastWin32Error() != 0) throw Error("EnumClipboardFormats");
                    break;
                }
                formats.Add($"{Name(id)} ({id})");
            }
            Console.WriteLine("Actual formats (may include Windows synthesis): " + string.Join(", ", formats));
            foreach (string flag in new[] { "CanUploadToCloudClipboard", "CanIncludeInClipboardHistory" })
                if (formats.Any(f => f.StartsWith(flag + " (", StringComparison.Ordinal)) != last.Any(p => p.Name == flag))
                    throw new InvalidDataException("Unexpected privacy flag presence: " + flag);
            foreach (var part in last)
            {
                byte[] actual = Read(part.Id, part.Bytes.Length);
                if (!actual.AsSpan().SequenceEqual(part.Bytes)) throw new InvalidDataException($"Readback mismatch: {part.Name}");
                if (part.Id == Text)
                {
                    string text = Encoding.Unicode.GetString(actual).TrimEnd('\0');
                    if (text != Samples.Comments) throw new InvalidDataException("Unicode text mismatch");
                    Console.WriteLine("Unicode readback: exact Cyrillic/multiline match");
                }
                if (part.Id == Drop)
                {
                    IntPtr h = GetClipboardData(Drop);
                    uint count = DragQueryFileW(h, uint.MaxValue, null, 0);
                    var files = new List<string>();
                    for (uint i = 0; i < count; i++)
                    {
                        uint n = DragQueryFileW(h, i, null, 0);
                        var path = new StringBuilder(checked((int)n + 1));
                        if (DragQueryFileW(h, i, path, n + 1) != n) throw new InvalidDataException("DragQueryFileW failed");
                        files.Add(path.ToString());
                    }
                    var expected = Samples.DecodeDrop(part.Bytes);
                    if (!files.SequenceEqual(expected, StringComparer.OrdinalIgnoreCase)) throw new InvalidDataException("HDROP order/path mismatch");
                    foreach (string path in files) if (!File.Exists(path)) throw new FileNotFoundException("HDROP file missing", path);
                    Console.WriteLine("HDROP readback: A.png → B.png → C.png; all files exist");
                }
                if (part.Name is "CanUploadToCloudClipboard" or "CanIncludeInClipboardHistory")
                    Console.WriteLine($"Privacy readback: {part.Name} = {BitConverter.ToUInt32(actual)}");
            }
            sequence = GetClipboardSequenceNumber();
            return formats.ToArray();
        }
        finally { CloseClipboard(); }
    }

    private static byte[] Read(uint id, int length)
    {
        IntPtr h = GetClipboardData(id);
        if (h == IntPtr.Zero) throw Error($"GetClipboardData({id})");
        if (GlobalSize(h) < (nuint)length) throw new InvalidDataException("Clipboard block too small");
        IntPtr p = GlobalLock(h);
        if (p == IntPtr.Zero) throw Error("GlobalLock(read)");
        try { var b = new byte[length]; Marshal.Copy(p, b, 0, length); return b; }
        finally { GlobalUnlock(h); }
    }
    public void ClearOwn()
    {
        Open();
        try
        {
            if (GetClipboardOwner() == Handle)
            {
                if (!EmptyClipboard()) throw Error("EmptyClipboard(clear own)");
                Console.WriteLine("Own clipboard cleared; other owners are never cleared.");
            }
            else Console.WriteLine("Clipboard belongs to another owner; untouched.");
            last = null;
        }
        finally { CloseClipboard(); }
    }
    private void Open()
    {
        for (int i = 0; i < 20; i++)
        {
            if (OpenClipboard(Handle)) return;
            Application.DoEvents();
            Thread.Sleep(25);
        }
        throw Error("OpenClipboard (busy after bounded retries)");
    }
    private static string Name(uint id)
    {
        if (id < 0xC000) return id switch
        {
            1 => "CF_TEXT", 2 => "CF_BITMAP", 7 => "CF_OEMTEXT", 8 => "CF_DIB", 13 => "CF_UNICODETEXT",
            15 => "CF_HDROP", 16 => "CF_LOCALE", 17 => "CF_DIBV5", _ => $"standard-{id}"
        };
        var name = new StringBuilder(256);
        return GetClipboardFormatNameW(id, name, name.Capacity) > 0 ? name.ToString() : $"registered-{id}";
    }
    private static Win32Exception Error(string api) => new(Marshal.GetLastWin32Error(), api);
    public void Dispose() => DestroyHandle();
    [DllImport("user32.dll", SetLastError = true)] private static extern bool OpenClipboard(IntPtr hwnd);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool CloseClipboard();
    [DllImport("user32.dll", SetLastError = true)] private static extern bool EmptyClipboard();
    [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr SetClipboardData(uint format, IntPtr data);
    [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr GetClipboardData(uint format);
    [DllImport("user32.dll", SetLastError = true)] private static extern uint EnumClipboardFormats(uint previous);
    [DllImport("user32.dll")] private static extern IntPtr GetClipboardOwner();
    [DllImport("user32.dll")] private static extern uint GetClipboardSequenceNumber();
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern uint RegisterClipboardFormatW(string name);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClipboardFormatNameW(uint id, StringBuilder name, int count);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern IntPtr GlobalAlloc(uint flags, nuint size);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern IntPtr GlobalLock(IntPtr handle);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool GlobalUnlock(IntPtr handle);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern IntPtr GlobalFree(IntPtr handle);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern nuint GlobalSize(IntPtr handle);
    [DllImport("kernel32.dll")] private static extern void SetLastError(uint error);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] private static extern uint DragQueryFileW(IntPtr drop, uint index, StringBuilder? path, uint count);
}
