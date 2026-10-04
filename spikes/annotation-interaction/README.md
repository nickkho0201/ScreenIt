# Spike 3 — Annotation Interaction

**CLOSED — disposable research evidence; interaction принят для production MVP.** Windows 11 x64, C# / .NET 10 / WPF / PerMonitorV2. Один full-monitor Screenshot A, без sessions/clipboard/export/tray/settings/installer. Нет production ScreenIt solution, NuGet packages, runtime network, remote assets, autosave или screenshot disk cache.

Spike 2 closed; BitBlt foundation скопирован как самостоятельный source file, без ProjectReference/runtime dependency на предыдущие spikes. Annotation interaction существует только в fullscreen frozen overlay. Launcher после commit — read-only inspection, без annotation editing.

## Запуск

На текущей машине:

```powershell
# Run from the repository root.
Set-Location '.\spikes\annotation-interaction'
.\bin\Release\net10.0-windows\AnnotationInteraction.exe
```

Нужен .NET 10 Windows Desktop runtime x64; проверен 10.0.11. Release EXE framework-dependent, не installer.

С установленным SDK:

```powershell
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
dotnet build -c Release
```

SDK 10.0.100 уже подготовлен вне repository на этой машине, если `dotnet` в PATH не содержит SDK:

```powershell
dotnet build -c Release
```

## Capture foundation

Выберите DISPLAY1 или DISPLAY2 в launcher. `Capture` / global `Ctrl+Alt+N` скрывает launcher/старую preview, снимает все текущие monitors через BitBlt до показа собственного overlay, затем открывает borderless topmost HWND на каждом monitor.

Выбранный monitor — один annotation draft с фиксированной буквой A. Остальные displays — frozen companions, без отдельного draft. Их Return button возвращает input в active monitor. Для другого monitor завершите/cancel текущий draft и выберите другой в launcher. В Spike 3 нет region-selection stage: annotation проверяется на полном monitor, crop уже доказан Spike 2.

Hotkey отличается от Spike 2, чтобы отдельные harness не конфликтовали. Повторный Ctrl+Alt+N возвращает существующий draft и открытый comment field, не делает новый capture. Если hotkey занят, launcher сообщает это; Capture button доступна.

Keys на companion HWND направляются в active draft: первый Esc при открытом comment editor отменяет именно edit, даже если input попал на другой frozen monitor.

## Marker/comment flow

Default tool — Marker. Click свободного места:

```text
provisional A1 badge
→ bounded floating TextBox immediately focused
→ type comment
→ Enter saves A1
→ Marker tool remains active
→ next free click opens A2
```

- `Enter` в editor сохраняет comment, **не screenshot**.
- `Shift+Enter` вставляет newline. Unicode/русский/английский/emoji остаются настоящим string text.
- `Esc` отменяет current edit: provisional marker исчезает, номер не расходуется; existing comment возвращается к предыдущему committed draft state.
- Empty/whitespace-only comment не сохраняется: editor остаётся с понятной error. Orphan marker не создаётся.
- Click вне editor не вызывает implicit commit или потерю typed text: сначала Enter/Save либо Esc/Cancel edit.
- Во время edit letter shortcuts M/A/R и Delete остаются обычным вводом TextBox. Ctrl+Z/Y относятся к native text editing; document history используется после закрытия editor.

Editor размером 360×235 DIP располагается рядом с physical anchor, flip left/up и clamp по monitor bounds. Badge у края сдвигается внутрь monitor для читаемости ID, но cyan anchor dot/leader сохраняет точный click point. Comment body показывается только в transient TextBox и отдельном text preview — **в screenshot не рисуется**.

## Existing annotations / tools

- Click existing badge → select. Drag badge → reposition marker; простое перемещение без resize.
- Double-click marker, `E`, либо Enter при selected marker → edit его comment.
- `M` — Marker, `A` — Arrow, `R` — Rectangle; toolbar показывает активный tool.
- Arrow/Rectangle: down → drag → up; arbitrary/reverse directions. Arrow короче 4 physical px игнорируется; rectangle меньше 3 px по любой оси игнорируется. Это простой accidental-gesture baseline, не performance quota.
- Direct hit testing выбирает existing annotation независимо от tool. Arrow — возле линии; rectangle — возле edges. Rectangle interior доступен для новых markers. Отдельный Select tool не добавлен.
- `Delete` удаляет selected annotation. Marker удаляется вместе с comment. Confirmation нет; действие доступно через Undo.
- `Ctrl+Z` — document Undo; `Ctrl+Y` / `Ctrl+Shift+Z` — Redo вне editor. Toolbar даёт те же действия.
- Mouse capture + physical clamp ограничивают gesture выбранным monitor. Interrupted gesture откатывает только preview; completed annotation model остаётся.

IDs стабильны: удалить A2 не переименовывает A3; следующий marker A4. Undo возвращает тот же internal GUID/ID/comment. High-water ordinal хранится отдельно от snapshot history: уже committed ID не переиспользуется даже после undo-create и нового history branch. Provisional cancel номер не расходует.

## Commit / Escape / focus

`Ctrl+Enter` вне editor/gesture либо toolbar Commit явно завершает screenshot. Plain Enter не завершает screenshot; это предотвращает путаницу с comment commit. Ctrl+Enter внутри editor blocked с подсказкой сначала Save/Cancel comment.

Commit создаёт in-memory annotated bitmap из frozen raster + badge/IDs + arrows + rectangles. Overlay toolbar/selection highlight/editor не участвуют в output. После закрытия overlays launcher отдельно показывает read-only image preview и human-readable comments:

```text
Screenshot A
A1 — проверить эту кнопку
A2 — русский
     многострочный комментарий
```

Annotation records остаются в RAM для inspection; undo stack больше не удерживается через launcher. Новый capture очищает previous inspection. Cancel удаляет uncommitted draft. Никаких export/clipboard operations или файлов screenshots.

Esc hierarchy:

1. Open comment editor → отменить только edit/provisional marker.
2. Unfinished drag → rollback только gesture.
3. Selected annotation без edit/drag → deselect.
4. Neutral state → explicit cancel всего draft/capture.

Alt+F4 overlay — explicit cancel. Обычная Deactivated/LostMouseCapture не закрывает draft и не очищает typed comment. При возврате Ctrl+Alt+N editor получает focus снова. Alt+Tab switching не обязан быть доступен из модального capture; это принятая boundary Spike 2, не повод считать реальный focus-loss path проверенным.

System topology/DPI/lock/power changes переводят existing state в явно обозначенный suspended mode, скрывают overlays, сохраняют model/editor text в RAM и требуют explicit cancel/restart. Recovery/persistence subsystem отсутствует; real system transitions не проверены.

## Geometry / renderer / history

`Draft.cs`: physical-pixel P/Bounds, immutable Marker/Arrow/Box records; snapshot undo/redo stacks. Internal GUID независим от display label. User strings существуют только в Marker.Comment и text formatter. Нет WPF geometry в этих records.

`AnnotationOverlay.cs`: per-HWND DIP↔physical boundary через CompositionTarget.TransformToDevice; HWND placement в physical monitor rect, including negative origin. Input gestures меняют preview до pointer-up; model меняется atomic history step. Comment body до Enter хранится в TextBox, не изменяет model автоматически.

`Painter.cs`: projection в BadgeGlyph/ArrowGlyph/BoxGlyph, **без поля comment body**. Shared physical drawing routines используются overlay (inverse DPI transform) и output RenderTargetBitmap (96 DPI, dimensions = native screenshot pixels). Render не захватывает WPF visual tree с editor. Selection hints и toolbar исключены. Badges фиксированы в physical px; editor/toolbar — обычные DIP UI controls.

`Launcher.cs`, `Native.cs`, `Program.cs`: disposable orchestration, BitBlt ownership/disposal и standalone test entry. HDC/HBITMAP освобождаются в finally; overlay removes hook и явно Dispose HwndSource. WPF bitmap lifetime — RAM references/GC, без disk cache. Runtime dependency на Spike 2 отсутствует; copied Native.cs можно удалить вместе со spike.

## Automated verification

Запуск показывает собственные transient windows, проверяет текущие HWND и закрывает их. Не взаимодействуйте с keyboard/mouse во время automated series. JSON содержит только metadata/check names/counters/timings; screenshot bytes и реальные comments не записываются.

```powershell
$reportPath = Join-Path $PWD 'artifacts\self-test.json'
$spikeProcess = Start-Process '.\bin\Release\net10.0-windows\AnnotationInteraction.exe' -ArgumentList @('--self-test','--report',('"'+$reportPath+'"')) -PassThru -Wait
$spikeProcess.ExitCode
Get-Content -LiteralPath $reportPath
```

Exit 0 + final report PASS — success. Exit 1 — failed assertion/exception; abnormal exit не превращает старый JSON в новый PASS. Default reports — `artifacts` рядом с binary; `--report` задаёт явный путь. `--topology` создаёт только technical inventory.

Покрытие: numbering/cancel/delete/undo branches, mixed history including create/move/edit/delete/supporting tools, empty comments, Unicode/multiline formatting, arrowhead/rect/hit math, 100/125/150/200 DIP conversions, editor clamp, renderer pixel parity/no-body contract. Actual HWND checks на доступных 100/125% monitors проверяют focus, provisional badges, editor bounds, input handler paths, output dimensions и own-window focus return. Это automation, не manual human UX PASS.

Stress: 50 markers +25 arrows +25 rectangles, 100 undo +100 redo, bitmap render; затем actual full-monitor stress render. 50 alternating capture/editor/cancel/commit lifetime cycles с 3 warmups и post-series quiet checkpoint. Test-only settle/GC — diagnostic policy, не production resource framework.

## Manual acceptance — короткий основной protocol

Выполните на DISPLAY1 (100%, negative origin), затем на DISPLAY2 (125%):

1. Capture → click → сразу type `проверить эту кнопку` → Enter. Без дополнительного click input должен попасть в comment; A1 badge остаётся, editor закрывается, capture остаётся. Создайте A2 с `русский`, Shift+Enter, `многострочный комментарий`, Enter.
2. Создайте provisional A3, попробуйте пустой Enter, затем Esc. Снова click: номер всё ещё A3. Создайте и сохраните его. Удалите selected A2, убедитесь, что A3 не переименован; Undo вернёт A2; новый marker должен быть A4.
3. Click/drag A1, Undo/Redo перемещения. Double-click/E edit его comment, Enter; Undo/Redo edit. Delete/Undo marker должен восстановить и его comment.
4. A → arrow в нескольких направлениях; R → rectangle reverse drag и возле edges. Click линия/рамка → Delete/Undo. Ctrl+Z/Y mixed sequence должна восстанавливать понятное состояние; tiny gestures не создают мусор.
5. M → markers в центре, справа, снизу и в углу. Editor полностью внутри monitor; можно увидеть marker ID/anchor, ввести multiline и Save/Cancel. Оцените удобство такого placement и читаемость.
6. Если реально происходит focus loss, оставьте незавершённый typed comment, переключите focus и вернитесь Ctrl+Alt+N: текст/model должны остаться. Не записывайте PASS focus-loss только потому, что Alt+Tab не переключил приложение.
7. Закройте editor → Ctrl+Enter. В read-only preview проверьте положения ID/arrow/rectangle, отсутствие comment bodies/UI и правильные dimensions. Ниже должны быть отдельные readable Unicode/multiline comments со стабильными IDs.
8. В launcher выберите Stress button на одном/обоих monitors. Попробуйте hit/select/move/edit, Undo/Redo и commit. Запишите subjective responsiveness, читаемость и конкретные failures, без выдуманного timing threshold.

В [RESULTS.md](RESULTS.md) записаны реальные manual PASS; детальные непроверенные combinations остаются NOT TESTED. Запишите outcomes и ответьте на семь UX вопросов: immediate typing vs separate panel; нужен ли reposition; Enter/Shift+Enter/Esc; оставаться ли в Marker; direct hit-test vs Select; edge placement; достаточно ли Undo без Delete confirmation. Harness позволяет проверить эти варианты, но окончательные ответы не предполагаются заранее.

## Источники Microsoft

- [DrawingVisual → RenderTargetBitmap](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/graphics-multimedia/how-to-create-a-bitmap-from-a-visual), [physical dimensions/DPI constructor](https://learn.microsoft.com/en-us/dotnet/api/system.windows.media.imaging.rendertargetbitmap.-ctor?view=windowsdesktop-10.0).
- [TextBox / AcceptsReturn](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/controls/textbox), [input and keyboard focus](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/input-overview).
- [TransformToDevice](https://learn.microsoft.com/en-us/dotnet/api/system.windows.media.compositiontarget.transformtodevice?view=windowsdesktop-10.0), [PerMonitorV2 manifest](https://learn.microsoft.com/en-us/windows/win32/hidpi/setting-the-default-dpi-awareness-for-a-process).
- [BitBlt / CAPTUREBLT](https://learn.microsoft.com/en-us/windows/win32/api/wingdi/nf-wingdi-bitblt), [multi-monitor positioning](https://learn.microsoft.com/en-us/windows/win32/gdi/positioning-objects-on-multiple-display-monitors).

Spike 3 CLOSED: immediate marker/comment interaction, move/edit/delete, supporting tools и commit приняты. Production разрешён отдельным указанием; harness остаётся независимым evidence.
