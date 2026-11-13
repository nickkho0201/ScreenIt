# Architecture

Архитектура исходников `0.3.0` с Classic/Ember (CONSTRUCT). Публичное использование — в [README](README.md), команды и проверки — в [DEVELOPMENT](DEVELOPMENT.md), правила изменений — в [AGENTS](AGENTS.md).

## System overview

ScreenIt — Windows tray utility для накопления снимков с аннотациями и передачи их в выбранное приложение. Основного постоянно открытого окна нет. Сессия и изображения находятся в RAM; сеть используется только по явным командам updater. Нет backend, account, telemetry, persistent history или пользовательского Save As. Текущие исходники включают Window capture.

```text
WM_HOTKEY / reserved shortcut message / tray Capture / tray double-click
  → Coordinator.Capture
  → monitor enumeration + sequential BitBlt (worker thread)
  → frozen frames → per-monitor AnnotationOverlay
  → region drag / Space / W + window click → source + ScreenshotDraft
  → Painter.Project/Render → Session.Commit + Rasters
  → overlays close → short feedback

WM_HOTKEY Paste (foreground HWND + PID captured immediately)
  → Coordinator.PreparePaste (session snapshot → temporary PNGs)
  → PasteSequencer (release keys + guards)
  → one-file CF_HDROP → SendInput Ctrl+V, repeated per screenshot
  → optional CF_UNICODETEXT → SendInput Ctrl+V
  → feedback; RAM session retained
```

Tray Paste не вставляет автоматически: меню само владеет foreground и лишь предлагает перейти в получатель и нажать hotkey.

## Projects and responsibilities

| Часть | Ответственность и граница |
|---|---|
| `ScreenIt.Core` (`net10.0`, namespace `ScreenIt.Core`) | Physical-pixel geometry, selection state, annotation records, draft history, immutable committed metadata, session letters и comment formatting. Без WPF, raster storage, clipboard, файлов и Win32. |
| `ScreenIt.App` (`net10.0-windows`, x64) | Весь production UI, coordination и Windows infrastructure. Классы объявлены в global namespace; отдельного infrastructure-проекта и DI container нет. |
| `Coordinator` | Владеет Session, raster dictionary, overlays, hidden control HWND, tray, registrations, Settings, Clear dialog, paste cancellation и toasts. Управляет допустимыми переходами. |
| `AnnotationOverlay`, `SettingsWindow`, `UtilityUi`/`PromptWindow` | WPF interaction и presentation; overlay переводит input DIP в physical pixels, UI делегирует действия Coordinator/Core. |
| `Painter` | Общая проекция annotations в glyphs для overlay и результата; comment bodies отсутствуют в glyphs. |
| `Native`, `ClipboardTransport`/`TemporaryImages`, `PasteInput`/`WindowsPasteDelivery`, `ToastNative` | Конкретная Windows integration. P/Invoke также есть в SettingsWindow и PromptWindow для DWM/foreground. Это фактическое размещение, а не единый изолированный platform layer. |
| `PasteSequencer`, `IPasteDelivery` | Последовательность передачи и injectable delivery boundary для verification. Не receiver-specific integration и не Core service. |
| `Preferences`/`HotkeyRegistration`, `Appearance`/`UiDesign`, `L` | Persistence и регистрации, effective theme/design palette и in-code EN/RU catalog. |
| `GithubUpdateSource`/`IUpdateSource`, `UpdateTransfer`, `UpdateFlow` | Release metadata, bounded byte transfer, validation/storage, installation detection и window-scoped operation/state orchestration; UpdateSection отображает модель. |
| `ScreenIt.Verification` | STA executable, использует `InternalsVisibleTo` из App; не runtime dependency. |
| `installer/`, `spikes/` | Inno/PowerShell delivery infrastructure и самостоятельные архивные harnesses соответственно. Production не ссылается на spikes. |

App зависит от Core; verification зависит от App. UI и Windows infrastructure внутри App связаны напрямую. Не следует описывать это как универсальную платформенную абстракцию или строгую многослойную service architecture.

Production entry point — `src/ScreenIt.App/Program.cs: Program.Main`; verification — `verification/ScreenIt.Verification/Program.cs: Verification.Main`. Три spike-проекта имеют собственные `Program.Main` и global-namespace classes: clipboard harness — console/WinForms, capture и annotation harnesses — WPF. Core — library без entry point. Inno `[Run]` запускает production executable, не альтернативный runtime.

## Runtime lifecycle

1. `[STAThread] Program.Main` создаёт именованный mutex `Local\ScreenIt.MVP`. Второй экземпляр сразу выходит; IPC, активации первого экземпляра и передачи ему аргументов нет. `Local` ограничивает coordination Windows session, а не все пользовательские sessions машины.
2. Загружаются Preferences, выбираются `L` language, design и Appearance. Создаётся WPF Application с `ShutdownMode.OnExplicitShutdown`.
3. Coordinator создаёт скрытое control Window/`HwndSource`, добавляет message hook, регистрирует hotkeys и WTS notifications, создаёт WinForms NotifyIcon/ContextMenuStrip. WPF владеет HWND/DPI initialization; WinForms используется для tray UI.
4. На startup запускается conservative cleanup clipboard PNG generations. После успешного возвращения конструктора Coordinator production `Application.Startup` вызывает однократный `NotifyStarted`: локализованный background toast на primary monitor, без Settings/активации. Проверка mutex предшествует созданию Application/Coordinator, поэтому второй process не показывает toast. Update check на startup отсутствует. В ожидании действий работает dispatcher/message loop.
5. Capture устанавливает `busy`, скрывает toast и Settings, закрывает tray menu, делает render yield и `DwmFlush`, снимает frames в `Task.Run`, проверяет topology и создаёт overlays. После всех `ContentRendered` (timeout 5 секунд) проверяет physical placement и фокусирует primary overlay.
6. Drag/Space либо Window click создаёт draft и detached source в выбранном overlay; Coordinator назначает единственный `Active`. Без annotation modifier сразу вызывается обычный Commit, с удерживаемым modifier остаётся существующий annotation mode. Остальные overlays продолжают показывать frozen monitors, но не редактируют draft.
7. Commit допускается для текущего Active, вне editing и suspension. Сначала render, затем `Session.Commit`, затем raster pair по GUID. Все overlays закрываются, отображается Added toast. Cancel закрывает временные окна без добавления снимка.
8. Paste/Copy используют committed snapshot. Clear блокируется при capture/paste, иначе открывает единственный owned modeless confirmation; при включённом предупреждении только Accepted очищает Session и Rasters; optional `disableClearConfirmation=true` пропускает окно после тех же busy/capture/paste/prompt guards. Cancel/Escape/X/обычный Enter сохраняют сессию.
9. Закрытие Settings уничтожает только это окно. Tray Exit или `WM_CLOSE` control запускает Exit: закрывает Clear dialog, запрашивает подтверждение при наличии committed снимков, затем Dispose и Application.Shutdown.
10. Dispose идемпотентен: отписывает language event, закрывает Settings/Clear, отменяет paste, освобождает toast timer/windows, overlays, session/rasters, tray/icon/menu, hotkeys, WTS registration, hook и control/source. Program освобождает mutex.

Это синхронный Dispose, не awaited shutdown coordinator. Capture worker и PNG preparation не принимают shutdown token; ожидаемое продолжение после Dispose не защищено во всех capture местах. Paste cancellation проверяется в delivery, но незавершённая preparation не ожидается. Нет гарантии crash recovery или сохранения RAM при OS termination. Exit проверяет committed count, а не наличие аннотаций в незавершённом draft; это важно при изменении close paths.

## Capture and annotation

### Acquisition и coordinates

`Native.Monitors` использует `EnumDisplayMonitors`, `GetMonitorInfoW`, `GetDpiForMonitor`; возвращает device, physical bounds, primary flag и effective DPI, сортируя по device. Topology key включает device/bounds/DPI. Primary flag в key не включён.

`Native.Capture` получает screen DC, memory DC и top-down 32-bit DIB, вызывает `BitBlt(SRCCOPY | CAPTUREBLT)` от monitor Left/Top, копирует bytes в Bgr32 BitmapSource на 96 DPI и Freeze. В `finally` восстанавливает выбранный GDI object, удаляет bitmap/DC и освобождает screen DC; cleanup failures выбрасываются. `LiveDcs/LiveBitmaps` — diagnostic counters, не resource manager.

Мониторы снимаются последовательно: нет одного атомарного кадра virtual desktop. Capture выполняется до создания overlay; crop берётся из оригинального frozen frame, поэтому dimming, toolbar, editor и selection chrome не входят в output. Другие видимые окна самого ScreenIt, помимо Settings/toast/menu, не скрываются универсальным механизмом.

Manifest задаёт PerMonitorV2 и `asInvoker`. `SetWindowPos` размещает topmost borderless overlay в physical monitor rect, включая taskbar и negative origins. `Placement` проверяет GetWindowRect и canvas size × actual HWND `TransformToDevice`. Monitor DPI — metadata/initial sizing; input использует transform конкретного HWND, а не общий desktop scale.

```text
WPF canvas DIP × HWND TransformToDevice
  → monitor-local physical pixels
  → floor(min), ceil(max), clamp для selection
  → crop-local physical pixels (subtract Region.X/Y) для annotations
```

Region ограничен одним монитором. Минимум selection — 2×2 px; 0/1-pixel drag игнорируется. Space выбирает весь монитор overlay, имеющего keyboard focus; начальный focus — primary monitor. Это не capture всех экранов одним изображением и не автоматический выбор монитора по foreground receiver.

### Overlay lifecycle и cancellation

Выбор региона автоматически создаёт detached frozen crop и ScreenshotDraft в том же HWND; отдельного editor window нет. При mouse-up и Space физическое состояние настроенного Ctrl/Shift/Alt читается через GetAsyncKeyState: удержание сохраняет annotation mode, иначе сразу вызывается тот же Coordinator.Commit. Клавиша может входить в capture hotkey, быть нажата во время drag или отпущена перед drop. Прямой clipboard transfer не добавлен. Default tool — Marker. Новый marker существует в model только после непустого comment commit; Escape editor не расходует номер. Номера high-water не переиспользуются после Delete/Undo. Arrow минимум 4 px; Box минимум 3×3 px. Undo/Redo — snapshots текущего draft, не всей session; committed draft sealed.

PreviewKeyDown/Up и activation/deactivation обновляют visual modifier state без polling. Верхний hint содержит отдельный локализованный modifier element. SelectionGlow — WPF element с мягкими внешними accent rings, clipped вне region/window target; исходная тонкая рамка остаётся в Surface. Dependency-property animations дают 140 ms конечный fade без постоянного breathing, при SystemParameters.ClientAreaAnimation=false — статичный highlight. При закрытии clocks снимаются. Ни этот UI, ни hint не передаются Painter: region/monitor используют crop исходного Frame.Image, window — detached WGC source.

При Deactivated/LostMouseCapture откатывается незавершённый gesture, frozen frame и comment editor сохраняются. Escape сначала отменяет gesture/edit, затем capture; draft с сохранёнными annotations требует Discard confirmation. Overlay close/Alt+F4 направляется в Coordinator.Cancel через `ClosingByApp` guard.

Изменение topology, DPI, соответствующие power/WTS messages вызывают Suspend: окна скрываются, gestures прерываются, frame/model остаются в RAM. Возврат Capture требует cancel и нового capture; автоматического remap/resume нет. Физические system-transition scenarios не полностью проверены. Закрытие overlay снимает hook, Dispose HwndSource и очищает Content.

### Window selection

Capture начинается в Region; W переключает Region/Window, Space независимо от режима снимает focused monitor. WindowTargets перечисляет top-level HWND в z-order, исключает текущий PID, invisible/minimized/cloaked, tiny bounds, tool/menu windows, taskbar/Progman/WorkerW и windows с capture exclusion affinity. Normal owned dialogs остаются отдельными targets. Bounds — DWMWA_EXTENDED_FRAME_BOUNDS с GetWindowRect fallback, physical screen coordinates. Child controls не становятся targets. WindowSelection владеет scoped out-of-context WinEvent hook и snapshot: native callback только запускает одноразовый coalesced 50 ms refresh; MouseMove ищет в snapshot без EnumWindows/capture. Перед click snapshot синхронно обновляется для повторного hit testing, исключая окно, переместившееся до coalesced refresh. Окна процесса ScreenIt исключены по PID, включая overlays/settings/toasts. Все overlays рисуют одну physical target rectangle через собственный TransformToDevice; cross-monitor rectangle не обрезается до source monitor.

Click считывает annotation modifier и запускает WindowCapture на MTA worker. Backend — Windows.Graphics.Capture CreateForWindow, CreateFreeThreaded frame pool, D3D11 BGRA staging readback. Native ABI ограничен WindowCapture.cs; SDK projection/third-party dependencies не добавлены. FrameArrived — agile COM event sink, callback только сигналит event, worker ждёт event/cancellation с 4 s timeout. COM references удерживают callback до последнего Release. Frame/session/pool IClosable и D3D interfaces освобождаются в finally, Map/Unmap парные. Source Size/ContentSize проверяются; resize/invalid HWND/слишком большой (>40 Mpx) source безопасно отклоняется. Source — полный WGC-defined frame с normal non-client chrome, без desktop occluder, ScreenIt UI и cursor. Shadow/rounded-corner/alpha поведение задаётся Windows. HWND/PID перепроверяются перед и после acquisition. Protected surfaces/unsupported GPU/HDR/exclusive fullscreen могут не поддерживаться; PrintWindow/desktop-crop fallback отсутствует. Краткий системный capture indicator не подавляется.

Region/monitor используют initial frozen Frame.Image; window получает текущие pixels при click. ChooseBitmap создаёт тот же ScreenshotDraft и Active; Commit/Painter/Session/Rasters/sequencer не меняются. Для window annotation source целиком показан в выбранном monitor overlay, SourceScale<=1 преобразует input/editor placement/render, source/draft/painter geometry остаётся physical pixels полного bitmap. Region использует SourceScale=1. Window highlight/tint/glow существуют только в UI, не передаются в Painter. Esc/close/suspend/shutdown отменяют acquisition и освобождают selection hooks; worker завершает owned cleanup, shutdown по-прежнему не имеет общего async drain. `Geo.Hit` выбирает annotations, не HWND; PasteInput receiver discovery остаётся независимым.

## Clipboard and file output

`PencilStroke` хранит собственную read-only копию physical-pixel точек; `ScreenshotDraft.AddPencil` ограничивает их границами снимка и добавляет весь штрих одним snapshot-действием. Длина пути менее 4 px игнорируется, включая одиночный click. Overlay накапливает точки жеста с шагом не менее 1 px и сохраняет точную конечную точку при release; прерывание очищает только временный путь. `PencilGlyph` использует общий Painter для preview и output; hit test проверяет сегменты, Delete/Undo/Redo работают со всем штрихом. Штрихи не создают markers/comment bodies и не расходуют marker numbers. Кнопка Pencil / `P` следует существующим editor/gesture guards; toolbar переносит кнопки при нехватке ширины.

Raster pair содержит original crop и annotated frozen bitmap; Core хранит metadata, не WPF pixels. Painter выводит badges, arrows, boxes и pencil strokes; comment body передаётся только через CommentFormatter. Формат текста остаётся `Screenshot A` / `A1 — ...` даже при русском UI.

`CommentFormatter` возвращает пустую строку для снимка без Marker и пропускает такие секции при форматировании session/snapshot. `PreparePaste` использует этот же formatter для текста, сохраняя полный snapshot для PNGs: фильтрация текста не меняет состав изображений, screenshot letters или session. Copy Comments получает тот же текст.

PNG materialization — технический clipboard transport, а не постоянное сохранение сессии. TemporaryImages создаёт GUID generation в `%TEMP%\ScreenIt\Clipboard-v1`, ownership marker/timestamp и `ScreenIt-A.png` и т. п. с `CreateNew`. Root/ancestors проверяются на reparse points. PNGs остаются после Paste, Clear, clipboard replacement и exit: receiver read completion неизвестен.

Следующий startup удаляет только generations старше семи суток с ожидаемым GUID, marker, timestamp, допустимыми файлами, без подкаталогов/reparse points. Locked/denied generations сохраняются. Это ownership/TTL policy, не надёжное определение завершения чтения. Write failures могут оставить partial generation.

Clipboard images — `CF_HDROP` (15), UTF-16 paths с double NUL; comments — `CF_UNICODETEXT` (13). Обе записи добавляют `CanUploadToCloudClipboard` и `CanIncludeInClipboardHistory` с DWORD 0. Эти hints не гарантируют privacy у любого clipboard manager/receiver. PNG/DIB/HTML competing representations production не публикует.

HGLOBAL buffers готовятся до Open/EmptyClipboard. После успешного SetClipboardData ownership передаётся Windows; непереданные handles освобождаются. Open retries: до 10 попыток с 20 ms интервалом. Publication заменяет clipboard; backup/restore прежнего содержимого нет. Ошибка после EmptyClipboard может оставить partial payload; транзакционного rollback нет.

### Paste sequencing

PreparePaste берёт ordered session snapshot, формирует comments при наличии непустых markers, пишет все PNGs в worker и проверяет readability/nonzero length до первой публикации. PasteSequencer допускает один run, отклоняет empty/own/invalid target и через Guard проверяет HWND+PID foreground на async boundaries. Probe только открывает/закрывает clipboard, не читает его.

Ожидание release modifiers, V и configured trigger: timeout 5 s, poll 20 ms, quiet interval 40 ms. Для каждого image — singleton HDROP, owner/sequence guard и SendInput Ctrl-down/V-down/V-up/Ctrl-up. Паузы между images и перед финальным text — 350 ms. Нет receiver acknowledgement; Completed означает отправленные запросы, а не подтверждённые attachments. Guard не различает два поля ввода внутри одного root HWND/PID.

Foreground change, reheld keys, cancellation и ошибки останавливают sequence. Partial SendInput пытается отпустить только synthetic V/Ctrl; paste не повторяется автоматически. Session не меняется, уже вставленное не отзывается; retry может создать дубликаты. UIPI может блокировать elevated receivers.

## Notifications and background UI

Coordinator инициирует ToastMessage после production startup, commit, clear, paste outcome; обычный status остаётся также в tray menu/tooltip. ToastService держит одно окно: opacity fade-in 150 ms → hold success 1400 ms / warning 3000 ms → fade-out 200 ms → close. Одноразовый DispatcherTimer начинает expiry после fade-in + hold. Tick и completion привязаны к конкретному ToastWindow и проверяют identity Current; replacement, Hide перед capture и Dispose немедленно останавливают/отписывают timer, снимают animation/completion и закрывают старое окно. Dispose идемпотентен и блокирует последующий Show; shutdown не ждёт анимации.

ToastWindow — компактный HUD с нейтральной Dark/Light surface, мягкими border/shadow, status badge 26 DIP и title/detail 13/12 DIP. Classic использует surface 0.2.3, radius 15 и системный шрифт; Ember — NoticeSurface, radius 4 и Commissioner. Surface измеряется по естественному DesiredSize Auto columns (badge + gap + text), без minimum/fixed width; max width 380 DIP, text stack ограничен сверху 308 DIP для wrapping. Shadow gutter добавляет 16 DIP к HWND. Обычная высота surface остаётся 68 DIP, title/detail wrapping при max width увеличивает высоту. Theme/language refresh повторно измеряет layout. HWND остаётся topmost, layered, toolwindow, noactivate, transparent hit-testing; не интерактивен, не перехватывает keyboard focus. Work area выбирается по capture monitor, paste receiver, primary для startup; DPI читается после размещения скрытого HWND на выбранном monitor. Physical bounds центрируются по ширине work area и отступают на 16 DIP от её нижнего края (с clamp для маленькой work area); специального taskbar API нет. Нет очереди, звука, notification actions или Notification Center. Не-OOM failure показа/expiry suppress/debug-log; завершённое действие не откатывается.

## Settings and hotkeys

Preferences path: `%LOCALAPPDATA%\ScreenIt\settings.json`. Load принимает object до 64 KiB/depth 12; валидирует поля и поддерживает theme-only `0.1.0`. Defaults — System, Classic, ru при Russian CurrentUICulture, иначе en, `Ctrl+Alt+S/V/X`. Invalid JSON/read error обычно ведут к defaults; invalid fields могут оставлять остальные уже прочитанные значения. Отдельной version-gated migration системы нет: schemaVersion записывается, но не управляет Load.

Save клонирует исходный JSON, сохраняет неизвестные поля, пишет schemaVersion 2, theme/design/language/hotkeys во временный соседний GUID файл и заменяет settings через File.Move(overwrite). Нет backup или fsync contract. Oversized/malformed файл не сохраняется как original; последующая запись может заменить его defaults. Theme/design/language применяются после успешного Save; Coordinator откатывает in-memory выбор при IO/permission failure.

HotkeyRegistration использует RegisterHotKey + MOD_NOREPEAT, dispatch через control WM_HOTKEY, для обычных сочетаний. Единственное исключение — Capture=Win+Shift+S: ReservedCaptureHook устанавливает WH_KEYBOARD_LL на отдельном message thread. Callback пропускает прочие keys, дополнительные Ctrl/Alt и injected input, подавляет только точный S gesture и repeats; PostMessage переносит capture в STA Coordinator с проверкой актуального dynamic ID. Modifier bits отслеживаются событиями и сверяются при S по состоянию предшествующих modifier keys, чтобы восстановиться после secure desktop. Hook не читает/логирует вводимый текст и не выполняет capture, save или UI callbacks. Startup при конфликте configured key/ошибке установки hook пробует default, предупреждает; даже недоступный default оставляет tray usable. Startup fallback меняет in-memory keys, не сохраняет их автоматически.

Replace проверяет полный уникальный valid набор, staged registration/reuse, затем persistence callback; при failure освобождает новые registrations, оставляя старые. Подготовленный reserved hook ещё не подавляет input до commit. Только после успеха снимаются obsolete registrations и включается новый binding. Hotkey IDs после replacement могут меняться: нельзя считать action равным wp ID. Runtime reserved hook остаётся только при назначенном reserved Capture; rebind/Dispose останавливает thread, unhooks и ждёт его завершения. Никаких Registry/system changes. Это отдельный lifecycle от HWND message hooks и mouse capture.

Settings владеет отдельным временным ShortcutRecordingHook для любого поля Capture/Paste/Clear. Он использует такой же dedicated message-thread pattern, но отвечает только за input acquisition, без runtime registrations. ShortcutRecordingState собирает физические left/right modifiers и первый main key, подавляет свежие down/repeat/up до release всей gesture; уже удержанные до установки modifiers учитываются, их release пропускается. Это сохраняет Win и позволяет записывать chords, которые не доходят до WPF. Result/Esc публикуется через PostMessage с generation token; UI передаёт chord обычному ChangeHotkeys, затем возвращает поле к рабочему binding и освобождает recorder даже при отказе. Hide/deactivation/Closed отменяют scope немедленно; запоздалые messages игнорируются. Production игнорирует injected input. WPF и runtime hotkey messages не являются вторым источником записи. Installation failure показывает inline ошибку и сохраняет прежние bindings. Hook хранит только текущую gesture, не текст/history.

Выбор backend находится в HotkeyRegistration.RegisterBinding(action,id,key): только Capture + точный Win+Shift+S использует reserved hook. Для всех остальных structurally valid chords, включая Win+Shift+S у Paste/Clear, доступность определяется RegisterHotKey без hook fallback и blacklist системных сочетаний. Validation проверяет supported modifiers/main keys и duplicates внутри набора. Reuse учитывает chord и backend, поэтому перенос S между Capture и другими actions требует соответствующей новой регистрации. HotkeyFailure сохраняет invalid/duplicate/unavailable/hook/save/busy причину и конкретную комбинацию; Settings показывает её в существующем inline error, startup fallback перечисляет отклонённые chords. Отказ регистрации или Save оставляет прежние mapping/settings/hook.

Preferences сохраняет optional поле `design` (`classic`/`ember`) в существующей schemaVersion 2. Отсутствующее/неподдерживаемое значение даёт Classic; старые theme/language/hotkeys/annotationModifier и unknown fields сохраняются. Save failure возвращает прежнее значение и не применяет design. General показывает отдельный список Design/Оформление; смена сначала проходит через существующий PromptWindow (Cancel — safe default), затем `Coordinator.ChangeDesign` пишет файл и только после успеха вызывает `Appearance.ChooseDesign`, который обновляет Settings, overlay, prompts и toast. Формат screenshot/session не изменён.

L использует английские фразы как keys, EN/RU dictionary и parameter patterns; refresh tree не должен переводить user TextBox content. Appearance выбирает theme palette и design palette (Classic — точные цвета 0.2.3, Ember — CONSTRUCT), читает HKCU AppsUseLightTheme для System, реагирует на WM_SETTINGCHANGE/WM_THEMECHANGED. `UiDesign` отдаёт Accent/OnAccent/Focus и системный шрифт для Classic; High Contrast остаётся системным в обоих оформлениях.

Optional autostart — единственный дополнительный HKCU Run value `ScreenIt` с quoted executable path; отдельного settings-поля и startup task нет. `AutoStart.cs` владеет чтением/изменением регистрации и не смешивается с preferences. Read классифицирует значение как Missing/Valid/Stale/Foreign/Unreadable и отдельно интерпретирует undocumented StartupApproved blob (observed 0x02/0x06 enabled, 0x03/0x07 disabled, иначе Unknown). `Enabled` = Valid + approval NotPresent/Enabled; Unknown никогда не показывается как confirmed Windows blocking. Enable/Disable подтверждают результат readback. Snapshot хранит value вместе с его реальным `RegistryValueKind` (raw, без expand environment names), поэтому неуспешный Enable восстанавливает прежние значение и тип (в т.ч. REG_EXPAND_SZ), а не подменяет тип. Значение или тип, изменённые внешне во время операции, не перезаписываются (RollbackConflict), сбой компенсации сообщается отдельно (Rollback). Disable перепроверяет владение непосредственно перед удалением; атомарного compare-and-delete для Run value нет, поэтому окно между проверкой и удалением лишь сокращается. Регистрация, принадлежащая другой копии ScreenIt (Stale) или другому приложению (Foreign), не удаляется молча; явный Enable может заменить регистрацию другой копии. Command ограничен задокументированными 260 символами. В production Settings → General вызывает Coordinator.ChangeAutoStart; verification подставляет fake/isolated store через фабрику `AutoStart.Create`.

## Updater and installer

Settings About по явному Check вызывает GitHub `/releases/latest` через HttpClient (5-minute timeout, User-Agent и API header). Metadata ≤1 MiB. Принимается только stable `v?major.minor.patch`, newer assembly version; draft/prerelease/equal/older не предлагаются. Требуются ровно один versioned installer и `SHA256SUMS.txt` и строгие GitHub page/asset paths.

Download разрешает HTTPS/default port без userinfo, заданный repository release path, redirects только в GitHub releases или `release-assets.githubusercontent.com` (до четырёх переходов). Checksums ≤64 KiB, installer ≤256 MiB. GUID generation в `%TEMP%\ScreenIt\Updates` получает ACL текущего пользователя, marker и reparse checks ancestors. SHA256SUMS должен иметь единственную корректную строку exact filename; hash сравнивается с installer bytes. Failed/cancelled download generation удаляется по ожидаемым файлам; cleanup itself может ошибиться. Успешно проверенные generations, в том числе после отмены install confirmation, автоматически по TTL не очищаются.

IUpdateSource.Download принимает optional IProgress<DownloadProgress>. Transport читает response Content-Length, считает реально записанные bytes, сообщает initial/final и промежуточные значения не чаще 100 ms; отсутствие длины остаётся null. Несоответствие EOF заявленной длине или превышение лимита отклоняется до verification. Check/checksum downloads не смешиваются с installer percentage. Streaming download и SHA-256 остаются async; signature verification не добавлена.

Settings владеет UpdateFlow: явный UpdateState содержит phase, release/version, downloaded/total bytes и classified error. Phases: Idle, Checking, UpToDate, Available, Downloading, Verifying, PreparingInstall, LaunchingInstaller, InstallerLaunched, Failed. Только Downloading с positive total имеет числовой Progress; остальные active phases indeterminate. Проверка hash повторяется перед preparation/identity guard и RAM-loss confirmation. LaunchingInstaller отдаёт render yield перед Process.Start; InstallerLaunched означает только успешный launch, затем прежний ExitForUpdate. Результат установки неизвестен, installation percent/complete не показывается.

UpdateSection подписывается на Changed и показывает state в существующем About, без нового окна/card. Active phase заменяет actionable buttons на статус/accent track/bytes; ошибки Check/Download/Verification/Preparation/Launch дают локализованный Retry. Retry получает новую GUID generation и очищает старые progress/error; attempt guard отбрасывает stale progress callbacks. Повторный клик блокируется по phase. UI notification failure логируется и не отменяет successful handoff. ProgressBar chrome отсутствует; indeterminate accent segment учитывает ClientAreaAnimation, при reduced motion используется static striped accent track.

Lifecycle остаётся window-scoped: Hide/show и переключение страниц сохраняют операцию/model state, Closed отменяет token и снимает subscriptions; новое Settings после close начинает Idle. Скрытое/неактивное окно не запускает confirmation автоматически, verified update возвращается в Available. Shutdown закрывает Settings и отменяет transfer, без нового общего async drain. Partial generation удаляется существующим cleanup либо остаётся изолированной при interruption/cleanup failure; retry не переиспользует её. Модель не записывается в preferences. Verification-only --update-ui-test публикует mock states в тот же IUpdateFlow/UpdateSection, без Coordinator/network/storage/installer.

Installed определяется по 64-bit HKCU uninstall entry с AppId, DisplayName, InstallLocation == AppContext.BaseDirectory и наличию `unins000.exe`. Portable открывает validated release page. Installed после download повторяет identity/hash check, затем спрашивает подтверждение RAM loss/unsigned setup. Только успешный Process.Start setup приводит к `ExitForUpdate`; failure/cancel сохраняет приложение. SHA-256 с того же release не является code signature или независимым trust root.

Inno AppId `{75AF53B9-2BC6-4AC3-A7D4-859884B5EAF0}` и mutex `Local\ScreenIt.MVP` связывают runtime, updater detection и install/upgrade. Setup ставит под `%LOCALAPPDATA%\Programs\ScreenIt`, `PrivilegesRequired=lowest`, Windows build ≥22000/x64, Start Menu и uninstall entry. `CloseApplications=no`, `RestartApplications=no`: setup не убивает и не перезапускает старую копию; AppMutex блокирует replacement/uninstall работающей программы. App запускает setup и затем выходит; результат установки после выхода не отслеживается. Optional final-page launch unchecked/skip-if-silent. Uninstall через `[Code] CurUninstallStepChanged(usUninstall)` удаляет Run value `ScreenIt` и связанный StartupApproved value только когда quoted path совпадает с `{app}\ScreenIt.App.exe` и Run value успешно удалён; регистрация portable копии остаётся. Settings и clipboard temp вне install directory не удаляются. Automated rollback, downgrade migration и RAM restore отсутствуют.

## Windows integration

| Boundary | Windows facilities |
|---|---|
| Capture — `Native.cs` | `user32` monitor enumeration/GetDC/GetWindowRect/SetWindowPos, `gdi32` DC/DIB/BitBlt/object cleanup, `shcore` monitor DPI, `dwmapi` DwmFlush. |
| Window capture — `WindowTargets`, `WindowSelection`, `WindowCapture` | `user32` top-level z-order/PID/visibility/WinEvent, `dwmapi` frame bounds/cloaking, WinRT HWND GraphicsCaptureItem/free-threaded pool and D3D11 staging readback. Scoped event/cancellation ownership; no desktop-crop fallback. |
| HWND/UI — `Program`, `Coordinator`, overlays | WPF Application/Dispatcher/HwndSource hooks, PerMonitorV2 manifest, WinForms NotifyIcon/menu; WM_HOTKEY, WM_CLOSE, display/settings/theme/DPI/power/WTS messages. |
| Session/power — `Coordinator` | `wtsapi32` session notification registration; lock/unlock и power transitions suspend capture, без secure-desktop acquisition. |
| Clipboard — `ClipboardTransport.cs` | `user32` clipboard formats/open/empty/set/close; `kernel32` HGLOBAL allocation/locking/free. Shell CF_HDROP file-list layout; actual image data хранится в PNG files. |
| Receiver/input — `PasteInput.cs` | `user32` foreground/root/PID/window/key-state, SendInput, clipboard owner/sequence guards; без receiver activation и low-level hooks. |
| Toasts/prompts/settings | `user32` monitor work area и window styles/hit-test/noactivate/placement, prompt foreground; `dwmapi` title-bar theme attribute. |
| Preferences/theme/install identity | LocalApplicationData, HKCU Windows theme registry, optional HKCU Run registration `ScreenIt` и 64-bit HKCU Inno uninstall entry. |
| Update/install shell | HttpClient HTTPS, Windows ACL текущего SID для update generation, shell Process.Start для validated page/setup; Inno per-user install/shortcut/uninstall/mutex. |
| Diagnostics | `GetGuiResources`, process handles/memory, installed RAM, remote-session и DPI-awareness queries; не постоянный telemetry/logging service. |

Release привязан к Windows x64 и Windows Desktop runtime. Отдельной переносимой implementation этих boundaries нет; `Core` независим от них по dependencies, но это не cross-platform версия приложения.

## Error handling, threading and diagnostics

Coordination/Core mutation, clipboard ownership, HWND/UI и timers работают на STA dispatcher; GDI acquisition и frozen PNG serialization вынесены в Task.Run. Не переносить WPF mutable objects или coordinator state в worker. Нет async disposal/drain barrier.

Capture catch закрывает overlays и сообщает safe error; commit/copy failures показывают сообщение, не предлагают automatic destructive retry. PasteSequencer возвращает outcome/count и в finally снимает IsActive. Settings/update ловят ошибки и показывают retry status. Program DispatcherUnhandledException помечает handled, приостанавливает capture и сообщает о RAM data; startup catch пишет type/stack в stderr и показывает MessageBox. Это не общий proof восстановления после любой ошибки.

Нет persistent logging service/файла и telemetry. Toast failure пишет Debug.WriteLine; Native diagnostics и JSON evidence относятся к verification. WinExe stderr может быть невидим без redirect. Некоторые Win32 return values (WTS registration/unregistration, hotkey cleanup, DWM title styling) не проверяются; нельзя обещать полный coverage отказов.

## Architectural invariants

Контракты, опирающиеся на code paths и проверки; не гарантия отсутствия bugs при любых OS/resource failures:

- Core geometry и annotations выражены в physical pixels; DIP остаются на WPF input/render boundary конкретного HWND.
- Только один active capture draft редактируется; после успешного commit/явного cancel overlays и Active очищаются. Suspended capture — намеренно retained state до cancel, не завершённый flow.
- Original frame/crop отделён от UI chrome; raster glyphs не содержат comment body.
- Committed screenshot metadata immutable; canceled draft не расходует session letter, marker high-water не откатывается с history.
- Для каждого production committed GUID Coordinator поддерживает соответствующий raster pair; Clear удаляет обе коллекции.
- Paste не изменяет session, не выбирает/активирует receiver сам и не продолжает при обнаруженной смене HWND/PID/clipboard.
- Temporary clipboard files не удаляются сразу после paste, Clear или exit.
- Confirmation guard принадлежит живому Clear window и сбрасывается на Closed; cancellation не очищает данные.
- Hotkey replacement не освобождает прежний рабочий набор до staged registration и persistence success.
- Notifications не крадут focus и не превращают успешный commit в failure при недоступном toast.
- Settings location и installer identity согласованы между versions; installation не удаляет preferences.
- Explicit shutdown освобождает owned HWND hooks, tray и registrations; изменения async paths должны учитывать его текущие ограничения.

## Known limitations and evidence boundaries

- RAM-only sessions без cap/history/crash recovery; original+annotated rasters и draft snapshot stacks увеличивают memory usage. Committed снимки нельзя reopen.
- Нет cross-monitor region, persistent Save As, cross-platform backend и receiver acknowledgement. Window capture использует WGC; real WPF occlusion/DPI/resource checks не доказывают совместимость всех browsers/accelerated/protected/HDR surfaces.
- Нет awaited capture/preparation shutdown и универсального transaction rollback для clipboard, session/raster commit или settings.
- README сообщает manual PASS для ChatGPT Web sequential paste и текущей 100%/125% mixed-DPI topology с negative origin. Физические 150%/200%, portrait, HDR/protected content и system transitions полностью не подтверждены.
- Архивный [clipboard RESULTS](spikes/clipboard-transfer/RESULTS.md) в заключительном production note ещё помечает Web acceptance OPEN/NOT TESTED, тогда как текущий README сообщает PASS. Это расхождение evidence chronology; точные receiver versions и обновлённая tracked acceptance matrix отсутствуют. Не расширять совместимость на все приложения.
- [installer RELEASE-NOTES](installer/RELEASE-NOTES.md) содержит notes текущей версии и историю. Локальные ignored artifacts могут содержать дополнительные прошлые отчёты, но не составляют воспроизводимую tracked test history.
- В manifest assemblyIdentity ещё `0.1.0.0`; App csproj/updater version — `0.3.0.0`/`0.3.0`. Это разные поля; release version берётся из assembly, не manifest.
- Installer/app unsigned; checksum и ACL не заменяют publisher signature. CI/CD workflows в tracked repository отсутствуют.

## Clear confirmation and UI feedback (0.2.2)

UI polish: `UiMotion` запускает только конечные presentation animations (110–150 ms), без timers/polling и задержек business actions. Settings сохраняет текущую визуальную позицию переключателя через Refresh; page transition стартует после Loaded. ComboBox использует штатный PopupAnimation.Fade. Checkbox fade и сдвиги/проявления учитывают SystemParameters.ClientAreaAnimation; owned clocks снимаются на Unloaded.

SettingsToggle automation peer направляет Toggle в тот же OnClick/callback, что обычный input, с сохранением/readback и rollback; изменение только IsChecked не заменяет действие. Disabled control отклоняет UIA Toggle. Contour/glow при annotation modifier используют CONSTRUCT Acid только в overlay UI. `Painter` задаёт явную export-палитру вне темы: Signal Red `#F04D32` со светлым контуром Paper `#FBFEF8` и прежними ширинами 2.5/4.5 для стрелок/рамок/карандаша; gesture/draft preview и bitmap используют один `Painter.Draw`. Метки используют фиксированные Ink/Paper/Signal.

Preferences сохраняет optional boolean `disableClearConfirmation` в schemaVersion 2 (missing/invalid → false). Подтверждение по умолчанию включено. Clear dialog применяет «Больше не спрашивать» только после Accepted; Cancel/Escape/Enter/X не меняют preference. General позволяет отключить или вернуть предупреждение. Save failure откатывает preference/UI; явно подтверждённая очистка всё равно выполняется с warning toast о несохранённых настройках. Оба пути очистки используют ClearCommittedSession и сохраняют cleared toast, reset A/A1 и очистку Session/Rasters. Exit/Discard confirmations не меняются.

## CONSTRUCT presentation (0.3.0)

`ConstructDesign` централизует brand colors, focus/hover surfaces, встроенные WPF font families, векторные tool icons и TechnicalDivider; его токены design-aware. Appearance сохраняет System/Dark/Light и использует системные цвета в High Contrast. В Ember Commissioner применяется к UI, Unbounded — к заголовкам Settings; в Classic используется системный шрифт и синий accent 0.2.3. Шрифты встроены как Resource; OFL notices копируются в output.

Classic/Ember (TASK-3): Classic воспроизводит поверхности 0.2.3 — синюю палитру и accent `#4255C5`, стандартное окно подтверждений 390 DIP, toolbar с текстовыми `● Marker/↗/▭/✎` и Done внутри панели, обычную карточку комментария, blue/white export-палитру и скруглённый toast. Ember сохраняет CONSTRUCT: 520 DIP NoticeSurface prompts, capture guide, split Done dock, MarkerSurface/CommentEditorCard, Signal/Paper export и угловатые toast. Выбор независим от System/Dark/Light; подтверждение и persistence описаны в Settings and hotkeys. TASK-2 pre-show placement применяется к обоим: Classic дополнительно измеряет non-client frame стандартного окна, поэтому первый видимый кадр сразу центрирован. Семантика capture/session/paste и TASK-1 palette Ember не изменены.

Ember Settings: те же три страницы, немедленное сохранение и существующий recorder/update lifecycle; preferred size 880×660 DIP, min 700×510. Overlay: компактная верхняя панель инструментов (brand скрывается на узких мониторах), отдельный верхний Done и нижний context dock; середина остаётся прозрачной. Bounds chrome используют per-HWND transform. Source, Core geometry, session и paste paths не меняются. Export-палитра Ember (Signal/Paper) задана в `Painter` явно и не следует за UI-темой; Classic сохраняет blue/white. Comment bodies по-прежнему не растеризуются. Preview build сохраняет обычные mutex/settings paths; он не предназначен для одновременного запуска с installed copy.

Capture selection chrome (2026-11-08): `CaptureSelectionBar` использует тот же compact CONSTRUCT surface, logo, vector icons и keycaps, что annotation toolbar. Текущий Region/Window mode выделен Signal, удерживаемый annotation modifier — Acid. Guide расположен сверху по центру, instruction/status — снизу слева. Единственная интерактивная кнопка guide показывает текущий Region/Window и делегирует клик существующему `ToggleWindowMode`, затем возвращает focus на surface. Остальные части guide пропускают input через HitTestCore; W/Space/modifier/Esc и surface drag handlers не изменены; при входе в annotation guide скрывается. Узкие мониторы переносят группы без обрезания клавиш.

Comment editor redesign (2026-11-08): `CommentEditorCard` отвечает за угловатую карточку, header/divider, input и key hints. `CommentEditorLink` рисует только UI-соединитель в DIPs от текущего `Geo.Badge` (с учётом SourceScale и actual HWND transforms) до ближней стороны clamped карточки 352×240 DIP. `AnnotationOverlay` сохраняет edit identity/body и логический Editing отдельно от короткого fade-out; Close немедленно освобождает editing/input, clocks снимаются при reopen/Closed. Enter/Shift+Enter/Esc, marker high-water и geometry/hit bounds Core не изменены. Painter меняет только вид badge на скошенный Ink/Paper/Signal; comment bodies, connector и card не входят в glyph projection/export. Это специально порученное изменение внешнего вида меток, а не общий retheme всех exported strokes.

Saved marker interaction: EndPointer различает короткий click (<3 physical px) и drag. Click после release открывает BeginEdit существующего GUID; drag сохраняет прежний Move path. Escape закрывает editor без сброса Selected, позволяя затем Delete. Double-click/E остаются рабочими путями редактирования. Проверки marker-editor и full suite покрывают оба gesture outcomes.

## Current comment composition (2026-11-08 refinement)

Интерактивные метки рисует `MarkerSurface` через `MarkerBadgeUi`, в overlay DIPs: bounds исходного Geo.Badge проецируются через SourceScale и per-HWND transform; высота ограничивается 22–28 DIP, ширина учитывает Commissioner Semibold 13 и длину ID. На слишком маленьком crop UI-face размещается рядом с ним; короткий leader указывает настоящий anchor. Surface больше не рисует UI-бейджи через Painter. `MarkerUiBounds`/`HitUiMarker` отвечают за UI hit testing; при drag raw pointer delta преобразуется в прежние physical coordinates, ограничивается только итоговый anchor. Это позволяет кликать по UI-бейджу вне crop, сохраняя источник, IDs и историю. Core и Painter/PNG остаются без изменений этой итерации.

`CommentEditorPlacement` выбирает доступную сторону возле UI-бейджа и размер до 380×246 DIP; на тесных viewports допускается узкая/более низкая карточка. `CommentEditorCard` имеет компактный Header (Unbounded ID 26), основной Input и отделённый keycap Footer. Custom TextBox template содержит PART_ContentHost; native editing/selection/Undo остаются у исходного TextBox. Neutral/focus brushes задаются ресурсами, системная синяя рамка не используется. Connection routed around badge; редактор/коннектор остаются UI-only. Existing finite animations, logical edit lifetime и immediate input semantics сохранены. Ранее описанные 352×240 DIP относятся к предыдущей итерации.

UI marker Hover/Selected/Dragging/Editing меняют только толщину контура (90/120 ms), не bounds. Открытие карточки: opacity и TranslateY 4→0, 140 ms; закрытие 100 ms. Все presentation clocks отключаются при закрытии и учитывают ClientAreaAnimation. Native TextBox сохраняет PART_ContentHost; padding применяется один раз, scrollbar получает локальный template.

CommentChrome owns the local floating-card material (stationary vector penumbra, matte surface, edge light and restrained reflection) and cut geometry. CommentInputFrame retains Border layout around the original PART_ContentHost while drawing the integrated input silhouette. No bitmap effects are applied to input text. High Contrast bypasses material layers. CommentEditorLink uses an axial stem/45-degree elbow and single tinted stroke; it never reaches Painter. These visual changes do not alter model or session ownership.

PromptWindow owns custom notice caption/content/action composition; ClearSessionDialog inserts its existing checkbox into BodyPanel. NoticeSurface/NoticeChrome provide presentation shared by prompts and ToastWindow, using the existing cut geometry/material helpers; no new state ownership. The custom caption close invokes the existing Complete(false) path. Owned-window lifetime, guard, opt-out persistence, foreground positioning, and modal/nonmodal behavior remain with their existing owners. ToastNative and ToastService timers/focus/input contracts are unchanged.

`PromptWindow.Prepare` keeps the owned HWND hidden, probes the selected monitor with a 1x1 `ToastNative.Place` so `GetDpiForWindow` reports that monitor's actual DPI, measures the same DIP content WPF will lay out and sets the final centered physical frame before `Show`; the first visible/rendered frame is therefore never the work-area origin. `ContentRendered` re-applies the same `PromptWindow.Centered` clamp/center math, which is a no-op unless the measured frame drifted, and still runs `BringForward` for focus. Foreground-monitor work-area choice, modal/nonmodal lifetime, safe defaults and passive bottom-anchored toasts are unchanged.
