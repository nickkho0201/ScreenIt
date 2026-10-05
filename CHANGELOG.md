# Changelog

История восстановлена по локальным Git tags, исходникам и изменениям между тегами. Даты ниже — даты тегов в UTC+03:00; дата публикации GitHub Release отдельно не установлена. В доступной истории три коммита; история до первого коммита отсутствует. Исследовательские этапы в `spikes/` не являются отдельными версиями продукта.

## [Unreleased]

### Added

- Внутренняя документация: [архитектура](ARCHITECTURE.md), [разработка и проверка](DEVELOPMENT.md), [правила для coding agents](AGENTS.md) и восстановленная история версий. README дополнен навигацией.

На момент начала аудита `main` совпадал с `v0.1.1`; unreleased функциональных изменений не было. Этот раздел относится к текущим изменениям документации.

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

[0.1.1]: https://github.com/nickkho0201/ScreenIt/tree/v0.1.1
[0.1.0]: https://github.com/nickkho0201/ScreenIt/tree/v0.1.0
