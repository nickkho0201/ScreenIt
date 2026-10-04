param([string]$Dotnet = 'dotnet', [string]$Iscc = 'ISCC')
$ErrorActionPreference = 'Stop'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$release = Join-Path $root 'artifacts/release'
$publish = Join-Path $release 'publish'
# Refuse stale output instead of deleting arbitrary or previously reviewed artifacts.
if (Test-Path -LiteralPath $release) { throw 'artifacts/release already exists. Move it aside before rebuilding.' }
Push-Location $root
try {
    & $Dotnet publish src/ScreenIt.App/ScreenIt.App.csproj -p:PublishProfile=ReleaseWinX64 -p:RestoreSources=https://api.nuget.org/v3/index.json -p:IncludeSourceRevisionInInformationalVersion=false -p:DebugType=None -p:DebugSymbols=false -o $publish
    if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
    if (Get-ChildItem -LiteralPath $publish -Recurse -Filter '*.pdb') { throw 'Unexpected debug symbols in publish output.' }
    Copy-Item -LiteralPath (Join-Path $root 'LICENSE') -Destination (Join-Path $publish 'LICENSE')
    $packages = if ($env:NUGET_PACKAGES) { $env:NUGET_PACKAGES } else { Join-Path $env:USERPROFILE '.nuget/packages' }
    $notices = Join-Path $publish 'licenses'; New-Item -ItemType Directory -Path $notices | Out-Null
    Copy-Item -LiteralPath (Join-Path $packages 'microsoft.netcore.app.runtime.win-x64/10.0.12/LICENSE.TXT') -Destination (Join-Path $notices 'dotnet-runtime-LICENSE.txt')
    Copy-Item -LiteralPath (Join-Path $packages 'microsoft.netcore.app.runtime.win-x64/10.0.12/THIRD-PARTY-NOTICES.TXT') -Destination (Join-Path $notices 'dotnet-runtime-THIRD-PARTY-NOTICES.txt')
    Copy-Item -LiteralPath (Join-Path $packages 'microsoft.windowsdesktop.app.runtime.win-x64/10.0.12/LICENSE') -Destination (Join-Path $notices 'dotnet-desktop-LICENSE.txt')
    & $Iscc "/DPublishDir=$publish" "/DReleaseDir=$release" (Join-Path $PSScriptRoot 'ScreenIt.iss')
    if ($LASTEXITCODE -ne 0) { throw 'Installer build failed.' }
    $zip = Join-Path $release 'ScreenIt-0.1.0-win-x64-portable.zip'
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    [IO.Compression.ZipFile]::CreateFromDirectory($publish,$zip,[IO.Compression.CompressionLevel]::Optimal,$false)
    $names = @('ScreenIt-Setup-0.1.0.exe','ScreenIt-0.1.0-win-x64-portable.zip')
    $sums = foreach($name in $names) { '{0}  {1}' -f (Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $release $name)).Hash.ToLowerInvariant(),$name }
    [IO.File]::WriteAllText((Join-Path $release 'SHA256SUMS.txt'),($sums -join "`n")+"`n",[Text.UTF8Encoding]::new($false))
    Get-Item -LiteralPath ($names | ForEach-Object { Join-Path $release $_ }) | Select-Object Name,Length
} finally { Pop-Location }
