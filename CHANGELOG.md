# Changelog

История восстановлена по локальным Git tags, исходникам и изменениям между тегами. Даты ниже — даты тегов в UTC+03:00; дата публикации GitHub Release отдельно не установлена. История до первого доступного коммита отсутствует. Исследовательские этапы в `spikes/` не являются отдельными версиями продукта.

## Unreleased

## [0.3.0] - 2026-11-13

### Added

- В «Основных» появился отдельный список оформления **Классический / Ember** (Classic / Ember в EN), независимый от System/Dark/Light. Классический воспроизводит синий интерфейс 0.2.3: палитру и accent `#4255C5`, стандартные окна подтверждений, старый toolbar/редактор комментария, blue/white экспорт и скруглённые toast. Ember сохраняет CONSTRUCT и палитру TASK-1 (Signal/Paper, capture guide, split Done dock, MarkerSurface/CommentEditorCard). Смена оформления сначала показывает локализованное предупреждение; только подтверждение применяет и сохраняет выбор, Cancel/Escape/X/безопасный Enter оставляют прежнее, повторный выбор текущего оформления не спрашивает. Поле `design` добавлено совместимо в schemaVersion 2: старые файлы без поля и неизвестные значения дают Classic, старые настройки и unknown JSON fields сохраняются, ошибка Save не рассогласует UI и файл. TASK-2 pre-show positioning сохранён для обоих оформлений, включая non-client frame Classic.

### Changed

- В Ember стрелки, рамки и карандаш переведены с синего `#4255C5` на Signal Red `#F04D32` со светлым контуром Paper `#FBFEF8`; Classic сохраняет синий/белый экспорт 0.2.3. Цвета одинаковы в gesture/draft preview и итоговом bitmap, не зависят от Light/Dark и fitted-window; толщины 2.5/4.5, геометрия, жесты, Undo/Redo/Delete и export semantics сохранены.

- Из разделителя подтверждений убрана надпись SCREENIT //; focus-обводка флажка «Больше не спрашивать» использует Signal Red вместо Acid.

- Общие окна подтверждений и информации больше не появляются на мгновение в углу work area перед центрированием: скрытый HWND сначала измеряется на выбранном мониторе (actual DPI), содержимое измеряется до `Show`, и уже первый видимый кадр находится в конечной центральной позиции. Политика foreground work area, modal/nonmodal, focus, безопасные Enter/Escape/Cancel/Close, Clear opt-out и нижняя позиция пассивных toast не изменены.

- Общие окна подтверждений и информации получили CONSTRUCT-композицию: собственная шапка с логотипом и закрытием, крупный заголовок Unbounded, пояснение Commissioner, технический разделитель и угловатые action-кнопки. Clear сохраняет Cancel как безопасный default и прежний opt-out. Пассивные toast-плашки получили согласованный матовый фон, срезанный контур и Signal-кромку без изменения таймеров/фокуса.

- «Готово» перенесено из нижнего правого угла в отдельный блок справа от верхней панели аннотаций; между блоками зазор 12 DIP. На узком экране инструменты переносятся с учётом ширины кнопки. Действие и Ctrl+Enter сохранены.

- Comment Editor: матовая floating-card поверхность, мягкая векторная тень, подсветка кромки и слабый тёплый рефлекс; интегрированное поле со срезом угла, более контрастные keycaps. Соединитель — один тонкий штрих с осевым участком/диагональю 45° и компактным узлом. Компактные размеры, физические anchors и экспорт сохранены.

- Компактная UI-метка: исходная physical-pixel геометрия проецируется через SourceScale и actual HWND DPI, с читаемым пределом высоты 22–28 DIP и Commissioner Semibold. Видимые bounds используются для hit testing и drag; PNG этой корректировкой не изменён. Редактор до 380×246 DIP: Unbounded ID 26, интегрированный TextBox без синей рамки, компактный тематический scrollbar и отдельный keycap footer. Конечные hover/selection и open/close переходы без изменения размеров метки.

- Короткий клик по существующей метке открывает комментарий, drag по-прежнему перемещает её; Escape оставляет метку выбранной для Delete. Новый редактор комментариев: компактная карточка со скошенными углами, номером метки, Signal-полем ввода и подсказками Enter/Shift+Enter/Esc; растущая соединительная линия, конечные fade-переходы 140/100 ms и статичное состояние при отключённых анимациях Windows. Сами exported метки — Ink/Paper с Signal-контуром и скошенными углами; прежние physical hit bounds, номера и comment semantics сохранены. Линия/редактор не попадают в bitmap.
- У кнопки Region/Window добавлены статичная иконка круговых стрелок справа, без нижней подписи; позиции иконки и W стабильны при переключении.
- Region/Window в панели выбора объединены в одну кнопку с текущей надписью/иконкой и keycap W. Клик и W используют тот же режим; остальная панель не перехватывает выделение.
- Панель выбора области до аннотаций приведена к оформлению annotation toolbar: логотип, иконки Region/Window/monitor, keycaps W/Space/modifier/Esc, подсветка текущего режима и удерживаемого modifier; краткая контекстная инструкция отдельно снизу слева. Input/capture semantics сохранены.

- CONSTRUCT (Ember): новое оформление Settings, capture/annotation overlay, редактора комментария, подтверждений и уведомлений в светлой/тёмной темах. Встроенные Commissioner/Unbounded с RU/EN; Signal Red и Acid, компактные векторные инструменты и отдельная верхняя кнопка «Готово». Сценарии аннотаций сохранены; актуальная палитра стрелок/рамок/карандаша описана в записи выше.
- Новый `S_IT` логотип, SVG и многоразмерный ICO с отдельным micro-вариантом для трея. Декоративное бесконечное дыхание selection glow заменено конечным переходом; уведомления учитывают отключённые анимации Windows.
- Локальная portable preview собирается через `verification/Build-DesignPreview.ps1`, без installer и изменения установленной копии. Проверки шрифтов, контраста и RU/EN preview доступны через `--design-only`.

## [0.2.3] - 2026-11-07

### Fixed

- Текстовая часть Paste Session и Copy Session Comments включает только снимки с метками комментариев. Снимки без меток сохраняют свои буквы и вставляются изображениями в прежнем порядке; пустые заголовки `Screenshot B` и лишние разделители пропускаются.

### Added

- Карандаш в режиме аннотаций: кнопка на панели и клавиша `P`, произвольные штрихи одним accent-цветом с тем же белым контуром, что у стрелок/рамок. Штрих входит в итоговый скриншот без метки/комментария; поддерживает выделение, Delete и Undo/Redo одним действием. Escape, потеря mouse capture и смена инструмента отменяют незавершённый штрих. Панель переносит кнопки на следующую строку при нехватке ширины.

## [0.2.2] - 2026-11-07

### Added

- Контур и внешнее свечение выделения используют `#A043C9` при удержании annotation modifier. Базовый accent, растровый output и интенсивность/тайминги свечения сохранены.
- Короткие UI-переходы в Settings: fade выпадающих списков, плавный ход переключателей, появление галочки и лёгкое проявление разделов. Учитывается Windows ClientAreaAnimation; операции и сохранение настроек не ждут анимаций.
- UI Automation Toggle у переключателей Settings выполняет то же действие с сохранением/readback и откатом при ошибке, что мышь/клавиатура; disabled переключатель отклоняет действие.
- Отключаемое подтверждение очистки сессии: стилизованный чекбокс «Больше не спрашивать» в диалоге и переключатель «Отключить предупреждение об удалении сессии» в основных настройках. Блоки автозапуска и предупреждения в основных настройках оформлены компактными карточками с одинаковыми переключателями. Выбор сохраняется между запусками, по умолчанию предупреждение включено. Cancel/Escape/Enter/X не сохраняют флажок; уведомление об успешной очистке остаётся.

## [0.2.1] - 2026-10-16

### Added

- Optional per-user «Запуск с Windows» переключатель в Settings (выключен по умолчанию): регистрация в HKCU `Run` с quoted executable path, отдельно от `settings.json`. Settings различает отсутствие, чужую/другую ScreenIt запись, внешний блок Windows и нераспознанное (undocumented) состояние, не выдавая последнее за подтверждённую блокировку. Изменение подтверждается readback, неуспешное включение откатывается с восстановлением прежнего значения и его типа (в т.ч. REG_EXPAND_SZ), без перезаписи внешних изменений и с отдельным сообщением при сбое компенсации. Disable перепроверяет владение перед удалением; чужая регистрация не удаляется. Inno uninstall удаляет только собственную запись установки. Deterministic проверки `--autostart-only` используют fake/изолированный registry с read-only tripwire; реальная автозагрузка Windows — manual acceptance.
- Native переключатель «Запуск с Windows» в Settings на базе настоящего WPF `ToggleButton` с шаблоном ScreenIt (состояния off/on, hover, pressed, keyboard focus, disabled), синхронизированный с реальным состоянием автозапуска: отображаемое состояние не является источником истины, неуспешное действие возвращает прежнее состояние. Полная матрица состояний, доступность (name/TogglePattern) и снимки Light/Dark EN/RU покрыты `--settings-visual-only`.

## [0.2.0] - 2026-10-07

### Added

- Window capture selection via W: native Windows.Graphics.Capture HWND source, z-order hover tint/border, PID/visibility/cloaking/tool/shell filtering, Quick Capture or modifier-to-Annotation through the existing session pipeline. Whole physical source is retained across monitors; enlarged sources fit the same annotation overlay. Native occlusion/resource checks and localized Region/Window hints are included; no desktop crop or PrintWindow fallback.
- Updater phase/progress UI: real installer download percentage/bytes when Content-Length is known, indeterminate unknown-size and verification/preparation/launch states, localized stage-specific errors and Retry. Shared renderer has a safe developer harness in Verification; no simulated production install progress.
- Capture hotkey `Win+Shift+S`: scoped low-level interception during the assigned binding, without Windows configuration changes. Settings input recording has its own temporary hook. Ordinary hotkeys retain RegisterHotKey; installation/save failures retain previous bindings.
- Persisted annotation hold-modifier Ctrl (default), Shift or Alt; localized Settings control and dynamic selection hint.
- UI-only outer accent glow and highlighted annotation hint while the modifier is held; smooth fades/breathing and a static Windows animation-disabled state.

### Changed

- Settings shortcut recording использует временный input-only hook для всех трёх actions: Win modifiers сохраняются, перехватываемые Windows chords доходят до существующей registration policy. Gesture подавляется только во время записи; release/Esc, focus loss и close снимают recorder. При отказе поле возвращается к прежнему binding с конкретной ошибкой. Runtime hotkey policy не расширена.
- Hotkey availability для всех structurally valid сочетаний определяется RegisterHotKey; специальный hook остаётся только у Capture=Win+Shift+S. Win+Shift+S для Paste/Clear пробует обычную регистрацию. Reuse учитывает backend; неудачный rebind сохраняет старый hook/binding/settings. Inline ошибки EN/RU указывают конкретную комбинацию и различают invalid, duplicate, global conflict, hook и save failure.
- Active annotation feedback усилен: более заметное внешнее accent-свечение и glow modifier hint, с немного большей интенсивностью Light theme. Geometry, animation timing и capture behavior сохранены.
- Settings по умолчанию выше (620 DIP), чтобы EN/RU страница Hotkeys помещалась без scrollbar при прежней typography/spacing. При уменьшении окна используется компактный themed scrollbar без стрелок.
- Region drop and focused-monitor Space immediately commit into the existing RAM session by default. Holding the configured modifier at drop/Space opens the existing annotation flow instead; Ctrl+Enter commits annotated drafts as before.
- Verification covers modifier-at-drop/Space decisions, clean frozen output, settings migration/persistence, reserved shortcut filtering and native hook lifecycle. Production smoke now expects immediate Space commit. Physical Snipping Tool suppression/restoration remains a manual acceptance check.

## [0.1.2] - 2026-10-05

### Added

- Внутренняя документация: [архитектура](ARCHITECTURE.md), [разработка и проверка](DEVELOPMENT.md), [правила для coding agents](AGENTS.md) и восстановленная история версий. README дополнен навигацией.
- Однократное локализованное уведомление о готовности ScreenIt к фоновой работе после успешной инициализации, на основном мониторе; второй экземпляр остаётся бесшумным.

### Changed

- Уведомления стали компактным HUD с нейтральной светлой/тёмной поверхностью, небольшим status badge и более лёгкой иерархией текста. Ширина определяется содержимым; длинные сообщения переносятся и растут по высоте. Счётчик использует «3 снимка в сессии» / «3 screenshots in session» с EN/RU pluralization.
- Toast расположен по центру у нижнего края соответствующего monitor work area; добавлены opacity fade-in/fade-out. Replacement, capture hide и shutdown закрывают окно сразу, сохраняя no-activate и click-through contract.

### Fixed

- Кнопка Done в светлой теме соответствует общему accent-оформлению annotation toolbar вместо отдельного тёмного фона; shortcut badge остаётся читаемым.

## [0.1.1] - 2026-10-04

### Added

- Окно Settings с General, Hotkeys и About; повторное открытие использует существующее окно.
- Системная тема с реакцией на изменение Windows app theme, наряду с Dark и Light.
- Английский и русский интерфейс; начальный язык выбирается по Windows UI culture с английским fallback. Пользовательские комментарии и технические идентификаторы не переводятся.
- Настройка трёх глобальных сочетаний Capture/Paste/Clear и восстановление стандартного набора. Недопустимые, повторяющиеся и занятые сочетания отклоняются; неудачная замена или сохранение оставляют прежние регистрации.
- Ручная проверка новых стабильных версий через GitHub Releases. Для установленной копии — загрузка installer/checksums, проверка SHA-256 и подтверждение потери RAM-сессии перед запуском setup. Portable-копия открывает страницу выпуска.
- Проверки настроек, миграции theme-only файла, локализации, hotkey replacement, fake updater и Settings lifecycle; отдельный pointer smoke для состояний Settings controls.
- Скрипт проверки upgrade с сохранённого `0.1.0` baseline на `0.1.1`.

### Changed

- Выбор темы перенесён из tray-подменю в Settings; Clear Session находится среди основных tray-команд.
- Новые настройки по умолчанию используют System; существующие `dark`/`light` сохраняются. При записи `settings.json` получает `schemaVersion: 2`, язык и hotkeys; неизвестные JSON-поля сохраняются при обычной успешной загрузке/записи.
- Tray, overlay, prompts и feedback используют локализованные подписи; общие ошибки показывают нейтральный текст вместо имени типа исключения.
- Перед capture окно Settings скрывается. Закрытие Settings не завершает фоновое приложение.
- Installer и portable artifacts обновлены до `0.1.1`; AppId, install directory, mutex и политика сохранения preferences сохранены.
- Русский README расширен описанием использования и ограничений.

Существующий capture → annotation → session → sequential paste contract сохранён. Git diff не подтверждает отдельного набора исправлений capture backend; такие исправления сюда не добавлены.

## [0.1.0] - 2026-10-04

### Added

- Первое состояние production ScreenIt в доступной истории: Windows 11 x64 tray utility, локальный mutex для одного экземпляра и глобальные `Ctrl+Alt+S/V/X`.
- Захват области одного монитора или целого монитора через Space; frozen overlay на каждом дисплее, physical-pixel geometry и PerMonitorV2 DPI handling.
- Разметка в том же overlay: маркеры с комментариями, перемещение/редактирование/удаление маркеров, стрелки, прямоугольники и draft-local Undo/Redo.
- RAM-сессии A, B… Z, AA…; устойчивые номера маркеров внутри снимка. Commit фиксирует изображение, а тела комментариев остаются отдельным Unicode-текстом.
- Paste Session: отдельная clipboard/input операция для каждого PNG по порядку, затем комментарии; ожидание отпускания клавиш, foreground и clipboard guards, остановка при ошибке/смене окна без удаления сессии.
- Ручные Copy Session Images и Copy Session Comments; временные PNG сохраняются для асинхронных получателей и очищаются позднее по ownership/семидневному TTL.
- Подтверждённая очистка сессии, безопасный Cancel по умолчанию, подтверждение выхода при наличии committed снимков.
- Dark/Light appearance с локальным theme-only preferences файлом; короткие визуальные уведомления без звука и без Windows Notification Center.
- Windows interop capture, clipboard, input, monitor/session/power handling; при изменении окружения текущий capture скрывается и приостанавливается.
- Self-contained installer и portable ZIP, checksums и bundled runtime licenses; per-user установка без autostart и обязательного повышения прав, сохранение настроек после uninstall.
- Verification executable, production smoke и installer verification; три архивных spike-проекта с исследовательскими результатами.

В `0.1.0` runtime updater и runtime networking отсутствовали. Поддержка capture окна, persistent sessions и пользовательский Save As не реализованы ни в этом теге, ни в `0.1.1`.

[0.3.0]: https://github.com/nickkho0201/ScreenIt/tree/v0.3.0
[0.2.3]: https://github.com/nickkho0201/ScreenIt/tree/v0.2.3
[0.2.2]: https://github.com/nickkho0201/ScreenIt/tree/v0.2.2
[0.2.1]: https://github.com/nickkho0201/ScreenIt/tree/v0.2.1
[0.2.0]: https://github.com/nickkho0201/ScreenIt/tree/v0.2.0
[0.1.2]: https://github.com/nickkho0201/ScreenIt/tree/v0.1.2
[0.1.1]: https://github.com/nickkho0201/ScreenIt/tree/v0.1.1
[0.1.0]: https://github.com/nickkho0201/ScreenIt/tree/v0.1.0
