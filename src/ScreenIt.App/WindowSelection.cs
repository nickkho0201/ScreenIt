using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Threading;

// Snapshot plus coalesced WinEvent invalidation, never EnumWindows per MouseMove.
internal sealed class WindowSelection : IDisposable
{
    private readonly DispatcherTimer refresh;
    private readonly WinEvent callback;
    private readonly nint hook;
    private WindowTarget[] targets;
    private bool disposed;
    internal event Action? Changed;
    internal WindowSelection()
    {
        targets=WindowTargets.Snapshot();
        refresh=new DispatcherTimer { Interval=TimeSpan.FromMilliseconds(50) };
        refresh.Tick+=(_,_)=> { refresh.Stop();if(disposed) return;try { targets=WindowTargets.Snapshot();Changed?.Invoke(); }catch(Exception ex) { Trace.TraceError("ScreenIt window targeting refresh failed: {0}",ex); } };
        callback=(_,_,_,obj,_,_,_)=> { if(!disposed && obj==0 && !refresh.IsEnabled) refresh.Start(); };
        hook=SetWinEventHook(0x8000,0x800B,0,callback,0,0,2);
        if(hook==0) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
    }
    internal WindowTarget? At(P point,bool refreshNow=false)
    {
        if(refreshNow) targets=WindowTargets.Snapshot();
        return WindowTargets.At(targets,point);
    }
    public void Dispose() { if(disposed) return;disposed=true;refresh.Stop();Changed=null;if(!UnhookWinEvent(hook)) Trace.TraceError("ScreenIt window targeting unhook failed.");GC.KeepAlive(callback); }
    private delegate void WinEvent(nint hook,uint evt,nint hwnd,int obj,int child,uint thread,uint time);
    [DllImport("user32.dll",SetLastError=true)] private static extern nint SetWinEventHook(uint min,uint max,nint module,WinEvent callback,uint process,uint thread,uint flags);
    [DllImport("user32.dll")] private static extern bool UnhookWinEvent(nint hook);
}
