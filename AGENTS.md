# ScreenIt

ScreenIt — локальная Windows 11 x64 tray utility для быстрого capture → annotation → multi-screenshot session → paste в выбранный receiver. Пользователь работает поверх frozen desktop, без отдельного image editor; badges связываются с настоящим текстом комментариев. Сессия живёт в RAM. Приложение должно оставаться быстрым, ненавязчивым и пригодным для постоянной фоновой работы.

## Mandatory onboarding

Перед изменением кода прочитать в этом порядке:

1. [README.md](README.md) — продукт, shortcuts, privacy и compatibility claims; [README.ru.md](README.ru.md) — русское публичное описание.
2. [ARCHITECTURE.md](ARCHITECTURE.md) — runtime, component ownership, invariants и limitations.
3. [DEVELOPMENT.md](DEVELOPMENT.md) — tools, build/verification, smoke, packaging/upgrade/release.
4. [CHANGELOG.md](CHANGELOG.md) — подтверждённая эволюция versions.

Затем прочитать implementation и все call sites затронутого flow, interfaces/services, verification checks и relevant scripts. Название класса не доказывает behavior.

> Documentation is context, code is the current implementation. If they disagree, investigate before changing either.

Spikes — архив исследований, не текущая implementation и не указание менять product. Их historical PASS/NOT TESTED не расширяет current compatibility. Не выдавать прошлый ignored artifact за новый run.

## Product principles

- Основной путь — drag/Space → annotation прямо в overlay → commit → продолжение сессии → один explicit Paste Session hotkey. Минимизировать дополнительные окна и шаги.
- Clipboard transfer и updater — явные пользовательские действия. Нет telemetry, автоматической отправки screenshot/comment или startup/background update polling.
- Background utility не должна красть receiver focus; feedback короткий, без отдельного тяжёлого interactive workflow. Ошибка feedback UI не отменяет уже успешно выполненное действие.
- Comment bodies остаются editable Unicode text отдельно от bitmap; labels/geometry стабильны.
- Clear/Discard/Exit с потерей данных используют существующие safe confirmations. Отмена сохраняет соответствующее retained состояние.
- Совместимость обещать в пределах evidence; задержка между pastes не является acknowledgement принимающего приложения.

## Current capability state (0.1.2)

Текущие capture modes — region одного монитора и full focused-monitor через Space. Window capture, cross-monitor region, Save As, autostart и повторное редактирование committed снимков пока не реализованы. Feedback сейчас без звука; updater не делает автоматический restart/rollback. Это факты версии 0.1.2, а не запреты на будущие специально спроектированные функции.

## Stable behavioral contract

Существующее поведение нельзя менять случайно или в unrelated task. Отсутствие функции в текущей версии не является permanent product invariant. Новая capture mode или другая feature — отдельная продуктовая задача: сохранить существующие semantics либо явно согласовать их переопределение с владельцем.

Контракты существующих flows:

- Configurable global defaults: Capture `Ctrl+Alt+S`, Paste `Ctrl+Alt+V`, Clear `Ctrl+Alt+X`; overlay keys фиксированы. Tray Paste только инструктирует использовать hotkey в receiver.
- Для существующих region/full focused-monitor modes: начальный focus primary, Space выбирает focused monitor, валидный drag сразу открывает annotation в том же HWND.
- Default Marker, nonempty comment commit, screenshot letters A…AA, high-water marker numbers без переиспользования после Undo/Delete.
- Escape сначала прерывает gesture/edit; annotation draft с сохранёнными annotations требует Discard confirmation. Cancel draft не расходует session letter.
- Paste отправляет singleton image payloads по порядку, затем comments при их наличии; session неизменна. Обнаруженная смена target/clipboard/held keys останавливает flow; нет auto-retry/rollback receiver.
- Copy/Paste заменяют clipboard без backup/restore. Temporary PNGs переживают clipboard replacement, Clear и Exit для asynchronous receivers.
- Clear только после confirmation, Cancel safe default; Escape/X/default Enter сохраняют session. Успех сбрасывает Session и Rasters, следующий снимок A/A1.
- Закрытие Settings не завершает app. Exit через tray/control с committed session спрашивает о потере RAM; single-instance второй process молча выходит.
- Theme/language/hotkeys сохраняются локально; old Dark/Light load поддерживается. Не удалять preferences при upgrade/uninstall.
- Update check manual; startup/background polling требует отдельного продуктового решения. Portable открывает page, installed download требует checksum и confirmation; app закрывается только после successful setup launch.

## Architecture boundaries

Подробные границы — в [ARCHITECTURE](ARCHITECTURE.md).

- `ScreenIt.Core` — geometry/domain snapshots/formatting; не добавлять WPF, WinForms, Win32, network, filesystem или receiver integration.
- `ScreenIt.App` — UI и concrete Windows integration; `Coordinator` владеет runtime resources/state и координирует flows. Это Windows-specific project, а не cross-platform infrastructure layer.
- `AnnotationOverlay` отвечает за input/DIP conversion и interaction, `Painter` за raster projection без comment bodies. Не добавлять clipboard/update/installer orchestration в draft/model/render path.
- PasteSequencer зависит от `IPasteDelivery`; updater — от `IUpdateSource`. Сохранять существующие verification seams; не вводить dependency на конкретный chat receiver в unrelated task. Receiver-specific integration требует отдельного проектирования и owner decision о границах ответственности.
- ToastService не должен владеть session, начинать capture/update или управлять receiver. Preferences не должны хранить screenshots/session.
- Installer/scripts отвечают за package/install; production не зависит от verification/spikes. Verification использует App internals, не наоборот.
- Platform-specific code уже находится в Native/ClipboardTransport/PasteInput/ToastNative/Updates и некоторых WPF UI methods. Размещать изменение рядом с ответственным boundary; не распространять P/Invoke в Core и не делать unsolicited platform rewrite.

## Invariants

- UI/coordinator state и mutable WPF objects остаются на STA dispatcher. Worker получает detached/frozen pixels, не UI ownership.
- Physical pixels — canonical geometry; actual per-HWND transforms обязательны, global virtual-desktop scale недопустим.
- После успешного commit или завершённого cancel не остаются temporary overlays, mouse capture, hooks или Active draft. Suspended flow ещё не завершён: retained state должен быть явно cancelled.
- Acquisition предшествует overlays; crop/output не включает ScreenIt selection/editor chrome. Comment body никогда не попадает в Painter glyphs/raster.
- Session committed GUID и Rasters соответствуют друг другу; Clear очищает обе коллекции, renderer/preparation failure не должен предлагать удалить session как «восстановление».
- Paste допускает один run, guards проверяются на async/delivery boundaries; receiver HWND/PID не активируется/угадывается. Sent count означает отправленный input, не подтверждённое содержимое receiver.
- HDC/HBITMAP/HGLOBAL ownership и cleanup сохраняются при exceptions. SetClipboardData success передаёт handle Windows; double free недопустим.
- Clipboard cleanup удаляет только собственные валидированные generations по существующей TTL policy. Не заменять это recursive delete всего ScreenIt temp root.
- Clear guard привязан к реальному owned window и снимается на Closed; repeated command не создаёт второй confirmation.
- Hotkey replacement оставляет старые registrations до staged+save success; dispatch по dynamic ID mapping, не hardcoded action IDs.
- Toast не перехватывает input/foreground; feedback failure не отменяет successful action.
- AppId, settings path, install path и mutex согласованы между runtime, updater и Inno.

Эти правила описывают сохраняемые code contracts. Не заявлять гарантию graceful completion всех async работ: текущий Dispose отменяет paste, но не ожидает capture/PNG preparation; нет общего shutdown drain.

## Safety

Более тщательная проверка нужна для:

- **Process lifecycle:** RAM loss, single-instance mutex, capture/paste/update во время Exit. Не Kill работающий пользовательский ScreenIt ради tests; его сессия не восстанавливается.
- **Win32/interop:** x64 INPUT layout, screen/memory DC, selected bitmap restoration, HWND hooks/Disposal, WTS и hotkey cleanup. Не путать message hooks с low-level keyboard hooks, которых нет.
- **Thread affinity/async:** UI callbacks после await, cancellation/disposed guards, закрытие Settings с in-flight update, capture worker во время shutdown. Синхронный Dispose не превращать в предполагаемый async barrier.
- **Clipboard/input:** global clipboard overwrite, ownership transfer, partial publication/SendInput, synthetic key release, focus guards/UIPI. Не читать/логировать чужой clipboard или автоматизировать receiver без task authorization.
- **Filesystem:** retained PNGs, marker/TTL/reparse checks, CreateNew, settings overwrite и unknown-field preservation; не удалять unrelated/locked files, preferences или reviewed release artifacts.
- **Settings:** malformed/oversized files могут стать defaults и быть заменены при Save; schemaVersion не gate миграции. Формат меняется только с проверкой old-user path и owner review.
- **Startup/update/installer:** в 0.1.2 autostart не реализован; не добавлять Run/task/background network в unrelated task. TLS URL allowlist, redirects, byte limits, checksums, private generation ACL, identity detection и RAM-loss confirmation — sensitive boundaries.

Suite/production smoke могут менять clipboard/user settings и занимать hotkeys; installer/upgrade tests реально меняют установку. Выполнять на подготовленном desktop/account, соблюдать preconditions из DEVELOPMENT. Автоматизированный PASS не подтверждает physical receiver/DPI/system UX.

## Change discipline

1. Проверить `git status`, происхождение dirty changes и current branch/tag.
2. Понять existing end-to-end flow; найти все call sites, state owners и cleanup paths.
3. Проверить связанные interfaces/services, verification, scripts и user contract.
4. Сделать минимальное scoped изменение; не смешивать unrelated refactoring.
5. Не вводить speculative abstractions, второй capture backend без установленной необходимости или premature rewrites.
6. Сохранить behavioral contract и Windows ownership/threading boundaries; объяснить необходимое отклонение владельцу.
7. Обновить relevant documentation при изменении behavior/architecture/tooling; не дублировать подробности во всех файлах.

## Verification

Каждое изменение проходит:

```text
build
  → automated tests/checks where available
  → relevant manual smoke tests
  → git diff review
  → git status review
```

Команды и scope — в [DEVELOPMENT](DEVELOPMENT.md). Existing checks — executable/GUI harness, не `dotnet test` suite; full Verification требует два монитора. Для docs-only правок достаточно источников, version/path/command/link validation и diff/status, если runtime не изменён; невыполненные проверки и ограничения сообщить честно.

Lifecycle/interop changes требуют resource/stress и cancellation/exit checks; coordinates — actual monitors/DPI, paste — manual receiver, settings — old-file/save failure/conflict, update/install — fake negative checks плюс disposable installation/upgrade и owner review. Если нужный SDK, hardware, receiver, monitor topology или другие prerequisites недоступны, не выдавать результат за PASS; перечислить невыполненные проверки. Не выполнять новый release ради verification.

## Git and main

- `main` — стабильная интеграционная ветка. Проверять status первым действием; dirty tree исследовать до изменений, не перезаписывать чужую работу.
- Scoped/reviewable changes; не смешивать unrelated work. Generated artifacts, user settings и evidence не включать случайно в commit.
- Не force-push; не rebase/переписывать опубликованную историю без прямой команды владельца.
- Не удалять чужие изменения и не reset main destructive-командами. Не применять reset/clean/checkout для «очистки» без явного разрешения.
- Не делать push без прямой команды владельца.
- Не создавать tag/GitHub Release, не публиковать installer/package, не изменять release artifacts без прямой команды владельца.
- Не commit, если задача запрещает commit. Для новой ветки по умолчанию `codex/`, если владелец не указал другую.

## Stop and hand off

Подготовить конкретный reviewable diff/result и проверку в авторизованном scope, затем остановиться и передать владельцу на ручную проверку **перед**:

- push; merge в main, если прямо не разрешён; tag, release, upload/publication installer/package или изменение existing release artifacts;
- принятием изменений update infrastructure/installer behavior в стабильную ветку или их исполнением на пользовательской установке;
- destructive migration, изменением settings/data format, затрагивающим existing users, или операцией с возможной потерей данных/preferences;
- security-sensitive Win32/input/handle/ACL behavior и изменением поведения, безопасность которого нельзя надёжно подтвердить доступной автоматикой/ручной проверкой.

Код/draft в рамках явно порученной задачи можно подготовить до handoff; эти правила не означают просить разрешение на каждое read/edit. Уже данное прямое разрешение не запрашивать повторно, но предоставить evidence и unresolved risks. Публикация/разрушительное исполнение требуют явной авторизации именно такого действия.

Если behavior неоднозначен, code/docs противоречат друг другу, нужен product choice или неожиданно требуется breaking change — исследовать и описать варианты, не выбирать новую semantics молча. Остановить зависимую часть, передать владельцу files/diff, findings, выполненные/невыполненные checks и требуемое решение. Не маскировать uncertainty переписыванием документации под предположение.

## Documentation maintenance

| Документ | Когда обновлять |
|---|---|
| [README.md](README.md), [README.ru.md](README.ru.md) | Публичное описание, user workflow, shortcuts, privacy/compatibility и installation. Согласовывать обе языковые версии. |
| [CHANGELOG.md](CHANGELOG.md) | Заметное user-facing или release изменение; Unreleased до выпуска. Versions/dates сверять с history/tags, не копировать git log. |
| [ARCHITECTURE.md](ARCHITECTURE.md) | Runtime architecture, responsibilities, lifecycle, significant data/control flow и установленные limitations. |
| [DEVELOPMENT.md](DEVELOPMENT.md) | Tooling, build/test/smoke/package/upgrade/release workflow и preconditions. |
| [AGENTS.md](AGENTS.md) | Только общие onboarding/change rules, invariants и safety boundaries. Не превращать в копию implementation guide. |

Использовать относительные ссылки. Исторические spikes/release notes сохранять как history; при расхождении явно обозначить область/дату evidence, не переписывать прошлый результат новым предположением.
