[English](README.md) | Русский

# ScreenIt

ScreenIt — локальная Windows utility для screenshot-аннотаций. Она помогает быстро показать AI-ассистенту, разработчику, дизайнеру или коллеге, что именно вы имеете в виду.

**Capture → Point → Comment → Continue → Paste**

Выберите область или целый monitor и добавьте аннотации прямо поверх замороженного desktop в overlay. Отдельного окна image editor нет. Markers **A1**, **A2** связывают точки изображения с комментариями, которые остаются настоящим редактируемым текстом. Следующие screenshots B, C и далее добавляются в ту же session.

Перейдите в поле чата и нажмите **Ctrl+Alt+V один раз**. ScreenIt последовательно вставит каждый screenshot отдельным изображением, затем structured comments.

## Возможности

- Windows 11 x64, работа в tray и global hotkeys.
- Capture области/целого monitor; multi-monitor и основа mixed-DPI поддержки.
- Markers с комментариями; перемещение, редактирование и удаление markers.
- Стрелки, рамки и Undo/Redo текущего screenshot.
- Sessions из нескольких screenshots: A, B, C… Z, AA…
- Paste Session и Clear Session с подтверждением.
- Dark/Light и короткие visual toasts, без звука.
- Local-first: без аккаунтов, облака, backend и telemetry.

## Установка

### Installer

1. Скачайте **ScreenIt-Setup-0.1.0.exe** из [Releases](https://github.com/nickkho0201/ScreenIt/releases).
2. Запустите installer.
3. Откройте ScreenIt из меню «Пуск».
4. Приложение работает в system tray.

Установка для текущего пользователя, без обязательных прав администратора и autostart. Uninstall удаляет файлы приложения, сохраняя выбранную тему.

**ScreenIt пока не подписан. Windows SmartScreen может показывать предупреждение для ранних релизов из-за отсутствия code-signing certificate.** Не отключайте SmartScreen целиком.

### Portable

Скачайте **ScreenIt-0.1.0-win-x64-portable.zip** из [Releases](https://github.com/nickkho0201/ScreenIt/releases), распакуйте архив и запустите **ScreenIt.App.exe**.

### Требования

**Windows 11 x64.** Оба release packages self-contained: установленный .NET runtime не требуется. Поддержка Windows 10 не заявляется.

## Быстрый старт

1. **Ctrl+Alt+S** → выделите область drag и отпустите мышь; либо **Space** для текущего monitor. Annotation mode включается сразу.
2. Click → **A1** → введите комментарий → **Enter**. Добавьте markers, стрелки или рамки.
3. **Ctrl+Enter** добавляет screenshot в session и закрывает overlay. Повторите для B, C…
4. Перейдите в target composer; нажмите **Ctrl+Alt+V** один раз и отпустите клавиши. Изображения вставляются по порядку, редактируемые comments — последними.
5. **Ctrl+Alt+X** → подтвердите Clear для новой session. Cancel, Escape и закрытие confirmation сохраняют session. Clear сбрасывает нумерацию на A/A1.

Во время Paste Session оставайтесь в том же target window. При смене focus или ошибке вставка остановится; ScreenIt session сохранится. Уже вставленное не откатывается, повторная попытка может создать duplicates.

Tray → **Theme → Dark / Light** переключает оформление. По умолчанию Dark; выбор сохраняется локально. Tray → **More** содержит вторичные ручные команды Copy Session Images/Comments.

## Хоткеи

Первые три сочетания глобальные; остальные действуют внутри overlay.

| Действие | Хоткей |
|---|---|
| Capture | `Ctrl+Alt+S` |
| Paste Session | `Ctrl+Alt+V` |
| Clear Session | `Ctrl+Alt+X` |
| Marker | `M` |
| Arrow | `A` |
| Rectangle | `R` |
| Edit comment | `E` |
| Удалить выбранную annotation | `Delete` |
| Undo | `Ctrl+Z` |
| Redo | `Ctrl+Y` |
| Commit screenshot | `Ctrl+Enter` |

Comment editor: **Enter** сохраняет, **Shift+Enter** добавляет новую строку, **Esc** отменяет edit. Перед commit сохраните/отмените edit. Undo/Redo внутри editor действует на текст. Escape сначала отменяет текущий gesture/edit; отмена screenshot с annotations требует подтверждения.

## Приватность

ScreenIt работает локально. Screenshots/comments остаются на устройстве. Нет backend ScreenIt, telemetry, analytics, сетевой runtime-функциональности, аккаунтов и автоматических uploads.

Paste Session явно передаёт данные выбранному вами приложению через Windows clipboard/input. Дальнейшая обработка зависит от receiver; ScreenIt не контролирует его сетевое поведение и сторонние clipboard managers.

Sessions хранятся в RAM. Единственная сохраняемая preference — тема: `%LOCALAPPDATA%/ScreenIt/settings.json`. Для paste создаются временные PNG под `%TEMP%/ScreenIt/Clipboard-v1`. Они остаются после paste, Clear и exit для асинхронных receivers; owned generations старше семи дней очищаются при следующем запуске.

## Совместимость

Вручную подтверждены **sequential Paste Session в ChatGPT Web** и текущая **Windows 11 multi-monitor конфигурация со mixed DPI 100%/125%**, включая отрицательный monitor origin.

Совместимость с другими приложениями может различаться в зависимости от обработки Windows clipboard и synthetic paste input. Это не исчерпывающий compatibility list. Ограничения Windows input могут мешать вставке в elevated applications.

## Известные ограничения

- Только Windows; sessions в RAM, без persistent history и восстановления после crash.
- Committed screenshots нельзя открыть для повторного редактирования.
- UI приложения на English; localization пока нет.
- Только явные Dark/Light, без System theme.
- Installer и executable не подписаны.
- Физические DPI 150%/200%, portrait, HDR/protected content и system transitions не полностью проверены вручную.

## Разработка

Требуются Windows и **.NET 10 SDK**.

```powershell
dotnet build ScreenIt.sln -c Release
.\verification\ScreenIt.Verification\bin\Release\net10.0-windows\ScreenIt.Verification.exe
.\verification\Smoke.ps1
```

Перед verification/smoke закройте пользовательский instance; RAM session не сохраняется. Generated evidence находится в игнорируемом `artifacts/`. Automated checks дополняют manual acceptance.

Release tooling: **Inno Setup 7.1.0**, только на этапе сборки. См. [инструкцию release build](installer/README.md). `spikes/` сохраняет research history; production не зависит от spike projects. Feature scope v0.1.0 заморожен.

## Лицензия

[MIT License](LICENSE). Компоненты .NET сохраняют собственные licenses и third-party notices.
