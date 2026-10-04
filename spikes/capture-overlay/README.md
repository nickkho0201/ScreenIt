# Spike 2 — Capture + Fullscreen Overlay + Mixed DPI

Disposable C# / .NET 10 / WPF harness для Windows 11 x64. Production ScreenIt, sessions, clipboard, annotations, tray, settings, export и installer здесь отсутствуют. NuGet packages отсутствуют; package sources очищены. Runtime не использует сеть. Захваченные pixels живут в RAM; JSON reports содержат только техническую metadata, geometry, counters и timings.

Статус: **Spike 2 CLOSED** после manual region/full-monitor/mixed-DPI/frozen-content/Escape acceptance пользователя. `BitBlt(SRCCOPY | CAPTUREBLT)` принят как MVP backend. Evidence и честные compatibility boundaries: [RESULTS.md](RESULTS.md). Этот harness остаётся archived evidence; production находится отдельно в src (см. root README).

## Запуск

На этой машине Release binary уже собран:

```powershell
# Run from the repository root.
Set-Location '.\spikes\capture-overlay'
.\bin\Release\net10.0-windows\CaptureOverlay.exe
```

Framework-dependent EXE требует Windows Desktop runtime .NET 10 x64. Проверенный runtime: 10.0.11. Сборка с установленным SDK:

```powershell
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
dotnet build -c Release
```

Если SDK отсутствует в PATH, установите .NET 10 SDK или укажите путь к своему `dotnet.exe`.

Это не installer; SDK/runtime не скачиваются самим harness.

## Controls

- `Start / return` или global `Ctrl+Alt+S`: начать capture. Если он уже существует — вернуть focus в прежний overlay, без повторного capture. При конфликте hotkey launcher сообщает ошибку; кнопка работает независимо.
- Drag левой кнопкой: region на одном monitor. Mouse capture и clamp не позволяют создать crop через границу monitor. Новый drag на другом monitor явно заменяет предыдущую selection.
- `Space`: выбрать весь monitor активного overlay. На другом monitor сначала click для focus, затем Space.
- `Enter`: commit выбранной region/full monitor; overlays исчезают. Launcher показывает размеры/crop bounds и read-only crop preview. Preview только в RAM, без editor/tools/export; очищается при следующем capture.
- `Esc` во время незавершённого/paused drag: rollback только gesture до предыдущей completed selection. Следующий Esc в idle state отменяет capture.
- `Esc` в idle state, включая уже завершённую selection: явная отмена capture. Дополнительного confirmation нет: в этом spike нет annotations/comments, теряется только временная selection. Приемлемость проверить руками.
- `Alt+F4` overlay: явная отмена capture. Обычный focus loss его не закрывает.
- `Show topology`: monitor physical rects, DPI metadata и machine/runtime.
- `Latency / resources: 100 cycles`: 5 warmups, затем 100 capture → show → cancel. Во время серии Esc отменяет её. Отчёт создаётся в `bin/Release/net10.0-windows/artifacts/measurements.json`.
- `Cancel retained capture` / `Restart retained capture`: явные действия после system suspension. Restart отбрасывает прежний frozen frame и снимает новый.

## Focus / interrupted drag

Launcher checkbox позволяет сравнить два режима до capture:

- **B, default:** Deactivated или LostMouseCapture отменяет только незавершённый gesture, возвращает geometry к состоянию до drag. Completed selection и frozen frame сохраняются.
- **A:** preview/anchor остаются paused; после возврата следующий mouse-down продолжает от исходного anchor. Это экспериментальный вариант для сравнения UX.

Automated state checks и реальные переходы focus между собственными HWND прошли для обоих. **B принят как deterministic MVP baseline**: rollback только unfinished gesture, frozen frame/completed selection сохраняются. A остаётся историческим experiment control в неизменяемом spike code, для MVP не требуется. Никакая обычная Deactivated/LostMouseCapture не вызывает cancel всего capture.

Реальный Alt+Tab в активном capture/selection не переключил приложение. Принята boundary: overlay — временно модальная interaction surface; Alt+Tab switching из selection для MVP не обязателен. Physical focus-loss path через Alt+Tab поэтому не проверен. Если реальный focus loss всё же происходит иным способом, draft должен сохраняться.

## Capture / geometry

Последовательность: trigger → убрать собственный launcher/preview → Dispatcher render yield + DwmFlush → EnumDisplayMonitors/GetMonitorInfo → BitBlt каждого monitor в top-down 32-bit DIB → copied/frozen WPF BitmapSource → проверить topology → создать overlays → ContentRendered каждого HWND + DwmFlush.

Backend: `BitBlt(SRCCOPY | CAPTUREBLT)`. HDC/HBITMAP освобождаются в finally, выбранный объект DC восстанавливается перед DeleteObject. WPF получает собственную копию pixels. Закрытие overlay снимает HWND hook и явно Dispose HwndSource. Между мониторами frames снимаются последовательно: это не общий атомарный кадр всех дисплеев.

По monitor создаётся borderless, topmost, не click-through Window без taskbar entry. Через SetWindowPos задаётся **physical full monitor rect**, включая taskbar и negative origin; root canvas проверяется по реальному HWND TransformToDevice. WPF-rendered dimming/HUD/selection не попадают в crop: CroppedBitmap читает original frozen raster. Bitmap background отображается 1:1 по physical bounds.

Manifest: PerMonitorV2. Canonical geometry — screenshot-local physical pixels. Преобразование на input boundary:

```text
WPF local DIP (relative to that overlay's canvas)
→ multiply by that HWND's CompositionTarget.TransformToDevice M11/M22
→ clamp to this monitor's [0..Width] × [0..Height]
→ screenshot-local physical pixels (capture origin is this monitor's top-left)
```

Для screen coordinates: subtract monitor Left/Top до local pixels. Единого virtual-desktop DPI scale нет. GetDpiForMonitor используется как enumeration metadata/initial sizing; input и placement проверяются через фактические GetDpiForWindow и TransformToDevice. Crop boundaries: floor minimum, ceil maximum, clamp. Gesture меньше 2 pixels по любой оси игнорируется и сохраняет prior selection.

## System transitions

WM_DISPLAYCHANGE, relevant settings/DPI change, WTS lock/unlock, power suspend/resume и shutdown request переводят capture в явно обозначенное suspended state: overlays скрываются, frame и completed geometry остаются в RAM, stale input не принимается. Launcher сообщает причину и требует explicit cancel/restart; автоматического remap/recovery нет. Topology сравнивается также после capture, до показа overlays. Отложенный WM_DPICHANGED старого/уже закрытого overlay не инвалидирует новый capture.

Для transition во время ещё не завершённого BitBlt данные могут ещё не существовать; показ отменяется с явной ошибкой/restart requirement. Это отличается от обычного focus loss. RAM не переживает завершение процесса/Windows shutdown; persistence не добавлена. Реальные disconnect/lock/sleep ещё требуют проверки.

## Автоматические проверки и измерения

GUI executable не печатает console output: ждать process exit и читать JSON. Exit 0 = завершён успешно, 1 = failed; при abnormal exit не считать старый report новым результатом. Пример:

```powershell
$reportPath = Join-Path $PWD 'artifacts\self-test.json'
$spikeProcess = Start-Process '.\bin\Release\net10.0-windows\CaptureOverlay.exe' -ArgumentList @('--self-test','--report',('"'+$reportPath+'"')) -PassThru -Wait
$spikeProcess.ExitCode
Get-Content -LiteralPath $reportPath
```

Заменить `--self-test` на `--measure 100` для measurements или `--topology` для inventory. `--report` задаёт явный путь; default — `artifacts` рядом с executable. Automated modes показывают transient test windows/overlays; запускайте, когда серия не мешает работе. Не сохраняйте screenshots/рабочий контекст для evidence.

Self-test покрывает simulated geometry 100/125/150/200%, reversed/clamped/tiny drags, negative origins/portrait math, A/B interruption, pixel-parity crop, реальные monitor frame dimensions, synthetic own-UI hide-before-capture, реальное deactivation/return собственных окон, WPF Escape handlers, retained suspension и native counters. Simulation и synthetic suspension не заменяют hardware/manual acceptance.

Latency metric: trigger в Start до всех WPF ContentRendered + DwmFlush. Включает hide, capture, managed copy, Window creation/render; исключает process startup и OS hotkey delivery до handler. Average/p50/p95/min/max измеряются после 5 warmups, percentiles = nearest rank. Target p95 ≤250 ms. Это software proxy, не photometric display measurement. Settle/GC после cancel вне latency; resource snapshots каждые 10 циклов и после warmup. Это stress test с явным GC для проверки lifetime, не production GC policy.

## Manual acceptance — архивная процедура

Пользователь подтвердил PASS на обоих реальных monitors для region/crop/preview, full-monitor Space → Enter, mixed DPI/negative origin, frozen video/dynamic content и доступных apps/browser/IDE, Escape и responsiveness. App names/versions не записаны. Physical 150/200/portrait, HDR/protected content и реальные system transitions остаются NOT TESTED, но не блокируют закрытый gate. Ниже сохранена исходная методика; новых tests ради полноты не назначается.

1. На обоих текущих monitors открыть обычное Win32/WPF окно, Firefox, Chromium-based приложение и IDE. При движущемся/обновляющемся UI вызвать Ctrl+Alt+S. Проверить неподвижный кадр, отсутствие launcher/предыдущего preview/overlay, отсутствие чёрных/missing областей, смещения и distortion. Для hardware-accelerated UI записать конкретный app/version; текущий HDR/SDR режим отдельно записать, не менять автоматически.
2. На каждом monitor выбрать region вокруг известных визуальных границ, проверить alignment pointer/selection и read-only crop. Проверить reverse drag, правый/нижний край, tiny click, Space → Enter. Drag через границу должен оставаться на исходном monitor. Left monitor с origin −2560 особенно важен.
3. Создать completed selection, Alt+Tab/Win key и вернуться Ctrl+Alt+S. Frozen frame и selection должны остаться; мышь/Space/Enter работают. Повторить loss focus во время drag сначала B, затем A. Записать какой вариант устойчив и предсказуем, не только наличие state.
4. Во время drag Esc должен вернуть prior selection; в idle Esc должен закрыть все overlays. Проверить, понятен ли cancel completed selection одним Esc без confirmation.
5. Если доступно безопасно: lock/unlock, sleep/resume, display arrangement/DPI change и monitor disconnect во время capture. Ожидается явный suspended state, сохранённый frame/geometry, возможность cancel/restart, отсутствие crash/stale-input. Не менять display configuration ради PASS без необходимости.
6. 150%, 200%, physical portrait сейчас не испытаны. Если конфигурации доступны — повторить шаги 1–4; иначе NOT TESTED. Synthetic math не является их PASS.
7. Записать субъективную скорость trigger и отсутствие selection lag; автоматические timings не заменяют это наблюдение. Внести только реально выполненные observations в RESULTS.

## Файлы

`Program.cs` — entry/test modes; `Harness.cs` — launcher, capture orchestration и measurements; `Overlay.cs` — WPF transient surface/input; `Geometry.cs` — physical selection math; `Native.cs` — небольшие Win32 calls/disposal; `Checks.cs` — disposable self-tests. `app.manifest`, `CaptureOverlay.csproj`, `global.json`, `NuGet.Config`, `.gitignore` — минимальная конфигурация. `artifacts/*.json` — локальное technical evidence, ignored.

## Microsoft documentation

- [BitBlt, CAPTUREBLT, ограничения и отсутствие ICM](https://learn.microsoft.com/en-us/windows/win32/api/wingdi/nf-wingdi-bitblt).
- [EnumDisplayMonitors](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-enumdisplaymonitors), [GetMonitorInfoW](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getmonitorinfow).
- [Manifest DPI awareness / PerMonitorV2](https://learn.microsoft.com/en-us/windows/win32/hidpi/setting-the-default-dpi-awareness-for-a-process), [WM_DPICHANGED](https://learn.microsoft.com/en-us/windows/win32/hidpi/wm-dpichanged).
- [WPF TransformToDevice](https://learn.microsoft.com/en-us/dotnet/api/system.windows.media.compositiontarget.transformtodevice?view=windowsdesktop-10.0).
- [Fullscreen positioning on multiple monitors](https://learn.microsoft.com/en-us/windows/win32/gdi/positioning-objects-on-multiple-display-monitors).
- [GetGuiResources](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getguiresources), [WTS session notifications](https://learn.microsoft.com/en-us/windows/win32/api/wtsapi32/nf-wtsapi32-wtsregistersessionnotification), [power messages](https://learn.microsoft.com/en-us/windows/win32/power/wm-powerbroadcast-messages).

До конкретного BitBlt failure второй backend не добавляется. Spike 2 закрыт; code не модифицируется дальше. Spike 3 также CLOSED; production MVP разрешён пользователем и создан отдельно от spikes (см. root README).
