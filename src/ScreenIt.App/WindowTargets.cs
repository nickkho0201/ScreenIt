using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

internal sealed record WindowTarget(IntPtr Hwnd,uint ProcessId,PxRect Bounds);

// Physical screen coordinates throughout; EnumWindows supplies top-level z-order.
internal static class WindowTargets
{
    internal static bool Eligible(uint pid,bool visible,bool minimized,bool cloaked,bool tool,bool shell,PxRect bounds,uint excludedProcess)
        => pid!=0 && pid!=excludedProcess && visible && !minimized && !cloaked && !tool && !shell && bounds.Width>=2 && bounds.Height>=2;
    internal static WindowTarget? Read(IntPtr hwnd,uint? excludedProcess=null)
    {
        if(!IsWindow(hwnd) || GetAncestor(hwnd,2)!=hwnd) return null;
        GetWindowThreadProcessId(hwnd,out uint pid);
        if(DwmGetWindowAttribute(hwnd,14,out int cloaked,4)!=0) return null;
        if(DwmGetWindowAttribute(hwnd,9,out Native.RECT r,16)!=0) { try { r=Native.WindowRect(hwnd); }catch { return null; } }
        var name=new StringBuilder(128);GetClassName(hwnd,name,name.Capacity);
        var bounds=new PxRect(r.Left,r.Top,r.Right-r.Left,r.Bottom-r.Top);
        // Tool windows cover taskbar/menu/utility popups; normal owned modal dialogs remain eligible.
        bool shell=name.ToString() is "Progman" or "WorkerW" or "Shell_TrayWnd" or "Shell_SecondaryTrayWnd" or "#32768";
        if(!Eligible(pid,IsWindowVisible(hwnd),IsIconic(hwnd),cloaked!=0,(GetWindowLongPtr(hwnd,-20).ToInt64()&0x08000080)!=0,shell,bounds,excludedProcess ?? (uint)Environment.ProcessId)) return null;
        if(GetWindowDisplayAffinity(hwnd,out uint affinity) && affinity!=0) return null;
        return new(hwnd,pid,bounds);
    }
    internal static WindowTarget[] Snapshot(uint? excludedProcess=null)
    {
        var targets=new List<WindowTarget>();EnumCallback callback=(h,_)=> { if(Read(h,excludedProcess) is { } target) targets.Add(target);return true; };
        if(!EnumWindows(callback,IntPtr.Zero)) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        GC.KeepAlive(callback);return targets.ToArray();
    }
    internal static WindowTarget? At(IEnumerable<WindowTarget> targets,P point) => targets.FirstOrDefault(t=>point.X>=t.Bounds.X && point.Y>=t.Bounds.Y && point.X<t.Bounds.X+t.Bounds.Width && point.Y<t.Bounds.Y+t.Bounds.Height);
    internal static P Cursor() { GetCursorPos(out POINT p);return new(p.X,p.Y); }
    private delegate bool EnumCallback(IntPtr hwnd,IntPtr data);
    [StructLayout(LayoutKind.Sequential)] private struct POINT { public int X,Y; }
    [DllImport("user32.dll",SetLastError=true)] private static extern bool EnumWindows(EnumCallback callback,IntPtr data);
    [DllImport("user32.dll")] private static extern bool IsWindow(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern IntPtr GetAncestor(IntPtr hwnd,uint flags);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hwnd,out uint pid);
    [DllImport("user32.dll",EntryPoint="GetWindowLongPtrW")] private static extern IntPtr GetWindowLongPtr(IntPtr hwnd,int index);
    [DllImport("user32.dll",CharSet=CharSet.Unicode,EntryPoint="GetClassNameW")] private static extern int GetClassName(IntPtr hwnd,StringBuilder name,int count);
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out POINT point);
    [DllImport("user32.dll")] private static extern bool GetWindowDisplayAffinity(IntPtr hwnd,out uint affinity);
    [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(IntPtr hwnd,int attribute,out int value,int size);
    [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(IntPtr hwnd,int attribute,out Native.RECT value,int size);
}
