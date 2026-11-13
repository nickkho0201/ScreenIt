# ScreenIt — CONSTRUCT Design System

> **Status:** Design specification / implementation brief · **Edition:** 1.0 · **Date:** 2026-11-07  
> **Product:** [ScreenIt](https://github.com/nickkho0201/ScreenIt) · **Platform:** Windows 11, WPF, .NET · **Languages:** Russian and English  
> **Audience:** AI coding agents, developers, UI/UX designers, reviewers  
> **Scope:** Full visual redesign of the **existing** application. Preserve behavior, capture pipeline, and session semantics.

---

## 0. Agent entrypoint — read this first

This document is the **design authority** for the CONSTRUCT redesign. It is **not** permission to redesign the product workflow or invent features. When this document differs from a conceptual mockup, **this document takes precedence**. When a technical detail differs from the actual repository, check the current source and preserve the current working behavior unless a change is explicitly specified here.

### Non-negotiable rules

1. **Do not change functional contracts.** A screenshot is added to the in-memory session by **Done / Ctrl+Enter**; it is **not** directly copied to the clipboard by the annotation overlay. The session is pasted into a target app via the existing global Paste Session action.
2. **Do not invent UI controls for unavailable features.** In particular: no annotation color picker, thickness slider, per-object style inspector, Export/Save As, cloud/AI integration, session gallery, or direct Copy button in the annotation overlay. Such ideas belong to a future backlog, not the current redesign.
3. **Keep current tools:** Marker (`M`), Pencil (`P`), Arrow (`A`), Rectangle (`R`), contextual Edit (`E`), Delete (`Delete`), Undo (`Ctrl+Z`), Redo (`Ctrl+Y`), Done (`Ctrl+Enter`). Pencil is **already present** in the reviewed source; it is not hypothetical.
4. **Overlay remains overlay.** No Photoshop-like window, dedicated annotation editor window, full-width opaque top navigation, or permanent full-width bottom footer.
5. **All overlay chrome must be excluded from the exported screenshot.** Capture from original frozen/target image; render only intended annotation glyphs. Preserve physical-pixel coordinates, monitor selection, scaling, window capture and selection logic.
6. **RU and EN are first-class.** Both typefaces must render Cyrillic without fallback. Text, shortcuts, focus, tooltip, automation names and error strings must work in both languages.
7. **Do not fabricate settings.** The current Settings sections are **General, Hotkeys, About**. The controls are wired to real preferences/update flows and **apply immediately**. Do not add a Save/Cancel footer, deferred-settings behavior, or extra pages merely because they appeared in an early concept.
8. **Motion is optional presentation, not a precondition for interaction.** All operations must remain correct with Windows client-area animations disabled and under reduced-motion/accessibility constraints.
9. **No new persistence, telemetry, backend, accounts, or network side effects** as part of a UI redesign.
10. **Prioritize visual regression safety:** changes must not break the screenshot selection surface, editor focus, hotkey recording, drag gestures, hit-testing, accessibility, or session/paste behavior.

### Keywords

- **MUST**: required for acceptance.
- **SHOULD**: strongly preferred, unless a documented technical constraint prevents it.
- **MAY**: optional enhancement only if explicitly approved.
- **DESIGN PROPOSAL**: visual measurement or interaction treatment proposed here; not a claim about existing code.

### Visual north star

**CONSTRUCT = restrained technical constructivism + editorial hierarchy + highly responsive utility.**

Not a car dashboard, sci-fi HUD, gaming launcher, brutalist parody, glossy SaaS template, or giant poster embedded in the UI. The result must be **unusual but calm**: disciplined modular composition, a recognisable signal red, occasional acid accents, meticulous typography, crisp technical separators, and very little ornamental motion.

---

## 1. Product reality and design boundaries

### 1.1 Existing product model (verified against source)

ScreenIt is a local Windows utility operated primarily through the tray and global shortcuts. The user selects a region, window, or full monitor, optionally annotates it in-place, then adds a committed screenshot to an in-memory session. Subsequent captures form A, B, C…; the user pastes the session into a focused receiver with a global shortcut, as separate images followed by editable comments.

The annotation model supports:

| Capability | Current product | Redesign rule |
|---|---|---|
| Region / window / current monitor capture | Yes | Preserve capture mechanism and mode hints. |
| Modifier to enter annotation mode | Ctrl/Shift/Alt (configurable) | Preserve physical modifier semantics on selection completion. |
| Marker with independent text comment | Yes | Preserve `A1`, `A2`, label associations and editor rules. |
| Arrow | Yes | Preserve. |
| Rectangle | Yes | Preserve. |
| Pencil stroke | Yes | Preserve; design the toolbar icon and active state. |
| Edit/delete selected annotation | Yes | Show contextually; do not lose the commands. |
| Undo/redo for the current draft | Yes | Preserve. |
| Done → commit to session | Yes | Primary action; `Ctrl+Enter`. |
| Session paste | Yes, global shortcut | Do **not** label Done as Copy or Paste. |
| Manual Copy Session Images/Comments | Yes, **tray / More** | Do **not** move into annotation overlay without separate UX approval. |
| Light/dark/system theme; RU/EN | Yes | Retheme, preserve immediate application and persistence. |
| Configurable global hotkeys | Yes | Preserve recording/conflict/rollback semantics. |
| Optional Start with Windows | Yes | Reflect true native state, including unavailable states. |
| Update controls in About | Yes | Retheme existing state/progress/error UI. |

### 1.2 The proposed reference UI is **not** a feature list

Some earlier design renders contained controls that are not in the real application (e.g., Save, Export, Copy, a styling properties pane, blur, circles, text labels, AI integrations, multiple new Settings sections). **Do not copy those controls into implementation.** Borrow **only** the visual language: layout discipline, compact dark toolbars, signal highlights, typographic hierarchy, technical divider and light-status rail.

### 1.3 Explicitly deferred

- Overlap-free annotation marker research, external reference crops, dual-image delivery, transparent marker map.
- User-selectable annotation stroke colors, pen widths, fill effects.
- Direct file export / save-as inside the overlay, cloud upload, AI integrations.
- Session thumbnails/shelf, annotation property inspector, unlimited new tool types.
- New Settings navigation pages unless real features exist and are separately approved.

A future feature may reuse these design components. The redesign MUST NOT create nonfunctional buttons as placeholders.

---

## 2. Brand foundation

### 2.1 Four immutable foundation colors

| Token | Name | Hex | Role |
|---|---|---|---|
| `brand.ink` | Ink | `#1A1A1A` | Primary dark, text, geometric stroke, dark surfaces. |
| `brand.paper` | Paper | `#FBFEF8` | Primary light, light-on-dark text, near-white paper. |
| `brand.signal` | Signal Red | `#F04D32` | Main brand accent, active/primary action, logo background. |
| `brand.acid` | Acid | `#C5D947` | Secondary accent, selective focus/status/interaction highlight. |

**Do not alter these four values in the brand palette.** Theme-specific hover/pressed shades and neutral background surfaces may be derived, but must use separate named tokens. There is no fifth competing brand hue.

### 2.2 Color usage hierarchy

- **Ink/Paper: approximately 80–90% of ordinary interface area.** Backgrounds, text, content structure, negative space.
- **Signal Red: 7–15% of interface visual emphasis.** Primary action, active tool, selected navigation, compact logo / anchor.
- **Acid: 1–4% of emphasis.** Context status signal, selected focus indicator, tiny technical accent, occasional secondary chip. Do not flood large surfaces with Acid.
- Percentages are **visual guidance**, not measured quotas; readability and hierarchy win over strict ratios.

**Meaning should not depend on color alone.** Selected state combines fill or marker with icon/text, keyboard focus has an independent outline, errors are identifiable via copy and icon. Acid MUST NOT be used as body text on Paper; Paper text MUST NOT be used on Acid.

### 2.3 Contrast and text pairing

Calculated WCAG contrast ratios for the *fixed* foundation colors:

| Combination | Contrast | Guidance |
|---|---:|---|
| Ink on Paper | ~17:1 | Main UI, excellent. |
| Paper on Ink | ~17:1 | Main dark UI, excellent. |
| **Ink on Signal** | **4.82:1** | **Primary button text: preferred.** |
| Paper on Signal | 3.55:1 | Not for ordinary-size labels. Restrict to large/bold display only if criteria met. |
| **Ink on Acid** | **11.10:1** | **Preferred for Acid chips / bright states.** |
| Paper on Acid | 1.54:1 | Prohibited for UI labels. |

**Important:** dark labels (`#1A1A1A`) on branded red (`#F04D32`) are the accessible default. Earlier mockups sometimes used white text on red; those are *visual references only*, not the source of truth for UI text color. White `A1` glyphs on small exported annotations are a separate raster legibility problem and require a distinct contrast-aware painter treatment.

### 2.4 Semantic theme tokens (proposed defaults)

Design constants are mapped by **role**, not by copying hex codes across controls. The four brand values above remain fixed; the extra neutral tones make both themes usable. These are Ember/CONSTRUCT defaults; Classic uses the separate 0.2.3 blue palette and system type described in §10.3.

| Semantic token | Light theme | Dark theme | Purpose |
|---|---|---|---|
| `bg.canvas` | `#FBFEF8` | `#1A1A1A` | Main Settings canvas. |
| `bg.sidebar` | `#F1F2EC` | `#171717` | Navigation rail. |
| `bg.surface` | `#F1F2EC` | `#232323` | Grouped row backgrounds. |
| `bg.elevated` | `#FFFFFF` | `#292929` | Popover, floating panel, dialog. |
| `bg.raisedHover` | `#E7EAE3` | `#303030` | Quiet hover state. |
| `text.primary` | `#1A1A1A` | `#FBFEF8` | Main text. |
| `text.secondary` | `#4F5751` | `#B6B6B0` | Descriptions. |
| `text.quiet` | `#616861` | `#999C97` | Secondary metadata; verify contrast in context. |
| `stroke.default` | `#C9CDC4` | `#454545` | Contained sections/dividers. |
| `stroke.strong` | `#1A1A1A` | `#9A9D95` | Key frames and active outlines. |
| `accent.primary` | `#F04D32` | `#F04D32` | Branded action & selected tool. |
| `accent.primaryHover` | `#FF654D` | `#FF654D` | Hover surface. |
| `accent.primaryPressed` | `#E84E34` | `#E84E34` | Pressed surface; use Ink text. |
| `accent.secondary` | `#C5D947` | `#C5D947` | Acid accent. |
| `accent.secondaryHover` | `#D4E75B` | `#D4E75B` | Hover surface for Acid. |
| `accent.secondaryPressed` | `#AFC433` | `#AFC433` | Pressed Accent surface. |
| `focus.outer` | **Ink `#1A1A1A`** | **Acid `#C5D947`** | Keyboard focus ring. |
| `focus.inner` | **Acid `#C5D947`** | `#1A1A1A` | Optional 2-tone ring on busy backgrounds. |
| `overlay.panel` | `#FBFEF8` (opaque or ≥0.94) | `#1A1A1A` (opaque or ≥0.94) | Capture/annotation chrome; prioritize readability. |

Avoid resolving semantic colors inside `Painter` with the general `UI Accent` token; exported annotation strokes need their own explicitly specified colors and outline logic. The current export palette is Signal `#F04D32` with a Paper `#FBFEF8` halo at the existing 2.5/4.5 stroke widths.

### 2.5 Required color guardrails

- Focus on **light** background: Acid alone is too low-contrast against Paper (~1.54:1). Use a **2 px Ink outer ring** with Acid inner accent *or* Ink focus outline; never depend on a lone Acid border to indicate keyboard focus.
- Focus on **dark** background: 2 px Acid outline is highly visible and may be paired with Ink gap.
- Avoid glowing gradients, fuzzy drop-shadows, translucency over text, noisy textures, and arbitrary decorative colored triangles.
- Limit shadows to elevated elements, subtle and stationary; strong geometry comes from borders, not blur.
- Distinguish Signal from error: destructive actions require explicit icon/copy and confirmation semantics, even if a related warm hue is used.
- All text contrast MUST be checked in both themes, including disabled and busy states. For ordinary text target **≥4.5:1**, for large text **≥3:1**, for critical non-text boundaries/state indicators **≥3:1** where applicable.

---

## 3. Typography — Unbounded + Commissioner

### 3.1 Families and open-source packaging

- **Unbounded** — display, brand headlines, editorial identifiers. Supports Cyrillic, Cyrillic Extended, Latin; OFL-family license.
- **Commissioner** — UI, body text, buttons, inputs, tooltips, captions. Supports Cyrillic and Latin; OFL-family license. Use a neutral/straight style for application controls rather than decorative variable-axis effects.
- These are the **only two required font families**. Do not silently add Inter, Montserrat, Manrope, or a third mono font because a mockup looks technical.
- Obtain files and license terms from their official upstreams; retain `OFL.txt`/copyright notices in the repo and distribution. Check embedded family names and weight behavior with the exact bundled files.
- For predictable WPF rendering, prefer tested static TTF instances (if available) and explicit weights. Do not assume that every WPF runtime handles all variable-font axes equivalently.

Official references:

- [Unbounded — Google Fonts metadata](https://github.com/google/fonts/blob/main/ofl/unbounded/METADATA.pb)
- [Commissioner — source repository](https://github.com/kosbarts/Commissioner)
- [WPF font packaging — Microsoft](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/packaging-fonts-with-applications)

For WPF, declare font files as `Resource`, **not** `EmbeddedResource`; resolve them using WPF pack-resource font-family syntax. Keep fonts self-contained; **never require users to install fonts system-wide**.

### 3.2 Typography tokens (DIP; starting specification)

| Token | Family | Weight | Size / line height | Use |
|---|---|---:|---|---|
| `type.brand.wordmark` | Custom `S_IT` vector, not live type | — | scalable | Brand mark; see §4. |
| `type.display.l` | Unbounded | 700 | 26 / 34 | Settings page title; width permitting. |
| `type.display.m` | Unbounded | 600–700 | 19 / 27 | Hero / prominent section title. |
| `type.editorial.label` | Unbounded | 500–600 | 10–11 / 15 | `01 / CAPTURE`, product sublabels; sparse. |
| `type.heading.l` | Commissioner | 650–700 | 18 / 25 | Dialog and section title. |
| `type.heading.m` | Commissioner | 600 | 15–16 / 22 | Row title, named tool. |
| `type.body` | Commissioner | 400 | 14 / 20 | Settings copy / help / comments. |
| `type.body.compact` | Commissioner | 400–500 | 13 / 18 | Toolbar and compact UI. |
| `type.button` | Commissioner | 600 | 13 / 18 | Buttons. |
| `type.meta` | Commissioner | 500–600 | 11–12 / 16 | State, keyboard hints, short captions. |
| `type.kbd` | Commissioner | 600 | 10–11 / 14 | `Ctrl+Enter`, `M`, `P`, `A`, `R`. |

These are target values, not a reason to clip Russian. Minimum readable text in essential UI SHOULD be **12 DIP**; 10–11 is reserved for short nonessential labels. Prefer two-line wrapping over truncating an instruction or error.

### 3.3 Typesetting rules

- **Unbounded is scarce.** Use on at most one main headline per view, a few editorial indices, and branding. Do not set every Settings control or overlay action in it.
- Prefer mixed-case **Commissioner** for Russian UI (`Общие`, `Горячие клавиши`, `Готово`). Uppercase is reserved for small editorial tags (`CAPTURE / 01`) and product marketing.
- Limit letter spacing: typography must remain natural in Cyrillic. Display may use slight negative spacing if optically checked; metadata may use +0.02–0.07 em only when there is room.
- Avoid hard-coded fixed widths for translated labels. Both languages must fit and wrap gracefully.
- Tabular digits may be used for counters and progress if supported by the bundled face; ensure `A1`, `A10`, `AA1` remain legible.
- Define line height and baseline consistently in WPF. Prefer text rendering and layout tests at 100%, 125%, 150%, 200% scaling.

### 3.4 Required font regression strings

Test in the real application, not only mockups:

```text
ЗАХВАТ ЭКРАНА / SCREEN CAPTURE
Настройки приложения · Горячие клавиши · Аннотации
ЁЖ ЙЦУКЕН ФЫВАПРОЛД 0123456789
Маркер A1 · Карандаш · Стрелка · Рамка
Shift+Enter — новая строка · Ctrl+Enter — Готово
Capture / Paste Session / Clear Session
```

Any tofu glyph, unexpected Cyrillic fallback, clipped descendant, or unreadable small label is a release blocker.

---

## 4. Logo and icon system

### 4.1 Approved direction (concept level)

- A **Signal Red square background** (`#F04D32`).
- Four heavy **Ink L-corners** suggesting a capture frame.
- Four short, fine **Ink alignment ticks** on the upper/lower/left/right central axes, turning the frame into a precise target system.
- A centered **Ink rectangular badge** with **Paper `S_IT`** set as custom, optically balanced lettering; underscore is part of the mark.
- All geometry is **orthogonal**, measured on a rigid grid. Zero gradients, zero bevels, zero noisy texture, zero unnecessary rounding.
- This is the agreed *direction*, **not a promise that a previously generated raster is a vector-perfect production master**.

### 4.2 Construction guidance

- Master canvas: **256 × 256 design units**, transparent asset variants as needed; optical safe area around the symbol.
- Base module: **8 design units**. Prefer increments of 4 or 8; inspect optical balance rather than trusting math blindly.
- Corner thickness and central badge size must be consistent across master variants. Center badge edges may deliberately overlap imaginary construction axes; preserve apparent symmetry unless a purposeful, documented deviation improves legibility.
- Alignment ticks are a *secondary* detail, shorter and thinner than the heavy L-brackets. They must not read as four extra buttons.
- `S_IT` must be vectorized/outlined for final icon assets. Do not rely on installed fonts at runtime to render the icon.
- No Acid inside the primary icon; Acid lives elsewhere in the UI / broader graphics system.

### 4.3 Responsive logo family

| Size/use | Treatment |
|---|---|
| 256–128 px, README/installer/hero | Full mark with crosshair ticks and custom `S_IT`. |
| 64–48 px, app shortcuts | Full mark, optically adjusted stroke and lettering. |
| 32–24 px, taskbar | Simplify delicate ticks / increase underscore and letter robustness if necessary. |
| 16 px, tray | **Micro variant is permitted/expected:** remove secondary ticks if they alias, simplify lettering if required, preserve red/ink silhouette. |
| Monochrome | Black-only and Paper-only versions; form must remain identifiable without red. |

**Do not downscale a 1024 px raster to 16 px and call that a solution.** Generate device-size raster masters from a vector source, inspect on light/dark Windows taskbars and at common DPI levels. Maintain icon `.ico` multi-size asset pipeline. No need to ship font files as part of the icon export.

### 4.4 Wordmark

Main product name is **ScreenIt** (brand spelling). The standalone symbol spells **`S_IT`**. The full wordmark can use a manually adjusted Unbounded-based construction, but should not compete with the small icon inside compact overlay chrome. A text brand/title may be shortened or hidden in compact UI; the action labels take priority.

---

## 5. Geometry, grid and density

### 5.1 Layout system

- Base spacing unit: **4 DIP**; primary spacing rhythm **8 DIP**.
- `space.1=4`, `space.2=8`, `space.3=12`, `space.4=16`, `space.5=20`, `space.6=24`, `space.8=32`, `space.10=40` DIP.
- Standard controls are designed around **32–40 DIP** height; key touch/mouse targets SHOULD be **≥32×32 DIP**, preferably 36×36 where space permits.
- Icon canvas: **20×20 or 24×24 DIP**, stroke ~1.75–2 DIP; match visual weight, not merely numeric stroke width.
- `radius.none=0`, `radius.xs=2`, `radius.sm=4`, `radius.md=6`, `radius.lg=10`. Strict squared edges for editorial blocks, slight rounding only for WPF comfort and hit target perception. **No pill-shaped primary panels.**
- Standard border 1 DIP; prominent separators and active frames 2 DIP if required. Avoid double-border clutter.
- App backgrounds are not elaborate gradients. A crisp change of surface value is preferable to 20 translucent card layers.

### 5.2 Technical divider motif

The distinctive thin line with a single 45° **step** from the approved Settings mockup is called **`TechnicalDivider`**.

- A light 1 DIP line travels horizontally, changes height via one small 45° segment, and continues.
- Construct on the grid with fixed bend dimensions (e.g. 12–16 DIP horizontal/vertical displacement depending on available area); use pixel snapping.
- SHOULD appear **once per large screen region** (e.g. page header, bottom action dock edge). Never draw it under every row.
- Never animate the entire line as a scan; it is a structural motif, not a progress indicator.
- Three diagonal hash marks from the mockup are an **optional static brand ornament**, *not* a progress component. They MAY appear once per large Settings page if space permits; otherwise omit.

### 5.3 Density and negative space

The design must remain visibly a small, fast Windows utility. Use generous structure in **Settings**, conservative floating chrome in **Capture/Annotation**. Negative space should frame useful controls, not create a giant blank billboard.

### 5.4 Responsive rules

- The overlay must work across multiple monitors, non-primary monitor offsets, negative desktop coordinates, and mixed DPI scaling.
- Define positions in WPF DIPs **only for chrome**. Do not modify source-image physical-pixel math, selection crop calculations, or annotation coordinates for the sake of design.
- Toolbar and editor must clamp to monitor bounds, avoid taskbar/system edges where practical, and not cover the pointer's active target when an alternative placement exists.
- Test 1366×768 at 100%; 1920×1080 at 125%; 2560×1440 at 150%; plus a mixed-DPI dual-monitor case. Also test narrow windows and large Windows text scale.
- When width is constrained, **collapse title/brand chrome before hiding tools or hotkey meaning**. Icon-only mode with tooltips is acceptable at very small width; keyboard interactions must stay unchanged.

---

## 6. Core interactive components

### 6.1 Action button variants

| Variant | Rest | Hover | Pressed | Focus | Usage |
|---|---|---|---|---|---|
| Primary | Signal background, Ink text | `primaryHover` fill, no jump | `primaryPressed` fill, subtle inset | Outer theme focus ring | Done, destructive confirmation only when explicitly appropriate. |
| Secondary | Elevated/surface, primary text, thin stroke | `raisedHover` + stronger stroke | Slight darker surface | Outer theme focus ring | Cancel, Reset, check updates. |
| Quiet | Transparent, text/icon | Low-contrast surface tint | Denser tint | Outer theme focus ring | Toolbar utility actions. |
| Selected tool | Signal fill, Ink label/icon, selected indicator | Stable selected treatment | Feedback without dropping selection | Focus ring distinct from selection | Marker/Pencil/Arrow/Rectangle active tool. |
| Danger | Explicit warning/error copy and icon | Controlled contrast | Controlled feedback | Focus ring | Clear session / discard confirmation only. |

No button should shift the surrounding layout on hover. No bouncing, scale-to-1.1, or shadow glow. Pressed feedback should be immediate and subtle (e.g. 0–1 DIP inset *inside its own bounds*), with no effect on hit testing.

### 6.2 Keyboard hint badge (`Keycap`)

- Small squared rectangle (2 DIP radius max), 1 DIP border and tabular centered text.
- Display `M`, `P`, `A`, `R` on tools and `Ctrl+Enter` on Done; where available, tooltips expose full action labels.
- Keycaps are **not buttons** on their own.
- Long localized shortcuts may need 2-piece layout or tooltip, rather than truncating the gesture.

### 6.3 Switch / ComboBox / input

- Switch: stable width approximately **44–48 DIP**, height **24–28 DIP**, integrated with full row label and hit area. Selected uses Signal but is additionally indicated by thumb position. Focus is visually distinct, not an animated glow.
- ComboBox: 36–40 DIP, restrained frame, no neon arrow. Dropdown shadow minimal, menu rows ≥32 DIP, keyboard navigation preserved.
- TextBox: dark/light elevated surface, 1 DIP border, readable 14 DIP Commissioner, strong caret, visible focus border; no unnecessary placeholder hiding important instructions.
- Validation error: shown near related control with copy that tells user what to do. Never replace previous working hotkeys/settings on invalid input.

### 6.4 Tool icons

Use a coherent icon grid and consistent stroke endings. Recommended directional primitives:

- Marker: precise indexed marker / point, *not* a geographical map pin.
- Pencil: recognizable pencil/continuous stroke.
- Arrow: clean diagonal arrow; reflect actual annotation output, not a generic share icon.
- Rectangle: square outline.
- Undo/Redo: minimal angular or smooth arrows, consistent stroke weight.
- Edit/Delete: familiar, concise glyphs, only appear when context permits.
- Done: checkmark paired with label and `Ctrl+Enter` badge.
- Close/cancel: X; distinguish cancel screenshot from Delete annotation.

Use monochrome ink/paper icons by default; Signal indicates selected/primary, Acid is a sparse status/focus accent. Avoid an icon pack look mismatched with editorial geometry. Create vector path assets for stable WPF rendering and no external icon-font dependency.

### 6.5 Dialogs, toast, progress and badges

- Dialog: compact editorial header, straightforward explanatory copy, clear keyboard focus. Center over owner where appropriate; do not show red danger wallpaper.
- Clear-session dialog retains its real confirmation and **“Do not ask again”** semantics and rollback behavior on cancellation.
- Toast: small, non-interactive, no sound, near the taskbar, content-sized. Use a tiny Signal or Acid status accent and a short title/detail; no huge floating card. Preserve non-focus-stealing behavior.
- Update progress: existing determinate/indeterminate model, style to tokens; avoid fake progress animation after completion.
- Errors: persistent enough to read, no flash animation; include readable wording in RU/EN.

---

## 7. Screen-by-screen specification

## 7A. Capture selection overlay

### Purpose

The user selects a region, window (`W`) or full current monitor (`Space`). The capture overlay is ephemeral and must not feel like a new application window.

### Layout

- Region/window uses one mode-toggle button showing the current mode name/icon and W keycap; click and W switch the same mode (owner-requested refinement, 2026-11-08). Other capture hints remain passive.
- Frozen desktop image and existing dimmed selection surface remain the visual priority.
- A **compact top hint** (or equivalent restrained technical status) explains the current capture mode and current annotation modifier. Avoid a permanent full-width opaque top bar at this stage.
- Visual selection rectangle uses precise 1–2 DIP border and corner hints. Optional modifier-held accent ring/glow must remain subtle, never baked into the source screenshot.
- On the overlay, bottom-left context line MAY show a single concise instruction for region/window/Space; it is not required to duplicate all hotkeys already visible above.
- Do not show annotation tool strip until annotation mode is actually entered. Ordinary capture may commit immediately, as it does now.

### Capture states and sample copy

| State | RU instruction | EN instruction |
|---|---|---|
| Region | `Выделите область` | `Drag to select a region` |
| Window | `Выберите окно` | `Select a window` |
| Annotation modifier held | `Отпустите с Ctrl, чтобы разметить` (modifier dynamic) | `Release with Ctrl to annotate` (modifier dynamic) |
| Full-monitor availability | `Space — весь монитор` | `Space — full monitor` |
| Window acquisition busy | `Снимок окна…` | `Capturing window…` |
| Suspended topology | Use existing actionable diagnostic; do not auto-resume | Same behavior in EN |

Do not hardcode Ctrl if the configured annotation modifier is Shift or Alt.

## 7B. Annotation overlay — top toolbar

### Principle

**The top controls tools. The bottom explains the situation and completes the capture.** Do not create a separate editor window or expose an entire top application navigation bar.

### Toolbar content and sequence

```text
[ S_IT (optional small) | ScreenIt (hide on narrow) ]
[ Marker M ] [ Pencil P ] [ Arrow A ] [ Rectangle R ]
[ Edit E* ] [ Delete Del* ] | [ Undo ] [ Redo ] | [ Cancel × ]

* Contextual controls, visible/enabled only when relevant.
```

`Done / Ctrl+Enter` is **below**, in the compact right-side action dock. If relocating it risks a functional regression in an intermediate phase, keep the existing button accessible until the new one is verified; never have two conflicting Done actions in the final UI.

**Design proposal measurements:** top 12–16 DIP inset; toolbar height 44–52 DIP; visual groups separated by 1 DIP strokes or 8–12 DIP gaps; near-opaque surface; 6–10 DIP corner treatment at most. Position robustly on each active monitor, with no false assumption of a single primary monitor.

The whole toolbar must remain accessible via keyboard and must not trap focus away from the screenshot surface or comment editor.

### Active/disabled/interaction

- Selected tool: stable Signal background, Ink text/icon, and clear selected indicator. Do not color the other three tool labels red.
- Hover: subtle surface change, 80–100 ms (if animations allowed), with full name + shortcut tooltip.
- Undo/Redo disabled: visibly muted, no false interactive hover.
- X/Esc: **cancel current operation according to existing hierarchy**, potentially confirm discard; not always immediate total exit.
- Do not include per-tool color/thickness popovers in the current redesign.

## 7C. Annotation overlay — bottom context rail / action dock

### Concept

The approved visual reference showed a full-width industrial footer. **Keep the editorial/technical style, but remove the permanent opaque footer.** Use a **split bottom dock**:

- **Bottom-left:** a short contextual status (`ContextHint`) inside its own modest container; max two lines if absolutely needed.
- **Bottom-right:** **one** primary action `Готово / Done` with `Ctrl+Enter` keycap.
- **Center:** transparent; the content must remain visible.
- Optionally connect zones with a **thin TechnicalDivider line**, not a 60+ DIP black block covering the entire screenshot.

**Design proposal**: dock height **44–52 DIP** in compact mode; 12–16 DIP bottom/side inset; left hint constrained to approx. 35–55% of available width; right action button ≥36 DIP high. If these cannot fit, text truncation rules must retain important error/help semantics; use a second line or stacked compact layout without covering the annotation target.

### Content policy

The bottom rail MUST NOT repeat `M / P / A / R` shortcut legends already present in the top toolbar. It should answer: **“What can I do right now?”**

Priority order:

1. **Blocking state / action required** (e.g. save or cancel comment before Done).
2. **Current interaction instruction** (e.g. click to place marker, drag to draw arrow).
3. **Short confirmation** of a completed interaction (temporarily replaces generic hint, then returns).
4. **Optional metadata** such as current screenshot letter only if accurate and useful. Do not display fake zoom, pointer coordinates, session count, progress, or timestamps just to feel technical.

### Context matrix (copy proposals; actual localization must be reviewed)

| Runtime state | RU main hint | EN main hint | Secondary hint, when valuable |
|---|---|---|---|
| Marker ready | `Укажите точку для комментария` | `Click a point to add a comment` | None. |
| Editing marker comment | `Введите комментарий` | `Type a comment` | `Enter — сохранить · Shift+Enter — новая строка · Esc — отмена` |
| Arrow ready | `Проведите стрелку к объекту` | `Drag to draw an arrow` | None. |
| Rectangle ready | `Выделите участок рамкой` | `Drag to draw a rectangle` | None. |
| Pencil ready | `Рисуйте поверх снимка` | `Draw directly over the screenshot` | None. |
| Selected marker | `Метка выбрана — можно изменить комментарий` | `Marker selected — comment can be edited` | `E — изменить · Delete — удалить` (only when selected) |
| Selected other object | `Аннотация выбрана` | `Annotation selected` | `Delete — удалить` |
| Undo applied | `Последнее действие отменено` | `Last action undone` | Brief confirmation only. |
| Redo applied | `Действие восстановлено` | `Action restored` | Brief confirmation only. |
| Editing blocks Done | `Сохраните или отмените комментарий` | `Save or cancel the comment first` | Maintain editor focus. |
| Commit ready | `Готово — добавьте снимок в сессию` | `Ready — add screenshot to session` | `Ctrl+Enter` shown on button; don't duplicate. |

Do not show an invented `Ready` mode or state-transition animation unrelated to actual `AnnotationOverlay`/`Coordinator` state. Use real events, no timers that affect logic; temporary message lifetimes follow existing UI feedback conventions.

## 7D. Marker comment editor

**Latest owner direction, 2026-11-08:** redesign composition toward the supplied reference: larger UI-only badge, editorial ID header, integrated custom input, separate keycap footer, responsive card up to 520×320 DIP. Freeze the current PNG marker appearance; UI geometry/hit testing is separate from Painter/Core. This supersedes the earlier compact 352×240 proposal below without changing comment/session semantics.


**Owner-requested refinement, 2026-11-08:** replace the original editor/badge visuals with a compact chamfered card and angular Ink/Paper/Signal badge, linked by an animated UI-only connector. Short click on an existing marker opens editing; drag still moves it and Escape retains selection for Delete. Marker export appearance is explicitly authorized by this later request; physical geometry, numbering, text semantics and other exported strokes remain unchanged. The card is approximately 352×240 DIP and uses finite reveal/fade transitions with immediate input state and reduced-motion fallback.


- Current marker comment remains editable plain text **separate from the image**. The exported image contains the visual marker/glyph but **does not bake the comment body into screenshot pixels**.
- Compact anchored editor on/near marker; default about current 320×205 DIP-equivalent geometry, with clamping to visible monitor. Adapt size based on content and available space without obscuring all of the target when avoidable.
- Header: `A1 / COMMENT` or localized equivalent, concise Commissioner. Use one narrow Signal side accent; do not build a large editorial card.
- Text input: appropriate caret, selection, wrapped lines, contrast and focus. Preserve Enter = save; Shift+Enter = newline; Esc = cancel; and Ctrl+Enter handling as currently implemented in editor state.
- Keep validation for empty comments and correct non-consumption of marker number on cancellation.
- Preserve hit-testing, moving existing marker, editing, deletion, and action history.
- **Marker-overlap redesign is deferred.** Do not change placement, numbering, painter geometry or export layering as part of this visual refresh unless a separate task explicitly requests it.

## 7E. Settings window

### Real navigation

`Основные / General` · `Клавиши / Hotkeys` · `О программе / About`.

The prior design concept with `Захват`, `AI`, `Экспорт`, `Аннотации` and other standalone pages was conceptual. **Do not add those pages without actual functionality.**

### Composition

- Preserve the familiar **left navigation rail + scrollable content area**.
- Approximate design target (not a hard implementation requirement): Settings preferred size around **820–900 × 600–680 DIP**, with min size compatible with current app/tests, bounded to work area. Existing implementation currently uses `780×620`, min `700×510`; avoid making it unusable on smaller displays.
- Sidebar width approx. **180–210 DIP**; quiet `bg.sidebar` surface. Small brand symbol + `ScreenIt`, short secondary caption (not an invented product promise). Selected nav item has a compact Signal accent/strong vertical bar or restrained fill; avoid a solid red rectangle wider than necessary.
- Main header: one **Unbounded** page title, smaller Commissioner explanation, single TechnicalDivider / small geometric markers if needed. Use negative space, not giant 48px typography.
- Content: real settings grouped into rectangular surfaces with restrained borders; each row has a primary label, optional supporting text, and actual control aligned consistently.
- Accent use: Signal for selected nav and active switch, Acid sparingly for focus/short informational markers. Three diagonal ornamental slashes are optional, maximum one group per page.
- **No Save/Cancel footer.** Existing changes apply and persist immediately; adding those buttons would be functionally misleading.

### Page-specific content

**General:** theme (`System/Light/Dark`), a separate design list (`Classic/Ember`), language (`Русский/English`), Start with Windows (real system state), Disable session clear warning. Theme/language apply immediately; a design change asks for confirmation first and only acceptance applies and persists it. Classic keeps the 0.2.3 blue interface, Ember keeps CONSTRUCT. All choices survive restart; old files without a design value use Classic.

**Hotkeys:** Capture, Paste Session, Clear Session recorders; Reset defaults; annotation modifier (`Ctrl/Shift/Alt`); supported keyboard instructions. Preserve conflict detection, busy/unavailable states, focus handling, Esc cancellation, and old binding retention after failed change.

**About:** actual installed version from source, repository link, update checks/download/install flow, progress and error states; no hardcoded `v0.9.0` from a mockup, no fabricated feature marketing.

### Light theme

Do not simply invert Dark. Use Paper as primary canvas, warm-neutral section backgrounds, Ink type and borders. Signal marks selection; Acid is used in rare focus/status accents (with dark keyline for visibility on light background). Type, spacing and structure must be **equally designed** in both themes.

## 7F. Tray, context menu, confirmation and notifications

- Preserve native Windows tray discoverability and menu structure; style where technically safe, but **do not sacrifice native menu keyboard behavior** for custom drawing.
- Preserve tray double-click capture, Paste Session hint, Clear Session, Settings, More → manual Copy commands, and Exit.
- The native tray menu may remain more system-native than WPF Settings; visual consistency should come from iconography, wording and minimal styling, not rewriting the tray infrastructure.
- Confirm/discard dialogs inherit theme and type scale but remain compact and legible.
- Toasts should be near-taskbar, content-sized, no click handling, no sound, and never steal focus.

---

## 8. Interaction states and motion system

### 8.1 Motion philosophy

**Fast, reactive, subtle, mechanical — never theatrical.** Motion should help users perceive state changes, not advertise the interface. Favor opacity, a 2–4 DIP translation, thumb movement, and restrained fill transitions. Avoid spring, bouncy scaling, parallax, blurring the desktop, whole-window whooshes, or HUD scanning lines.

### 8.2 Token scale

| Token | Duration | Easing | Use |
|---|---:|---|---|
| `motion.instant` | 0 ms | none | Keyboard action execution, selection geometry, mouse drag, content commit, caret. |
| `motion.micro` | 80 ms | ease-out | Hover and short icon/fill emphasis. |
| `motion.fast` | 120 ms | quadratic ease-out | Tool selected-state visuals; simple text/state swap. |
| `motion.base` | 160 ms | quadratic ease-out | Popover/editor entrance and compact toolbar reveal. |
| `motion.slow` | 200 ms | quadratic ease-out | Toast or modal entrance where allowed. |
| `motion.exit` | 100–120 ms | ease-in | Dismissal without delayed interaction. |

Keep total animations short and finite. WPF `QuadraticEase(EaseOut)` is a suitable safe default; no requirement for a custom easing library.

### 8.3 Transition recipes

| Element | Entrance/transition | Exit | Hard rule |
|---|---|---|---|
| Tool hover | Fill/border transition 80 ms | 80 ms | No geometry shift. |
| Tool selection | Color/indicator 120 ms | Immediate state, optional 120 ms visual | Switching tools is instant in model. |
| Toolbar appear | Opacity 0→1 + Y 3→0 DIP, 160 ms | 100 ms | No delayed capture activation. |
| Context hint change | Text swap or 70–100 ms fade | Immediate on error | No delayed help after state change. |
| Comment editor | Opacity + Y 3→0 DIP, 120–160 ms | 100 ms | Editor focus/caret ready immediately. |
| Toggle | Thumb translation 120–160 ms | Same | Actual saved state before animation. |
| Settings section | Opacity 0.75→1 + Y 4→0 DIP, 140–160 ms | No navigation block | Preserve focus and scroll logic. |
| Tooltip | Fast reveal, system delay | Immediate | Do not obscure pointer. |
| Toast | Opacity-only 160–200 ms | 120–160 ms | Noninteractive and focusless. |
| Confirmation | Opacity + ≤3 DIP, 160 ms | 100 ms | Keyboard/focus immediately usable. |
| TechnicalDivider | **Static** | — | No scanning/glow animation. |

### 8.4 Reduced motion and system accessibility

- Honor `SystemParameters.ClientAreaAnimation == false`: use **static end states**, remove animation clocks, no forever-running decorative effects.
- Any other Windows animation/reduced motion preferences used by the project must be respected; do not add GPU-expensive visuals in overlays.
- Prefer animations that don't change measure/arrange geometry. Set the actual state before starting visual animation, and cancel clocks on unload/dispose.
- Do not animate frozen screenshot pixels, drag selection source rectangles, marker hit targets, or focus itself.
- Any transition longer than its equivalent keyboard response is a bug.

---

## 9. Accessibility, localization and input correctness

### 9.1 Keyboard and focus

- Every actionable element remains reachable/operable by keyboard in Settings and modal dialogs; use distinct, highly visible focus ring.
- Tool switching via `M/P/A/R`, comment `E`, delete, undo/redo and Done must remain reliable.
- Escape hierarchy preserved: cancel current editor/gesture before discarding the whole capture where existing behavior requires it.
- Focus handoff between overlay surface and editor MUST be tested, including activation/deactivation and multi-monitor input.
- During hotkey recording, keep all existing failure/rollback semantics and suppression behavior. Do not “simplify” by replacing the recording control with a nonfunctional text input.

### 9.2 Accessibility implementation

- Add/maintain `AutomationProperties.Name`, `HelpText`, `ItemStatus` where appropriate; for icon-only controls, provide localized accessible names.
- Focus indication never relies on fill color alone. Selected tool should have both visual treatment and accessible selected status.
- Disable visually and semantically unavailable actions; use `IsEnabled=false` and preserve tooltips if helpful.
- Respect Windows font scaling and high contrast expectations; if true OS High Contrast is detected, prefer system color overrides over strict brand colors to preserve readability.
- Don't set a custom cursor for buttons where it degrades Windows expected behavior; screenshot canvas crosshair is contextual.

### 9.3 Localization

- English source phrases and RU translations are maintained through the existing `Localization.cs` path. New strings must be registered and included in source-translation verification.
- No hardcoded Russian/English UI copy inside visual control templates if translation hooks are required.
- Use accurate and consistent terms: `Маркер`, `Карандаш`, `Стрелка`, `Рамка`, `Готово`, `Сессия`, `Клавиши`.
- Leave technical identifiers `A1`, `B2`, `Ctrl+Enter`, `S_IT` invariant.
- Avoid ALL CAPS for long Russian paragraphs; test long phrase wrapping, especially in hotkey conflicts and update dialogs.

---

## 10. Architecture mapping — where the agent should work

**Do not rewrite the engine for a cosmetic task.** The project uses `ScreenIt.Core` for annotation/session data and `ScreenIt.App` for WPF and Win32 integration. Recommended design extraction fits within the existing structure.

| File / area | Current role | Intended redesign intervention |
|---|---|---|
| `src/ScreenIt.App/Appearance.cs` | Theme + palette | Extend to semantic theme tokens; keep system/light/dark reaction. |
| `src/ScreenIt.App/UtilityUi.cs` | Shared controls/buttons/prompt helpers | Move reusable visual styles/brushes to coherent resources; do not break action callback plumbing. |
| `src/ScreenIt.App/SettingsWindow.cs` | Current 3-page Settings, hotkey capture | Retheme real control tree/layout; preserve immediate saves and recorder lifecycle. |
| `src/ScreenIt.App/AnnotationOverlay.cs` | Selection, editing, toolbar, gestures, layout | New compact top toolbar + context rail; preserve coordinate transforms, focus and event lifecycle. |
| `src/ScreenIt.App/Painter.cs` | Bitmap/glyph render | **Do not change export semantics** during UI-only phase. Separate rendering tokens carefully if needed. |
| `src/ScreenIt.Core/Draft.cs` | Tools and history | No model changes for design-only work. |
| `src/ScreenIt.App/UiMotion.cs` | Existing finite transitions | Centralize duration/easing tokens, honor reduced-motion; avoid proliferating timers. |
| `src/ScreenIt.App/ToastService.cs` | Small noninteractive toasts | Retheme tokens, preserve positioning and fade lifecycle. |
| `src/ScreenIt.App/UpdateSection.cs` | About update/progress UI | Restyle existing progress/action/error states. |
| `src/ScreenIt.App/Localization.cs` | RU/EN strings | Add required copy with real translations. |
| `src/ScreenIt.App/Coordinator.cs` | Session/capture/paste/tray orchestration | Treat as a behavior guardrail, not the target of a visual rewrite. |
| `src/ScreenIt.App/Preferences.cs` | Persistence/hotkeys | Preserve schema and behavior; do not add style knobs prematurely. |
| `assets/` + installer resources | Icon/assets | Replace/augment approved vector source and multi-size `.ico` assets after pixel-perfect sign-off. |
| `verification/` | Project verification | Add/adjust visual and interaction tests without weakening existing tests. |

### 10.1 Recommended resource structure (proposal)

```text
src/ScreenIt.App/
  Design/
    ConstructColors.xaml       # color tokens / theme resources
    ConstructTypography.xaml   # bundled typefaces and type scale
    ConstructControls.xaml     # reusable styles for WPF buttons/toggles/etc.
    ConstructIcons.xaml        # path geometry resources
    ConstructMotion.cs         # durations and transitions; or extend UiMotion.cs
  Assets/
    Fonts/                    # licensed font binaries, only if distribution approved
    Icons/                    # vector-derived assets
```

This is a suggested organization, **not** a reason to replace the current project architecture with a new UI framework. For existing C#-constructed UI, resource dictionaries or equivalent centralized style factories are both acceptable; consistency matters more than XAML purity.

### 10.2 Independent animation vs behavior

The model and input lifecycle should not wait for `Storyboard.Completed`. Actual state changes immediately; animations interpolate only presentation properties. Cleanup must stop clocks and avoid retaining WPF windows after closing.

### 10.3 Retheme versus export color

`Painter` must not follow the theme-dependent UI accent. The Ember export palette is an explicit, separately reviewed decision: **annotation stroke Signal Red `#F04D32` with a Paper `#FBFEF8` halo**, at the existing 2.5/4.5 widths. Classic reproduces the 0.2.3 export: **`#4255C5` stroke with a white halo**, plus the rounded blue badge. Both palettes are selected by the persisted design and shared by gesture/draft preview and bitmap export; a change to `UtilityUi.Accent`, Light/Dark, or overlay chrome must never silently alter exported marks.

```text
ui.accentPrimary             = Signal Red (brand chrome, Ember)
ui.accentSecondary           = Acid (focus/status, Ember)
annotation.strokePrimary     = Signal Red #F04D32 (Ember) / #4255C5 (Classic)
annotation.strokeContrast    = Paper #FBFEF8 halo (Ember) / white halo (Classic)
```

Gesture/draft preview and bitmap export share `Painter.Draw`, so a palette change must update that one path and be covered by synthetic preview/export checks on both light and dark backgrounds. Further annotation appearance changes still require their own tested and approved decision.

---

## 11. Deliverable sequence for the implementing AI agent

Implement in small reviewable increments. Each phase must leave the application runnable and its existing verification meaningful.

### Phase 0 — baseline and inventory

- [ ] Inspect current `README.ru.md`, `ARCHITECTURE.md`, `DEVELOPMENT.md`, `AnnotationOverlay.cs`, `SettingsWindow.cs`, `Appearance.cs`, `Painter.cs`, `UiMotion.cs`, verification suites.
- [ ] Capture baseline RU/EN, Light/Dark screenshots and test logs, including selection/annotation, Settings pages, dialog, toast, tray.
- [ ] Confirm current tool set; pencil exists in reviewed source. Record behavior that mockups omit.
- [ ] Confirm no uncommitted user changes will be overwritten.

### Phase 1 — foundation tokens, typography, icon resources

- [ ] Implement centralized semantic color mapping and theme switching.
- [ ] Bundle licensed Cyrillic + Latin Unbounded/Commissioner, verify static weights and family names.
- [ ] Establish text scale, spacing, border/radius, Keycap, icon geometry and focus patterns.
- [ ] Produce logo vector master and multi-size test outputs; keep original sign-off reference for comparison.
- [ ] Run text/contrast/font fallback tests on both themes.

### Phase 2 — Settings visual rewrite

- [ ] Restyle the **existing** General/Hotkeys/About pages.
- [ ] Preserve immediate save, theme & language switching, native startup state, hotkey recorder, update flow.
- [ ] Add TechnicalDivider sparingly, set correct typographic hierarchy, no fabricated Save/Cancel.
- [ ] Verify narrow window, scrolling, long RU strings, keyboard-only use, high contrast.

### Phase 3 — annotation toolbar and context rail

- [ ] Restyle actual Marker/Pencil/Arrow/Rectangle tools; include tool keycaps.
- [ ] Keep conditional Edit/Delete, Undo/Redo, cancel.
- [ ] Move Done to compact lower-right dock only when functional parity is tested.
- [ ] Implement lower-left **state-driven hint**, with no duplicated shortcut guide.
- [ ] Reduce opaque bottom coverage and keep screenshot center exposed.
- [ ] Test editor placement, target hit-testing, Escape hierarchy, multi-monitor layout.

### Phase 4 — dialogs, tray, toasts, update UI

- [ ] Restyle confirmations, feedback/toasts, about/update progress and simple tray visual language.
- [ ] Preserve content, actions, accessibility, owner window/focus behavior, and status specificity.
- [ ] Keep system-native elements where overriding them would create reliability/accessibility regressions.

### Phase 5 — motion and hardening

- [ ] Implement shared motion tokens and finite transitions; zero-motion fallback.
- [ ] Verify no delayed action state, infinite animations, retained clocks, or CPU/GPU spikes while overlay is active.
- [ ] Run all existing verification and manual visual smoke matrix.
- [ ] Document remaining design tradeoffs and unresolved marker/export concerns **without silently expanding scope**.

---

## 12. Acceptance checklist / definition of done

### Visual

- [ ] Both Light and Dark use exactly `#1A1A1A`, `#FBFEF8`, `#F04D32`, `#C5D947` for the four brand tokens.
- [ ] Distinct, legible semantic surface/border/text tokens are in place.
- [ ] Unbounded is visibly present **only** in display/editorial roles; Commissioner drives all routine UI.
- [ ] Cyrillic and Latin render from bundled fonts without silent fallback, at all target sizes.
- [ ] The app icon is red/ink `S_IT` crosshair-frame; variants are crisp at 256, 64, 32, 24 and 16 px.
- [ ] The industrial technical divider is used sparingly and without simulated progress meaning.
- [ ] The UI is recognizably CONSTRUCT without feeling like automotive software or a generic SaaS theme.

### Functional parity

- [ ] Capture region/window/monitor and annotation-modifier timing unchanged.
- [ ] Marker, Pencil, Arrow, Rectangle, Edit, Delete, Undo, Redo, Done all work.
- [ ] `Ctrl+Enter` commits to in-memory session; no new Copy/Save/Export button in overlay.
- [ ] Comment edit semantics, selection, validation, return focus and discard confirmation unchanged.
- [ ] No overlay chrome baked into exported image; annotation raster output unchanged unless separately approved.
- [ ] Session sequence and global Paste/Clear behavior unchanged.
- [ ] Settings apply immediately; hotkey conflicts retain previous binding; Update actions remain real.
- [ ] Tray menu and toasts remain operational and do not steal unintended focus.

### Accessibility and performance

- [ ] Keyboard-only interaction and `AutomationProperties` retain meaningful behavior.
- [ ] Focus visible in both themes, including Light background where Acid alone is insufficient.
- [ ] Contrast verified for main body text, controls and small shortcut badges.
- [ ] Motion 0 ms when disabled; otherwise finite and generally ≤200 ms.
- [ ] No major overlay content obstruction due to top/bottom chrome; bottom remains split/compact.
- [ ] Mixed-DPI multi-monitor selection/editing works; no physical-pixel / DIP coordinate regression.
- [ ] Existing verification passes; modified tests remain behavior-preserving, not weakened.

### Manual visual regression matrix

| Surface | Themes | Languages | Key scenarios |
|---|---|---|---|
| Settings / General | Dark + Light + System | RU + EN | Switch theme, language, autostart, clear warning. |
| Settings / Hotkeys | Dark + Light | RU + EN | Record, conflict, cancel, reset, keyboard focus. |
| Settings / About | Dark + Light | RU + EN | Update check, downloading, error, version wrapping. |
| Capture | Dark + Light | RU + EN | Region, window, Space, modifier-held state. |
| Annotation | Dark + Light | RU + EN | All four tools, select, edit/delete, undo/redo. |
| Comment editor | Dark + Light | RU + EN | Enter/Shift+Enter/Esc, empty error, bounds clamp. |
| Dialogs/toasts | Dark + Light | RU + EN | Clear confirmation, discard, success, error, focus. |
| Windows tray | Both Windows themes | RU + EN | Icon at 16 px, menu interactions, shortcut labels. |

---

## 13. Decisions, proposals, and unresolved questions

### Fixed by design discussion

- Theme/brand: **Ink `#1A1A1A`, Paper `#FBFEF8`, Signal `#F04D32`, Acid `#C5D947`.**
- Fonts: **Unbounded + Commissioner**, Cyrillic and Latin, FOSS-compatible distribution.
- Logo direction: **red field, black capture corners + central alignment ticks, black `S_IT` block**.
- Mood: precise, editorial-industrial, unusual but subdued; rigid grid, restrained diagonals.
- UX: top tools, compact bottom contextual hints + Done, no duplicate tool-key legend, no Copy button in overlay.
- Motion: quick and light, no disruption to interaction.

### Proposed, not yet approved pixel-for-pixel

- Exact neutrals beyond the four brand colors, spacing/radii, specific component dimensions, text scale and animation durations in this document.
- Exact tool-icon path drawings and application wordmark vectors.
- Micro-icon optical adjustments, status rail width and responsive breakpoints.
- Annotation export palette is decided in §10.3 (Signal/Paper); any further export appearance changes require separate approval.

### Deliberately not decided

- How to prevent marker glyphs from covering screenshot content without sacrificing correlation with comments and other annotations. **Do not solve incidentally during redesign.**
- Whether user-configurable annotation styling, a richer session viewer, or new export flows should be introduced.

---

## 14. Implementation prompt for an AI coding agent

> Read `DESIGN.md` completely, then inspect the current ScreenIt source. Implement CONSTRUCT as a **UI-only redesign** in small, tested changes. Use `#1A1A1A`, `#FBFEF8`, `#F04D32`, `#C5D947`, Unbounded (display) and Commissioner (interface), with verified RU+EN glyph coverage. Keep all working capture/annotation/session/tray/hotkey/settings/update behavior intact. Follow actual product features rather than conceptual mockups. Begin with semantic tokens, font resources, icons and Settings, then annotation toolbar plus compact split bottom context rail, then dialogs/toasts and motion. Do not add fake functionality, do not change export annotation appearance without explicit approval, and do not bake overlay chrome into screenshots. Include before/after screenshots and verification evidence for dark/light, RU/EN and mixed DPI. Treat the Definition of Done in §12 as acceptance criteria.

---

## 15. Source of truth and references

**Product source:**

- [ScreenIt repository](https://github.com/nickkho0201/ScreenIt)
- [`README.ru.md`](https://github.com/nickkho0201/ScreenIt/blob/main/README.ru.md)
- [`ARCHITECTURE.md`](https://github.com/nickkho0201/ScreenIt/blob/main/ARCHITECTURE.md)
- [`AnnotationOverlay.cs`](https://github.com/nickkho0201/ScreenIt/blob/main/src/ScreenIt.App/AnnotationOverlay.cs)
- [`SettingsWindow.cs`](https://github.com/nickkho0201/ScreenIt/blob/main/src/ScreenIt.App/SettingsWindow.cs)
- [`Appearance.cs`](https://github.com/nickkho0201/ScreenIt/blob/main/src/ScreenIt.App/Appearance.cs)
- [`Painter.cs`](https://github.com/nickkho0201/ScreenIt/blob/main/src/ScreenIt.App/Painter.cs)
- [`UiMotion.cs`](https://github.com/nickkho0201/ScreenIt/blob/main/src/ScreenIt.App/UiMotion.cs)

**Type/legal/platform references:**

- [Unbounded metadata / character subsets](https://github.com/google/fonts/blob/main/ofl/unbounded/METADATA.pb)
- [Commissioner typeface source / language support](https://github.com/kosbarts/Commissioner)
- [Microsoft: Packaging fonts with WPF applications](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/packaging-fonts-with-applications)
- [Microsoft: WPF pack URIs](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/app-development/pack-uris-in-wpf)

**Visual references** are the agreed ScreenIt CONSTRUCT conversation sketches: restrained Dark Settings with one technical divider; compact annotation top toolbar; split contextual bottom dock; `S_IT` red crosshair icon. These are *directional references*. Any ornamental/mock-functionality shown in generated images is explicitly overridden by the functional rules of this document.

**Document maintenance rule:** whenever an implemented component deviates from this guide, update the implementation evidence and either correct the component or record an explicit approved decision here. **Never silently expand product scope to match a picture.**

## 16. Compact marker/editor correction — 2026-11-08

This component decision supersedes the rejected 88×46 DIP badge and 520×320 DIP card prototype. Do not enlarge a badge to imitate the enlarged reference image.

- UI badge: project the original physical Geo.Badge through source fit and actual HWND DPI; retain a readable 22–28 DIP height, measured Commissioner Semibold 13 text, 3 DIP cuts, Ink/Paper/Signal, no shadow or scaling on hover. Bounds used for drawing and input are identical. If a crop is too small, put the readable UI face just outside it and retain the real anchor.
- Card: up to 380×246 DIP; padding 16–18, header 38, Unbounded ID 24 (22 constrained), secondary Commissioner SCREENIT with small Acid slashes, one neutral bent divider. Integrated native TextBox, neutral normal border and Signal focus, compact themed scrollbar. Footer wraps passive keycaps into two rows when required.
- Placement stays in overlay DIPs after actual per-HWND conversion, picks available space next to the badge and clamps to the monitor. Stored anchors and PNG rendering remain in physical pixels.
- Open: 140 ms opacity/4 DIP translation ease-out; close: 100 ms opacity; hover/selection: 90/120 ms contour transition. ClientAreaAnimation disables these transitions; data changes never await animation.
- This correction changes interactive presentation only. Painter/Core/export behavior from the pre-task baseline is frozen. See verification/CONSTRUCT-REVIEW.md for current evidence and limitations.

## 17. Floating comment material — owner-directed refinement, 2026-11-08

For Marker + Comment Editor only, the latest visual reference explicitly supersedes the flat/no-gradient guidance: use a matte floating surface with a subtle Ink→bg.surface gradient, restrained Signal reflection and stationary soft shadow. This is a local exception, not a new global material for Settings/toolbars/toasts.

Keep the projected compact badge and physical anchor contract from §16. Card footprint remains 380×246 DIP; header ID becomes Unbounded 26. Header/Input/Footer retain their structural zones, with a custom input silhouette (small rounded corners and one lower-right chamfer). The native TextBox remains the editor. Keycaps use a subtle raised surface and clearer foreground. A neutral top-edge light and warm lower reflection separate the panel from the screenshot; no backdrop blur or glass.

Shadow is drawn from the card silhouette in small stationary vector layers, with no DropShadowEffect on the TextBox or its contents. High Contrast bypasses material/shadow. The connector is a single 1.2 DIP warm Signal tint with an axial stem and 45-degree elbow, a small Paper/Signal port at the badge, and no thick white outer stroke. Adjacent card/badge spacing is 16 DIP; placement still uses available-side selection and screen clamping. Exported PNGs are untouched.

## 18. Done placement — owner direction, 2026-11-08

Done is an independent top block to the right of annotation tools, separated by 12 DIP. Center both blocks as one row; measure Done first and reserve its width when wrapping tools. Bottom contextual hints remain separate; there is no bottom Done block. This supersedes earlier bottom-Done proposals. Commit/keyboard behavior is unchanged.

## 19. Notices — owner reference, 2026-11-08

Application-owned confirmations/information use a 520 DIP window including shadow gutters, with custom 48 DIP caption (logo/title/close), cut outer silhouette, matte surface and restrained warm header tint. Unbounded 22 headline with Signal vertical rule; Commissioner 15 supporting text; technical divider without a SCREENIT signature; existing optional checkbox with Signal focus border; separate angular action row. No new prompt semantics: Cancel remains the default for destructive confirmations, Escape/X keep data, opt-out is persisted only by the existing accept path. Short passive feedback uses the compact cut material and Signal top edge; it retains Commissioner for narrow localized text, existing measured wrapping and noninteractive lifetime. Light and High Contrast keep their theme roles.
