param([string]$Dotnet = 'dotnet', [string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $root ('artifacts/construct-preview-' + (Get-Date -Format 'yyyyMMdd-HHmmss')) }
$output = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $output) { throw 'Preview output already exists. Choose a new directory.' }
$publish = Join-Path $output 'ScreenIt'
Push-Location $root
try {
    & $Dotnet publish src/ScreenIt.App/ScreenIt.App.csproj -p:PublishProfile=ReleaseWinX64 -p:RestoreSources=https://api.nuget.org/v3/index.json -o $publish
    if ($LASTEXITCODE -ne 0) { throw 'Preview publish failed.' }
    Copy-Item -LiteralPath (Join-Path $root 'LICENSE') -Destination $publish
    $packages = if ($env:NUGET_PACKAGES) { $env:NUGET_PACKAGES } else { Join-Path $env:USERPROFILE '.nuget/packages' }
    $notices = Join-Path $publish 'licenses'
    New-Item -ItemType Directory -Path $notices | Out-Null
    Copy-Item -LiteralPath (Join-Path $packages 'microsoft.netcore.app.runtime.win-x64/10.0.12/LICENSE.TXT') -Destination (Join-Path $notices 'dotnet-runtime-LICENSE.txt')
    Copy-Item -LiteralPath (Join-Path $packages 'microsoft.netcore.app.runtime.win-x64/10.0.12/THIRD-PARTY-NOTICES.TXT') -Destination (Join-Path $notices 'dotnet-runtime-THIRD-PARTY-NOTICES.txt')
    Copy-Item -LiteralPath (Join-Path $packages 'microsoft.windowsdesktop.app.runtime.win-x64/10.0.12/LICENSE') -Destination (Join-Path $notices 'dotnet-desktop-LICENSE.txt')
    @'
ScreenIt CONSTRUCT — local design preview based on 0.2.3

This is a portable test build, not a new release or an installer.
1. Finish/paste the RAM session in your usual ScreenIt, then exit it via the tray.
2. Run ScreenIt/ScreenIt.App.exe from this folder. Open Settings from its tray menu.
3. Test General, Hotkeys, About, capture and all annotation tools in both themes/languages.
4. Exit the preview via its tray menu; start your installed ScreenIt as usual.

No reinstallation is needed. Keep the entire ScreenIt folder together.
The existing single-instance guard is retained: only one ScreenIt can run at once.
Preferences are shared with the installed copy. Changes in Settings apply immediately.
Enabling Start with Windows explicitly registers this portable executable's path.
Sessions remain RAM-only. Done adds to the session; Paste Session uses the global hotkey.
Arrow/rectangle/pencil export colors are preserved; marker badges use the new CONSTRUCT style.
Bundled Commissioner/Unbounded fonts use their included SIL OFL licenses.
'@ | Set-Content -LiteralPath (Join-Path $output 'README.txt') -Encoding utf8
    $zip = $output + '.zip'
    if (Test-Path -LiteralPath $zip) { throw 'Preview ZIP already exists.' }
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    [IO.Compression.ZipFile]::CreateFromDirectory($output,$zip,[IO.Compression.CompressionLevel]::Optimal,$false)
    Get-FileHash -Algorithm SHA256 -LiteralPath $zip
    Get-Item -LiteralPath $zip,(Join-Path $publish 'ScreenIt.App.exe') | Select-Object FullName,Length
} finally { Pop-Location }
