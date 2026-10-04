# Spike 2 — Capture + Fullscreen Overlay + Mixed DPI: результаты

Обновлено: 2026-10-04. **Spike 2 CLOSED.** Ручные observations пользователя добавлены к автоматическим evidence. `BitBlt(SRCCOPY | CAPTUREBLT)` принят как MVP capture backend ScreenIt. Production MVP теперь находится отдельно в src; второй backend не требуется.

## Evidence / environment

- Windows 11 x64, OS version 10.0.26200.0; desktop runtime 10.0.11; SDK 10.0.100 вне repository.
- Intel Core i5-13400F, 16 logical processors, 32 GiB installed RAM; NVIDIA GeForce RTX 3080, driver 32.0.15.9186 (локальные CIM metadata).
- Local interactive session, не Remote Desktop. Thread DPI awareness проверена: PerMonitorV2.
- DISPLAY1: left secondary, physical rect `(−2560, 0, 2560, 1440)`, enumeration DPI 96, actual HWND DPI 96 / scale 1.0.
- DISPLAY2: primary, physical rect `(0, 0, 3440, 1440)`, enumeration DPI 120, actual HWND DPI 120 / scale 1.25.
- Это реальная mixed-DPI / negative-origin конфигурация. Portrait, 150%, 200% на физическом monitor не испытывались. SDR/HDR state не зафиксирован; отсутствие capture error не доказывает color/HDR fidelity.
- Release build: PASS, 0 warnings, 0 errors. Внешних NuGet dependencies нет.
- [Self-test JSON](artifacts/self-test.json): **100 checks, PASS, process exit 0**.
- [100-cycle measurement JSON](artifacts/measurements.json): process exit 0, 5 warmups + 100 measured captures на обоих monitors; все placement checks прошли.
- Reports содержат только technical metadata, без screenshot bytes, файлов изображений, оконных titles или пользовательского текста. `artifacts`, `bin`, `obj` ignored; evidence — локальные файлы этого workspace.
- Manual evidence — сообщённые пользователем реальные observations на обоих указанных monitors: region/full-monitor capture, DPI alignment, frozen video/dynamic content, доступные обычные applications/browser/IDE, Escape и responsiveness. Названия/версии конкретных apps, точная дата теста и HDR/SDR mode не записаны; не подменяются догадками.

## Автоматическая verification

| Check | Evidence | Status | Limits |
|---|---|---|---|
| DIP ↔ physical math | Scales 1/1.25/1.5/2; two dimensions including portrait; origins 0/negative | PASS | 150/200/portrait math не является hardware PASS |
| Screenshot-local coordinates | Subtract negative monitor origin; no global DPI scale | PASS | Visual pointer alignment на текущих 100/125% monitors подтверждён также вручную |
| Crop bounds | Floor/ceil, reversed drags, clamp, non-finite rejection | PASS | Input gestures требуют manual acceptance |
| Accidental tiny gesture | 0/1 pixel dimension ignores gesture and restores prior selection | PASS | Threshold — простой spike UX choice, не product measurement |
| Crop exact pixel parity | Synthetic 16×12 source → 7×5 crop, full row-byte comparison | PASS | Не доказывает correctness capture чужого приложения |
| Native monitor frames | Actual 2560×1440 / 3440×1440, frozen BitmapSource | PASS | Доступные apps/browser/IDE проверены вручную; точные names/versions не записаны |
| Hide-before-capture | Own lime cover captured as control; after Hide + DwmFlush known red reference appears | PASS | Synthetic own WPF window, не все desktop surfaces |
| Fullscreen placement / mixed DPI | GetWindowRect exact bounds; actual canvas × HWND transform = physical monitor dimensions | PASS | Реальные 100% +125%, negative origin; no physical 150/200 |
| Focus interruption B | State math + actual own-window Deactivated restores completed selection; capture retained | PASS | Actual drag/mouse capture + Alt+Tab/Win key ещё не проверены |
| Focus interruption A | Actual own-window Deactivated pauses preview/anchor; state preserved | PASS | Resume by next click mathematically tested; UX руками pending |
| Return to overlay | Actual own-window Activate, selection retained | PASS | Не заменяет foreground policy внешних apps/system UI |
| Escape handlers | Routed WPF key event: cancel gesture only; idle Esc closes all overlays | PASS | Дополнительно реальные Esc interactions — PASS |
| Full monitor commit | WPF Space selects actual monitor bounds; Enter produces matching crop and closes overlays | PASS | Space → Enter также проверены вручную на обоих monitors |
| Render layer separation | Source crop equality; overlay HUD/dimming/selection не render into bitmap | PASS | Launcher read-only preview используется для manual review |
| Suspension path | Direct synthetic Suspend retains frame/selection until explicit cancel | PASS | Не реальный disconnect/lock/sleep |
| Native HDC/HBITMAP disposal | Owned counters = 0 после каждой capture; GDI restore/delete/release checked | PASS | Finite stress test, не доказательство отсутствия всех возможных leaks |
| Repeated overlay close | Explicit HwndSource Dispose; 100 cycles, counters below | PASS | Ограничено этим environment/проверенной серией |

## Scenario / manual compatibility matrix

PASS означает подтверждённый **указанный** результат; PARTIAL — automated portion прошла, visual/manual часть ещё отсутствует. NOT TESTED означает, что сценарий реально не наблюдался. Встроенные assertions не превращают foreign-app visual acceptance в PASS.

| Scenario | Environment | Result | Status | Notes |
|---|---|---|---|---|
| Secondary 100% DPI | Actual DISPLAY1 2560×1440, left | Drag/Enter/preview/physical dimensions aligned | PASS | Manual observation пользователя, DPI offsets не обнаружены |
| Primary 125% DPI | Actual DISPLAY2 3440×1440 | Drag/Enter/preview/physical dimensions aligned | PASS | Manual observation пользователя, DPI offsets не обнаружены |
| Mixed DPI 100% /125% | Both actual displays | Pointer/selection/crop alignment correct | PASS | Реальная hardware configuration, не simulation |
| Negative origin | DISPLAY1 at −2560,0 | Region capture работает без смещения | PASS | Manual + native placement evidence |
| 150% physical DPI | Not configured in measured topology | — | NOT TESTED | Math-only self-check passed |
| 200% physical DPI | Not configured in measured topology | — | NOT TESTED | Math-only self-check passed |
| Portrait physical monitor | Both current monitors landscape | — | NOT TESTED | Portrait geometry math passed |
| Dynamic desktop visually frozen | Current machine, video/dynamic content | Correct stop-frame примерно на момент hotkey; no black/missing/stale/distorted areas or holes observed | PASS | Manual visual acceptance; не photometric timestamp |
| Own UI excluded | Synthetic own WPF cover/reference | Correct known pixels after hide | PASS | Real launcher absence still inspect manually |
| BitBlt WPF | Own solid-color WPF synthetic windows | Known RGB values correct | PASS | Limited pattern; arbitrary WPF apps NOT TESTED |
| BitBlt ordinary applications | Names/versions/frameworks not recorded | Visually correct in available checked apps | PASS | Не утверждение о всех Win32/WPF applications |
| BitBlt browser, checked by user | Name/engine/version not recorded | Visually correct, including observed frozen dynamic content | PASS | Не приписывать неназванному browser конкретный engine |
| BitBlt Firefox | Version not recorded | — | NOT TESTED | No claim from backend success alone |
| BitBlt Chromium-based app | App/version not recorded | — | NOT TESTED | Include hardware-accelerated content |
| BitBlt IDE | App/version not recorded | Visually correct in checked IDE | PASS | Не универсальная IDE/accelerated-UI compatibility |
| Hardware-accelerated UI | App/version not recorded | — | NOT TESTED | GPU model alone is insufficient |
| SDR capture/color correctness | SDR/HDR mode not recorded | — | NOT TESTED | Confirm actual desktop mode before observation |
| Pointer region → Enter → preview | Both current monitors | Region alignment and physical crop dimensions correct | PASS | Reverse/edge/tiny-specific manual details отдельно не записаны |
| Drag across monitor boundary | Current monitors | Clamp logic implemented | NOT TESTED | Must remain on starting monitor; no cross-monitor crop |
| Space → Enter | Both actual monitors | DISPLAY1 2560×1440; DISPLAY2 3440×1440; full-monitor commit correct | PASS | Real keyboard manual observation |
| Alt+Tab modal interaction boundary | Actual active capture/selection | Application switching did not occur; capture remains active | PASS | Accepted MVP boundary. Physical Alt+Tab focus-loss/return path NOT TESTED because focus did not switch |
| Win key / return | Actual manual transition not performed | — | NOT TESTED | Must preserve frame/selection |
| Notification/popup / focus return | Reproducibility unknown | — | NOT TESTED | Only if readily reproducible |
| Interrupted physical drag | Actual external focus-loss path not observed | — | NOT TESTED | B chosen deterministic baseline; own-window interruption automatically tested, A not required for MVP |
| Escape human UX | Real gestures/idle capture | Gesture cancel and capture cancel behaved as expected; no UX issues | PASS | No additional confirmation required |
| Display topology/DPI change | Real event not induced | — | NOT TESTED | Synthetic suspend passed; old transforms must not accept input |
| Monitor disconnect | Real event not induced | — | NOT TESTED | Retain state and explicit cancel/restart |
| Sleep/resume | Real event not induced | — | NOT TESTED | No automatic resumption with stale frame |
| Lock/unlock | Real event not induced | — | NOT TESTED | WTS registration in harness; no secure desktop capture |
| Session shutdown | Real event not induced | — | NOT TESTED | RAM cannot survive process termination; no persistence promised |
| Selection responsiveness | User observation on current machine | Capture/selection felt normal; no noticeable selection lag | PASS | Subjective observation, no physical latency measurement |
| Trigger-to-render proxy | 100 samples / actual topology | p95 156.09 ms | PASS | Below 250 ms target for this software metric only |
| Resource cycles | 100 after 5 warmups | Stable GDI/USER, bounded handles/memory below | PASS | Finite series; no universal leak guarantee |

## Latency

Final measured build: `BitBlt(SRCCOPY | CAPTUREBLT)`, WPF hardware rendering default, two actual monitor frames, fullscreen per-monitor windows. 100 samples after 5 warmups. Target p95 ≤250 ms.

| Metric | Milliseconds |
|---|---:|
| Average | 132.71 |
| p50 (nearest rank) | 129.95 |
| p95 (nearest rank) | 156.09 |
| Minimum | 98.33 |
| Maximum | 161.48 |

Start: invocation of Start handler, before hiding own UI. End: all WPF ContentRendered + DwmFlush completed. Process startup and hotkey delivery before handler excluded. This is a software proxy, not an instrumented physical screen-visible timestamp. Native frame capture timings and per-monitor placement stored for each sample in JSON. Пользователь дополнительно подтвердил нормальную субъективную скорость и отсутствие заметного selection lag на текущей машине.

## Resource lifetime

Snapshots после cancel, settle и explicit test-only GC. No screenshot disk cache. Counters owned by BitBlt wrapper: **LiveDcs = 0, LiveBitmaps = 0 во всех checkpoints**.

| Checkpoint | GDI | USER | Process handles | Private MiB | Working set MiB | Managed MiB |
|---|---:|---:|---:|---:|---:|---:|
| After 5 warmups | 33 | 30 | 600 | 259.78 | 191.84 | 1.44 |
| Cycle 10 | 33 | 30 | 602 | 276.43 | 213.81 | 1.45 |
| Cycle 20 | 33 | 30 | 602 | 279.58 | 218.73 | 1.45 |
| Cycle 50 | 33 | 30 | 603 | 275.10 | 214.87 | 1.46 |
| Cycle 100 | 33 | 30 | 603 | 275.40 | 215.03 | 1.47 |

GDI/USER постоянны; total handles после начального роста стабилизировались на 602–603. Memory не растёт пропорционально captures: после early cache/warmup fluctuation private bytes держатся около 274–280 MiB. Небольшой managed growth включает сохраняемые sample/metadata report objects. Это наблюдаемая bounded series, не объяснение каждого runtime handle и не универсальный proof no leaks.

Во время разработки первый вариант без explicit HwndSource Dispose показывал рост handles примерно на 2/cycle (600 →805). После изменения close path два последующих 100-cycle runs показали bounded counts. Скрытый launcher также получил корректное получение HwndSource через HWND; ранний startup failure устранён. Эти pre-fix runs не используются как финальный PASS evidence.

## Preliminary findings / решения harness

1. BitBlt прошёл автоматические tests и manual visual acceptance доступных apps/browser/IDE/dynamic content на текущих реальных mixed-DPI monitors. Существенный capture failure не обнаружен. **BitBlt(SRCCOPY | CAPTUREBLT) принят как MVP backend.**
2. PerMonitorV2 + per-HWND transforms + physical SetWindowPos дают точные bounds на 100/125%; не нужен общий virtual-desktop DPI scale. WPF DIP остаются на UI boundary.
3. Capture precedes creation of overlays. Synthetic own UI hide-before-capture и crop source separation проверены; screenshots не содержат нарисованный selection/HUD по конструкции и pixel parity tests.
4. Обычный Deactivated/LostMouseCapture не уничтожает capture. Реальные собственные HWND focus transitions сохраняют state. **B: rollback только interrupted gesture — принятый deterministic baseline**, preserving frozen frame/completed selection. A остаётся историческим экспериментом в disposable code, но не MVP behavior. Manual Alt+Tab не переключил приложение: overlay принят как временно модальная interaction surface; возможность Alt+Tab из active selection не обязательна. Actual physical Alt+Tab focus-loss path не проверен.
5. Escape: active gesture rollback, idle capture cancel даже с completed selection. Enter commits, Space selects full monitor. Automated и реальные interactions passed; дополнительный confirmation не нужен.
6. System event policy: сохранить существующий frame/geometry в RAM, скрыть invalid overlays, сообщить suspended reason, разрешить explicit cancel/restart. Реальные event scenarios ещё не наблюдались; after process termination RAM recovery не обещается.
7. Final 100-cycle test не показывает прежний линейный handle growth. Latency software p95 соответствует ориентиру; субъективная interaction responsiveness подтверждена пользователем для текущей машины.

## Decision gate / open questions

**Spike 2 CLOSED.** Manual acceptance достаточна для MVP backend selection в указанном environment. System transitions и физически недоступные configurations не блокируют этот gate.

Принятый capture stack: Windows 11 x64; C# / .NET 10 / WPF; PerMonitorV2; Win32 monitor APIs; `BitBlt(SRCCOPY | CAPTUREBLT)`; physical-pixel canonical geometry; fullscreen frozen WPF overlays.

Proven current environment: два monitors, 100% +125%, negative origin, 2560×1440 +3440×1440; region и full-monitor capture; frozen dynamic/video content; корректные preview/crop и Escape; normal responsiveness.

Known unverified boundaries: physical 150%/200%, portrait, HDR/color fidelity, protected content, lock/sleep/disconnect/live topology transitions, unseen application-specific gaps. Имена/версии проверенного browser/apps не записаны; отдельные Firefox/Chromium claims не выводятся из общего browser observation. Эти ограничения остаются NOT TESTED и не являются blocking gate.

Только при конкретном будущем capture gap рассматривать Windows.Graphics.Capture / Desktop Duplication; второй backend сейчас не реализовывается. Обычная реальная потеря focus по-прежнему не должна автоматически уничтожать draft, хотя Alt+Tab switching из active overlay не обязателен.

Spike 2 code далее не меняется. Следующий этап — самостоятельный disposable Spike 3 Annotation Interaction, без runtime dependency между spikes; production ScreenIt не начинается.
