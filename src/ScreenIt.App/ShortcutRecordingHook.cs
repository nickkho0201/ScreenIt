using System.ComponentModel;
using System.Runtime.InteropServices;

// Settings input acquisition only: never registers or dispatches runtime hotkeys.
// Like ReservedCaptureHook, the callback runs on a dedicated message thread and
// posts a result to STA. Waiting for releases prevents rebind from seeing repeats
// and pairs every swallowed modifier down/up (especially Win/Alt menu gestures).
internal sealed class ShortcutRecordingHook : IDisposable
{
    internal const int Message=0x8002;
    private readonly IntPtr target;
    private readonly int generation;
    private readonly bool includeInjected;
    private readonly Thread thread;
    private readonly ManualResetEventSlim started=new();
    private readonly Callback callback;
    private readonly ShortcutRecordingState state=new();
    private uint threadId;
    private IntPtr handle;
    private int error;
    private volatile bool stopping;
    private bool disposed;
    internal ShortcutRecordingHook(IntPtr target,int generation,bool includeInjected=false)
    {
        this.target=target;this.generation=generation;this.includeInjected=includeInjected;callback=Hook;
        thread=new Thread(Run) { IsBackground=true,Name="ScreenIt Settings shortcut recorder" };
        thread.Start();started.Wait();
        if(handle==IntPtr.Zero) { thread.Join();started.Dispose();throw new Win32Exception(error,"SetWindowsHookExW"); }
    }
    private void Run()
    {
        threadId=GetCurrentThreadId();PeekMessageW(out _,IntPtr.Zero,0,0,0);
        foreach(uint key in ReservedShortcutState.ModifierKeys) if((GetAsyncKeyState((int)key)&0x8000)!=0) state.Seed(key);
        handle=SetWindowsHookExW(13,callback,GetModuleHandleW(null),0);error=Marshal.GetLastWin32Error();started.Set();
        if(handle==IntPtr.Zero) return;
        try { while(GetMessageW(out var message,IntPtr.Zero,0,0)>0) { TranslateMessage(ref message);DispatchMessageW(ref message); } }
        finally { if(!UnhookWindowsHookEx(handle)) System.Diagnostics.Trace.TraceError("ScreenIt recorder hook cleanup failed: {0}",Marshal.GetLastWin32Error()); }
    }
    private IntPtr Hook(int code,IntPtr wp,IntPtr lp)
    {
        if(code<0 || stopping) return CallNextHookEx(IntPtr.Zero,code,wp,lp);
        int message=(int)wp;
        if(message is not (0x100 or 0x101 or 0x104 or 0x105)) return CallNextHookEx(IntPtr.Zero,code,wp,lp);
        var data=Marshal.PtrToStructure<KeyboardData>(lp);
        var result=state.Process(data.Key,message is 0x100 or 0x104,!includeInjected && (data.Flags&0x12)!=0);
        if(result.Complete && !PostMessageW(target,Message,new IntPtr(generation),new IntPtr(result.Key==null ? 0 : (long)(result.Key.Key<<16|result.Key.Modifiers))))
            System.Diagnostics.Trace.TraceError("ScreenIt recorder result post failed: {0}",Marshal.GetLastWin32Error());
        return result.Suppress ? new IntPtr(1) : CallNextHookEx(IntPtr.Zero,code,wp,lp);
    }
    public void Dispose()
    {
        if(disposed) return;disposed=true;stopping=true;
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

// Only keys in this short recording gesture are retained, never text/history.
internal sealed class ShortcutRecordingState
{
    private readonly HashSet<uint> held=[],swallowed=[];
    private Hotkey? chord;
    private bool cancelled,complete;
    internal void Seed(uint key) => held.Add(key); // Already forwarded down before installation: its up must pass.
    private uint Modifiers => (held.Contains(0xA4)||held.Contains(0xA5) ? 1u : 0)|(held.Contains(0xA2)||held.Contains(0xA3) ? 2u : 0)|(held.Contains(0xA0)||held.Contains(0xA1) ? 4u : 0)|(held.Contains(0x5B)||held.Contains(0x5C) ? 8u : 0);
    internal (bool Suppress,bool Complete,Hotkey? Key) Process(uint key,bool down,bool injected)
    {
        if(injected || complete) return (false,false,null);
        bool suppress;
        if(down)
        {
            bool first=held.Add(key);if(first) swallowed.Add(key);suppress=swallowed.Contains(key);
            if(key==0x1B) { cancelled=true;chord=null; }
            else if(first && chord==null && !cancelled && !ReservedShortcutState.ModifierKeys.Contains(key)) chord=new(Modifiers,key);
        }
        else { held.Remove(key);suppress=swallowed.Remove(key); }
        if(!down && held.Count==0 && (chord!=null || cancelled)) { complete=true;return (suppress,true,chord); }
        return (suppress,false,null);
    }
}
