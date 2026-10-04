param([string]$ExecutablePath, [string]$ReportName = 'smoke.json', [switch]$CustomBindings)
$ErrorActionPreference = 'Stop'
if (Get-Process ScreenIt.App -ErrorAction SilentlyContinue) { throw 'Exit ScreenIt before running the production smoke test.' }
if (-not ('ScreenItSmoke' -as [type])) {
Add-Type @'
using System;
using System.Text;
using System.Collections.Generic;
using System.Runtime.InteropServices;
public static class ScreenItSmoke {
delegate bool Callback(IntPtr hwnd,IntPtr data);
[DllImport("user32.dll")] static extern bool EnumWindows(Callback cb,IntPtr data);
[DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hwnd,out uint pid);
[DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern int GetWindowText(IntPtr hwnd,StringBuilder text,int size);
[DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern int GetClassName(IntPtr hwnd,StringBuilder text,int size);
[DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr hwnd);
[DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr hwnd,uint msg,IntPtr wp,IntPtr lp);
[DllImport("user32.dll",SetLastError=true)] public static extern bool RegisterHotKey(IntPtr hwnd,int id,uint mods,uint key);
[DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr hwnd,int id);
[DllImport("user32.dll")] public static extern uint GetClipboardSequenceNumber();
public static IntPtr Control(int process) {IntPtr result=IntPtr.Zero;EnumWindows((h,d)=>{GetWindowThreadProcessId(h,out uint p);if(p==process){var text=new StringBuilder(256);GetWindowText(h,text,256);if(text.ToString()=="ScreenIt control")result=h;}return true;},IntPtr.Zero);return result;}
public static IntPtr[] VisibleWpf(int process) {var result=new List<IntPtr>();EnumWindows((h,d)=>{GetWindowThreadProcessId(h,out uint p);if(p==process&&IsWindowVisible(h)){var name=new StringBuilder(256);GetClassName(h,name,256);if(name.ToString().StartsWith("HwndWrapper"))result.Add(h);}return true;},IntPtr.Zero);return result.ToArray();}
}
'@
}
Add-Type -AssemblyName UIAutomationClient
function Invoke-SmokeButton([IntPtr]$window, [string]$name) {
    $element = [System.Windows.Automation.AutomationElement]::FromHandle($window)
    $condition = [System.Windows.Automation.AndCondition]::new([System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::NameProperty, $name),[System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::IsOffscreenProperty, $false))
    $button = $element.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $condition)
    if ($null -eq $button) { throw "Own-process button not found: $name" }
    ([System.Windows.Automation.InvokePattern]$button.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern)).Invoke()
}
function Wait-SmokeCapture([int]$processId) {
    for ($attempt=0; $attempt -lt 100; $attempt++) {
        foreach($window in [ScreenItSmoke]::VisibleWpf($processId)) {
            try { $element=[System.Windows.Automation.AutomationElement]::FromHandle($window) } catch { continue }
            if($element.Current.Name -eq '') { Start-Sleep -Milliseconds 500;return $window }
        }
        Start-Sleep -Milliseconds 50
    }
    throw 'Capture overlay was not visible.'
}
function Wait-SmokeWindow([int]$processId, [string]$buttonName) {
    for ($attempt=0; $attempt -lt 100; $attempt++) {
        foreach ($window in [ScreenItSmoke]::VisibleWpf($processId)) {
            try { $element = [System.Windows.Automation.AutomationElement]::FromHandle($window) } catch { continue }
            $condition = [System.Windows.Automation.AndCondition]::new([System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::NameProperty, $buttonName),[System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::IsOffscreenProperty, $false))
            if ($null -ne $element.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $condition)) { return $window }
        }
        Start-Sleep -Milliseconds 50
    }
    $visibleCount = [ScreenItSmoke]::VisibleWpf($processId).Length
    $roles=foreach($hwnd in [ScreenItSmoke]::VisibleWpf($processId)) {
        try {
            $name=([System.Windows.Automation.AutomationElement]::FromHandle($hwnd)).Current.Name
            if($name -eq 'Clear session — ScreenIt') { 'clear' } elseif($name -eq 'ScreenIt feedback') { 'feedback' } elseif($name -eq '') { 'overlay' } elseif($name -eq 'ScreenIt') { 'application-prompt' } else { 'other-owned-window' }
        } catch { 'unavailable-owned-window' }
    }
    throw "Timed out waiting for own-process control: $buttonName; own visible HWND count=$visibleCount; roles=$($roles -join ',')"
}
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$exe = if ($ExecutablePath) { (Resolve-Path -LiteralPath $ExecutablePath).Path } else { Join-Path $repoRoot 'src/ScreenIt.App/bin/Release/net10.0-windows/ScreenIt.App.exe' }
if ([IO.Path]::GetFileName($ReportName) -ne $ReportName) { throw 'ReportName must be a filename.' }
$checks = [Collections.Generic.List[string]]::new()
$artifacts = Join-Path $repoRoot 'artifacts'; [void][IO.Directory]::CreateDirectory($artifacts)
# Exercise a deterministic English/default binding configuration, restoring the user's preferences byte-for-byte.
$settingsPath=Join-Path $env:LOCALAPPDATA 'ScreenIt/settings.json'
$settingsBytes=if(Test-Path -LiteralPath $settingsPath) { [IO.File]::ReadAllBytes($settingsPath) } else { $null }
[void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($settingsPath))
$mods=if($CustomBindings) { 0x4006 } else { 0x4003 }
$captureKey=if($CustomBindings) { 0x51 } else { 0x53 }
$pasteKey=if($CustomBindings) { 0x57 } else { 0x56 }
$clearKey=if($CustomBindings) { 0x45 } else { 0x58 }
$config=@{schemaVersion=2;theme='dark';language='en';hotkeys=@{capture=@{modifiers=($mods -band 15);key=$captureKey};paste=@{modifiers=($mods -band 15);key=$pasteKey};clear=@{modifiers=($mods -band 15);key=$clearKey}}}
[IO.File]::WriteAllText($settingsPath,($config | ConvertTo-Json -Depth 5))
$first = Start-Process -FilePath $exe -PassThru -WindowStyle Hidden -RedirectStandardError (Join-Path $artifacts 'startup-error.txt')
try {
    $control = [IntPtr]::Zero
    for ($i=0; $i -lt 100 -and $control -eq [IntPtr]::Zero; $i++) { Start-Sleep -Milliseconds 100; $control = [ScreenItSmoke]::Control($first.Id) }
    if ($control -eq [IntPtr]::Zero -or $first.HasExited) { throw 'Startup failed.' }
    $checks.Add('Production startup')
    if (Test-Path -LiteralPath (Join-Path ([IO.Path]::GetDirectoryName($exe)) 'coreclr.dll')) {
        $loaded = (Get-Process -Id $first.Id).Modules | Where-Object ModuleName -eq 'coreclr.dll'
        if ($null -eq $loaded -or $loaded.FileName -ne (Join-Path ([IO.Path]::GetDirectoryName($exe)) 'coreclr.dll')) { throw 'Self-contained runtime not loaded from package.' }
        $checks.Add('Self-contained CoreCLR loaded from published/installed application directory')
    }
    if ([ScreenItSmoke]::VisibleWpf($first.Id).Length -ne 0) { throw 'Unexpected persistent WPF startup window.' }
    $checks.Add('Background startup without editor window')
    # HWND becomes visible to enumeration before the constructor finishes startup.
    Start-Sleep -Milliseconds 1500
    if ([ScreenItSmoke]::RegisterHotKey([IntPtr]::Zero,77,$mods,$captureKey)) { [void][ScreenItSmoke]::UnregisterHotKey([IntPtr]::Zero,77); throw 'Expected registered Ctrl+Alt+S.' }
    $checks.Add('Global capture hotkey registered')
    if ([ScreenItSmoke]::RegisterHotKey([IntPtr]::Zero,78,$mods,$pasteKey)) { [void][ScreenItSmoke]::UnregisterHotKey([IntPtr]::Zero,78); throw 'Expected registered Ctrl+Alt+V.' }
    $checks.Add('Global Paste Session hotkey registered')
    if ([ScreenItSmoke]::RegisterHotKey([IntPtr]::Zero,79,$mods,$clearKey)) { [void][ScreenItSmoke]::UnregisterHotKey([IntPtr]::Zero,79); throw 'Expected registered Ctrl+Alt+X.' }
    $checks.Add('Global Clear Session hotkey registered alongside Capture/Paste')
    $clipboardBefore = [ScreenItSmoke]::GetClipboardSequenceNumber()
    [void][ScreenItSmoke]::SendMessage($control,0x312,[IntPtr]2,[IntPtr]::Zero)
    Start-Sleep -Milliseconds 100
    if ([ScreenItSmoke]::GetClipboardSequenceNumber() -ne $clipboardBefore) { throw 'Empty-session Paste Session changed clipboard.' }
    $checks.Add('Production Paste hotkey with empty session leaves clipboard untouched')
    [void][ScreenItSmoke]::SendMessage($control,0x312,[IntPtr]3,[IntPtr]::Zero)
    Start-Sleep -Milliseconds 100
    if ([ScreenItSmoke]::GetClipboardSequenceNumber() -ne $clipboardBefore -or [ScreenItSmoke]::VisibleWpf($first.Id).Length -ne 0) { throw 'Empty Clear changed clipboard or opened confirmation.' }
    $checks.Add('Empty Clear hotkey leaves clipboard intact without confirmation')
    $second = Start-Process -FilePath $exe -PassThru -WindowStyle Hidden
    if (-not $second.WaitForExit(3000) -or $second.ExitCode -ne 0) { throw 'Second process did not exit normally.' }
    $first.Refresh(); if ($first.HasExited) { throw 'Original instance stopped.' }
    $checks.Add('Single instance: second exits 0, first alive')
    [void][ScreenItSmoke]::SendMessage($control,0x312,[IntPtr]1,[IntPtr]::Zero)
    $overlays = @()
    for ($i=0; $i -lt 100 -and $overlays.Length -eq 0; $i++) { Start-Sleep -Milliseconds 50; $overlays = [ScreenItSmoke]::VisibleWpf($first.Id) }
    if ($overlays.Length -eq 0) { throw 'Hotkey route produced no overlays.' }
    # Visibility precedes WPF ContentRendered and async capture completion.
    Start-Sleep -Milliseconds 500
    $checks.Add('Production WM_HOTKEY route shows frozen overlays')
    [void][ScreenItSmoke]::SendMessage($overlays[0],0x10,[IntPtr]::Zero,[IntPtr]::Zero)
    for ($i=0; $i -lt 100 -and [ScreenItSmoke]::VisibleWpf($first.Id).Length -ne 0; $i++) { Start-Sleep -Milliseconds 50 }
    if ([ScreenItSmoke]::VisibleWpf($first.Id).Length -ne 0) { throw 'Capture cancel left visible overlays.' }
    $checks.Add('Explicit selection cancel returns to background')
    # All automation below targets only this newly spawned ScreenIt PID. No receiver automation.
    [void][ScreenItSmoke]::SendMessage($control,0x312,[IntPtr]1,[IntPtr]::Zero)
    $captureWindow = Wait-SmokeCapture $first.Id
    [void][ScreenItSmoke]::SendMessage($captureWindow,0x100,[IntPtr]0x20,[IntPtr]::Zero)
    [void][ScreenItSmoke]::SendMessage($captureWindow,0x101,[IntPtr]0x20,[IntPtr]::Zero)
    Start-Sleep -Milliseconds 150
    $captureWindow = Wait-SmokeWindow $first.Id 'Done (Ctrl+Enter)'
    Invoke-SmokeButton $captureWindow 'Done (Ctrl+Enter)'
    [void](Wait-SmokeWindow $first.Id 'Screenshot A added')
    $checks.Add('Real process: full-monitor selection and actual screenshot commit seed RAM session')
    [void][ScreenItSmoke]::SendMessage($control,0x312,[IntPtr]3,[IntPtr]::Zero)
    $confirmation = Wait-SmokeWindow $first.Id 'Keep session'
    Invoke-SmokeButton $confirmation 'Keep session'
    Start-Sleep -Milliseconds 150
    if ([ScreenItSmoke]::VisibleWpf($first.Id) -contains $confirmation) { throw 'Cancelled confirmation remained visible.' }
    $checks.Add('Real process: Clear hotkey opens actual confirmation; Cancel destroys it')
    [void][ScreenItSmoke]::SendMessage($control,0x312,[IntPtr]1,[IntPtr]::Zero)
    $captureWindow = Wait-SmokeCapture $first.Id
    [void][ScreenItSmoke]::SendMessage($captureWindow,0x10,[IntPtr]::Zero,[IntPtr]::Zero)
    Start-Sleep -Milliseconds 150
    $checks.Add('Real process: Capture command works after Clear Cancel')
    [void][ScreenItSmoke]::SendMessage($control,0x312,[IntPtr]3,[IntPtr]::Zero)
    $confirmation = Wait-SmokeWindow $first.Id 'Keep session'
    Invoke-SmokeButton $confirmation 'Clear session'
    Start-Sleep -Milliseconds 150
    $checks.Add('Real process: Clear can reopen after Cancel; Confirm destroys it')
    # Empty Clear cannot create a new confirmation: establishes actual count reset, not just UI closing.
    [void][ScreenItSmoke]::SendMessage($control,0x312,[IntPtr]3,[IntPtr]::Zero)
    Start-Sleep -Milliseconds 150
    foreach($window in [ScreenItSmoke]::VisibleWpf($first.Id)) {
        try { $element=[System.Windows.Automation.AutomationElement]::FromHandle($window) } catch { continue }
        $condition=[System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::NameProperty,'Keep session')
        if($null -ne $element.FindFirst([System.Windows.Automation.TreeScope]::Descendants,$condition)) { throw 'Confirmed Clear did not empty session.' }
    }
    $checks.Add('Real process: confirmed Clear empties RAM session')
    [void][ScreenItSmoke]::SendMessage($control,0x312,[IntPtr]1,[IntPtr]::Zero)
    $captureWindow = Wait-SmokeCapture $first.Id
    [void][ScreenItSmoke]::SendMessage($captureWindow,0x10,[IntPtr]::Zero,[IntPtr]::Zero)
    Start-Sleep -Milliseconds 150
    $checks.Add('Real process: Capture works after confirmed Clear')
    [void][ScreenItSmoke]::SendMessage($control,0x10,[IntPtr]::Zero,[IntPtr]::Zero)
    if (-not $first.WaitForExit(5000) -or $first.ExitCode -ne 0) { throw 'Production shutdown failed.' }
    $checks.Add('Clean shutdown exit 0')
    if (-not [ScreenItSmoke]::RegisterHotKey([IntPtr]::Zero,77,$mods,$captureKey)) { throw 'Capture hotkey not released.' }
    [void][ScreenItSmoke]::UnregisterHotKey([IntPtr]::Zero,77)
    $checks.Add('Capture hotkey released')
    if (-not [ScreenItSmoke]::RegisterHotKey([IntPtr]::Zero,78,$mods,$pasteKey)) { throw 'Paste Session hotkey not released.' }
    [void][ScreenItSmoke]::UnregisterHotKey([IntPtr]::Zero,78)
    $checks.Add('Paste Session hotkey released')
    if (-not [ScreenItSmoke]::RegisterHotKey([IntPtr]::Zero,79,$mods,$clearKey)) { throw 'Clear Session hotkey not released.' }
    [void][ScreenItSmoke]::UnregisterHotKey([IntPtr]::Zero,79)
    $checks.Add('Clear Session hotkey released')
    $report = [ordered]@{status='PASS';customBindings=[bool]$CustomBindings;count=$checks.Count;checks=$checks;date=[DateTimeOffset]::UtcNow.ToString('O')}
} catch {
    $report = [ordered]@{status='FAIL';checks=$checks;error=$_.Exception.Message;date=[DateTimeOffset]::UtcNow.ToString('O')}
    throw
} finally {
    if (-not $first.HasExited) { Stop-Process -Id $first.Id }
    if($null -ne $settingsBytes) { [IO.File]::WriteAllBytes($settingsPath,$settingsBytes) } elseif(Test-Path -LiteralPath $settingsPath) { Remove-Item -LiteralPath $settingsPath }
    $artifacts = Join-Path $repoRoot 'artifacts'; [void][IO.Directory]::CreateDirectory($artifacts)
    $report | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $artifacts $ReportName) -Encoding utf8
}
$report | ConvertTo-Json -Depth 4
