# Development

Практический workflow текущего ScreenIt `0.2.0`. Начните с [README](README.md) и [AGENTS](AGENTS.md); runtime flow и ограничения описаны в [ARCHITECTURE](ARCHITECTURE.md), история — в [CHANGELOG](CHANGELOG.md).

## Environment

- Поддерживаемый продукт: Windows 11 x64. Installer требует Windows build ≥22000. Windows 10 не заявлен; интерактивные проверки требуют обычной Windows desktop session с доступными мониторами.
- .NET 10 SDK: `global.json` задаёт `10.0.100`, `rollForward: latestFeature`, prerelease запрещён. Используйте совместимый stable .NET 10 SDK, проверяя `dotnet --info` и `dotnet --version` из корня.
- App и Verification: `net10.0-windows`, WPF + WinForms, `PlatformTarget=x64`. Core: `net10.0`. Все три проекта включают warnings-as-errors. Обозначения solution x86 не означают поддержку x86 приложения.
- Для framework-dependent build/run нужен .NET 10 Windows Desktop runtime x64. SDK включает developer runtime; installer/portable self-contained и не требуют runtime на машине получателя.
- PowerShell scripts используются для smoke и packaging. PowerShell 7 — практический вариант для этих команд; minimum host version явно не закреплена. `Verify-Installer.ps1`/`Verify-Upgrade.ps1` используют .NET `Path.GetRelativePath`; не считать Windows PowerShell 5.1 проверенным host. Pointer smoke содержит Add-Type references к современным System.Drawing assemblies и тоже требует проверки в выбранном host.
- Inno Setup **7.1.0**, `ISCC.exe` — только build-time dependency installer. Передавайте полный путь, если нет в PATH.
- Нет third-party runtime PackageReference. Root `NuGet.Config` очищает package sources; обычный build использует установленные SDK targeting packs. Self-contained publish явно включает nuget.org для Microsoft runtime packs.
- Publish profile закрепляет .NET Core/Windows Desktop runtime **10.0.12**. Он должен быть доступен из restore/cache, независимо от developer runtime.
- Git нужен для workflow; GitHub CLI не нужен build/test/package. CLI или GitHub UI можно использовать для публикации только после прямого разрешения владельца; checked-in автоматизации release нет.
- Python 3 используется только необязательным `assets/generate_icon.py` (standard library): пересоздаёт tracked SVG/ICO и ignored preview. Не запускать ради обычного build.

## Repository structure

| Путь | Назначение |
|---|---|
| `ScreenIt.sln` | Core, App, Verification; Debug/Release configs. |
| `src/ScreenIt.Core/` | Geometry, selection, annotations/draft history, session/comment formatter. |
| `src/ScreenIt.App/` | Production Windows utility; manifest, embedded icon, publish profile. |
| `verification/ScreenIt.Verification/` | STA verification executable и subsystem checks; не test-framework проект. |
| `verification/*.ps1` | Запуск production process и GUI smoke. |
| `installer/` | Release builder, Inno script, installer/upgrade verification и исторические release notes. |
| `spikes/clipboard-transfer/` | Archived clipboard format/receiver experiments. |
| `spikes/capture-overlay/` | Archived capture/DPI/selection experiment. |
| `spikes/annotation-interaction/` | Archived annotation interaction experiment. |
| `assets/` | SVG/ICO и генератор icon. |
| `artifacts/` | Ignored local output/evidence. Не источник текущего кода и не tracked release manifest. |

Spikes — отдельные `.csproj`/entry points, не входят в solution и production dependency graph. Их controls/результаты не следует переносить на production без проверки; подробности в их README/RESULTS. Нет tracked `.github` CI/CD workflow, conventional unit-test runner или release orchestrator.

## Restore, build and run

Все команды ниже выполняются из корня в PowerShell. Сначала проверьте рабочее дерево; перед запуском App/GUI tests завершите собственный ScreenIt через tray, предварительно передав нужную RAM-сессию. Не убивайте процесс владельца автоматически.

```powershell
git status --short
dotnet --info
dotnet restore ScreenIt.sln
dotnet build ScreenIt.sln -c Release --no-restore
dotnet run --project src/ScreenIt.App/ScreenIt.App.csproj -c Release --no-build
```

Эквивалентная сборка с restore: `dotnet build ScreenIt.sln -c Release`. Для Debug замените configuration последовательно. Run запускает tray utility, а не editor startup window. Повторный экземпляр выходит без активации первого. Build не создаёт installer или self-contained ZIP.

Если root NuGet sources не позволяют получить отсутствующий Microsoft targeting pack, проверьте установку SDK. В release builder отдельный явный restore source уже задан; не добавляйте произвольные feeds/dependencies ради обхода ошибки окружения.

## Automated verification

```powershell
.\verification\ScreenIt.Verification\bin\Release\net10.0-windows\ScreenIt.Verification.exe
.\verification\Smoke.ps1
.\verification\Smoke.ps1 -CustomBindings -ReportName custom-hotkeys-smoke.json
.\verification\SettingsHoverSmoke.ps1 -ExecutablePath .\src\ScreenIt.App\bin\Release\net10.0-windows\ScreenIt.App.exe
```

`dotnet test` не запускает существующие checks: нет Microsoft.NET.Test.Sdk/xUnit/NUnit/MSTest suite. Verification — executable с собственными assertions, JSON report и exit code 0/1. Нужны actual desktop, свободные hotkeys и **как минимум два монитора**: Program без fallback берёт companion overlay через `First(w => w != selectedWindow)`. Одномониторный запуск текущего полного suite может упасть, хотя продукт способен работать на одном мониторе. В suite также используются крупные fixed crops; фактические размеры мониторов должны их вмещать.

Покрытие:

- Core letters/marker IDs, snapshots Undo/Redo, immutability, Unicode multiline formatter, crop/DPI math, marker geometry и отсутствие comment body в bitmap.
- Реальные capture/placement на доступных мониторах, same-HWND selection → annotation, cancellation и stress cycles; native counters, software latency и resource checkpoints.
- Actual clipboard payload bytes/readback, privacy hints, owned temporary lifetime/TTL; fake delivery sequence, focus/modifier/cancellation/error branches и native boundary guards. Suite заменяет clipboard синтетическими данными и не восстанавливает прежнее содержимое.
- Toast replacement/expiry/noactivate placement/stress; Clear confirmation routes, guards и safe default; theme palette, shortcut discoverability.
- Preferences/theme-only migration/unknown fields, fake и actual hotkey registration/conflicts, Settings cycles/live theme/language/controls. Updater metadata/hash/launch-order проверяются с fake providers/synthetic downloads без исполнения setup.

`artifacts/verification.json` — default отчёт; создаются PNG previews. Результат относится только к текущему environment/run. Прошлые ignored reports не заменяют повторную проверку.

Для безопасной проверки toast layout/localization при работающей пользовательской копии:

```powershell
.\verification\ScreenIt.Verification\bin\Release\net10.0-windows\ScreenIt.Verification.exe --toast-static
```

Этот узкий режим измеряет и рендерит WPF content offscreen, проверяет EN/RU, Dark/Light, wrapping и bottom-center math; не показывает HWND, не создаёт Coordinator, не захватывает desktop, не меняет clipboard/preferences и не регистрирует hotkeys. Отдельные outputs — `artifacts/toast-static-verification.json` и `artifacts/toast-offscreen-preview.png`. Это не PASS native placement/focus/animation lifecycle: соответствующие проверки остаются в полном suite и production smoke с обычными preconditions.

`Smoke.ps1` проверяет production startup/background HWND, занятость hotkeys, single instance, empty paste/clear без clipboard mutation, WM_HOTKEY route, full-monitor commit, реальные Clear buttons и clean shutdown/released keys. Он посылает Win32 messages и использует UIAutomation, поэтому не подтверждает physical key delivery во всех environments и не автоматизирует third-party receiver.

Smoke временно записывает deterministic EN/dark/default или Ctrl+Shift+Q/W/E configuration, затем восстанавливает исходные settings bytes в finally; при failure может Kill только запущенный им process. Это тестовый cleanup, не product shutdown policy. Mandatory Settings visual gate: `ScreenIt.Verification.exe --settings-visual-only` (также входит в full suite). Он создаёт скрытый production Settings UI, использует реальные templates/resources и проверяет RU/EN, Light/Dark, layout и отрисованные normal/hover/pressed states; read-only WPF input state задаётся только verification reflection, без tray/foreground/cursor. Отчёт — `artifacts/settings-visual-verification.json`. Interactive SettingsHoverSmoke отдельно меняет user settings, двигает cursor и проверяет pixel differences normal/hover/pressed. Outcomes: PASS, FAIL, INCONCLUSIVE (external desktop input/foreground interference); последний не доказывает regression и не закрывает deterministic gate. Повтор допустим только для явно INCONCLUSIVE, FAIL требует расследования. Наблюдатель пропускает весь input, не использует BlockInput; собственные mouse events помечены, injected reserved VK 0xB9 учитывается отдельно только во время own tray transition. `-InjectInterferenceForVerification` посылает один немаркированный mouse move для проверки INCONCLUSIVE. Default executable — локальный `artifacts/settings-polish/publish`; путь задавать явно. PNG, crops, pointer/key/foreground evidence и report — `artifacts/settings-layout/`, либо `-EvidenceDirectory <path>`.

Только при отдельной необходимости и с пониманием нестабильности внешнего состояния:

```powershell
.\verification\ScreenIt.Verification\bin\Release\net10.0-windows\ScreenIt.Verification.exe --check-updates
```

Этот flag делает live GitHub request и **ожидает отсутствие версии новее 0.2.0**. После следующего релиза такой assertion может упасть при корректном updater. Default suite сеть updater не использует.

Не покрыты автоматикой: принятие attachments/text реальным receiver, rollback данных получателя, SmartScreen/обычный setup UX, все physical DPI/HDR/protected-content configs, все lock/sleep/disconnect transitions, OS crash/termination и безопасное завершение всех async races. Manual acceptance обязательна для затронутого flow.

## Manual smoke testing

### Window capture (0.2.0)

Release App: обычный Capture → W → hover/click. W возвращает Region; Space всегда full focused monitor, configured modifier при click/Space открывает Annotation. Window pixels берутся при click через WGC, Region/monitor остаются frozen. Вручную проверить Explorer/browser/IDE, частично перекрытое окно (click по exposed части), Settings PID exclusion, maximized/borderless/accelerated surfaces, current mixed DPI/negative origin и cross-monitor target, A/B/C → Paste. Не обещать protected/HDR/exclusive-fullscreen compatibility без evidence; WGC-defined chrome/rounded corners и системный capture indicator зависят от Windows.

```powershell
.\verification\ScreenIt.Verification\bin\Release\net10.0-windows\ScreenIt.Verification.exe --window-only
```

WindowCaptureChecks входят также в полный suite. Native fixture — отдельный verification process с WPF target/foreground occluder; no production settings/network/installer. Проверки реально используют CreateForWindow/D3D readback, pixel comparison, 20 capture cycles/handle bounds, full Session integration, own PID filtering, Region/Window keyboard lifecycle, Space, actual monitor DPI/origin и cross-monitor source. Fixture завершается через WM_CLOSE; аварийный cleanup Kill ограничен собственным fixture process. Outputs — ignored `artifacts/window-verification.json` и `artifacts/window-capture/*.png`, в том числе RU/EN Light/Dark hints. Native ABI требует x64 (existing platform); build использует AllowUnsafeBlocks только App, без новых packages или version/release изменений. Standard suite/smoke preconditions сохраняются.

При отсутствии overlays assertion сохраняет expanded before/after evidence в `artifacts/window-capture/overlay-transition.json` и verification report: guards, accepted capture generation, исходный exception, owned windows, monitor/draft/session state. Collection/reflection/serialization выполняются только Verification; production держит пассивный счётчик и последнее capture exception. Один исторический full-suite отказ создания overlays не воспроизведён повторными Window/full прогонами; его причина не установлена и не считается исправленной.

### Updater UI review (0.2.0)

Safe deterministic harness, separate verification executable:

```powershell
.\verification\ScreenIt.Verification\bin\Release\net10.0-windows\ScreenIt.Verification.exe --update-ui-test
```

Harness не создаёт Coordinator/mutex/hotkeys, не читает/сохраняет user preferences, не использует сеть/update temp folders, не запускает installer и не закрывает пользовательский ScreenIt. Это developer path только в Verification, без production CLI/settings option. Окно показывает настоящий UpdateSection и mock IUpdateFlow states. Previous/Next дают Available, 0/25/50/75/100%, unknown-total, Verifying, PreparingInstall, LaunchingInstaller, три stage failures и InstallerLaunched. RU/EN и Light/Dark переключаются только в test process. Retry сбрасывает state к 0%; Next проходит successful handoff, без реальных system actions. InstallerLaunched не означает завершение установки.

```powershell
.\verification\ScreenIt.Verification\bin\Release\net10.0-windows\ScreenIt.Verification.exe --updates-only
.\verification\ScreenIt.Verification\bin\Release\net10.0-windows\ScreenIt.Verification.exe --update-ui-test --snapshot
```

Первый режим выполняет updater-specific checks с fake sources/launch callbacks, настоящими private temp generations и WPF lifecycle; не запускает installer/сеть, не меняет clipboard/settings, но создаёт verification Coordinator для Settings close/hide checks. Второй показывает harness, сохраняет PNG и закрывает только test window для build/run smoke. Outputs: ignored `artifacts/updater-verification.json` и `artifacts/updater-ui/*.png`. Полный suite также включает UpdateChecks. Проверки: bytes/percent/unknown length/truncation, phase failures и Retry, double click/cancellation/partial cleanup, confirmation decline и successful handoff, shared renderer RU/EN × Light/Dark, wrapping, reduced motion, Settings hide/show/close. Full suite сохраняет общие preconditions выше.

Ручная приёмка: пройти все states в четырёх locale/theme вариантах, проверить unknown-total/static reduced-motion state, long errors и Retry. В подготовленной installed test environment дополнительно проверить реальный download/progress и RAM-loss confirmation; не принимать harness как evidence GitHub/installer integration. При закрытии Settings download отменяется; при Hide/show state сохраняется. Actual installation progress API отсутствует, package/version/release менять ради этого review не нужно.

### Minimum для большинства runtime/UI изменений

1. Запустить одну копию: tray доступен, startup не открывает capture/settings и не делает update check. Второй запуск не создаёт второй tray process.
2. Tray Capture/double-click и настоящий configured capture hotkey открывают frozen overlays; повторный capture возвращает текущий draft.
3. Drag/drop без modifier → immediate session commit; Space → quick focused monitor. Ctrl (либо настроенный Shift/Alt) при drop/Space → annotation в том же HWND. Проверить нажатие во время drag и отпускание до drop; modifier из capture hotkey также учитывается. Проверить marker click/type/Enter, move/edit/delete, arrow/rectangle, Undo/Redo. Comment body остаётся text, не рисуется на image.
4. Escape gesture/editor не теряет остальной draft; Discard annotated screenshot спрашивает подтверждение. Commit закрывает все overlays, A/B numbering и Added toast соответствуют сессии.
5. В известном receiver с тестовыми данными вставить A/B + multiline RU/EN comments настоящим hotkey: release keys, image order, отдельные attachments и editable text. Смена foreground останавливает оставшиеся requests; retry может дублировать уже отправленные.
6. Clear Cancel/Esc/X и default Enter сохраняют session; Confirm удаляет session/rasters и следующий capture начинает A/A1. Settings close возвращает background utility. Exit с session предупреждает; после confirmed exit нет tray/overlays, shortcuts свободны.

Для docs-only change GUI smoke не обязателен, если команды/контракты проверены по источникам и не утверждается новый PASS.

### Capture UX / reserved shortcut acceptance

Полный suite включает CaptureUxChecks: filtering/repeat/injected cases, installation/save failure rollback, native install/rebind/dispose cycles, старые settings и restart persistence, все Ctrl/Shift/Alt drop/Space варианты, RGB comparison обоих output paths с frozen crop. Existing annotation stress намеренно удерживает modifier. Эти checks не доказывают физическое подавление Snipping Tool; синтетический SendInput помечен injected и намеренно пропускается hook.

HotkeyPolicyChecks проверяет action-specific backend selection, ordinary registration success/failure, отсутствие hook fallback, chord/backend-aware reuse и rollback при переносе Win+Shift+S между actions, error classification и сохранение persisted bytes при отказе Settings. Controlled native conflict проверяет сохранение Capture hook при неудачном rebind. Inline conflict previews EN/RU × Dark/Light — `artifacts/settings-conflict-*.png`; конкретный global chord должен быть виден без предположения о владельце. Для ordinary OS shortcuts acceptance определяется результатом RegisterHotKey, а не гарантией override всех Windows shell paths.

ShortcutRecordingChecks покрывает left/right Win с Shift/Ctrl/Alt, repeats, seeded modifiers, оба порядка release, injected filtering, Esc и Win-only gesture. Native WH_KEYBOARD_LL + SendInput integration использует verification-only opt-in для injected input и проверяет полный chord в RegisterHotKey seam, failure/error UI, persistence, смену recording field, Reset defaults, hide/deactivation/close/shutdown с удерживаемыми keys и native thread disposal cycles.

После закрытия пользовательской копии на подготовленном desktop проверить руками:

- Записать Win+Shift+X для Paste/Clear, Win+Shift+V и Win+Shift+S для каждого action; проверить полный chord, прежний binding при отказе, Esc, focus loss, close и Win-only release без зависшего Start/menu. Runtime Capture=Win+Shift+S должен по-прежнему подавлять Snipping Tool после закрытия Settings.
- При обычном Capture оба shortcut доступны: ScreenIt shortcut открывает ScreenIt, Win+Shift+S — Snipping Tool. Отменить каждый capture.
- Settings → Capture record → физический Win+Shift+S: распознаётся, сохраняется и отображается. Закрыть Settings, нажать shortcut: только ScreenIt, без Snipping Tool и двойного capture/repeats. Проверить обе стороны Win/Shift и отсутствие Start menu после release.
- Перезапустить тестовую копию: binding/modifier сохраняются. Изменить Capture обратно: сразу работает Snipping Tool. Назначить повторно и выйти: штатный shortcut восстановлен. Windows configuration не меняется.
- Для Ctrl/Shift/Alt: press/release во время selection переключает только modifier hint и мягкое внешнее свечение, без внутренней заливки/геометрических скачков; проверить Dark/Light, обе DPI, края монитора и Windows animation-disabled state. Drop/Space используют текущее физическое состояние.
- Quick A → annotated B → Quick C, настоящий Paste Session в disposable receiver: порядок/текст/session сохраняются. На output нет рамки, glow, hint, toolbar/editor. Проверить Esc и cancel draft без расходования letter.
- Shutdown с installed reserved hook, cancel recording, переходы Settings, capture cancellation и sleep/lock return: нет оставшегося hook/зависшего thread. Поведение сторонних keyboard hooks/secure desktop и восстановление после Windows LowLevelHooksTimeout требуют target-machine acceptance; Windows может молча снять сторонний hook при OS scheduling stalls.

Не считать выполненный message-based production smoke доказательством physical shortcut acceptance. Отчёты находятся в ignored artifacts; не изменять release packages/version для этих проверок.

### Дополнительно по подсистеме

| Изменение | Проверки |
|---|---|
| Capture/coordinates/interop | Все доступные monitors, mixed DPI, negative origin, края crop/marker/editor, no overlay chrome in output; ресурсные cycles. 150/200%, portrait/HDR помечать непроверенными, если hardware недоступен. |
| Overlay/lifecycle | Interrupted mouse capture/focus, editing/commit guards, suspended capture после topology/DPI/lock/sleep events; exit во время acquisition/preparation в отдельной тестовой копии. |
| Clipboard/paste/files | Busy clipboard, замена clipboard между publish/input, held keys, focus/PID change, receiver loss, textless session, partial input; файлы доступны после Clear/exit и чужие generations не удаляются. Clipboard overwrite ожидаем, restore не обещан. |
| Toasts | Capture скрывает toast до frame, нет focus stealing/click interception, expiry/replacement, warning/partial count, обе темы и monitor work area. |
| Preferences/hotkeys/UI | Restart persistence, theme-only baseline, malformed/oversized settings, unknown fields, save denial, conflict/duplicate/default reset rollback, EN/RU и System theme changes; user text не переводится. |
| Installer/updater | Installed/portable detection, equal/older/prerelease exclusion, unavailable/malformed release, invalid/missing/duplicate SHA, Cancel/launch failure, RAM-loss warning и running-copy mutex. Использовать disposable Windows account/VM для реальной install проверки. |

Production Smoke.ps1 не выбирает HWND: Window capture проверяется отдельными native WindowCaptureChecks/--window-only и manual acceptance. Save As не реализован. Startup проверяется как background startup; autostart не реализован.

## Packaging

Checked-in builder — [installer/Build-Release.ps1](installer/Build-Release.ps1), профиль — [ReleaseWinX64.pubxml](src/ScreenIt.App/Properties/PublishProfiles/ReleaseWinX64.pubxml), installer — [ScreenIt.iss](installer/ScreenIt.iss). Дополнительные сведения — [installer README](installer/README.md).

```powershell
.\installer\Build-Release.ps1 -Dotnet dotnet -Iscc 'C:\path\to\Inno Setup 7\ISCC.exe'
```

Замените пример на существующий compiler path. Builder отказывается работать при существующем `artifacts/release`; после проверки ownership сохраните прежний output отдельно, не удаляйте его вслепую.

Builder выполняет `dotnet publish src/ScreenIt.App/ScreenIt.App.csproj -p:PublishProfile=ReleaseWinX64` с явным nuget.org restore source и output `artifacts/release/publish`. Release, win-x64, self-contained runtime 10.0.12, без single-file/trimming/PDB; затем копирует MIT LICENSE и runtime notices из `$env:NUGET_PACKAGES` либо `%USERPROFILE%\.nuget\packages`, запускает ISCC, создаёт ZIP и SHA256SUMS.

| Artifact | Содержимое |
|---|---|
| `artifacts/release/publish/` | App EXE/DLL, Core DLL, deps/runtimeconfig, .NET/Windows Desktop native/managed runtime files, LICENSE и `licenses/`. ICO embedded в App; отдельный assets directory не требуется. |
| `ScreenIt-Setup-0.2.0.exe` | Per-user Inno installer всего publish tree. |
| `ScreenIt-0.2.0-win-x64-portable.zip` | Всё содержимое publish без дополнительного enclosing directory. |
| `SHA256SUMS.txt` | SHA-256 installer и ZIP с exact versioned names, lowercase hash. |

Не ship source, spikes, verification, screenshots/evidence, settings или PDB. Inno включает всё из publish recursively: чистота publish directory обязательна. Byte-for-byte reproducibility архивов/metadata не гарантируется.

После сборки на подготовленном desktop:

```powershell
.\verification\Smoke.ps1 -ExecutablePath .\artifacts\release\publish\ScreenIt.App.exe -ReportName publish-smoke.json
.\installer\Verify-Installer.ps1
```

Вторая команда **реально устанавливает и удаляет** программу; допускает только отсутствующую installation directory, HKCU entry и Start Menu shortcut. Сравнивает каждый publish file hash, version/path/shortcut, выполняет installed smoke, затем uninstall и проверяет preferences byte-for-byte. Вывод — `artifacts/installer-verification.json`, installed smoke и install/uninstall logs. Скрипт не проверяет third-party receiver или обычный installer UI.

## Upgrade testing

Сохранять AppId, mutex, per-user path и settings location. Установка/удаление не должны трогать `%LOCALAPPDATA%\ScreenIt` и clipboard temp. Installer AppMutex блокирует работающую копию; автоматического Kill/restart нет.

Для **закрытой установленной 0.1.2** и сохранённого baseline:

```powershell
.\installer\Verify-Upgrade.ps1 -BaselineDirectory 'C:\path\to\retained-0.1.2-baseline'
```

Baseline должен содержать `publish/`, `ScreenIt-Setup-0.1.2.exe` и SHA256SUMS; новая `0.2.0` — в `artifacts/release/`. Default baseline path — `artifacts/release-0.1.2-preserved`. Скрипт проверяет три ключевых installed binary hashes, reapplies baseline installer, затем upgrade installer с проверкой checksums, identity/path/version, preferences hashes, единственного uninstall entry, publish files и upgraded smoke. **Оставляет 0.2.0 установленной**, не удаляет existing installation. Проверка baseline ограничена указанными binaries, не всеми файлами old installation.

Дополнительная ручная проверка в disposable environment:

- Записать old theme preferences; upgrade, первый запуск, сохранение нового language/hotkeys и повторный запуск. Installer preservation byte-for-byte и application migration после Save — разные проверки.
- Setup/uninstall при запущенной baseline: отказ/предложение закрыть приложение, без автоматического уничтожения RAM session. После ручного выхода повторить.
- Uninstall/reinstall сохраняют preferences; cancel setup не меняет working version. Portable не устанавливает поверх себя через updater.
- Download/verification/launch failure сохраняют app/session; Confirm update теряет RAM session только после запуска setup. Завершение setup и повторный запуск проверять руками.

Нет автоматического rollback или сохранения RAM session; explicit previous-version installer не является проверенной downgrade policy. Формат v2 более объёмный, старый loader `0.1.0` принимает файл только до 256 bytes: полный v2 файл может привести к fallback старой темы после downgrade. Не обещать обратную миграцию; сохранять baseline/settings copies в тестовой среде.

## Release workflow (только подготовка, без публикации)

Tracked автоматического CI/CD/release job нет. Ниже checklist из существующих tools и необходимых точек согласования; это не доказательство прошлых удалённых release operations.

1. Проверить `git status`, review scope и [AGENTS](AGENTS.md). `main` — стабильная интеграционная ветка; не выпускать unrelated/непроверенные изменения.
2. Согласовать version; обновить App csproj `Version`, `AssemblyVersion`, `FileVersion`, `InformationalVersion`, Inno AppVersion, builder ZIP/checksum names, Verify-Installer version/name и Verify-Upgrade version pair. Проверить Updates User-Agent, version-bound SettingsChecks/live assertion, README/README.ru и installer documentation. Manifest identity сейчас отдельно `0.1.0.0`: не использовать её как источник release version и не менять вслепую. Единого central version файла нет.
3. Обновить CHANGELOG по результату, docs по затронутым контрактам; перенести опубликованные пункты из Unreleased в согласованную версию/дату. Обновить `installer/RELEASE-NOTES.md`, сохраняя предыдущие notes как историю.
4. Restore/build, verification и relevant manual smoke; проверить git diff/status. Закрыть тестовую копию безопасно.
5. Сохранить previous artifacts, собрать package, проверить содержимое/notice files/SHA и portable startup; выполнить publish/installer/upgrade checks в подходящей тестовой среде.
6. Передать владельцу version, notes, diff, результаты и ограничения. Commit, push, merge, annotated tag `vX.Y.Z`, GitHub Release и upload — только при прямом разрешении.
7. При отдельной авторизованной публикации exact stable tag и asset names должны соответствовать updater URL parser. Release должен содержать versioned installer, portable ZIP и SHA256SUMS. После upload сравнить hashes/download и install behavior; факт remote публикации требует отдельной проверки.

## Git workflow

- Всегда начать с `git status`; dirty tree исследовать по diff/происхождению, чужие изменения сохранить.
- Scoped reviewable changes; минимальный fix без unrelated refactoring, случайных files/settings/release artifacts в commit.
- `main` — стабильная integration branch. Для новой рабочей ветки default prefix `codex/`, если владелец не задал иначе.
- Без destructive reset/clean/checkout, удаления чужой работы, force-push или переписывания опубликованной истории без прямого указания.
- Без push, tag, GitHub Release, публикации installer/package и изменения существующих release artifacts без прямой команды владельца.
- Handoff rules для installer/updater/data/security changes — в [AGENTS](AGENTS.md). Завершить проверкой `git diff --check`, `git diff` и `git status --short`; untracked документы тоже необходимо прочитать, они не отображаются обычным `git diff`.
