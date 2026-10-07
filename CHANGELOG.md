# Changelog

История восстановлена по локальным Git tags, исходникам и изменениям между тегами. Даты ниже — даты тегов в UTC+03:00; дата публикации GitHub Release отдельно не установлена. История до первого доступного коммита отсутствует. Исследовательские этапы в `spikes/` не являются отдельными версиями продукта.

## [Unreleased]

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

[0.2.0]: https://github.com/nickkho0201/ScreenIt/tree/v0.2.0
[0.1.2]: https://github.com/nickkho0201/ScreenIt/tree/v0.1.2
[0.1.1]: https://github.com/nickkho0201/ScreenIt/tree/v0.1.1
[0.1.0]: https://github.com/nickkho0201/ScreenIt/tree/v0.1.0
