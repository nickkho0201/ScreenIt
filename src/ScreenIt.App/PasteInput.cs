using System.Runtime.InteropServices;

internal static class PasteInput
{
    public static PasteTarget Foreground()
    {
        var hwnd = GetForegroundWindow(); if (hwnd == IntPtr.Zero) return default;
        // Root (not root owner): a modal popup is a different target and aborts the sequence.
        var root = GetAncestor(hwnd, 2); if (root != IntPtr.Zero) hwnd = root;
        GetWindowThreadProcessId(hwnd, out uint pid); return new(hwnd, pid);
    }
    public static bool IsCurrent(PasteTarget target) => IsWindow(target.Hwnd) && Foreground() == target;
    public static bool IsOwn(PasteTarget target) => target.ProcessId == (uint)Environment.ProcessId;
    public static bool KeysReleased() => new[] { 0x11, 0x12, 0x56, 0x10, 0x5B, 0x5C }.All(key => (GetAsyncKeyState(key) & 0x8000) == 0);
    [StructLayout(LayoutKind.Sequential)] internal struct KeyboardInput { public ushort Vk, Scan; public uint Flags, Time; public UIntPtr Extra; }
    [StructLayout(LayoutKind.Sequential)] private struct MouseInput { public int X, Y; public uint Data, Flags, Time; public UIntPtr Extra; }
    [StructLayout(LayoutKind.Explicit)] internal struct InputUnion { [FieldOffset(0)] public KeyboardInput Keyboard; [FieldOffset(0)] private MouseInput mouse; }
    [StructLayout(LayoutKind.Sequential)] internal struct Input { public uint Type; public InputUnion Data; }
    internal static Input[] Chord() => [Key(0x11, false), Key(0x56, false), Key(0x56, true), Key(0x11, true)];
    private static Input Key(ushort key, bool up) => new() { Type = 1, Data = new() { Keyboard = new() { Vk = key, Flags = up ? 2u : 0u } } };
    public static void SendPaste(PasteTarget target)
    {
        if (!IsCurrent(target) || IsOwn(target)) throw new PasteAbortedException("Foreground target changed.");
        if (!KeysReleased()) throw new PasteAbortedException("Keyboard modifiers are held.");
        var inputs = Chord(); uint sent = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Input>());
        if (sent == 4) return;
        if (sent > 0)
        {
            // Release only the synthetic chord after partial injection. Never retry paste or edit the receiver.
            Input[] release = [Key(0x56, true), Key(0x11, true)]; SendInput(2, release, Marshal.SizeOf<Input>());
        }
        throw new InvalidOperationException("Paste input was not fully inserted.");
    }
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern IntPtr GetAncestor(IntPtr hwnd, uint flags);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);
    [DllImport("user32.dll")] private static extern bool IsWindow(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll", SetLastError = true)] private static extern uint SendInput(uint count, Input[] inputs, int size);
}

internal sealed class WindowsPasteDelivery(IntPtr owner) : IPasteDelivery
{
    private uint publishedSequence;
    public bool IsTargetCurrent(PasteTarget target) => PasteInput.IsCurrent(target);
    public bool IsOwnTarget(PasteTarget target) => PasteInput.IsOwn(target);
    public bool KeysReleased() => PasteInput.KeysReleased();
    public Task ProbeAsync(CancellationToken token, Action guard) => ClipboardTransport.ProbeAsync(owner, token, guard);
    public async Task PublishAsync(ClipboardPart[] payload, CancellationToken token, Action guard)
    {
        await ClipboardTransport.PublishAsync(owner, payload, token, guard);
        publishedSequence = GetClipboardSequenceNumber();
    }
    public void SendPaste(PasteTarget target)
    {
        if (GetClipboardOwner() != owner || GetClipboardSequenceNumber() != publishedSequence)
            throw new PasteAbortedException("Clipboard changed before paste.");
        PasteInput.SendPaste(target);
    }
    public Task Delay(TimeSpan duration, CancellationToken token) => Task.Delay(duration, token);
    [DllImport("user32.dll")] private static extern IntPtr GetClipboardOwner();
    [DllImport("user32.dll")] private static extern uint GetClipboardSequenceNumber();
}
