# Release build

Windows, .NET 10 SDK, and Inno Setup 7.1.0 are build dependencies. ScreenIt has no third-party runtime packages. NuGet is used at build time to restore Microsoft's self-contained runtime packs.

```powershell
.\installer\Build-Release.ps1 -Iscc 'C:\path\to\Inno Setup 7\ISCC.exe'
```

Optional `-Dotnet` selects a particular SDK executable. `global.json` permits stable .NET 10 feature bands from 10.0.100 onward. The publish profile pins .NET/Windows Desktop runtime **10.0.12**.

Output under ignored `artifacts/release/`:

- `publish/`: win-x64 Release self-contained application, ScreenIt MIT license and bundled runtime notices.
- `ScreenIt-Setup-0.1.0.exe`
- `ScreenIt-0.1.0-win-x64-portable.zip`
- `SHA256SUMS.txt`

The script refuses existing output to avoid stale files. Move the previous owned `artifacts/release` directory aside before rebuilding. The procedure is reproducible; archive timestamps/compiler metadata are not promised to be byte-for-byte identical across builds.

Single-file publishing and trimming are intentionally disabled. Installer/ZIP already package the runtime files together; there is no need to add extraction behavior or change WPF/resource loading. No PDBs, tests, spikes, evidence, or source are shipped. License/notice files are the necessary non-runtime packaging exception.

The English installer targets Windows 11 x64, installs per-user under `%LOCALAPPDATA%/Programs/ScreenIt`, creates a Start Menu shortcut and uninstall entry. No desktop shortcut or autostart. Launch on the final page is optional and unchecked. Uninstall preserves `%LOCALAPPDATA%/ScreenIt` preferences and clipboard temporary generations.

Verification (close ScreenIt first):

```powershell
.\verification\Smoke.ps1 -ExecutablePath .\artifacts\release\publish\ScreenIt.App.exe -ReportName publish-smoke.json
.\installer\Verify-Installer.ps1
```

Installer verification requires a fresh target directory and no pre-existing ScreenIt installation. It performs an actual silent per-user install, compares published/installed file hashes, checks shortcut/uninstall metadata, runs the installed application's production smoke, then uninstalls and confirms settings were preserved. It does not automate a third-party receiver. Normal installer UI and SmartScreen can also be checked manually by running Setup and leaving Launch unchecked.
