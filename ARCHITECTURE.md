# Architecture

Фактическая production-архитектура `v0.1.1` (`98f74b5`), проверенная по исходникам 2026-10-05. Публичное использование — в [README](README.md), команды и проверки — в [DEVELOPMENT](DEVELOPMENT.md), правила изменений — в [AGENTS](AGENTS.md).

## System overview

ScreenIt — Windows tray utility для накопления снимков с аннотациями и передачи их в выбранное приложение. Основного постоянно открытого окна нет. Сессия и изображения находятся в RAM; сеть используется только по явным командам updater. Нет backend, account, telemetry, persistent history, window capture или пользовательского Save As.

```text
WM_HOTKEY / tray Capture / tray double-click
  → Coordinator.Capture
  → monitor enumeration + sequential BitBlt (worker thread)
  → frozen frames → per-monitor AnnotationOverlay
  → region drag / Space → crop + ScreenshotDraft
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
| `Preferences`/`HotkeyRegistration`, `Appearance`, `L` | Persistence и регистрации, effective palette и in-code EN/RU catalog. |
| `GithubUpdateSource`/`IUpdateSource`, `UpdateTransfer` | Release metadata, download validation/storage, installation detection и launcher helpers; UI orchestration находится в SettingsWindow. |
| `ScreenIt.Verification` | STA executable, использует `InternalsVisibleTo` из App; не runtime dependency. |
| `installer/`, `spikes/` | Inno/PowerShell delivery infrastructure и самостоятельные архивные harnesses соответственно. Production не ссылается на spikes. |

App зависит от Core; verification зависит от App. UI и Windows infrastructure внутри App связаны напрямую. Не следует описывать это как универсальную платформенную абстракцию или строгую многослойную service architecture.

Production entry point — `src/ScreenIt.App/Program.cs: Program.Main`; verification — `verification/ScreenIt.Verification/Program.cs: Verification.Main`. Три spike-проекта имеют собственные `Program.Main` и global-namespace classes: clipboard harness — console/WinForms, capture и annotation harnesses — WPF. Core — library без entry point. Inno `[Run]` запускает production executable, не альтернативный runtime.

## Runtime lifecycle

1. `[STAThread] Program.Main` создаёт именованный mutex `Local\ScreenIt.MVP`. Второй экземпляр сразу выходит; IPC, активации первого экземпляра и передачи ему аргументов нет. `Local` ограничивает coordination Windows session, а не все пользовательские sessions машины.
2. Загружаются Preferences, выбираются `L` language и Appearance. Создаётся WPF Application с `ShutdownMode.OnExplicitShutdown`.
3. Coordinator создаёт скрытое control Window/`HwndSource`, добавляет message hook, регистрирует hotkeys и WTS notifications, создаёт WinForms NotifyIcon/ContextMenuStrip. WPF владеет HWND/DPI initialization; WinForms используется для tray UI.
4. На startup запускается conservative cleanup clipboard PNG generations. Update check на startup отсутствует. В ожидании действий работает dispatcher/message loop.
5. Capture устанавливает `busy`, скрывает toast и Settings, закрывает tray menu, делает render yield и `DwmFlush`, снимает frames в `Task.Run`, проверяет topology и создаёт overlays. После всех `ContentRendered` (timeout 5 секунд) проверяет physical placement и фокусирует primary overlay.
6. Drag/Space переводит выбранный overlay в annotation mode; Coordinator назначает единственный `Active`. Остальные overlays продолжают показывать frozen monitors, но не редактируют draft.
7. Commit допускается для текущего Active, вне editing и suspension. Сначала render, затем `Session.Commit`, затем raster pair по GUID. Все overlays закрываются, отображается Added toast. Cancel закрывает временные окна без добавления снимка.
8. Paste/Copy используют committed snapshot. Clear блокируется при capture/paste, иначе открывает единственный owned modeless confirmation; только Accepted очищает Session и Rasters. Cancel/Escape/X/обычный Enter сохраняют сессию.
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

Выбор региона автоматически создаёт detached frozen crop и ScreenshotDraft в том же HWND; отдельного editor window нет. Default tool — Marker. Новый marker существует в model только после непустого comment commit; Escape editor не расходует номер. Номера high-water не переиспользуются после Delete/Undo. Arrow минимум 4 px; Box минимум 3×3 px. Undo/Redo — snapshots текущего draft, не всей session; committed draft sealed.

При Deactivated/LostMouseCapture откатывается незавершённый gesture, frozen frame и comment editor сохраняются. Escape сначала отменяет gesture/edit, затем capture; draft с сохранёнными annotations требует Discard confirmation. Overlay close/Alt+F4 направляется в Coordinator.Cancel через `ClosingByApp` guard.

Изменение topology, DPI, соответствующие power/WTS messages вызывают Suspend: окна скрываются, gestures прерываются, frame/model остаются в RAM. Возврат Capture требует cancel и нового capture; автоматического remap/resume нет. Физические system-transition scenarios не полностью проверены. Закрытие overlay снимает hook, Dispose HwndSource и очищает Content.

### Window selection

Window enumeration/hit-testing для screenshot target отсутствуют. `Geo.Hit` выбирает annotations, а не desktop windows: маркеры имеют приоритет, затем проверяются arrow segment/box edges. `GetWindowRect` используется для собственных HWND placement. `PasteInput` получает foreground root через `GetForegroundWindow`/`GetAncestor(GA_ROOT)` и PID, чтобы проверять receiver; не перечисляет/фильтрует screenshot windows и не измеряет их capture bounds.

## Clipboard and file output

Raster pair содержит original crop и annotated frozen bitmap; Core хранит metadata, не WPF pixels. Painter выводит badges, arrows и boxes; comment body передаётся только через CommentFormatter. Формат текста остаётся `Screenshot A` / `A1 — ...` даже при русском UI.

PNG materialization — технический clipboard transport, а не постоянное сохранение сессии. TemporaryImages создаёт GUID generation в `%TEMP%\ScreenIt\Clipboard-v1`, ownership marker/timestamp и `ScreenIt-A.png` и т. п. с `CreateNew`. Root/ancestors проверяются на reparse points. PNGs остаются после Paste, Clear, clipboard replacement и exit: receiver read completion неизвестен.

Следующий startup удаляет только generations старше семи суток с ожидаемым GUID, marker, timestamp, допустимыми файлами, без подкаталогов/reparse points. Locked/denied generations сохраняются. Это ownership/TTL policy, не надёжное определение завершения чтения. Write failures могут оставить partial generation.

Clipboard images — `CF_HDROP` (15), UTF-16 paths с double NUL; comments — `CF_UNICODETEXT` (13). Обе записи добавляют `CanUploadToCloudClipboard` и `CanIncludeInClipboardHistory` с DWORD 0. Эти hints не гарантируют privacy у любого clipboard manager/receiver. PNG/DIB/HTML competing representations production не публикует.

HGLOBAL buffers готовятся до Open/EmptyClipboard. После успешного SetClipboardData ownership передаётся Windows; непереданные handles освобождаются. Open retries: до 10 попыток с 20 ms интервалом. Publication заменяет clipboard; backup/restore прежнего содержимого нет. Ошибка после EmptyClipboard может оставить partial payload; транзакционного rollback нет.

### Paste sequencing

PreparePaste берёт ordered session snapshot, формирует comments при наличии непустых markers, пишет все PNGs в worker и проверяет readability/nonzero length до первой публикации. PasteSequencer допускает один run, отклоняет empty/own/invalid target и через Guard проверяет HWND+PID foreground на async boundaries. Probe только открывает/закрывает clipboard, не читает его.

Ожидание release modifiers, V и configured trigger: timeout 5 s, poll 20 ms, quiet interval 40 ms. Для каждого image — singleton HDROP, owner/sequence guard и SendInput Ctrl-down/V-down/V-up/Ctrl-up. Паузы между images и перед финальным text — 350 ms. Нет receiver acknowledgement; Completed означает отправленные запросы, а не подтверждённые attachments. Guard не различает два поля ввода внутри одного root HWND/PID.

Foreground change, reheld keys, cancellation и ошибки останавливают sequence. Partial SendInput пытается отпустить только synthetic V/Ctrl; paste не повторяется автоматически. Session не меняется, уже вставленное не отзывается; retry может создать дубликаты. UIPI может блокировать elevated receivers.

## Notifications and background UI

Coordinator инициирует ToastMessage после commit, clear, paste outcome; обычный status остаётся также в tray menu/tooltip. ToastService держит одно окно и DispatcherTimer: success 1400 ms, warning 3000 ms, replacement закрывает предыдущее окно. Capture скрывает feedback до acquisition.

ToastWindow — topmost, layered, toolwindow, noactivate, transparent hit-testing; не интерактивен, не перехватывает keyboard focus. Work area выбирается по capture monitor или paste receiver, DPI читается после размещения HWND. Нет очереди, звука, notification actions или Notification Center. Не-OOM failure показа suppress/debug-log; завершённое действие не откатывается.

## Settings and hotkeys

Preferences path: `%LOCALAPPDATA%\ScreenIt\settings.json`. Load принимает object до 64 KiB/depth 12; валидирует поля и поддерживает theme-only `0.1.0`. Defaults — System, ru при Russian CurrentUICulture, иначе en, `Ctrl+Alt+S/V/X`. Invalid JSON/read error обычно ведут к defaults; invalid fields могут оставлять остальные уже прочитанные значения. Отдельной version-gated migration системы нет: schemaVersion записывается, но не управляет Load.

Save клонирует исходный JSON, сохраняет неизвестные поля, пишет schemaVersion 2, theme/language/hotkeys во временный соседний GUID файл и заменяет settings через File.Move(overwrite). Нет backup или fsync contract. Oversized/malformed файл не сохраняется как original; последующая запись может заменить его defaults. Theme/language применяются после успешного Save; Coordinator откатывает in-memory выбор при IO/permission failure.

HotkeyRegistration использует RegisterHotKey + MOD_NOREPEAT, dispatch через control WM_HOTKEY. Startup при конфликте configured key пробует default, предупреждает; даже недоступный default оставляет tray usable. Startup fallback меняет in-memory keys, не сохраняет их автоматически.

Replace проверяет полный уникальный valid набор, staged registration/reuse, затем persistence callback; при failure освобождает новые registrations, оставляя старые. Только после успеха снимает obsolete registrations. Hotkey IDs после replacement могут меняться: нельзя считать action равным wp ID. Dispose снимает оставшиеся registrations. Это не low-level keyboard hook; HWND message hooks и mouse capture имеют другой lifecycle.

L использует английские фразы как keys, EN/RU dictionary и parameter patterns; refresh tree не должен переводить user TextBox content. Appearance выбирает palette и читает HKCU AppsUseLightTheme для System, реагирует на WM_SETTINGCHANGE/WM_THEMECHANGED. Autostart setting, Run registry key и startup task отсутствуют.

## Updater and installer

Settings About по явному Check вызывает GitHub `/releases/latest` через HttpClient (5-minute timeout, User-Agent и API header). Metadata ≤1 MiB. Принимается только stable `v?major.minor.patch`, newer assembly version; draft/prerelease/equal/older не предлагаются. Требуются ровно один versioned installer и `SHA256SUMS.txt` и строгие GitHub page/asset paths.

Download разрешает HTTPS/default port без userinfo, заданный repository release path, redirects только в GitHub releases или `release-assets.githubusercontent.com` (до четырёх переходов). Checksums ≤64 KiB, installer ≤256 MiB. GUID generation в `%TEMP%\ScreenIt\Updates` получает ACL текущего пользователя, marker и reparse checks ancestors. SHA256SUMS должен иметь единственную корректную строку exact filename; hash сравнивается с installer bytes. Failed generation удаляется по ожидаемым файлам; cleanup itself может ошибиться. Успешные/cancelled generations автоматически по TTL не очищаются.

Installed определяется по 64-bit HKCU uninstall entry с AppId, DisplayName, InstallLocation == AppContext.BaseDirectory и наличию `unins000.exe`. Portable открывает validated release page. Installed после download повторяет identity/hash check, затем спрашивает подтверждение RAM loss/unsigned setup. Только успешный Process.Start setup приводит к `ExitForUpdate`; failure/cancel сохраняет приложение. SHA-256 с того же release не является code signature или независимым trust root.

Inno AppId `{75AF53B9-2BC6-4AC3-A7D4-859884B5EAF0}` и mutex `Local\ScreenIt.MVP` связывают runtime, updater detection и install/upgrade. Setup ставит под `%LOCALAPPDATA%\Programs\ScreenIt`, `PrivilegesRequired=lowest`, Windows build ≥22000/x64, Start Menu и uninstall entry. `CloseApplications=no`, `RestartApplications=no`: setup не убивает и не перезапускает старую копию; AppMutex блокирует replacement/uninstall работающей программы. App запускает setup и затем выходит; результат установки после выхода не отслеживается. Optional final-page launch unchecked/skip-if-silent. Settings и clipboard temp вне install directory не удаляются. Automated rollback, downgrade migration и RAM restore отсутствуют.

## Windows integration

| Boundary | Windows facilities |
|---|---|
| Capture — `Native.cs` | `user32` monitor enumeration/GetDC/GetWindowRect/SetWindowPos, `gdi32` DC/DIB/BitBlt/object cleanup, `shcore` monitor DPI, `dwmapi` DwmFlush. |
| HWND/UI — `Program`, `Coordinator`, overlays | WPF Application/Dispatcher/HwndSource hooks, PerMonitorV2 manifest, WinForms NotifyIcon/menu; WM_HOTKEY, WM_CLOSE, display/settings/theme/DPI/power/WTS messages. |
| Session/power — `Coordinator` | `wtsapi32` session notification registration; lock/unlock и power transitions suspend capture, без secure-desktop acquisition. |
| Clipboard — `ClipboardTransport.cs` | `user32` clipboard formats/open/empty/set/close; `kernel32` HGLOBAL allocation/locking/free. Shell CF_HDROP file-list layout; actual image data хранится в PNG files. |
| Receiver/input — `PasteInput.cs` | `user32` foreground/root/PID/window/key-state, SendInput, clipboard owner/sequence guards; без receiver activation и low-level hooks. |
| Toasts/prompts/settings | `user32` monitor work area и window styles/hit-test/noactivate/placement, prompt foreground; `dwmapi` title-bar theme attribute. |
| Preferences/theme/install identity | LocalApplicationData, HKCU Windows theme registry и 64-bit HKCU Inno uninstall entry; нет autostart registration. |
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
- Нет window capture, cross-monitor region, persistent Save As, startup integration, cross-platform backend и receiver acknowledgement.
- Нет awaited capture/preparation shutdown и универсального transaction rollback для clipboard, session/raster commit или settings.
- README сообщает manual PASS для ChatGPT Web sequential paste и текущей 100%/125% mixed-DPI topology с negative origin. Физические 150%/200%, portrait, HDR/protected content и system transitions полностью не подтверждены.
- Архивный [clipboard RESULTS](spikes/clipboard-transfer/RESULTS.md) в заключительном production note ещё помечает Web acceptance OPEN/NOT TESTED, тогда как текущий README сообщает PASS. Это расхождение evidence chronology; точные receiver versions и обновлённая tracked acceptance matrix отсутствуют. Не расширять совместимость на все приложения.
- [installer RELEASE-NOTES](installer/RELEASE-NOTES.md) описывает только `0.1.0`, не текущую версию. Локальные ignored artifacts могут содержать дополнительные прошлые отчёты, но не составляют воспроизводимую tracked test history.
- В manifest assemblyIdentity ещё `0.1.0.0`; App csproj/updater version — `0.1.1.0`/`0.1.1`. Это разные поля; release version берётся из assembly, не manifest.
- Installer/app unsigned; checksum и ACL не заменяют publisher signature. CI/CD workflows в tracked repository отсутствуют.
