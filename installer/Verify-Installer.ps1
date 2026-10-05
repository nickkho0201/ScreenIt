$ErrorActionPreference = 'Stop'
if (Get-Process ScreenIt.App -ErrorAction SilentlyContinue) { throw 'Exit ScreenIt before installer verification.' }
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$release = Join-Path $root 'artifacts/release'
$publish = Join-Path $release 'publish'
$target = Join-Path $env:LOCALAPPDATA 'Programs/ScreenIt'
$entry = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\{75AF53B9-2BC6-4AC3-A7D4-859884B5EAF0}_is1'
$shortcut = Join-Path ([Environment]::GetFolderPath('Programs')) 'ScreenIt.lnk'
$settings = Join-Path $env:LOCALAPPDATA 'ScreenIt/settings.json'
if ((Test-Path -LiteralPath $target) -or (Test-Path -LiteralPath $entry) -or (Test-Path -LiteralPath $shortcut)) { throw 'A pre-existing installation/shortcut is present. Refusing to alter it.' }
$settingsBefore = if(Test-Path -LiteralPath $settings) { (Get-FileHash -LiteralPath $settings).Hash } else { $null }
$checks = [Collections.Generic.List[string]]::new()
$installed = $false
try {
    $process = Start-Process -FilePath (Join-Path $release 'ScreenIt-Setup-0.1.2.exe') -ArgumentList @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART',('/LOG="'+(Join-Path $root 'artifacts/install.log')+'"')) -WindowStyle Hidden -Wait -PassThru
    if($process.ExitCode -ne 0) { throw "Install failed: $($process.ExitCode)" }
    $installed = $true
    $checks.Add('Actual per-user silent installer exits 0')
    foreach($file in Get-ChildItem -LiteralPath $publish -Recurse -File) {
        $relative = [IO.Path]::GetRelativePath($publish,$file.FullName)
        $copy = Join-Path $target $relative
        if (!(Test-Path -LiteralPath $copy) -or (Get-FileHash -LiteralPath $copy).Hash -ne (Get-FileHash -LiteralPath $file.FullName).Hash) { throw "Installed file mismatch: $relative" }
    }
    $checks.Add('Every installed application file byte-equivalent to publish output')
    $record = Get-ItemProperty -LiteralPath $entry
    if($record.DisplayVersion -ne '0.1.2' -or $record.InstallLocation.TrimEnd('\') -ne $target) { throw 'Unexpected per-user uninstall metadata.' }
    $checks.Add('HKCU uninstall entry/version/install path correct')
    $shell = New-Object -ComObject WScript.Shell
    $link = $shell.CreateShortcut($shortcut)
    if($link.TargetPath -ne (Join-Path $target 'ScreenIt.App.exe')) { throw 'Start Menu shortcut mismatch.' }
    [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($link)
    [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($shell)
    $checks.Add('Per-user Start Menu shortcut targets installed executable')
    & (Join-Path $root 'verification/Smoke.ps1') -ExecutablePath (Join-Path $target 'ScreenIt.App.exe') -ReportName installed-smoke.json
    $smoke = Get-Content -LiteralPath (Join-Path $root 'artifacts/installed-smoke.json') -Raw | ConvertFrom-Json
    if($smoke.status -ne 'PASS') { throw 'Installed application smoke failed.' }
    $checks.Add('Installed self-contained app: tray/hotkeys/capture/commit/Clear/shutdown smoke PASS')
} finally {
    # This script only uninstalls its own newly created, previously absent installation.
    $uninstaller = Join-Path $target 'unins000.exe'
    if ($installed -and (Test-Path -LiteralPath $uninstaller)) {
        $process = Start-Process -FilePath $uninstaller -ArgumentList @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART',('/LOG="'+(Join-Path $root 'artifacts/uninstall.log')+'"')) -WindowStyle Hidden -Wait -PassThru
        if($process.ExitCode -ne 0) { throw "Uninstall failed: $($process.ExitCode)" }
    }
}
for($attempt=0;$attempt -lt 50 -and (Test-Path -LiteralPath $target);$attempt++) { Start-Sleep -Milliseconds 100 }
if ((Test-Path -LiteralPath $target) -or (Test-Path -LiteralPath $entry) -or (Test-Path -LiteralPath $shortcut)) { throw 'Installed files, uninstall entry, or shortcut remain after uninstall.' }
$checks.Add('Actual uninstall removes installed files, shortcut and registry entry')
$settingsAfter = if(Test-Path -LiteralPath $settings) { (Get-FileHash -LiteralPath $settings).Hash } else { $null }
if ($settingsBefore -ne $settingsAfter) { throw 'User theme preference changed during install/uninstall.' }
$checks.Add('User theme preference preserved byte-for-byte')
$report = [ordered]@{status='PASS';count=$checks.Count;checks=$checks;installedSmokeChecks=$smoke.count}
$report | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $root 'artifacts/installer-verification.json') -Encoding utf8
$report | ConvertTo-Json -Depth 4
