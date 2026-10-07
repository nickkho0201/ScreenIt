using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;

// Verification-only observer. Never consumes input or activates windows.
public sealed class DesktopInputEvidence : IDisposable {
 const ulong OwnInput=0x53495456;
 delegate IntPtr Hook(int code,IntPtr wp,IntPtr lp);
 [DllImport("user32.dll")] static extern IntPtr SetWindowsHookEx(int id,Hook callback,IntPtr module,uint thread);
 [DllImport("user32.dll")] static extern bool UnhookWindowsHookEx(IntPtr hook);
 [DllImport("user32.dll")] static extern IntPtr CallNextHookEx(IntPtr hook,int code,IntPtr wp,IntPtr lp);
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode)] static extern IntPtr GetModuleHandle(string name);
 [DllImport("user32.dll")] static extern bool PeekMessage(out MSG msg,IntPtr hwnd,uint min,uint max,uint remove);
 [DllImport("user32.dll")] static extern bool TranslateMessage(ref MSG msg);
 [DllImport("user32.dll")] static extern IntPtr DispatchMessage(ref MSG msg);
 [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
 [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hwnd,out uint pid);
 [DllImport("user32.dll")] static extern bool GetCursorPos(out POINT point);
 [DllImport("user32.dll")] static extern IntPtr WindowFromPoint(POINT point);
 [DllImport("user32.dll")] static extern short GetAsyncKeyState(int key);
 [DllImport("user32.dll")] static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);
 [DllImport("user32.dll")] static extern bool SetCursorPos(int x,int y);
 [DllImport("user32.dll")] static extern void mouse_event(uint flags,uint x,uint y,uint data,UIntPtr extra);
 [StructLayout(LayoutKind.Sequential)] struct POINT {public int x,y;}
 [StructLayout(LayoutKind.Sequential)] struct MOUSE {public POINT pt;public uint data,flags,time;public UIntPtr extra;}
 [StructLayout(LayoutKind.Sequential)] struct KEY {public uint vk,scan,flags,time;public UIntPtr extra;}
 [StructLayout(LayoutKind.Sequential)] struct MSG {public IntPtr hwnd;public uint message;public UIntPtr wp;public IntPtr lp;public uint time;public POINT point;public uint privateData;}
 readonly object gate=new object();readonly Thread thread;readonly ManualResetEventSlim ready=new ManualResetEventSlim();
 readonly List<string> events=new List<string>();readonly List<string> housekeeping=new List<string>();readonly List<string> faults=new List<string>();int applicationPid;Hook mouse,key;IntPtr mh,kh;Exception error;volatile bool stopped;bool active;int x,y;IntPtr foreground;int allowedPid;
 public DesktopInputEvidence() {
  mouse=Mouse;key=Keyboard;thread=new Thread(Run){IsBackground=true};thread.Start();ready.Wait();if(error!=null){Dispose();throw error;}
 }
 void Run(){SetThreadDpiAwarenessContext(new IntPtr(-4));try{mh=SetWindowsHookEx(14,mouse,GetModuleHandle(null),0);kh=SetWindowsHookEx(13,key,GetModuleHandle(null),0);if(mh==IntPtr.Zero||kh==IntPtr.Zero)throw new System.ComponentModel.Win32Exception();}catch(Exception ex){error=ex;}finally{ready.Set();}
  try{while(!stopped){while(PeekMessage(out var msg,IntPtr.Zero,0,0,1)){TranslateMessage(ref msg);DispatchMessage(ref msg);}lock(gate){if(active){var actual=GetForegroundWindow();GetWindowThreadProcessId(actual,out uint pid);if(actual!=foreground && (allowedPid==0 || pid!=allowedPid)){if(pid==applicationPid){if(faults.Count==0)faults.Add("Unexpected own application foreground HWND="+actual);active=false;}else Record("foreground changed to "+actual+" pid="+pid);}}}Thread.Sleep(8);}}
  finally {if(mh!=IntPtr.Zero){UnhookWindowsHookEx(mh);mh=IntPtr.Zero;}if(kh!=IntPtr.Zero){UnhookWindowsHookEx(kh);kh=IntPtr.Zero;}}
 }
 void Record(string text){if(events.Count==0)events.Add(text);active=false;}
 IntPtr Mouse(int code,IntPtr wp,IntPtr lp){if(code>=0){var e=Marshal.PtrToStructure<MOUSE>(lp);lock(gate){if(active && e.extra.ToUInt64()!=OwnInput && !(wp.ToInt64()==0x200 && e.pt.x==x && e.pt.y==y))Record("mouse msg="+wp+" cursor="+e.pt.x+","+e.pt.y+" flags="+e.flags);}}return CallNextHookEx(IntPtr.Zero,code,wp,lp);}
 IntPtr Keyboard(int code,IntPtr wp,IntPtr lp){if(code>=0){var e=Marshal.PtrToStructure<KEY>(lp);lock(gate){if(active && e.extra.ToUInt64()!=OwnInput){if(allowedPid!=0 && e.vk==0xB9 && (e.flags&0x10)!=0){if(housekeeping.Count<128)housekeeping.Add("Injected reserved VK_B9 during tray transition");}else Record("key msg="+wp+" vk="+e.vk+" flags="+e.flags);}}}return CallNextHookEx(IntPtr.Zero,code,wp,lp);}
 public void Start(){lock(gate){GetCursorPos(out var p);x=p.x;y=p.y;foreground=GetForegroundWindow();for(int vk=1;vk<256;vk++)if((GetAsyncKeyState(vk)&0x8000)!=0)Record("Input already held at step start: vk="+vk);active=true;}}
 public void Application(int pid){lock(gate){applicationPid=pid;}}
 public void TransitionTo(int pid){Verify();lock(gate){foreground=GetForegroundWindow();allowedPid=pid;}}
 public void ExpectForeground(IntPtr hwnd){Verify();lock(gate){foreground=hwnd;allowedPid=0;}}
 public void Move(int px,int py){Verify();lock(gate){x=px;y=py;}var old=SetThreadDpiAwarenessContext(new IntPtr(-4));try{if(!SetCursorPos(px,py))throw new System.ComponentModel.Win32Exception();mouse_event(0x2001,0,0,0,new UIntPtr(OwnInput));}finally{SetThreadDpiAwarenessContext(old);}}
 public static void Mouse(uint flags){mouse_event(flags,0,0,0,new UIntPtr(OwnInput));}
 public static void InjectExternalMove(){mouse_event(1,3,0,0,UIntPtr.Zero);}
 public void Verify(){lock(gate){if(faults.Count!=0)throw new InvalidOperationException(faults[0]);if(events.Count!=0)throw new InvalidOperationException("INCONCLUSIVE: external desktop input detected: "+events[0]);}}
 public object Snapshot(){lock(gate){var old=SetThreadDpiAwarenessContext(new IntPtr(-4));try{GetCursorPos(out var p);var keys=new List<int>();for(int vk=1;vk<256;vk++)if((GetAsyncKeyState(vk)&0x8000)!=0)keys.Add(vk);return new {expectedCursor=new[]{x,y},actualCursor=new[]{p.x,p.y},expectedForeground=foreground.ToInt64(),actualForeground=GetForegroundWindow().ToInt64(),allowedForegroundPid=allowedPid,actualHitTest=WindowFromPoint(p).ToInt64(),downKeys=keys.ToArray(),events=events.ToArray(),transitionHousekeeping=housekeeping.ToArray(),faults=faults.ToArray()};}finally{SetThreadDpiAwarenessContext(old);}}}
 public void Stop(){lock(gate){active=false;}}
 public void Dispose(){Stop();stopped=true;thread.Join();ready.Dispose();}
}
