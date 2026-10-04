param([string]$ExecutablePath = "$PSScriptRoot/../artifacts/settings-polish/publish/ScreenIt.App.exe")
$ErrorActionPreference='Stop'
if(Get-Process ScreenIt.App -ErrorAction SilentlyContinue) { throw 'Close ScreenIt before this smoke test.' }
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
 public static void Save(IntPtr hwnd,string path) {
  RECT r; GetWindowRect(hwnd,out r);
  using(var b=new Bitmap(r.right-r.left,r.bottom-r.top)) using(var g=Graphics.FromImage(b)) {
   var dc=g.GetHdc();try {if(!PrintWindow(hwnd,dc,2))throw new Exception("PrintWindow failed");}finally {g.ReleaseHdc(dc);}b.Save(path);
  }
 }
}
'@
$settings=Join-Path $env:LOCALAPPDATA 'ScreenIt/settings.json'
$backup=if(Test-Path $settings){[IO.File]::ReadAllBytes($settings)}else{$null}
$root=[System.Windows.Automation.AutomationElement]::RootElement
$out=Join-Path $PSScriptRoot '../artifacts/settings-layout'
[void][IO.Directory]::CreateDirectory($out)
$checks=[Collections.Generic.List[string]]::new()
$labels=@{'General'='Основные';'Hotkeys'='Клавиши';'About'='О программе';'Settings'='Настройки';'Reset defaults'='Восстановить стандартные';'Open repository'='Открыть репозиторий';'Check for updates'='Проверить обновления'}
function Ui-Name([string]$name) { if($script:uiLanguage -eq 'ru' -and $labels.ContainsKey($name)){$labels[$name]}else{$name} }
function Find-Name($parent,[string]$name) {
 $parent.FindFirst([System.Windows.Automation.TreeScope]::Descendants,[System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::NameProperty,(Ui-Name $name)))
}
function Invoke-Item($item) { ([System.Windows.Automation.InvokePattern]$item.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern)).Invoke();Start-Sleep -Milliseconds 180 }
function Click-Right($item) {
 $r=$item.Current.BoundingRectangle
 [void][SettingsPointerSmoke]::SetCursorPos([int]($r.Left+$r.Width/2),[int]($r.Top+$r.Height/2))
 [SettingsPointerSmoke]::mouse_event(8,0,0,0,[UIntPtr]::Zero);[SettingsPointerSmoke]::mouse_event(16,0,0,0,[UIntPtr]::Zero);Start-Sleep -Milliseconds 200
}
function Save-State($window,[string]$name) { [SettingsPointerSmoke]::Save([IntPtr]$window.Current.NativeWindowHandle,(Join-Path $out "$name.png")) }
function Sample-Button($window,$button) {
 $r=$button.Current.BoundingRectangle;$w=$window.Current.BoundingRectangle
 $path=Join-Path $out 'sample.png';Save-State $window 'sample'
 $bitmap=[Drawing.Bitmap]::new($path)
 try { $bitmap.GetPixel([int]($r.Left-$w.Left+8),[int]($r.Top-$w.Top+$r.Height/2)).ToArgb() }finally { $bitmap.Dispose() }
}
$process=$null
try {
 foreach($theme in @('dark','light')) {
  $script:uiLanguage=if($theme -eq 'dark'){'ru'}else{'en'}
  [IO.File]::WriteAllText($settings,"{`"theme`":`"$theme`",`"language`":`"$script:uiLanguage`"}")
  $process=Start-Process -FilePath $ExecutablePath -WindowStyle Hidden -PassThru;Start-Sleep -Milliseconds 800
  $icons=$root.FindAll([System.Windows.Automation.TreeScope]::Descendants,[System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::ControlTypeProperty,[System.Windows.Automation.ControlType]::Button))
  $icon=$icons | Where-Object {$_.Current.Name -match '^ScreenIt ScreenIt'} | Select-Object -First 1
  if(!$icon) {
   $overflow=$icons | Where-Object {$_.Current.AutomationId -eq 'SystemTrayIcon'} | Select-Object -First 1
   if(!$overflow) { $overflow=$icons | Where-Object {$_.Current.Name -match 'hidden icons|скрытые значки'} | Select-Object -First 1 }
   if(!$overflow) { throw 'Tray overflow control unavailable.' };Invoke-Item $overflow
   $icon=$root.FindAll([System.Windows.Automation.TreeScope]::Descendants,[System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::ControlTypeProperty,[System.Windows.Automation.ControlType]::Button)) | Where-Object {$_.Current.Name -match '^ScreenIt ScreenIt'} | Select-Object -First 1
  }
  if(!$icon) { throw 'ScreenIt tray icon unavailable.' };Click-Right $icon
  $menu=$root.FindFirst([System.Windows.Automation.TreeScope]::Descendants,[System.Windows.Automation.AndCondition]::new([System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::NameProperty,(Ui-Name 'Settings')),[System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::ProcessIdProperty,$process.Id))); if(!$menu){throw 'Settings menu not found.'};Invoke-Item $menu;Start-Sleep -Milliseconds 500
  $window=$root.FindAll([System.Windows.Automation.TreeScope]::Children,[System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::ProcessIdProperty,$process.Id)) | Where-Object {$_.Current.Name -eq 'ScreenIt'} | Select-Object -First 1
  if(!$window){$root.FindAll([System.Windows.Automation.TreeScope]::Children,[System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::ProcessIdProperty,$process.Id)) | ForEach-Object {$_.Current.Name};throw 'Production Settings window not found.'}
  [void][SettingsPointerSmoke]::SetWindowPos([IntPtr]$window.Current.NativeWindowHandle,[IntPtr]::new(-1),0,0,0,0,3)
  foreach($entry in @(@('General','General'),@('Hotkeys','Hotkeys'),@('About','About'),@('Hotkeys','Reset defaults'),@('About','Open repository'),@('About','Check for updates'))) {
   [void][SettingsPointerSmoke]::SetForegroundWindow([IntPtr]$window.Current.NativeWindowHandle)
   Invoke-Item (Find-Name $window $entry[0]);$button=Find-Name $window $entry[1]
   $r=$button.Current.BoundingRectangle;$w=$window.Current.BoundingRectangle
   [void][SettingsPointerSmoke]::SetCursorPos([int]($w.Left+10),[int]($w.Top+10));Start-Sleep -Milliseconds 180
   $normal=Sample-Button $window $button;Save-State $window "$theme-$($entry[1])-normal"
   if($entry[1] -eq 'General') {Save-State $window "general-$theme-$script:uiLanguage"}
   if($theme -eq 'dark' -and $entry[1] -eq 'Reset defaults') {Save-State $window 'hotkeys'}
   if($theme -eq 'dark' -and $entry[1] -eq 'Open repository') {Save-State $window 'about'}
   [void][SettingsPointerSmoke]::SetCursorPos([int]($r.Left+$r.Width/2),[int]($r.Top+$r.Height/2));Start-Sleep -Milliseconds 180
   $hover=Sample-Button $window $button;Save-State $window "$theme-$($entry[1])-hover"
   [SettingsPointerSmoke]::mouse_event(2,0,0,0,[UIntPtr]::Zero);Start-Sleep -Milliseconds 180
   $pressed=Sample-Button $window $button;Save-State $window "$theme-$($entry[1])-pressed"
   [void][SettingsPointerSmoke]::SetCursorPos([int]($w.Left+10),[int]($w.Top+10));[SettingsPointerSmoke]::mouse_event(4,0,0,0,[UIntPtr]::Zero)
   if($normal -eq $hover -or $hover -eq $pressed){throw "Indistinct states: $theme / $($entry[1])"}
   $checks.Add("Production pointer normal/hover/pressed distinct: $theme / $($entry[1])")
  }
  [void][SettingsPointerSmoke]::SendMessage([IntPtr]$window.Current.NativeWindowHandle,0x10,[IntPtr]::Zero,[IntPtr]::Zero)
  # Empty test session: closing the tray utility needs no destructive confirmation.
  [SettingsPointerSmoke]::Exit($process.Id)
  if(!$process.WaitForExit(5000) -or $process.ExitCode -ne 0) { throw 'Production shutdown failed.' }

 }
 $report=@{status='PASS';count=$checks.Count;checks=$checks}
} catch { $report=@{status='FAIL';checks=$checks;error=$_.Exception.Message};throw }
finally {
 [SettingsPointerSmoke]::mouse_event(4,0,0,0,[UIntPtr]::Zero)
 if($process -and !$process.HasExited){$process.Kill();$process.WaitForExit()}
 if($null -ne $backup){[IO.File]::WriteAllBytes($settings,$backup)}else{Remove-Item -LiteralPath $settings -ErrorAction SilentlyContinue}
 $report | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $out 'hover-smoke.json') -Encoding utf8
}
$report | ConvertTo-Json -Depth 4
