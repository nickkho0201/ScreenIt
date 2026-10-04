using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;

internal record MonitorData(string Device, int Left, int Top, int Width, int Height, bool Primary, uint EffectiveDpi)
{
    public string TopologyKey => $"{Device}:{Left},{Top},{Width},{Height},{EffectiveDpi}";
}
internal record Frame(MonitorData Monitor, BitmapSource Image);
internal static class Native
{
    public static int LiveDcs, LiveBitmaps;
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct MONITORINFO
    {
        public int Size; public RECT Monitor, Work; public uint Flags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string Device;
    }
    [StructLayout(LayoutKind.Sequential)] private struct BITMAPINFO
    {
        public uint Size; public int Width, Height; public ushort Planes, BitCount; public uint Compression, SizeImage;
        public int Xppm, Yppm; public uint ClrUsed, ClrImportant;
    }
    private delegate bool MonitorEnum(IntPtr monitor, IntPtr dc, ref RECT rect, IntPtr data);
    public static MonitorData[] Monitors()
    {
        var result = new List<MonitorData>();
        Exception? failure = null;
        MonitorEnum callback = (IntPtr m, IntPtr dc, ref RECT r, IntPtr data) =>
        {
            var info = new MONITORINFO { Size = Marshal.SizeOf<MONITORINFO>(), Device = "" };
            if (!GetMonitorInfoW(m, ref info)) { failure = Error("GetMonitorInfoW"); return false; }
            int hr = GetDpiForMonitor(m, 0, out uint dx, out _); // Metadata only; UI uses its HWND's actual transform.
            if (hr != 0) { failure = new InvalidOperationException($"GetDpiForMonitor HRESULT {hr:X}"); return false; }
            result.Add(new(info.Device, info.Monitor.Left, info.Monitor.Top, info.Monitor.Right - info.Monitor.Left,
                info.Monitor.Bottom - info.Monitor.Top, (info.Flags & 1) != 0, dx));
            return true;
        };
        if (!EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, callback, IntPtr.Zero)) throw failure ?? Error("EnumDisplayMonitors");
        return result.OrderBy(m => m.Device, StringComparer.Ordinal).ToArray();
    }
    public static Frame Capture(MonitorData m)
    {
        IntPtr screen = IntPtr.Zero, memory = IntPtr.Zero, bitmap = IntPtr.Zero, old = IntPtr.Zero;
        var cleanup = new List<string>();
        try
        {
            screen = GetDC(IntPtr.Zero); if (screen == IntPtr.Zero) throw Error("GetDC"); Interlocked.Increment(ref LiveDcs);
            memory = CreateCompatibleDC(screen); if (memory == IntPtr.Zero) throw Error("CreateCompatibleDC"); Interlocked.Increment(ref LiveDcs);
            int stride = checked(m.Width * 4), count = checked(stride * m.Height);
            var info = new BITMAPINFO { Size = 40, Width = m.Width, Height = -m.Height, Planes = 1, BitCount = 32, SizeImage = (uint)count };
            bitmap = CreateDIBSection(screen, ref info, 0, out IntPtr bits, IntPtr.Zero, 0);
            if (bitmap != IntPtr.Zero) Interlocked.Increment(ref LiveBitmaps);
            if (bitmap == IntPtr.Zero || bits == IntPtr.Zero) throw Error("CreateDIBSection");
            old = SelectObject(memory, bitmap); if (old == IntPtr.Zero || old == new IntPtr(-1)) throw Error("SelectObject");
            if (!BitBlt(memory, 0, 0, m.Width, m.Height, screen, m.Left, m.Top, 0x00CC0020 | 0x40000000)) throw Error("BitBlt");
            var pixels = new byte[count]; Marshal.Copy(bits, pixels, 0, count);
            var image = BitmapSource.Create(m.Width, m.Height, 96, 96, PixelFormats.Bgr32, null, pixels, stride);
            image.Freeze(); return new(m, image);
        }
        finally
        {
            if (old != IntPtr.Zero && old != new IntPtr(-1) && SelectObject(memory, old) == IntPtr.Zero) cleanup.Add("Restore SelectObject");
            if (bitmap != IntPtr.Zero) { if (DeleteObject(bitmap)) Interlocked.Decrement(ref LiveBitmaps); else cleanup.Add("DeleteObject"); }
            if (memory != IntPtr.Zero) { if (DeleteDC(memory)) Interlocked.Decrement(ref LiveDcs); else cleanup.Add("DeleteDC"); }
            if (screen != IntPtr.Zero) { if (ReleaseDC(IntPtr.Zero, screen) == 1) Interlocked.Decrement(ref LiveDcs); else cleanup.Add("ReleaseDC"); }
            if (cleanup.Count > 0) throw new InvalidOperationException("Native cleanup failure: " + string.Join(", ", cleanup));
        }
    }
    public static void Place(IntPtr hwnd, MonitorData m)
    {
        if (!SetWindowPos(hwnd, new IntPtr(-1), m.Left, m.Top, m.Width, m.Height, 0x0010)) throw Error("SetWindowPos");
    }
    public static RECT WindowRect(IntPtr h) { if (!GetWindowRect(h, out RECT r)) throw Error("GetWindowRect"); return r; }
    public static void Flush() { int hr = DwmFlush(); if (hr != 0) throw new InvalidOperationException($"DwmFlush HRESULT {hr:X}"); }
    public static object Resources()
    {
        using var p = Process.GetCurrentProcess(); p.Refresh();
        return new { gdi = GetGuiResources(p.Handle, 0), user = GetGuiResources(p.Handle, 1), handles = p.HandleCount,
            privateBytes = p.PrivateMemorySize64, workingSet = p.WorkingSet64, managedBytes = GC.GetTotalMemory(false), liveDcs = LiveDcs, liveBitmaps = LiveBitmaps };
    }
    public static object Machine()
    {
        GetPhysicallyInstalledSystemMemory(out ulong kb);
        return new { os = Environment.OSVersion.VersionString, runtime = Environment.Version.ToString(), x64 = Environment.Is64BitProcess,
            processors = Environment.ProcessorCount, installedRamBytes = kb * 1024, remoteSession = GetSystemMetrics(0x1000) != 0,
            dpiAwareness = AreDpiAwarenessContextsEqual(GetThreadDpiAwarenessContext(), new IntPtr(-4)) ? "PerMonitorV2" : "OTHER", hdr = "NOT VERIFIED" };
    }
    private static Win32Exception Error(string api) => new(Marshal.GetLastWin32Error(), api);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool EnumDisplayMonitors(IntPtr dc, IntPtr clip, MonitorEnum callback, IntPtr data);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern bool GetMonitorInfoW(IntPtr monitor, ref MONITORINFO info);
    [DllImport("shcore.dll")] private static extern int GetDpiForMonitor(IntPtr monitor, int type, out uint x, out uint y);
    [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr GetDC(IntPtr window);
    [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr window, IntPtr dc);
    [DllImport("gdi32.dll", SetLastError = true)] private static extern IntPtr CreateCompatibleDC(IntPtr dc);
    [DllImport("gdi32.dll", SetLastError = true)] private static extern IntPtr CreateDIBSection(IntPtr dc, ref BITMAPINFO info, uint usage, out IntPtr bits, IntPtr section, uint offset);
    [DllImport("gdi32.dll", SetLastError = true)] private static extern IntPtr SelectObject(IntPtr dc, IntPtr obj);
    [DllImport("gdi32.dll", SetLastError = true)] private static extern bool DeleteObject(IntPtr obj);
    [DllImport("gdi32.dll", SetLastError = true)] private static extern bool DeleteDC(IntPtr dc);
    [DllImport("gdi32.dll", SetLastError = true)] private static extern bool BitBlt(IntPtr target, int x, int y, int w, int h, IntPtr source, int sx, int sy, uint operation);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int w, int h, uint flags);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);
    [DllImport("dwmapi.dll")] private static extern int DwmFlush();
    [DllImport("user32.dll")] public static extern uint GetDpiForWindow(IntPtr hwnd);
    [DllImport("user32.dll", SetLastError = true)] public static extern bool RegisterHotKey(IntPtr hwnd, int id, uint modifiers, uint key);
    [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr hwnd, int id);
    [DllImport("user32.dll")] private static extern uint GetGuiResources(IntPtr process, uint type);
    [DllImport("user32.dll")] private static extern int GetSystemMetrics(int index);
    [DllImport("kernel32.dll")] private static extern bool GetPhysicallyInstalledSystemMemory(out ulong kb);
    [DllImport("user32.dll")] private static extern IntPtr GetThreadDpiAwarenessContext();
    [DllImport("user32.dll")] private static extern bool AreDpiAwarenessContextsEqual(IntPtr a, IntPtr b);
    [DllImport("wtsapi32.dll", SetLastError = true)] public static extern bool WTSRegisterSessionNotification(IntPtr hwnd, uint flags);
    [DllImport("wtsapi32.dll")] public static extern bool WTSUnRegisterSessionNotification(IntPtr hwnd);
}
