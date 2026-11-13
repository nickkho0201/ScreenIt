# CONSTRUCT local design review — 2026-11-07

Branch: `codex/design-refresh`, based on `efe3043` (`main`, `v0.2.3`).
This is an Unreleased visual change and a local portable preview, not a release.
The owner's original `DESIGN.md` and `logo.png` are preserved.

## Implemented

- Ink/Paper/Signal/Acid foundation, light/dark surfaces, theme-dependent focus
  and system-color overrides for Windows High Contrast.
- Bundled static Commissioner and Unbounded with OFL notices; actual pack-resource
  URIs and Cyrillic/Latin glyph coverage are checked, including SemiBold UI text.
- Three existing Settings pages, grouped General rows, technical header divider,
  immediate preference changes and existing hotkey/update controls.
- Compact vector tool strip; responsive brand; contextual Edit/Delete;
  separate lower-left context and lower-right Done. Narrow screens wrap tools.
- Comment editor accent, themed confirmations and compact rectangular toasts.
  Selection uses a finite transition instead of decorative continuous breathing;
  toast fade honors Windows client-area animation policy.
- Vector interpretation of the supplied logo, outlined lettering, eight ICO sizes
  and optically simplified 16/20 px masters. Tray selects the system small-icon size;
  window/executable/installer resources use the same ICO.
- `Painter` export palette is explicit and independent of UI/theme: Signal/Paper
  (`#F04D32`/`#FBFEF8`) after the later owner-approved palette change (see below).
  Core/capture/input/clipboard/session/preferences/update/installer behavior is
  unchanged. Native tray menu behavior is retained, as allowed by DESIGN.md.

## Initial 2026-11-07 evidence

| Check | Result |
|---|---|
| Release solution build | PASS, 0 warnings / 0 errors |
| Full verification | PASS, 3225 checks |
| Design-only font/contrast/layout checks | PASS, 90 checks; also in full suite |
| Settings visual matrix | PASS; 508 checks, also in full suite |
| Standalone toast layout | PASS; 428 checks, also in full suite |
| Portable production smoke | PASS, 22 checks, self-contained runtime confirmed |
| Pencil raster before/after | Identical SHA-256 `b252a330e3398cea69cfc6f2226d078bf49cf6c30ace892269f024e8cc7a0853` |
| Diff whitespace review | PASS |

Full-suite environment: Windows 11 build 26200, x64, real 3440×1440 at 125%
and 2560×1440 at 100%; second monitor at positive X=3440. This run does not
establish negative-origin or physical 150%/200% acceptance.

The first portable smoke stopped at its foreground assertion. After adding
before/after HWND/PID diagnostics, the next run passed without changing runtime
code. The original cause was not established. Both reports are retained; do not
reinterpret the first failure as a PASS or a confirmed fix.

Generated local evidence (ignored by Git):

- [Initial full verification](../artifacts/construct-full-run.log)
- [Initial design verification](../artifacts/construct-design-run.log)
- [First portable smoke](../artifacts/construct-preview-smoke.json)
- [Passing portable smoke](../artifacts/construct-preview-smoke-diagnostic.json)
- [Baseline General RU/Dark](../artifacts/construct-baseline/settings-page-general-ru-dark.png)
- [New General RU/Dark](../artifacts/construct/settings-0-ru-Dark.png)
- [New General EN/Light](../artifacts/construct/settings-0-en-Light.png)
- [Annotation RU/Dark](../artifacts/construct/annotation-1366-ru-Dark.png)
- [Narrow comment editor RU/Light](../artifacts/construct/comment-640-ru-Light.png)

## Portable review

Run `artifacts/construct-preview-20261107/ScreenIt/ScreenIt.App.exe`, or extract
the sibling ZIP. Runtime 10.0.12 and font licenses are included. No installer is
executed, no installed files are replaced. First finish/paste the existing RAM
session and exit the usual copy through its tray. The preview retains the same
single-instance mutex and settings path; preference changes apply to both copies.
Enabling autostart explicitly points the Windows entry at the preview executable.
Exit the preview before returning to the installed copy.

ZIP SHA-256: `d3049664add2166ef9d7c7f1420cf674c4a5de2b7eebb4cf6d2e4c5268fdfecd`.

Owner acceptance remains: visual fit against references, physical keyboard/mouse
use of every tool and editor, real receiver paste, tray/taskbar icon appearance,
High Contrast and reduced-motion system UX, large text and physical 150%/200%.
Synthetic screenshots and automated native checks supplement these checks.
No commit, push, merge, tag, release or installation was performed.

## Capture selection refinement — 2026-11-08

Replaced the remaining single-line capture hint with `CaptureSelectionBar`, styled
like the annotation toolbar: logo/brand, vector Region/Window/monitor icons, Signal
current-mode indicator, keycaps for W/Space/configured modifier/Esc, and an Acid
held-modifier state. The compact panel is centered at the top, wraps at narrow
widths, and gives way to the annotation tools on selection completion. Context,
window-acquisition and error text use the lower-left dock. The guide is passive:
all input passes to the existing selection surface/keyboard handlers.

Current-run results: Release build PASS (0 warnings/errors); design-only PASS
298; full suite PASS 3447; new portable smoke PASS 22. The full suite includes
actual W mode changes, both monitor DPIs, capture output exclusion and the added
capture chrome state assertions. Manual visual acceptance remains with the owner.

- [Capture preview RU/Dark](../artifacts/construct/capture-region-1366-ru-Dark.png)
- [Narrow capture RU/Light](../artifacts/construct/capture-region-640-ru-Light.png)
- [Window guide, Shift held](../artifacts/construct/capture-bar-1366-ru-Dark-True-True.png)
- [Full result](../artifacts/capture-chrome-full.log)
- [Portable smoke](../artifacts/capture-chrome-smoke.json)

New portable folder: `artifacts/construct-preview-20261108-capture/ScreenIt/`;
ZIP: `artifacts/construct-preview-20261108-capture.zip` (previous build preserved).
ZIP SHA-256: `75663a7cbb28ceaf332d163130b4469db7cfd56d1038d7548a35722b992c8703`.
The same single-instance and shared-preferences instructions above apply.

## Single capture mode button — 2026-11-08

Owner requested Region/Window to be one interactive button. It now shows only the
current mode's icon/name and W keycap; click delegates to the existing coordinator
mode switch and returns focus to the selection surface. W remains unchanged.
Only this button consumes pointer input; passive hints and spacing pass through.

Current checks: Release build PASS (0 warnings/errors), design-only PASS 346
(including hit testing, click transitions and localized labels), pencil PASS 33,
portable smoke with `-CaptureModeToggle` PASS 24. The latter invokes the actual
published UI button in both directions. The previously running preview was not
terminated; smoke ran after no ScreenIt process remained. Full suite was not
repeated for this refinement; its 3447-check result above belongs to the preceding
capture-guide revision. Physical mouse/keyboard acceptance remains manual.

- [Current design checks](../artifacts/mode-toggle-design.log)
- [Current portable smoke](../artifacts/mode-toggle-smoke.json)
- [Single-button panel](../artifacts/construct/capture-bar-1366-ru-Dark-False-False.png)

New build: `artifacts/construct-preview-20261108-mode-toggle/ScreenIt/ScreenIt.App.exe`.
ZIP: `artifacts/construct-preview-20261108-mode-toggle.zip`.
SHA-256: `a92d21d15cb59abcbd076c4a98b74e400b6997adbf05509ca319255b946618ae`.
Previous builds are preserved; installed files are unchanged; preferences remain shared.

## Mode-switch affordance — 2026-11-08

Added a static circular-arrows icon at the right of the single mode button and a
localized secondary action label: Region → Select a window, Window → Select a
region. The icon/keycap positions and button width remain stable between states.
The caption tree is retained across selection refreshes; only text/state changes.
No click, W, acquisition or session behavior changed.

Release build PASS (0 warnings/errors); existing design matrix PASS 346 with
pointer hit testing, transitions, RU/EN and both themes/narrow wrapping. Rendered
Region/Window panels were visually inspected. Production smoke/full suite were
not repeated: the owner's previous preview remains running and was not stopped.
Previous results above remain evidence of their respective revisions.

Build: `artifacts/construct-preview-20261108-mode-hint/ScreenIt/ScreenIt.App.exe`.
ZIP: `artifacts/construct-preview-20261108-mode-hint.zip`.
SHA-256: `4e4c577cbf0e440059da744371b55ddefe3504e1890eac09f805fd66897cf5a5`.
[Current checks](../artifacts/mode-affordance-design.log).
[Region panel](../artifacts/construct/capture-bar-1366-ru-Dark-False-False.png).
[Window panel](../artifacts/construct/capture-bar-1366-ru-Dark-True-False.png).

## Compact mode caption — 2026-11-08

Removed the secondary action subtitle at the owner's request. The single compact
button keeps only the current mode name/icon, W keycap and circular arrows.
Build PASS (0 warnings/errors); design matrix PASS 346; rendered panel inspected.
Smoke/full suite not repeated while the owner's preview remains running.

Build: `artifacts/construct-preview-20261108-mode-clean/ScreenIt/ScreenIt.App.exe`.
ZIP: `artifacts/construct-preview-20261108-mode-clean.zip`.
SHA-256: `4a8f03b1a268275c91a85210dc40088c5d0f0d1c0e6b81a6201729e6e8689fa0`.
[Current checks](../artifacts/mode-clean-design.log).

## Marker/comment redesign — 2026-11-08

Later owner request explicitly authorizes changing the marker appearance and
editor experience. Markers now have an angular Ink face, Paper label/contrast
halo and Signal outline in both preview and exported bitmap. `Geo.Badge` physical
bounds/hit testing and all IDs/high-water/history rules are unchanged; arrows,
rectangles and pencil kept their previous export colors at that time (later
changed to Signal/Paper by explicit owner request; see below).

`CommentEditorCard` is an opaque compact 352×240 DIP chamfered card with a large
marker ID, technical divider, red-framed Unicode input and Enter/Shift+Enter/Esc
hints. `CommentEditorLink` grows to the card from the nearest marker edge; it is
UI-only, transformed through SourceScale and actual HWND scaling. Reveal/fade
transitions are finite (140/100 ms), stop on close/reopen and have a static Windows
animations-disabled path. Logical editing/focus does not wait for animation.

A short click on a saved marker opens its comment. Drag retains Move; E and
double-click remain available. Escape closes editing while retaining selection
for Delete. Empty/cancelled comments do not spend a marker number. Comment bodies
are still editable text; card/connector never enter exported glyphs.

Current-run checks:

- Release solution build PASS, zero warnings/errors.
- Marker/editor checks PASS 93 (RU/EN × Dark/Light, corners, Unicode, Enter/Escape,
  short click versus drag, rapid reopen, history, export isolation, long IDs/tiny sources).
- Design matrix PASS 346; pencil checks PASS 33; both are included in full suite.
- Final full suite PASS 3592 on the real 100%/125% dual-monitor desktop, including
  focus/capture/editor cancellation/repeated lifecycle checks and bounded resources.
- Delivered self-contained portable smoke PASS 24 with capture mode toggle.
- Diff whitespace review PASS.

[Full run](../artifacts/comment-card-full.log),
[marker checks](../artifacts/marker-editor-verification.json),
[portable smoke](../artifacts/marker-editor-smoke.json),
[Dark editor](../artifacts/construct/comment-1366-ru-Dark.png),
[Light compact editor](../artifacts/construct/comment-640-ru-Light.png).

Delivery: `artifacts/construct-preview-20261108-markers/ScreenIt/ScreenIt.App.exe`;
ZIP: `artifacts/construct-preview-20261108-markers.zip`.
SHA-256: `8f4bb6443313d39afd40bb022e0c2d176252a15a6daec31771b070805f13cc5a`.
The earlier `marker-editor` and `comments` folders are intermediate local builds;
use `markers` for the final click-to-edit behavior. Installed files were not changed.

Manual acceptance remains: subjective animation pace/appearance, physical mouse/
keyboard and real receiver paste; High Contrast/reduced motion and physical
150%/200%/large-text configurations are not claimed as verified by these runs.

## Reference-based composition / UI-only badges — 2026-11-08

Current delivery supersedes the earlier compact editor iterations. The card has
an editorial header (Unbounded ID 34 DIP, secondary SCREENIT identity), integrated
input as the dominant region, and a separated footer with individual keycaps.
Regular maximum size is 520×320 DIP; placement selects the available side around
the UI badge and adapts to constrained viewports. Compact variants retain text
entry and keyboard help. The TextBox now has its own template with PART_ContentHost,
neutral unfocused border and Signal focus, and no system blue frame/focus chrome.

Interactive badges are a separate DIP drawing/hit-testing layer: 88×46 DIP for
short IDs, Ink face, Signal contour, Paper Unbounded ID and explicit anchor point.
They remain readable on fitted sources. Pointer deltas remain raw until the final
marker anchor is clamped, allowing click/drag on a badge face outside the crop.
Painter/Core/PNG marker appearance were frozen at the start of this request.

Current verification: Release build PASS (zero warnings/errors); only the targeted
marker/editor suite ran, PASS 263. It covers existing comment semantics plus
normal/focused input templates, text selection/Undo/Redo, UI face clicks/drags at
crop edges and fitted source, and simulated 100/125/150/200% viewport placement.
No unrelated full suite or production smoke was run. Offscreen focus snapshots
exercise actual templates but do not claim physical keyboard/IME, animation pace
or real mixed-DPI/high-contrast acceptance.

PNG preservation verified by SHA-256 against the pre-task snapshot:

- Painter source: `d860b027fa451b16c7429bb3d40a7e354328367e3ba46682ed40e62456fe792d` (unchanged).
- Control PNG: `55010c224e6b8e70a16eea1fc910a635a386aead1ba96a9ee12c99bdd7dd87e7` (unchanged).

[Targeted checks](../artifacts/comment-layout-marker.log),
[export preservation](../artifacts/comment-layout-export-check.json),
[Dark focused preview](../artifacts/marker-editor/reference-ruDark-focused.png),
[Dark normal preview](../artifacts/marker-editor/reference-ruDark-normal.png),
[Light focused preview](../artifacts/marker-editor/reference-ruLight-focused.png).
Rendered RU/EN × Light/Dark previews were visually inspected.

Final portable: `artifacts/construct-preview-20261108-comment-layout/ScreenIt/ScreenIt.App.exe`.
ZIP: `artifacts/construct-preview-20261108-comment-layout.zip`.
SHA-256: `e0d38b790fa926d4db4440c184fdb81ce428aa5b9921dda2d9d211eda81f647d`.
The running previous preview was left intact; installed files/preferences were not
replaced. The shared-preferences/single-instance instructions above still apply.

## Compact badge/editor correction — 2026-11-08 (current)

This delivery supersedes the rejected 88×46/520×320 prototype above. Historical runs remain evidence for their own iterations only.

### Diagnosis and coordinate behavior

Git main/v0.2.3 used Geo.Badge: height min(source height,32) physical pixels; width min(source width,max(40,label length×10+12)). Its projected bounds followed SourceScale and the actual per-HWND transform. The rejected UI layer instead requested 88×46 DIPs and Unbounded 22 independent of that projection: at 125% approximately 110×58 physical pixels instead of the original 40×32. The card grew to 520×320 DIPs. These changes, rather than a change to stored anchor coordinates, caused the oversized composition.

The new UI starts with the original physical Geo.Badge projected via SourceScale and actual HWND DPI, limits height to 22–28 DIPs for readability and measures Commissioner Semibold 13 for variable-length IDs. Drawing and hit-testing use the same bounds; raw drag delta is transformed and only the final physical anchor is clamped. A tiny crop gets a readable face just outside the crop with a minimal anchor leader. This is UI-only: the original physical anchor, Core/model and PNG projection remain unchanged by this correction.

### Composition and interaction

Badge: Ink face, thin Signal contour, Paper ID, 3 DIP cuts; stable geometry for Normal/Hover/Selected/Dragging/Editing. Emphasis changes the contour rather than scaling the badge.

Card: maximum 380×246 DIPs, 16–18 padding, 38-high header with Unbounded 24 ID, small red indicator, vertical rule and secondary SCREENIT //; single neutral bent divider; integrated native TextBox with neutral normal/Signal focus border; compact themed scrollbar; separate wrapping keycap footer. TextBox padding is applied once (live verification exposed and fixed doubled padding). Existing native selection/caret/multiline/Undo services remain. Placement chooses available space below/right/left/above and clamps to monitor bounds; short connector uses adjacent badge/card edges.

Open uses opacity and TranslateY 4→0 over 140 ms ease-out, close 100 ms opacity, hover/selection 90/120 ms contour emphasis. All honor ClientAreaAnimation and do not delay data commits/input. No new framework/model/export format.

### Files in this correction

Runtime: src/ScreenIt.App/MarkerBadgeUi.cs, CommentEditorCard.cs, AnnotationOverlay.cs. Verification: verification/ScreenIt.Verification/MarkerEditorChecks.cs and Program.cs. Current documentation corrected in DESIGN.md, ARCHITECTURE.md, DEVELOPMENT.md, CHANGELOG.md, README.md/README.ru.md and this report. Other dirty files belong to earlier authorized redesign iterations; no unrelated runtime components were edited for this correction.

### Current evidence

- Release solution build: PASS, 0 warnings/errors; artifacts/compact-comment-build.log.
- Targeted --marker-editor-only: PASS 466; artifacts/marker-editor-verification.json. RU/EN × Dark/Light, save/newline/cancel, selection/Undo/Redo, marker numbering and bitmap independence, UI face clicks/drag at crop edges/fitted/tiny sources; simulated 100/125/150/200% geometry and variable IDs. No unrelated full suite or production smoke.
- --marker-editor-live: PASS 12; artifacts/marker-editor-live-verification.json. Production AnnotationOverlay shown by the verification process on actual 3440×1440 at 125% and 2560×1440 at 100%. Real keyboard focus, actual HWND badge/card dimensions, multiline scrolling, themed scrollbar, immediate save/reopen/cancel verified. Opaque synthetic background protects user desktop content. Owned windows closed; existing user ScreenIt process/session left intact. These are real screen captures of the actual UI, not mockups or offscreen-only renders.
- Visually inspected actual captures: [100% input](../artifacts/compact-comment/live-96.png), [125% input](../artifacts/compact-comment/live-120.png), [125% long-text scroll](../artifacts/compact-comment/live-scroll-120.png). Compared silhouette, compact scale, hierarchy, divider, padding and density against reference; the enlarged concept was adapted to the required working scale. Subjective final acceptance remains with the owner.
- Painter source SHA-256 remains d860b027fa451b16c7429bb3d40a7e354328367e3ba46682ed40e62456fe792d; control PNG remains 55010c224e6b8e70a16eea1fc910a635a386aead1ba96a9ee12c99bdd7dd87e7. Both match artifacts/comment-layout-baseline. Core has no Git diff.

### Portable and limitations

Portable path: artifacts/construct-preview-20261108-compact-comments/ScreenIt/ScreenIt.App.exe; sibling ZIP. No installer/release/push. Finish/paste your RAM session and exit the old copy via tray before starting this build: same mutex and preferences, not simultaneous isolated instances. No installed files or preferences were replaced by packaging.

Actual 150%/200%, physical IME/large-text/high-contrast/reduced-motion appearance and manual mouse behavior at every edge were not verified. Edge geometry and larger DPI were automated/simulated. Receiver/paste and unrelated application flows were deliberately not retested. Screenshots show the production overlay under a verification process; the user's running application was not restarted.

Current portable ZIP SHA-256: a4961b3b815b55ab4339d63c945b149192a8809bb19592e5982a1cec4f23fa5f (76,653,302 bytes). Required executable/runtime/license files verified after packaging.

## Floating matte comment card — 2026-11-08 (current material)

Owner explicitly requested reference-like depth/edge lighting, superseding the previous flat material direction for these components. Existing compact marker sizing/physical geometry was retained; it was not enlarged to reproduce the zoomed concept.

Runtime changes are limited to CommentEditorCard.cs, new CommentChrome.cs, CommentEditorLink (in card file), and the necessary placement/port call sites in MarkerBadgeUi.cs/AnnotationOverlay.cs. Card remains 380×246 DIP. Header ID 26, matte material using Ink/bg.surface, subtle lower Signal reflection and directional edge light; small stationary vector shadow follows the chamfered outline. No DropShadowEffect on text or blurred screenshot/glass. Integrated CommentInputFrame draws rounded upper corners and lower-right cut while preserving native content host/selection/scroll. Footer has clearer text and restrained raised keycaps. High Contrast bypasses material/shadow.

Connector uses a single warm Signal-derived tint (1.2 DIP), axial stem and 45-degree diagonal, with a small Paper/Signal port at the badge. Removed thick white outer stroke and incidental multi-segment routing. Adjacent gap 16 DIP: initial 24 DIP gap caused overlap on the 1366×768/200% long-ID simulated viewport; targeted checks caught it and it was corrected before delivery. Model, input handlers, source transforms, anchor/drag and export remain unchanged.

Current evidence: Release build PASS (0 warnings/errors), targeted marker-editor-only PASS 466, marker-editor-live PASS 12. Logs: artifacts/floating-comment-build.log, floating-comment-checks.log, floating-comment-live.log. No unrelated suite/smoke. Actual production overlay was shown on real 100% and 125% monitors by verification, then closed; user ScreenIt/session left untouched. Actual screenshots: [125% empty](../artifacts/floating-comment/live-empty-120.png), [100% text](../artifacts/floating-comment/live-96.png), [125% text](../artifacts/floating-comment/live-120.png), [125% scroll](../artifacts/floating-comment/live-scroll-120.png). Dark actual screenshots and Light offscreen template preview visually inspected.

Compared with the reference: compact dark cut silhouette and small badge retained; editorial ID dominates secondary identity; input gets its own integrated cut silhouette; shadow/edge light now separate the whole card from background, with a restrained warm reflection; connector is clean and geometrical rather than white-outlined. The reference is enlarged artwork; real card uses a two-row hint footer to preserve legibility within its compact working width. No background blur, ornamental giant badge or new functionality. Final subjective visual acceptance remains with owner.

Limits: actual 150%/200%, physical IME/high-contrast/reduced-motion appearance and every edge on physical hardware not tested; those DPI/edge placements are covered by simulated geometry. Screenshots show real production UI under verification on an opaque synthetic background, not a mockup. Existing user process was not restarted. Portable packaging does not change installation; same mutex/preferences require exiting the old copy after finishing its RAM session.

New portable: artifacts/construct-preview-20261108-floating-comments-r2/ScreenIt/ScreenIt.App.exe; sibling ZIP. No push/commit/tag/release/installer.

Final visual correction: themed scrollbar has an internal 4/8 DIP top/bottom margin to keep its thumb away from the input chamfer. Release build and live 12 checks rerun after this presentation-only correction; long-text actual screenshot re-inspected. The 466-check component run preceded this final scrollbar margin change.

Final portable r2 required files/licenses: PASS. ZIP SHA-256: FD1F1D3FA1BB0729DAA4761A0A749C8E07179730D4F501F92498A2EA1E4BAF51.

## Top Done block — 2026-11-08

Done moved to a separate top block, 12 DIP right of annotation tools, both centered as a row. Tools reserve Done width and wrap within remaining space; bottom hint can use full viewport width. Only AnnotationOverlay.LayoutToolbar runtime presentation changed. Existing action/disabled guards and Ctrl+Enter retained. Build PASS (zero warnings/errors), targeted toolbar checks PASS 32 across 640/960/1366/1920 DIP, RU/EN, Dark/Light. Production-UI offscreen render visually inspected; no desktop smoke or comment/receiver tests rerun for this layout change. Preview: artifacts/construct-preview-20261108-top-done/ScreenIt/ScreenIt.App.exe and sibling ZIP.

## Confirmation/information notices — 2026-11-08

Runtime scope: UtilityUi.cs (PromptWindow/ClearSessionDialog presentation), new NoticeChrome.cs, ToastService.cs presentation. PromptWindow has a custom caption and cut matte shell, restrained warm header tint/Signal top edge, logo/title/close, Unbounded 22 headline with vertical accent, Commissioner description, technical divider/signature, existing checkbox and angular action row. Width 520 DIP includes shadow gutter; height remains content-based. Clear checkbox insertion moved to BodyPanel without changing its persistence owner. Caption title binding is restored after translation so localized Clear title remains visible. Existing focus/foreground/placement and modal/nonmodal handlers retained. X invokes Complete(false); Cancel stays default, no autoaccept.

ToastWindow uses the same compact NoticeSurface, keeping existing native input transparency/noactivation, timers, measured wrapping and text fonts. An initial Unbounded toast heading caused a short Russian warning to expand to full width (static check caught width regression); Commissioner was retained for compact feedback readability. No session/export/model/settings schema/update behavior changed.

Release build: PASS, zero warnings/errors. --notices-only: PASS 476 (prompt cancel/default/Enter/Esc/close/explicit accept in RU/EN × Dark/Light, single-OK information, actual toast noactivation/noninteraction, and existing static toast wrapping/adaptive width/placement checks). No unrelated full suite/smoke or receiver/install tests. Actual cropped screenshots of shown production UI were visually inspected: [Dark Clear](../artifacts/notices/clear-ruDark.png), [Light Clear](../artifacts/notices/clear-ruLight.png), [Dark toast](../artifacts/notices/toast-ruDark.png). Logs artifacts/notices-build.log and notices-checks.log; report artifacts/notices-verification.json. User ScreenIt was not stopped/restarted and its session was not touched. Screenshots include the local desktop around transparent margins; no mockup.

Limits: actual shown notice screenshots are from primary 125% DPI; static toast layout covers simulated 100/125/150/200%. Physical high contrast, large text and manual mouse/keyboard UX on every monitor were not verified. Safe routes were exercised through existing WPF event/control handlers. No installer/new release was created.

Portable: artifacts/construct-preview-20261108-notices/ScreenIt/ScreenIt.App.exe and sibling ZIP. Existing preview preserved. Same mutex/preferences: finish current RAM session and exit the old copy through tray before running preview.

## Notice detail correction — 2026-11-08
Removed SCREENIT // from the confirmation divider; checkbox focus outline uses Signal Red locally in prompts, including initial Clear creation and theme refresh. Global Settings checkbox styles unchanged. Release build PASS (0 warnings/errors), diff check PASS. No suite rerun for this small presentation-only change. Preview: artifacts/construct-preview-20261108-notice-details/ScreenIt/ScreenIt.App.exe and sibling ZIP.

## Annotation palette — 2026-11-12

The owner explicitly requested replacing the blue arrow/rectangle/pencil stroke with the CONSTRUCT palette. `Painter` now uses an explicit export palette, not the theme-dependent UI accent: Signal Red `#F04D32` stroke with a Paper `#FBFEF8` halo, at the unchanged 2.5/4.5 widths. `Painter.Draw` is still the single path for live gesture/draft preview and bitmap export; geometry, marker/comment behavior, gestures, Undo/Redo/Delete and fitted-window physical-pixel semantics are untouched. No settings or controls were added.

Verification (synthetic, no desktop capture, clipboard, preferences or hotkeys): Release solution build PASS 0 warnings/errors; `--design-only` PASS 366 including 20 new palette/fitted-window checks; `--pencil-only` PASS 33; `--comments-only` PASS 11. The palette checks assert exact Signal/Paper RGB, 2.5/4.5 widths, theme independence, no legacy blue pixels, Paper halo on dark, and matching Signal core across live gesture preview, committed draft preview and exported bitmap, plus a synthetic fitted-window source. Samples: [light](../artifacts/annotation-palette/arrow-box-pencil-light.png), [dark](../artifacts/annotation-palette/arrow-box-pencil-dark.png); report `artifacts/annotation-palette-verification.json`.

Full suite/smoke and manual receiver/on-screen acceptance were not run while the owner's preview remained open. Actual physical 150%/200%, high-contrast and subjective appearance remain manual acceptance. No commit/push/tag/release/installer and no new portable package.

