# Spike 3 — Annotation Interaction: результаты

Обновлено: 2026-10-04. **CLOSED.** Manual interaction принят пользователем; production MVP разрешён отдельным указанием. Automated и manual evidence различаются.

## Environment / evidence

- Windows 11 x64, OS 10.0.26200.0; runtime 10.0.11; SDK 10.0.100. PerMonitorV2 проверен.
- Текущая машина: 16 logical processors, 32 GiB RAM; аппаратный environment тот же, что в закрытом Spike 2.
- DISPLAY1: 2560×1440, left origin `(−2560,0)`, actual HWND DPI 96 / scale 1.0.
- DISPLAY2: 3440×1440, primary origin `(0,0)`, actual HWND DPI 120 / scale 1.25.
- Standalone AnnotationInteraction.csproj; ProjectReference/PackageReference отсутствуют. Native.cs — независимая source copy доказанной foundation, а не runtime dependency.
- Release build: PASS, 0 warnings/errors.
- [Final automated evidence](artifacts/self-test.json): **242 checks PASS**, process exit 0. Actual HWND placements, stress timings и 50-cycle counters включены.
- Source reports не содержат screenshot pixels, actual user comments, foreign clipboard data или window titles. Нет clipboard operations, export, network, autosave. Все captured/rendered bitmaps и draft text остаются в RAM.
- Physical 150%/200%, portrait, HDR/color fidelity, реальные внешние focus/system transitions не проверены. Math tests этих scales не являются hardware PASS.

## Automated verification

PASS в этой таблице означает выполненную автоматическую проверку, а не human acceptance.

| Check | Result / evidence | Status | Limits |
|---|---|---|---|
| Empty comments | Empty/whitespace rejected in model and actual editor; no orphan/ordinal consumption | PASS | Human error UX ещё не оценён |
| Stable numbering | Delete A2 leaves A1/A3; next A4; undo restores same A2 GUID/body | PASS | Screenshot letter fixed A; session A/B/C не реализованы |
| Undo-create branch | Previously committed labels not recycled; new branch clears redo | PASS | High-water independent from history, deliberate spike rule |
| Mixed undo/redo | Exact record state after each create/move/edit/delete/arrow/rectangle/supporting delete step | PASS | Snapshot approach, no generic command framework |
| Unicode / multiline | Cyrillic/English/emoji strings; CRLF/LF normalization only in readable formatter | PASS | Real keyboard/IME interaction needs manual observation |
| Marker geometry | Physical anchors, clamped move; projection preserves coordinates | PASS | Visual alignment ещё требует manual inspection |
| Hit testing | Badge, arrow segment, rectangle edge; interior available for markers | PASS | Usability/overlapping dense annotations pending |
| Arrowhead / rectangles | Eight directions, normalized reverse drag, edge clamp, tiny rejection | PASS | Human readability pending |
| DIP conversion | 100/125/150/200 math roundtrip; actual per-HWND 100/125 placements | PASS | No physical 150/200/portrait claim |
| Editor layout | Center/right/bottom/corner bounds on both actual monitors; flip/clamp | PASS | Human typing visibility/comfort pending |
| Immediate focus | Actual visible TextBox.IsKeyboardFocused after provisional click handler on both displays | PASS | Automated own-window focus, not a human speed measurement |
| Provisional cancel | Badge/text discarded, model unchanged and next A1 preserved | PASS | Manual Esc flow pending |
| Companion key routing | Esc delivered on frozen companion cancels active comment edit, not whole draft | PASS | Same hierarchy on all fullscreen HWNDs |
| Existing edit/move/delete | Actual interaction handlers; Delete removes marker and comment; Undo restores identity | PASS | Handler invocation not physical mouse/keyboard acceptance |
| Enter context | Routed WPF Enter saves Unicode comment while capture remains active | PASS | Physical Shift+Enter needs manual check; modifier policy checked separately |
| Focus retention | Real own-window Deactivated keeps typed editor/model; return restores TextBox focus | PASS | Physical Alt+Tab focus-loss not assumed |
| Interrupted move | Preview rollback only; completed model/capture untouched | PASS | External focus interruption during physical drag pending |
| Body exclusion | Change only comment body → entire output bitmap byte-identical | PASS | Whole output bytes compared on synthetic source |
| Render contract | Glyph projection has no Comment/body field; shared physical draw routines | PASS | Toolbar/editor/selection do not enter output tree |
| Output fidelity | Native monitor dimensions, frozen output; cyan anchor at exact synthetic source pixel | PASS | Human overlay/output appearance comparison pending |
| Inspection | Commit closes overlays and shows visible read-only launcher with image + separate structured comments | PASS | No editor actions in launcher |
| Stress correctness | 50 markers +25 arrows +25 rectangles; exact state after 100 undo +100 redo; actual full-monitor render | PASS | Subjective responsiveness NOT TESTED; no arbitrary threshold |
| Native ownership | LiveDcs/LiveBitmaps = 0 after capture and all 50 editor/commit/cancel cycles | PASS | Counters cover own native resources, not all WPF/runtime allocations |

## Manual acceptance matrix

Ручные observations предоставлены пользователем 2026-10-04. Точный monitor отдельных gestures не записан; commits наблюдались при 3440×1440. Детальные combinations без отдельного наблюдения остаются NOT TESTED.

| Scenario | Monitor | Result | Status | Notes |
|---|---|---|---|---|
| Provisional marker / immediate editor / focus / entry / Enter commit | Tested overlay; monitor not recorded | Click → badge → focused editor → typing → Enter saves | PASS | No separate editor or comment panel needed |
| Marker delete | Tested overlay; monitor not recorded | Deletion works normally | PASS | Detailed stable-ID/Undo combinations automated only |
| Normal interaction responsiveness | Tested overlay | Natural and fast; interaction works very well | PASS | User verdict, not universal benchmark |
| Marker + immediate editor | DISPLAY1 100%, negative origin | — | NOT TESTED | Click → type without another click |
| Marker + immediate editor | DISPLAY2 125% | — | NOT TESTED | Badge/input alignment |
| Unicode comment | Both | — | NOT TESTED | Cyrillic/English/emoji actual typing |
| Multiline Shift+Enter | Both | — | NOT TESTED | Enter saves; newline remains in structured text |
| Empty Enter / provisional Esc | Both | — | NOT TESTED | No orphan; no consumed number |
| Edit existing comment | Tested overlay; monitor not recorded | Comment editing works | PASS | Detailed cancel/undo combinations not recorded |
| Marker move | Tested overlay; monitor not recorded | Marker can be moved; interaction feels normal | PASS | Detailed undo/redo combinations automated only |
| Stable IDs after delete | Both | — | NOT TESTED | A2 delete leaves A3; undo A2; next A4 |
| Arrow | Tested overlay; monitor not recorded | Tool/hotkey and normal workflow work | PASS | Reverse/tiny/delete combinations not separately observed |
| Rectangle | Tested overlay; monitor not recorded | Tool/hotkey and normal workflow work | PASS | Reverse/tiny/delete combinations not separately observed |
| Supporting selection/delete | Both | — | NOT TESTED | Line/edge hit; Delete/Undo |
| Mixed Undo/Redo | Both | — | NOT TESTED | No unexpected text/document undo mixing |
| Edge editor placement | Both | — | NOT TESTED | Center/right/bottom/corner usable |
| Actual focus loss / return | Available real transition | — | NOT TESTED | Typed edit/model retained; no claim if switching blocked |
| Escape hierarchy | Both | — | NOT TESTED | Edit → gesture → deselect → neutral capture cancel |
| Commit preview | 3440×1440 | Ctrl+Enter: separate bitmap/text previews; bodies absent from raster | PASS | 2 annotations: 86.21 ms; 3 annotations: 83.18 ms. Individual samples, not benchmark |
| Structured comments | Tested overlay | Comments formed separately from bitmap | PASS | Detailed Unicode/multiline keyboard combinations not separately recorded |
| 100 annotation stress | Current monitors | — | NOT TESTED | Select/move/edit/history feel responsive |
| Physical 150% /200% | Not configured | — | NOT TESTED | Not blocking this spike |
| Physical portrait | Not configured | — | NOT TESTED | Not blocking this spike |
| Real lock/sleep/disconnect/topology | Not induced | — | NOT TESTED | Known risk boundary; no recovery subsystem |

Procedure: [README](README.md). Preview масштабируется для inspection; физические dimensions указаны отдельно. Сравнивать screenshot marker anchors/geometry с overlay; не трактовать fit-to-window preview как изменение output resolution.

## Stress timings — single local observations

50 markers, 25 arrows, 25 rectangles. Это synthetic stress, не заявленный обычный user scenario. Значения из финального одного self-test run, без distribution/benchmark threshold:

| Operation | Observed ms |
|---|---:|
| Populate 100 records with history | 0.57 |
| 100 undo +100 redo | 0.02 |
| Render synthetic 1100×800 bitmap | 38.74 |
| Render actual full-monitor 3440×1440 annotated bitmap | 96.45 |

Actual render duration измерена внутри Painter.Render + Freeze, до text formatting, launcher inspection display и overlay closing. Это не pointer latency, screenshot-capture latency или human responsiveness. Actual stress overlay rendered through WPF/DwmFlush before commit; human interaction acceptance remains NOT TESTED.

## Resource observations

50 alternating capture → editor → comment commit → screenshot cancel/commit cycles after 3 warmups; explicit test-only settle/GC and quiet checkpoint. Preview/model cleared before resource snapshots. No screenshots written to disk.

| Cycle / phase | GDI | USER | Process handles | Private MiB | Working MiB | Managed MiB |
|---|---:|---:|---:|---:|---:|---:|
| Warmup baseline | 33 | 31 | 619 | 267.22 | 203.63 | 2.01 |
| 10 | 35 | 31 | 622 | 313.31 | 225.36 | 2.09 |
| 20 | 35 | 33 | 628 | 351.68 | 230.18 | 2.14 |
| 30 | 35 | 33 | 628 | 288.71 | 234.23 | 2.19 |
| 40 | 35 | 33 | 628 | 308.45 | 233.78 | 2.24 |
| 50 | 35 | 33 | 628 | 284.36 | 231.95 | 2.28 |
| Quiet after series | 35 | 32 | 628 | 282.87 | 228.61 | 2.28 |

Own HDC/HBITMAP counters = 0 at every checkpoint. GDI stays 35–36 after initial baseline; USER/handles rise during the series then vary within 33–34 /628–630 in late checkpoints. Private bytes peak earlier then fall; full-size pixel buffers do not accumulate linearly in this finite series. Diagnostic text log/check report grows deliberately, and managed bytes include it; exact attribution of runtime/cache allocations was not traced. Не обещается отсутствие любых leaks за пределами проверенной серии; GC/settle — test mechanism, не production policy.

## Preliminary findings

1. Click → provisional marker → immediately focused editor → typing → Enter is manually accepted as natural and fast.
2. Marker move, existing comment edit and delete are manually accepted. Arrow/Rectangle tools and hotkeys work in the normal workflow.
3. Ctrl+Enter commits annotated bitmap and separate structured text; comment bodies are not rasterized. Two observed 3440×1440 renders (86.21/83.18 ms) are individual samples.
4. Automated evidence establishes stable IDs, snapshot Undo/Redo, body exclusion and physical geometry. Detailed manual combinations not observed remain NOT TESTED.
5. No separate image editor or comment panel is required.

## Final decision

**Spike 3: CLOSED.** Accepted production baseline:

    Marker click → stable screenshot-scoped ID → immediate focused comment editor
    Enter saves · Shift+Enter newline · Esc cancels current edit
    M Marker · A Arrow · R Rectangle
    marker move · comment edit · delete · undo/redo
    Ctrl+Enter commits screenshot

Raster contains marker badges/IDs, arrows and rectangles. Comment bodies remain structured text. Production extends the proven algorithms to session-scoped letters and selection → annotation within the same overlay lifecycle. Unobserved hardware/system scenarios remain boundaries, not a reason to reopen the spike.
