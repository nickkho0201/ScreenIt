English | [Русский](README.ru.md)

# ScreenIt

**ScreenIt 0.1.2**

The workflow below includes unreleased changes in the current source tree; release packages remain 0.1.2.

ScreenIt is a local-first Windows screenshot annotation utility designed for quickly showing AI assistants, developers, designers, and teammates exactly what you mean.

**Capture → Point → Comment → Continue → Paste**

Capture a region, window, or full monitor and annotate directly in the desktop overlay. There is no separate image editor window. Markers **A1**, **A2**, etc. connect points in the image to comments that remain real, editable text. Capture B, C, and more into the same session.

Focus your chat/composer and press **Ctrl+Alt+V once**. ScreenIt sequentially pastes each screenshot as a separate image, followed by structured comments.

## Features

- Windows 11 x64 tray utility with global shortcuts.
- Region/window/full-monitor capture, multi-monitor and mixed-DPI foundation.
- Markers with structured comments; marker move/edit/delete.
- Arrows, rectangles, and Undo/Redo for the current screenshot.
- Multi-screenshot sessions: A, B, C… Z, AA…
- Paste Session and confirmed Clear Session.
- System/Dark/Light appearance, English/Russian UI, and configurable global shortcuts (0.1.2).
- Compact, content-sized feedback above the taskbar with soft fades, including confirmation that ScreenIt is running in the background; without sound.
- Local-first: no account, cloud, backend, or telemetry.

## Installation

### Installer

1. Download **ScreenIt-Setup-0.1.2.exe** from [Releases](https://github.com/nickkho0201/ScreenIt/releases).
2. Run the installer.
3. Start ScreenIt from the Start menu.
4. ScreenIt runs in the system tray.

Installation is per-user, without mandatory administrator privileges or autostart. Uninstall removes application files but preserves your preferences.

**ScreenIt is currently unsigned. Windows SmartScreen may show a warning for early releases because they do not have a code-signing certificate.** Do not disable SmartScreen globally.

### Portable

Download **ScreenIt-0.1.2-win-x64-portable.zip** from [Releases](https://github.com/nickkho0201/ScreenIt/releases), extract it, and run **ScreenIt.App.exe**.

### Requirements

**Windows 11 x64.** Both release packages are self-contained: no installed .NET runtime is required. Windows 10 support is not claimed.

## Quick start

1. **Ctrl+Alt+S** → drag a region and release the mouse, or **Space** for the active monitor. The screenshot is immediately added to the session.
2. To annotate, hold **Ctrl** when releasing the mouse or pressing Space. Click → **A1** → type a comment → **Enter**. Add more markers, arrows, or rectangles.
3. In annotation mode, **Ctrl+Enter** commits the screenshot and closes the overlay. Repeat for B, C…
4. Focus the target composer; press **Ctrl+Alt+V** once and release the keys. Images arrive in session order, with editable comments last.
5. **Ctrl+Alt+X** → confirm Clear for a new session. Cancel, Escape, or closing confirmation preserves your session. Clear resets numbering to A/A1.

For a window screenshot, press **W** in capture selection, hover an eligible window and click. W returns to Region; Space still captures the focused monitor. Hold the configured annotation modifier at click to annotate. Window capture uses Windows.Graphics.Capture for the selected HWND, including ordinary title bar/chrome: overlapping windows and ScreenIt highlights are excluded. Unlike region/monitor capture, window pixels are acquired at click, not from the initial frozen desktop. A large or cross-monitor window is fitted into the annotation overlay for editing; its screenshot retains the full physical resolution.

Stay in the same target window during Paste Session. Focus changes or errors stop pasting; your ScreenIt session is unchanged. Already pasted items cannot be rolled back, and retry may produce duplicates.

Tray → **Settings** opens General, Hotkeys, and About. General selects **System / Dark / Light** and **English / Русский**; changes apply immediately. New installations default to System and the Windows UI language (Russian or English fallback). Existing Dark/Light preferences are preserved. System follows changes to the Windows app theme.

Hotkeys lets you record a new Capture/Paste/Clear combination using Ctrl, Alt, Shift, or Win plus a letter, digit, F1–F12, Backspace, Delete, Insert, Home, End, PageUp/PageDown, arrow key, or Space. Unavailable or duplicate combinations keep previous bindings active. **Reset defaults** restores the default set atomically. Other overlay shortcuts remain fixed. Tray → **More** contains secondary manual copy commands.

Capture also accepts **Win+Shift+S**, temporarily replacing Windows Snipping Tool while ScreenIt runs with that binding. Changing Capture to another shortcut or exiting releases interception; no Windows settings are modified. Settings → Hotkeys also selects the **annotation modifier: Ctrl / Shift / Alt**. Its physical state at release/Space decides whether annotation opens, even if it was part of the capture shortcut. The hint and an outer accent glow indicate the held modifier; overlay visuals never enter the screenshot. Windows disabled client-area animations use a static highlight.

Every other supported combination uses ordinary Windows hotkey registration, including Win+Shift+S for Paste/Clear. There is no general override of occupied system shortcuts. A rejected binding shows the exact combination and reason; the previous binding remains active and saved.

While recording a shortcut, Settings temporarily captures keyboard input, including Windows-key combinations, and suppresses that gesture until you release the keys. Escape cancels recording; switching away or closing Settings also ends it. This does not override unavailable shortcuts during normal operation.

## Shortcuts

The first three shortcuts are configurable global defaults; the others apply inside the overlay.

| Action | Shortcut |
|---|---|
| Capture | `Ctrl+Alt+S` |
| Paste Session | `Ctrl+Alt+V` |
| Clear Session | `Ctrl+Alt+X` |
| Switch Region / Window selection | `W` |
| Full focused monitor | `Space` |
| Marker | `M` |
| Arrow | `A` |
| Rectangle | `R` |
| Edit comment | `E` |
| Delete selected annotation | `Delete` |
| Undo | `Ctrl+Z` |
| Redo | `Ctrl+Y` |
| Commit screenshot | `Ctrl+Enter` |

Comment editor: **Enter** saves, **Shift+Enter** adds a newline, **Esc** cancels edit. Save/cancel the current edit before committing. Undo/Redo inside the editor applies to text. Escape cancels the current gesture/edit first; cancelling a screenshot with annotations requires confirmation.

## Privacy

ScreenIt is local-first. Screenshots/comments remain local. No ScreenIt backend, telemetry, analytics, account, automatic uploads, or background networking.

The only runtime network functionality is an explicit **Settings → About → Check for updates** action (and a separately requested installer download). It contacts GitHub Releases over HTTPS; no screenshots or comments are sent. There is no startup update check.

Paste Session explicitly transfers data to your selected application through Windows clipboard/input. Further processing depends on that receiver; ScreenIt does not control its network behavior or third-party clipboard managers.

Sessions live in RAM. Appearance, language, global shortcuts, and annotation modifier are saved locally (schema version 2): `%LOCALAPPDATA%/ScreenIt/settings.json`. Older files default the modifier to Ctrl. Paste creates temporary PNGs under `%TEMP%/ScreenIt/Clipboard-v1`. They remain after paste, Clear, and exit for asynchronous receivers; owned generations older than seven days are cleaned on a later startup.

## Updates (0.1.2)

Checks are manual: **Settings → About → Check for updates**. Only newer stable versions are offered; older versions are never proposed.

An installed copy can download the versioned installer and `SHA256SUMS.txt`, verify the exact SHA-256, and request confirmation before launching setup. ScreenIt closes only after successful installer launch; the warning explains that the RAM session will be lost. Setup remains unsigned and may trigger SmartScreen.

Portable copies open the release page instead of installing over themselves. Update downloads use a private generation directory under `%TEMP%/ScreenIt/Updates`; failed verification removes that generation. A verified download may remain after cancellation/installation. Installation retains the existing application identity and preferences.

In the current source tree, About shows real download percentage and bytes when the server provides a size; otherwise it shows downloaded bytes and an indeterminate indicator. Verification, preparation, and installer launch have separate statuses. Failures identify the stage and offer Retry. Closing Settings cancels the operation; hiding and showing it preserves progress. ScreenIt reports installer launch, then closes after confirmation; it does not report installation completion.

## Compatibility

Manually confirmed: **ChatGPT Web sequential Paste Session** and the current **Windows 11 multi-monitor, 100%/125% mixed-DPI environment**, including a negative monitor origin.

Compatibility with other applications may vary depending on how they handle Windows clipboard and synthetic paste input. This is not an exhaustive compatibility list. Windows input restrictions may prevent pasting into elevated applications.

## Known limitations

- Windows only; RAM-only sessions, no persistent history or crash recovery.
- Committed screenshots cannot be reopened for editing.
- English and Russian UI only.
- Windows-reserved shortcuts and combinations registered by another app may be unavailable, except the scoped Win+Shift+S Capture support. Physical Snipping Tool suppression requires manual acceptance on the target Windows desktop.
- Unsigned installer and executable.
- Physical 150%/200% DPI, portrait, HDR/protected content, and system transitions are not fully manually tested.
- Window selection excludes ScreenIt's process, hidden/minimized/cloaked windows, tool/menu windows, taskbar and desktop infrastructure. Protected/excluded capture, exclusive fullscreen, unusual transparent surfaces and HDR may be unavailable or render differently. Failed acquisition keeps selection available; no desktop-crop fallback is used. A brief Windows capture indicator may appear. Separate owned dialogs are separate targets; independent popup windows are not merged into the target frame. Browser/Explorer/accelerated-app compatibility requires manual acceptance on the target desktop.

## Development

Internal documentation: [agent onboarding](AGENTS.md), [runtime architecture](ARCHITECTURE.md), [development and verification](DEVELOPMENT.md), and [changelog](CHANGELOG.md).

Requirements: Windows and **.NET 10 SDK**.

```powershell
dotnet build ScreenIt.sln -c Release
.\verification\ScreenIt.Verification\bin\Release\net10.0-windows\ScreenIt.Verification.exe
.\verification\Smoke.ps1
```

For startup with saved custom shortcuts, run `Smoke.ps1 -CustomBindings -ReportName custom-hotkeys-smoke.json`. An explicit live GitHub check is available with `ScreenIt.Verification.exe --check-updates`; the default suite uses fake update providers and performs no update network requests.

Close the running ScreenIt instance before verification/smoke; its RAM session is not saved. Generated evidence stays under ignored `artifacts/`. Automated checks supplement manual acceptance.

Release tooling: **Inno Setup 7.1.0**, build-time only. See [release build instructions](installer/README.md). `spikes/` preserves research history; production does not reference spike projects. The accepted 0.1.0 capture/annotation/paste behavior is preserved in 0.1.2.

## License

[MIT License](LICENSE). Bundled .NET components retain their own licenses and third-party notices.
