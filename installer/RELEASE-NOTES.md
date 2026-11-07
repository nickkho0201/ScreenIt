## ScreenIt 0.2.3 — 2026-11-07

### Draw freely with Pencil

- Select **Pencil** on the annotation toolbar or press **P**, then draw with the left mouse button held down.
- Strokes use the existing accent color and white outline, and appear in the final screenshot without creating comment markers.
- Each stroke supports selection, Delete and Undo/Redo as one action. Escape, focus loss or switching tools cancels an unfinished stroke.
- The annotation toolbar wraps when there is not enough width for all buttons.

### Comments only where you left them

- Paste Session and Copy Session Comments now include text only for screenshots with comment markers.
- Images without comments are still pasted and keep their letters: A/B/C images can have text sections only for A/C.
- Screenshot ordering, marker numbering and the retained session are unchanged.

Download **ScreenIt-Setup-0.2.3.exe** or **ScreenIt-0.2.3-win-x64-portable.zip**. Both are self-contained for Windows 11 x64; **SHA256SUMS.txt** contains their checksums. Existing installation identity and preferences are preserved. Sessions remain RAM-only; installer/executable remain unsigned.

### Validation

Release build, full automated verification (3125 checks), and portable smoke with default/custom hotkeys passed. The portable ZIP was extracted and all 477 files matched publish hashes. The owner manually accepted Pencil and the filtered session-comment workflow in the test build. Fresh install/upgrade execution was not re-tested for this release.

## ScreenIt 0.2.2 — 2026-11-07

### Clear Session your way

- Select **Don't ask again** when confirming Clear to skip future warnings. The **Session cleared** notification remains.
- Restore the warning in **Settings → General** by turning off **Disable session clear warning**. Your choice is saved locally between launches; confirmation remains enabled by default.
- Cancelling or closing the Clear dialog preserves your session and does not save the checkbox.

### UI polish

- Compact, matching switch cards for Windows startup and the Clear warning.
- Short transitions for dropdowns, switches, checkboxes and Settings pages, respecting Windows animation preferences.
- A contrasting **#A043C9** selection outline and glow while holding the annotation modifier (Ctrl by default). Screenshot pixels are unaffected.
- Settings switches now execute their full action through UI Automation, including persistence/readback and failure rollback.

Download **ScreenIt-Setup-0.2.2.exe** or **ScreenIt-0.2.2-win-x64-portable.zip**. Both are self-contained for Windows 11 x64; **SHA256SUMS.txt** contains their checksums. Installation identity and existing preferences are preserved. Sessions remain RAM-only; installer/executable remain unsigned.

### Validation

Release build, full automated verification (3070 checks), and portable smoke with default/custom hotkeys passed. Fresh install/upgrade execution and physical reduced-motion UX were not re-tested for this release.

## ScreenIt 0.2.1 — 2026-10-16

### Start with Windows

- Optional per-user autostart, off by default, managed from **Settings → General** with a native ScreenIt switch.
- To enable or disable it, toggle **Start with Windows**; the displayed state always reflects the real Windows `Run` registration. If Windows has disabled the entry, Settings says so.
- Uninstall removes only this installation's own startup entry; a portable copy's entry is left untouched. The choice is stored only in the current user's registry, never in `settings.json`.

Download **ScreenIt-Setup-0.2.1.exe** or **ScreenIt-0.2.1-win-x64-portable.zip**. Both are self-contained for Windows 11 x64; **SHA256SUMS.txt** contains their checksums. Upgrade from 0.2.0 keeps the installation identity and preferences; autostart remains opt-in and does not change on upgrade. Sessions remain RAM-only; installer/executable remain unsigned.

## ScreenIt 0.2.0 — 2026-10-07

### Faster capture

- Release a region selection or press Space to add the screenshot immediately to your session.
- Hold Ctrl (configurable to Shift or Alt) to annotate instead. An accent glow and active hint show when annotation is selected.
- Improved shortcut recording and clear conflict messages. Capture can use Win+Shift+S, replacing Snipping Tool only while ScreenIt runs with that binding.

### Window capture

- Press W to switch between Region and Window selection. Hover highlights the target; click captures it, or hold the annotation modifier to edit.
- Captures the selected window through Windows.Graphics.Capture, excluding overlapping windows and ScreenIt highlights. Large and cross-monitor windows retain their full screenshot resolution.

### Clearer updates

- Real download percentage and downloaded/total size, with an indeterminate indicator when the total is unknown.
- Separate verification, preparation and installer-launch statuses; stage-specific errors and Retry. Installation progress is not simulated.

Download **ScreenIt-Setup-0.2.0.exe** or **ScreenIt-0.2.0-win-x64-portable.zip**. Both are self-contained for Windows 11 x64; **SHA256SUMS.txt** contains their checksums. Upgrade from 0.1.2 keeps the installation identity and preferences. Sessions remain RAM-only; installer/executable remain unsigned.

Protected content, HDR/exclusive fullscreen and unusual transparent surfaces may be unavailable or render differently. Independent popup windows are not merged into the selected window.

## ScreenIt 0.1.2 — 2026-10-05

- A short notification confirms that ScreenIt is ready to run in the background.
- Notifications use a compact, content-sized HUD at the bottom center of the relevant monitor work area, with soft fade-in/fade-out and clearer text hierarchy.
- Long messages wrap; session counts use natural English/Russian wording.
- Done in the Light annotation toolbar now uses the shared accent styling, with a readable shortcut badge.

Download **ScreenIt-Setup-0.1.2.exe** or **ScreenIt-0.1.2-win-x64-portable.zip**. Both are self-contained for Windows 11 x64; checksums are in **SHA256SUMS.txt**. Preferences are preserved when upgrading from 0.1.1. Sessions remain RAM-only.

## ScreenIt 0.1.0

First public release.

### Highlights

- Capture a region or full monitor; annotate directly on the frozen desktop overlay.
- Numbered markers with structured, editable comments; arrows and rectangles.
- Multi-screenshot sessions; paste the entire session with Ctrl+Alt+V.
- Dark and Light themes, visual feedback, and confirmed Clear Session.
- Local-first, no account, backend, network runtime functionality, or telemetry.

### Shortcuts

Ctrl+Alt+S — Capture · Ctrl+Alt+V — Paste Session · Ctrl+Alt+X — Clear Session.

Inside the overlay: M/A/R — Marker/Arrow/Rectangle; Ctrl+Z/Ctrl+Y — Undo/Redo; Ctrl+Enter — commit. In comments: Enter — save, Shift+Enter — newline, Esc — cancel edit.

### Installation

Download **ScreenIt-Setup-0.1.0.exe**. A self-contained **ScreenIt-0.1.0-win-x64-portable.zip** is also available. No installed .NET runtime required. SHA-256 checksums are in **SHA256SUMS.txt**.

### Notes

- Windows 11 x64.
- Installer/executable are unsigned and may trigger Windows SmartScreen.
- Sessions are RAM-only; committed screenshots cannot be reopened for editing.
- ChatGPT Web sequential A/B/C + comments flow is manually confirmed. Other receivers/configurations may vary.

English and Russian project documentation is available in the repository.
