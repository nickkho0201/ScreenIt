param([string]$ExecutablePath = "$PSScriptRoot/../artifacts/settings-polish/publish/ScreenIt.App.exe",[string]$EvidenceDirectory = "$PSScriptRoot/../artifacts/settings-layout",[switch]$InjectInterferenceForVerification)
$ErrorActionPreference='Stop'
if(Get-Process ScreenIt.App -ErrorAction SilentlyContinue) { throw 'Close ScreenIt before this smoke test.' }
Add-Type -Path (Join-Path $PSScriptRoot 'DesktopInputEvidence.cs')
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName System.Drawing
Add-Type -ReferencedAssemblies 'System.Drawing.Common','System.Drawing.Primitives','System.Runtime','System.Private.Windows.GdiPlus','System.Private.Windows.Core','System.Private.CoreLib' @'
using System;
using System.Drawing;
using System.Runtime.InteropServices;
public static class SettingsPointerSmoke {
 [DllImport("user32.dll",EntryPoint="SetCursorPos")] private static extern bool NativeCursor(int x,int y);
 [DllImport("user32.dll")] private static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);
 public static bool SetCursorPos(int x,int y) {var old=SetThreadDpiAwarenessContext(new IntPtr(-4));try{return NativeCursor(x,y);}finally{SetThreadDpiAwarenessContext(old);}}
 [DllImport("user32.dll")] public static extern void mouse_event(uint flags,uint x,uint y,uint data,UIntPtr extra);
 [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hwnd);
 [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr hwnd,IntPtr after,int x,int y,int width,int height,uint flags);
 [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr hwnd,IntPtr dc,uint flags);
 [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hwnd,out RECT rect);
 delegate bool WindowCallback(IntPtr hwnd,IntPtr data);
 [DllImport("user32.dll")] static extern bool EnumWindows(WindowCallback callback,IntPtr data);
 [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hwnd,out uint pid);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern int GetWindowText(IntPtr hwnd,System.Text.StringBuilder text,int length);
 public static void Exit(int pid) {EnumWindows((hwnd,data)=>{GetWindowThreadProcessId(hwnd,out uint owner);if(owner==pid){var name=new System.Text.StringBuilder(100);GetWindowText(hwnd,name,100);if(name.ToString()=="ScreenIt control")SendMessage(hwnd,0x10,IntPtr.Zero,IntPtr.Zero);}return true;},IntPtr.Zero);}
 [DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr hwnd,uint msg,IntPtr wp,IntPtr lp);
 public struct RECT {public int left,top,right,bottom;}
 public struct POINT {public int x,y;}
 [DllImport("user32.dll")] static extern bool GetCursorPos(out POINT point);
 [DllImport("user32.dll")] static extern IntPtr WindowFromPoint(POINT point);
 [DllImport("user32.dll")] public static extern uint GetDpiForWindow(IntPtr hwnd);
 public static string Pointer() {var old=SetThreadDpiAwarenessContext(new IntPtr(-4));try {GetCursorPos(out var p);return $"{p.x},{p.y}; hwnd={WindowFromPoint(p)}";}finally {SetThreadDpiAwarenessContext(old);}}
 public static bool PointerAt(IntPtr hwnd,int x,int y,bool down) {var old=SetThreadDpiAwarenessContext(new IntPtr(-4));try {GetCursorPos(out var p);return p.x==x && p.y==y && WindowFromPoint(p)==hwnd && ((GetAsyncKeyState(1)&0x8000)!=0)==down;}finally {SetThreadDpiAwarenessContext(old);}}
 [DllImport("user32.dll")] static extern short GetAsyncKeyState(int key);
 public static double[] Diff(string first,string second,int x,int y,int width,int height,string crop) {
  using(var a=new Bitmap(first)) using(var b=new Bitmap(second)) {
   var rect=new Rectangle(x,y,width,height);using(var c=b.Clone(rect,b.PixelFormat))c.Save(crop);
   long changed=0,sum=0;int max=0;
   for(int py=y;py<y+height;py++)for(int px=x;px<x+width;px++){var ca=a.GetPixel(px,py);var cb=b.GetPixel(px,py);int d=Math.Abs(ca.R-cb.R)+Math.Abs(ca.G-cb.G)+Math.Abs(ca.B-cb.B);if(d!=0)changed++;sum+=d;max=Math.Max(max,Math.Max(Math.Abs(ca.R-cb.R),Math.Max(Math.Abs(ca.G-cb.G),Math.Abs(ca.B-cb.B))));}
   return new double[]{changed,max,(double)sum/(width*height*3)};
  }
 }
 public static void Save(IntPtr hwnd,string path) {
  var old=SetThreadDpiAwarenessContext(new IntPtr(-4));try {
  RECT r; if(!GetWindowRect(hwnd,out r))throw new Exception("GetWindowRect failed");
  using(var b=new Bitmap(r.right-r.left,r.bottom-r.top)) using(var g=Graphics.FromImage(b)) {
   var dc=g.GetHdc();try {if(!PrintWindow(hwnd,dc,2))throw new Exception("PrintWindow failed");}finally {g.ReleaseHdc(dc);}b.Save(path);
  }
  } finally {SetThreadDpiAwarenessContext(old);}
 }
}
'@
$settings=Join-Path $env:LOCALAPPDATA 'ScreenIt/settings.json'
$backup=if(Test-Path $settings){[IO.File]::ReadAllBytes($settings)}else{$null}
$root=[System.Windows.Automation.AutomationElement]::RootElement
$out=[IO.Path]::GetFullPath($EvidenceDirectory)
[void][IO.Directory]::CreateDirectory($out)
$checks=[Collections.Generic.List[string]]::new()
$diagnostics=[Collections.Generic.List[object]]::new();$desktop=[DesktopInputEvidence]::new();$desktop.Start()
$labels=@{'General'='Основные';'Hotkeys'='Клавиши';'About'='О программе';'Settings'='Настройки';'Reset defaults'='Восстановить стандартные';'Open repository'='Открыть репозиторий';'Check for updates'='Проверить обновления'}
function Ui-Name([string]$name) { if($script:uiLanguage -eq 'ru' -and $labels.ContainsKey($name)){$labels[$name]}else{$name} }
function Find-Name($parent,[string]$name) {
 $parent.FindFirst([System.Windows.Automation.TreeScope]::Descendants,[System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::NameProperty,(Ui-Name $name)))
}
function Wait-Observed([string]$description,[scriptblock]$condition) {
 $deadline=[Diagnostics.Stopwatch]::StartNew()
 do { $desktop.Verify();$value=& $condition;if($value){return $value};Start-Sleep -Milliseconds 16 }while($deadline.ElapsedMilliseconds -lt 5000)
 throw "Timed out observing $description; pointer=$([SettingsPointerSmoke]::Pointer())"
}
function Invoke-Item($item) { ([System.Windows.Automation.InvokePattern]$item.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern)).Invoke() }
function Click-Right($item) {
 $r=$item.Current.BoundingRectangle
 $desktop.Move([int]($r.Left+$r.Width/2),[int]($r.Top+$r.Height/2))
 [DesktopInputEvidence]::Mouse(8);[DesktopInputEvidence]::Mouse(16)
}
function Save-State($window,[string]$name) { $desktop.Verify(); [SettingsPointerSmoke]::Save([IntPtr]$window.Current.NativeWindowHandle,(Join-Path $out "$name.png")) }
function Sample-Button($window,$button,[string]$name,[int]$expected) {
 $r=$button.Current.BoundingRectangle;$w=$window.Current.BoundingRectangle
 $path=Join-Path $out "$name.png"
 # Observe the actual rendered template surface, rather than assuming a fixed delay rendered it.
 Wait-Observed "rendered $name surface=$expected" {
  Save-State $window $name;$bitmap=[Drawing.Bitmap]::new($path)
  try { $script:lastSurface=$bitmap.GetPixel([int]($r.Left-$w.Left+8),[int]($r.Top-$w.Top+$r.Height/2)).ToArgb();$script:lastSurface -eq $expected }finally { $bitmap.Dispose() }
 } | Out-Null
 return $script:lastSurface
}
function Tray-Icons { $root.FindAll([System.Windows.Automation.TreeScope]::Descendants,[System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::ControlTypeProperty,[System.Windows.Automation.ControlType]::Button)) }
function Native-Parent($item) {
 while($item -and !$item.Current.NativeWindowHandle){$item=[System.Windows.Automation.TreeWalker]::ControlViewWalker.GetParent($item)}
 if(!$item){throw 'Owned menu native HWND unavailable'}
 return [IntPtr]$item.Current.NativeWindowHandle
}
$process=$null;$ownMouseDown=$false
try {
 foreach($theme in @('dark','light')) {
  $desktop.Start();$script:uiLanguage=if($theme -eq 'dark'){'ru'}else{'en'}
  [IO.File]::WriteAllText($settings,"{`"theme`":`"$theme`",`"language`":`"$script:uiLanguage`"}")
  $process=Start-Process -FilePath $ExecutablePath -WindowStyle Hidden -PassThru;$desktop.Application($process.Id)
  $icons=Tray-Icons
  $icon=$icons | Where-Object {$_.Current.Name -match '^ScreenIt ScreenIt'} | Select-Object -First 1
  if(!$icon) {
   $overflow=$icons | Where-Object {$_.Current.Name -match 'hidden icons|скрытые значки'} | Select-Object -First 1
   if(!$overflow) { $overflow=$icons | Where-Object {$_.Current.Name -match 'hidden icons|скрытые значки'} | Select-Object -First 1 }
   if(!$overflow) { throw 'Tray overflow control unavailable.' };$desktop.TransitionTo($overflow.Current.ProcessId);$overflowWindow=$root.FindAll([System.Windows.Automation.TreeScope]::Children,[System.Windows.Automation.Condition]::TrueCondition) | Where-Object {$_.Current.ClassName -in @('TopLevelWindowForOverflowXamlIsland','NotifyIconOverflowWindow') -and !$_.Current.IsOffscreen} | Select-Object -First 1;if(!$overflowWindow){Invoke-Item $overflow}
   $icon=Wait-Observed 'ScreenIt tray icon' {Tray-Icons | Where-Object {$_.Current.Name -match '^ScreenIt ScreenIt' -and !$_.Current.IsOffscreen} | Select-Object -First 1}
  }
  if(!$icon) { throw 'ScreenIt tray icon unavailable.' };$desktop.TransitionTo($process.Id);Click-Right $icon
  $menu=Wait-Observed 'owned Settings tray menu' {$root.FindFirst([System.Windows.Automation.TreeScope]::Descendants,[System.Windows.Automation.AndCondition]::new([System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::NameProperty,(Ui-Name 'Settings')),[System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::ProcessIdProperty,$process.Id)))};$menuHwnd=Native-Parent $menu;$desktop.ExpectForeground($menuHwnd);$diagnostics.Add(@{step='tray menu';expectedMenuHwnd=$menuHwnd.ToInt64();desktop=$desktop.Snapshot()});$desktop.TransitionTo($process.Id);Invoke-Item $menu
  $window=Wait-Observed 'owned Settings window' {$root.FindAll([System.Windows.Automation.TreeScope]::Children,[System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::ProcessIdProperty,$process.Id)) | Where-Object {$_.Current.Name -eq 'ScreenIt'} | Select-Object -First 1}
  if(!$window){$root.FindAll([System.Windows.Automation.TreeScope]::Children,[System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::ProcessIdProperty,$process.Id)) | ForEach-Object {$_.Current.Name};throw 'Production Settings window not found.'}
  [void][SettingsPointerSmoke]::SetWindowPos([IntPtr]$window.Current.NativeWindowHandle,[IntPtr]::new(-1),0,0,0,0,3)
  $desktop.ExpectForeground([IntPtr]$window.Current.NativeWindowHandle);
  foreach($entry in @(@('General','General'),@('Hotkeys','Hotkeys'),@('About','About'),@('Hotkeys','Reset defaults'),@('About','Open repository'),@('About','Check for updates'))) {
   [void][SettingsPointerSmoke]::SetForegroundWindow([IntPtr]$window.Current.NativeWindowHandle)
   Invoke-Item (Find-Name $window $entry[0]);Wait-Observed "selected page $($entry[0])" {(Find-Name $window $entry[0]).Current.ItemStatus -eq 'Current page'} | Out-Null
   $button=Wait-Observed "laid out button $($entry[1])" {$item=Find-Name $window $entry[1];if($item -and $item.Current.BoundingRectangle.Width -gt 16 -and !$item.Current.IsOffscreen){$item}}
   $r=$button.Current.BoundingRectangle;$w=$window.Current.BoundingRectangle
   $hwnd=[IntPtr]$window.Current.NativeWindowHandle;$nx=[int]($w.Right-30);$ny=[int]($w.Bottom-35);$cx=[int]($r.Left+$r.Width/2);$cy=[int]($r.Top+$r.Height/2)
   $colors=if($theme -eq 'dark'){@(@(32,39,51),@(53,65,83),@(13,19,31))}else{@(@(255,255,255),@(229,235,246),@(202,214,235))}
   $expected=@($colors | ForEach-Object {[Drawing.Color]::FromArgb($_[0],$_[1],$_[2]).ToArgb()})
   $desktop.Move($nx,$ny);Wait-Observed 'normal pointer outside button' {[SettingsPointerSmoke]::PointerAt($hwnd,$nx,$ny,$false)} | Out-Null
   $normal=Sample-Button $window $button "$theme-$($entry[1])-normal" $expected[0];$normalPointer=[SettingsPointerSmoke]::Pointer();$normalDesktop=$desktop.Snapshot();if($InjectInterferenceForVerification){[DesktopInputEvidence]::InjectExternalMove();Wait-Observed 'injected external event' {$desktop.Verify();$false}}
   if($entry[1] -eq 'General') {Save-State $window "general-$theme-$script:uiLanguage"}
   if($theme -eq 'dark' -and $entry[1] -eq 'Reset defaults') {Save-State $window 'hotkeys'}
   if($theme -eq 'dark' -and $entry[1] -eq 'Open repository') {Save-State $window 'about'}
   $desktop.Move($cx,$cy);Wait-Observed 'hover pointer inside owned button' {[SettingsPointerSmoke]::PointerAt($hwnd,$cx,$cy,$false)} | Out-Null
   $hover=Sample-Button $window $button "$theme-$($entry[1])-hover" $expected[1];$hoverPointer=[SettingsPointerSmoke]::Pointer();$hoverDesktop=$desktop.Snapshot()
   [DesktopInputEvidence]::Mouse(2);$ownMouseDown=$true;Wait-Observed 'left mouse down inside owned button' {[SettingsPointerSmoke]::PointerAt($hwnd,$cx,$cy,$true)} | Out-Null
   $pressed=Sample-Button $window $button "$theme-$($entry[1])-pressed" $expected[2];$pressedPointer=[SettingsPointerSmoke]::Pointer()
   $prefix=Join-Path $out "$theme-$($entry[1])";$x=[int]($r.Left-$w.Left);$y=[int]($r.Top-$w.Top);$width=[int]$r.Width;$height=[int]$r.Height
   $nh=[SettingsPointerSmoke]::Diff("$prefix-normal.png","$prefix-hover.png",$x,$y,$width,$height,"$prefix-hover-crop.png")
   $hp=[SettingsPointerSmoke]::Diff("$prefix-hover.png","$prefix-pressed.png",$x,$y,$width,$height,"$prefix-pressed-crop.png")
   [void][SettingsPointerSmoke]::Diff("$prefix-normal.png","$prefix-normal.png",$x,$y,$width,$height,"$prefix-normal-crop.png")
   $diagnostics.Add(@{theme=$theme;button=$entry[1];hwnd=$window.Current.NativeWindowHandle;controlType=$button.Current.ControlType.ProgrammaticName;bounds="$r";windowBounds="$w";dpi=[SettingsPointerSmoke]::GetDpiForWindow([IntPtr]$window.Current.NativeWindowHandle);crop=@($x,$y,$width,$height);normalPointer=$normalPointer;hoverPointer=$hoverPointer;pressedPointer=$pressedPointer;samples=@($normal,$hover,$pressed);normalHover=$nh;hoverPressed=$hp;expectedHitTest=$hwnd.ToInt64();expectedNormalDownKeys=@();expectedHoverDownKeys=@();expectedPressedDownKeys=@(1);normalDesktop=$normalDesktop;hoverDesktop=$hoverDesktop;pressedDesktop=$desktop.Snapshot()})
   # WPF keeps IsMouseOver true during capture. Observe IsPressed clearing (hover surface)
   # before mouse-up; otherwise the queued release can accidentally invoke the action.
   $desktop.Move($nx,$ny);Wait-Observed 'release pointer outside owned button' {[SettingsPointerSmoke]::PointerAt($hwnd,$nx,$ny,$true)} | Out-Null;Sample-Button $window $button "$theme-$($entry[1])-release-outside" $expected[1] | Out-Null;[DesktopInputEvidence]::Mouse(4);$ownMouseDown=$false
   if($normal -eq $hover -or $hover -eq $pressed){throw "Indistinct states: $theme / $($entry[1])"}
   $checks.Add("Production pointer normal/hover/pressed distinct: $theme / $($entry[1])")
  }
  $desktop.Verify();$desktop.Stop();
  [void][SettingsPointerSmoke]::SendMessage([IntPtr]$window.Current.NativeWindowHandle,0x10,[IntPtr]::Zero,[IntPtr]::Zero)
  # Empty test session: closing the tray utility needs no destructive confirmation.
  [SettingsPointerSmoke]::Exit($process.Id)
  if(!$process.WaitForExit(5000) -or $process.ExitCode -ne 0) { throw 'Production shutdown failed.' }

 }
 $report=@{status='PASS';count=$checks.Count;checks=$checks}
} catch { $evidence=$desktop.Snapshot();if($evidence.events.Length -gt 0){$report=@{status='INCONCLUSIVE';checks=$checks;reason='external desktop input detected';error=$_.Exception.Message;desktop=$evidence}}else{$report=@{status='FAIL';checks=$checks;reason='Controlled desktop; expected production UI/state was not observed';error=$_.Exception.Message;desktop=$evidence};throw} }
finally {
 $desktop.Stop();$desktop.Dispose()
 if($ownMouseDown){[DesktopInputEvidence]::Mouse(4)}
 if($process -and !$process.HasExited){$process.Kill();$process.WaitForExit()}
 if($null -ne $backup){[IO.File]::WriteAllBytes($settings,$backup)}else{Remove-Item -LiteralPath $settings -ErrorAction SilentlyContinue}
 $report | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $out 'hover-smoke.json') -Encoding utf8
 $diagnostics | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $out 'pointer-evidence.json') -Encoding utf8
}
$report | ConvertTo-Json -Depth 4
