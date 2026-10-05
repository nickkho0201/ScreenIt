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
