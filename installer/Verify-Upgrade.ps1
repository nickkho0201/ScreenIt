param([string]$BaselineDirectory)
$ErrorActionPreference='Stop'
if(Get-Process ScreenIt.App -ErrorAction SilentlyContinue) { throw 'Exit ScreenIt before upgrade verification.' }
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$baseline=if($BaselineDirectory) { (Resolve-Path -LiteralPath $BaselineDirectory).Path } else { Join-Path $root 'artifacts/release-0.1.2-preserved' }
$release=Join-Path $root 'artifacts/release'
$target=Join-Path $env:LOCALAPPDATA 'Programs/ScreenIt'
$settings=Join-Path $env:LOCALAPPDATA 'ScreenIt/settings.json'
$entry='HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\{75AF53B9-2BC6-4AC3-A7D4-859884B5EAF0}_is1'
# This deliberately upgrades the existing, closed, byte-matching baseline in place and leaves it installed.
# It does not uninstall an existing user installation or delete any preferences.
if(!(Test-Path -LiteralPath (Join-Path $target 'unins000.exe'))) { throw 'Expected installed baseline missing.' }
foreach($name in @('ScreenIt.App.exe','ScreenIt.App.dll','ScreenIt.Core.dll')) {
    if((Get-FileHash -LiteralPath (Join-Path $target $name)).Hash -ne (Get-FileHash -LiteralPath (Join-Path $baseline "publish/$name")).Hash) { throw "Installed baseline mismatch: $name" }
}
$before=if(Test-Path -LiteralPath $settings) { (Get-FileHash -LiteralPath $settings).Hash } else { $null }
$checks=[Collections.Generic.List[string]]::new()
foreach($version in @('0.1.2','0.2.0')) {
    $folder=if($version -eq '0.1.2') { $baseline } else { $release }
    $filename="ScreenIt-Setup-$version.exe"
    $sumLine=Get-Content -LiteralPath (Join-Path $folder 'SHA256SUMS.txt') | Where-Object { $_ -match ("^[a-fA-F0-9]{64}  "+[regex]::Escape($filename)+'$') }
    if(@($sumLine).Count -ne 1 -or (Get-FileHash -LiteralPath (Join-Path $folder $filename)).Hash -ne $sumLine.Substring(0,64)) { throw 'Installer checksum mismatch.' }
    # Reapply unchanged official baseline first, establishing its original AppId registry record before upgrade.
    $process=Start-Process -FilePath (Join-Path $folder $filename) -ArgumentList @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART',('/DIR="'+$target+'"'),('/LOG="'+(Join-Path $root "artifacts/upgrade-$version.log")+'"')) -WindowStyle Hidden -Wait -PassThru
    if($process.ExitCode -ne 0) { throw "Installer $version failed: $($process.ExitCode)" }
    $record=Get-ItemProperty -LiteralPath $entry
    if($record.DisplayVersion -ne $version -or $record.InstallLocation.TrimEnd('\') -ne $target) { throw 'Upgrade changed installation identity/path.' }
    $checks.Add("Installer ${version}: same AppId/per-user directory/version")
    $after=if(Test-Path -LiteralPath $settings) { (Get-FileHash -LiteralPath $settings).Hash } else { $null }
    if($before -ne $after) { throw 'Upgrade changed user preferences.' }
    $checks.Add("Installer $version preserves preferences byte-for-byte")
}
foreach($file in Get-ChildItem -LiteralPath (Join-Path $release 'publish') -Recurse -File) {
    $relative=[IO.Path]::GetRelativePath((Join-Path $release 'publish'),$file.FullName)
    if((Get-FileHash -LiteralPath $file.FullName).Hash -ne (Get-FileHash -LiteralPath (Join-Path $target $relative)).Hash) { throw "Installed publish mismatch: $relative" }
}
$checks.Add('Upgraded files byte-equivalent to publish')
$entries=@(Get-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\*' | Where-Object DisplayName -Like 'ScreenIt*')
if($entries.Count -ne 1) { throw 'Expected exactly one ScreenIt uninstall entry.' }
$checks.Add('Exactly one uninstall entry; no parallel installation')
& (Join-Path $root 'verification/Smoke.ps1') -ExecutablePath (Join-Path $target 'ScreenIt.App.exe') -ReportName upgrade-smoke.json
if((Get-Content -LiteralPath (Join-Path $root 'artifacts/upgrade-smoke.json') -Raw | ConvertFrom-Json).status -ne 'PASS') { throw 'Upgraded application smoke failed.' }
$checks.Add('Installed 0.2.0 startup/capture/commit/Clear/hotkeys/shutdown smoke PASS')
$report=[ordered]@{status='PASS';count=$checks.Count;checks=$checks;leftInstalled=$true}
$report | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $root 'artifacts/upgrade-verification.json') -Encoding utf8
$report | ConvertTo-Json -Depth 4
