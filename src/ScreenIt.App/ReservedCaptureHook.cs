using System.ComponentModel;
using System.Runtime.InteropServices;

// Only Win+Shift+S is consumed. No UI, capture or persistence runs in the callback.
// A dedicated message thread keeps slow WPF rendering outside LowLevelHooksTimeout.
internal sealed class ReservedCaptureHook : IDisposable
{
    internal const int Message = 0x8001;
    private readonly IntPtr target;
    private readonly Thread thread;
    private readonly ManualResetEventSlim started = new();
    private readonly Callback callback;
    private readonly ReservedShortcutState state = new();
    private uint threadId;
    private IntPtr handle;
    private int error;
    private volatile int binding;
    private bool disposed;
    internal ReservedCaptureHook(IntPtr target)
    {
        this.target=target;callback=Hook;
        thread=new Thread(Run) { IsBackground=true,Name="ScreenIt reserved capture shortcut" };
        thread.Start();started.Wait();
        if(handle==IntPtr.Zero) { thread.Join();started.Dispose();throw new Win32Exception(error,"SetWindowsHookExW"); }
    }
    internal void Configure(int id) { binding=id; }
    private void Run()
    {
        threadId=GetCurrentThreadId();PeekMessageW(out _,IntPtr.Zero,0,0,0);
        foreach(uint key in ReservedShortcutState.ModifierKeys) state.SetModifier(key,(GetAsyncKeyState((int)key)&0x8000)!=0);
        handle=SetWindowsHookExW(13,callback,GetModuleHandleW(null),0);
        error=Marshal.GetLastWin32Error();started.Set();
        if(handle==IntPtr.Zero) return;
        try { while(GetMessageW(out var message,IntPtr.Zero,0,0)>0) { TranslateMessage(ref message);DispatchMessageW(ref message); } }
        finally { if(!UnhookWindowsHookEx(handle)) System.Diagnostics.Trace.TraceError("ScreenIt keyboard hook cleanup failed: {0}",Marshal.GetLastWin32Error()); }
    }
    private IntPtr Hook(int code,IntPtr wp,IntPtr lp)
    {
        if(code<0) return CallNextHookEx(IntPtr.Zero,code,wp,lp);
        int message=(int)wp;
        if(message is not (0x100 or 0x101 or 0x104 or 0x105)) return CallNextHookEx(IntPtr.Zero,code,wp,lp);
        var data=Marshal.PtrToStructure<KeyboardData>(lp);
        bool down=message is 0x100 or 0x104;
        // The current S event is not in async state yet. Modifiers precede it;
        // resampling those also repairs missed releases across a secure desktop.
        if(data.Key==0x53 && down && (data.Flags&0x12)==0)
            foreach(uint key in ReservedShortcutState.ModifierKeys) state.SetModifier(key,(GetAsyncKeyState((int)key)&0x8000)!=0);
        int id=binding;
        var result=state.Process(data.Key,down,(data.Flags&0x12)!=0,id!=0);
        if(result.Trigger)
        {
            if(!PostMessageW(target,Message,new IntPtr(id),IntPtr.Zero))
            { state.PublicationFailed();return CallNextHookEx(IntPtr.Zero,code,wp,lp); }
        }
        return result.Suppress ? new IntPtr(1) : CallNextHookEx(IntPtr.Zero,code,wp,lp);
    }
    public void Dispose()
    {
        if(disposed) return;disposed=true;Configure(0);
        PostThreadMessageW(threadId,0x12,IntPtr.Zero,IntPtr.Zero);thread.Join();started.Dispose();
    }
    private delegate IntPtr Callback(int code,IntPtr wp,IntPtr lp);
    [StructLayout(LayoutKind.Sequential)] private struct KeyboardData { internal uint Key,Scan,Flags,Time;internal UIntPtr Extra; }
    [StructLayout(LayoutKind.Sequential)] private struct MessageData { internal IntPtr Hwnd;internal uint Message;internal UIntPtr Wp;internal IntPtr Lp;internal uint Time;internal int X,Y;internal uint Private; }
    [DllImport("user32.dll",SetLastError=true)] private static extern IntPtr SetWindowsHookExW(int type,Callback callback,IntPtr module,uint thread);
    [DllImport("user32.dll",SetLastError=true)] private static extern bool UnhookWindowsHookEx(IntPtr hook);
    [DllImport("user32.dll")] private static extern IntPtr CallNextHookEx(IntPtr hook,int code,IntPtr wp,IntPtr lp);
    [DllImport("user32.dll",SetLastError=true)] private static extern bool PostMessageW(IntPtr hwnd,int message,IntPtr wp,IntPtr lp);
    [DllImport("user32.dll",SetLastError=true)] private static extern bool PostThreadMessageW(uint thread,int message,IntPtr wp,IntPtr lp);
    [DllImport("user32.dll")] private static extern int GetMessageW(out MessageData message,IntPtr hwnd,uint min,uint max);
    [DllImport("user32.dll")] private static extern bool PeekMessageW(out MessageData message,IntPtr hwnd,uint min,uint max,uint remove);
    [DllImport("user32.dll")] private static extern bool TranslateMessage(ref MessageData message);
    [DllImport("user32.dll")] private static extern IntPtr DispatchMessageW(ref MessageData message);
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode)] private static extern IntPtr GetModuleHandleW(string? name);
}

// State contains only modifier bits and the swallowed S gesture, never typed text.
internal sealed class ReservedShortcutState
{
    internal static readonly uint[] ModifierKeys=[0xA0,0xA1,0xA2,0xA3,0xA4,0xA5,0x5B,0x5C];
    private uint held;
    private bool swallowed;
    private bool sDown;
    internal void PublicationFailed() => swallowed=false;
    internal void SetModifier(uint key,bool down)
    {
        int index=Array.IndexOf(ModifierKeys,key);if(index<0) return;
        uint bit=1u<<index;held=down ? held|bit : held&~bit;
    }
    internal (bool Suppress,bool Trigger) Process(uint key,bool down,bool injected,bool enabled)
    {
        if(injected) return (false,false);
        SetModifier(key,down);
        if(key!=0x53) return (false,false);
        bool firstDown=down && !sDown;sDown=down;
        if(swallowed) { if(!down) swallowed=false;return (true,false); }
        bool match=(held&3)!=0 && (held&192)!=0 && (held&60)==0;
        if(enabled && firstDown && match) { swallowed=true;return (true,true); }
        return (false,false);
    }
}
