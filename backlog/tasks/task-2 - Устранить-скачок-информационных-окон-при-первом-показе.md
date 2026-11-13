---
id: TASK-2
title: Устранить скачок информационных окон при первом показе
status: Done
assignee:
  - '@flash-worker'
created_date: '2026-11-12T20:26:00Z'
updated_date: '2026-11-12T20:46:00Z'
labels: []
dependencies: []
priority: medium
type: bug
ordinal: 2000
---

## Description

<!-- SECTION:DESCRIPTION:BEGIN -->
Пользователь видит информационные окна сначала в левом верхнем углу, затем в центре. Исправить общий PromptWindow и связанные вызовы: первый видимый кадр сразу в конечной центральной позиции. Область: позиционирование ScreenIt.App, узкие проверки и необходимая документация. Сохранить политику выбора монитора и work area, actual HWND DPI, modal/nonmodal, focus, безопасные Enter/Escape/Cancel/Close, Clear opt-out. Пассивные toast не переносить в центр. Существующий dirty редизайн и TASK-1 сохранить. Исключены unrelated refactoring, Core, clipboard/input/paste, settings format, updater/installer, версии, release artifacts и OpenCode. Не закрывать пользовательский процесс, не менять clipboard/settings/hotkeys. Без commit/push/deploy. Разрешены локальные build и изолированные synthetic checks, включая собственные тестовые окна. Backlog является источником задания, плана и evidence. Реальная визуальная приёмка остаётся пользователю.
<!-- SECTION:DESCRIPTION:END -->

## Acceptance Criteria
<!-- AC:BEGIN -->
- [x] #1 Первый видимый кадр общих информационных окон и подтверждений сразу в конечной центральной позиции, без показа в углу и скачка.
- [x] #2 Сохранены выбор work area, actual HWND DPI, размещение на доступных мониторах и длинный текст RU/EN.
- [x] #3 Сохранены lifetime, focus, safe default, Escape/Cancel/Close и Clear opt-out; toast не перенесены в центр.
- [x] #4 Выполнены build и узкие регрессионные проверки первого показа и notices; результаты и непроверенные сценарии записаны.
- [x] #5 Изменения минимальны относительно dirty-базы; выполнен diff/status review и передан результат на визуальную проверку.
<!-- AC:END -->

## Implementation Plan

<!-- SECTION:PLAN:BEGIN -->
1. Прочитать TASK-2, overview/execution/finalization, AGENTS и onboarding docs; исследовать реальный lifecycle PromptWindow (ctor → Prepare → Show/ShowDialog → ContentRendered → Position), все call sites (UtilityUi.Confirm/Inform, Coordinator Clear, SettingsWindow install confirmation) и политику ToastNative.Area по foreground HWND.
2. Зафиксировать baseline dirty tree: копии затрагиваемых файлов, git status и git diff в approved TEMP; проверить окружение (локальный .NET SDK 10.0.404 в TEMP, системного SDK нет).
3. Добавить в verification notices проверку ПЕРВОГО показа: rect сразу после Show, rect первого кадра CompositionTarget.Rendering, rect после idle; modal-вариант через ShowDialog; длинный RU/EN, Clear dialog; DPI/центрирование по доступным мониторам. Прогнать до фикса и зафиксировать провал (доказательство root cause).
4. Минимально исправить PromptWindow: Prepare оставляет HWND скрытым, снимает actual DPI целевого монитора, измеряет тот же контент, который разложит WPF, и ставит финальный центрированный physical frame ДО Show; ContentRendered-коррекция остаётся no-op; общая математика Centered() для pre-show, post-show и notices checks.
5. Пересобрать, прогнать --notices-only (дважды), сохранить JSON/PNG evidence; саморевью delta против baseline и git status; обновить ARCHITECTURE/CHANGELOG/DEVELOPMENT; записать evidence в Backlog и передать Sol (без Done).
<!-- SECTION:PLAN:END -->

## Implementation Notes

<!-- SECTION:NOTES:BEGIN -->
Исследование и root cause.
- Lifecycle до фикса: ctor ставит Width=520, SizeToContent=Height, WindowStartupLocation=Manual, Left/Top=NaN; Prepare(owner) делает EnsureHandle + ToastNative.Place(hwnd, work.X, work.Y, 1, 1) и Width=520; Show/ShowDialog раскладывает контент и оставляет HWND в work origin; ContentRendered вызывает Position() и только тогда центрирует. Первый видимый кадр оказывается в левом верхнем углу work area.
- Фактически измерено verification-проверкой (DISPLAY1 3440x1440@120, work area 3440x1380): сразу после Show() окно было Rect(0,0,650,345), целевой центр — (1395,517). Полноразмерное окно на один и более кадров видно в углу, затем скачок.
- WPF округляет SizeToContent.Height ВВЕРХ в DIP до DPI-преобразования (DesiredSize 275.2 DIP → 276 DIP → 345 px при 125%), поэтому наивное round(DesiredSize*scale) давало 344 px и сдвиг на 1 px; в фиксе используется Ceiling в DIP.
- Политика work area — foreground HWND (ToastNative.Area(null, PasteInput.Foreground().Hwnd)); Prepare вызывается до показа, когда foreground ещё принадлежит receiver. Сохранено.
- DPI — actual per-HWND: скрытый 1x1 probe на целевом мониторе, затем GetDpiForWindow (тот же приём, что в ToastWindow).
- Call sites: UtilityUi.Confirm (modal ShowDialog), UtilityUi.Inform (modal), Coordinator Clear (modal:false, Show+Activate), Settings install confirmation (modal через Confirm). Modal/nonmodal, focus, safe defaults и Clear opt-out не менялись.
- Toast уже ставится до показа (EnsureHandle → 1x1 → PlaceContent → Show → PlaceContent) и намеренно снизу; его код, таймеры и noactivate-контракт не тронуты.

Решение и изменения.
- src/ScreenIt.App/UtilityUi.cs (PromptWindow): Prepare теперь (1) берёт work area по foreground HWND, (2) EnsureHandle + скрытый 1x1 probe на целевом мониторе, чтобы GetDpiForWindow вернул actual DPI этого монитора, (3) ограничивает Width/MaxHeight размерами work area в DIP, (4) PlaceBeforeShow измеряет тот же контент, который разложит WPF, и ставит финальный центрированный физический frame ДО Show. При отсутствии DPI-ответа/контента сохраняется прежний путь через ContentRendered.
- Общая математика PromptWindow.Centered(work,width,height) используется pre-show placement, ContentRendered-коррекцией и verification; она центрирует и не выходит за work area.
- Position() в ContentRendered оставлен как коррекция (BringForward/focus не изменён); на нормальных размерах он теперь no-op, что подтверждено проверкой «No visible reposition after first frame».
- Минимальный delta: +21/-3 строки в PromptWindow, пользовательский CONSTRUCT-редизайн и TASK-1 палитра не затронуты.
- verification/ScreenIt.Verification/NoticeChecks.cs: добавлены проверки первого показа (rect сразу после Show, rect первого кадра CompositionTarget.Rendering, rect после idle), modal-вариант через ShowDialog, длинный RU/EN, Clear dialog и общая геометрия/DPI по всем доступным мониторам.
- Документация: ARCHITECTURE.md (механизм pre-show placement и Centered), CHANGELOG.md (Unreleased Changed), DEVELOPMENT.md (что теперь покрывает --notices-only).

Проверки и evidence (рабочая папка C:\Users\nickk\Documents\ScreenIt).
- Окружение: системного .NET SDK нет; использован локальный SDK 10.0.404 из C:\Users\nickk\AppData\Local\Temp\opencode\dotnet-sdk (DOTNET_ROOT + PATH).
- Baseline: копии затрагиваемых файлов, git status и git diff до правок сохранены в C:\Users\nickk\AppData\Local\Temp\opencode\task2-baseline\ (UtilityUi.cs, NoticeChecks.cs, Program.cs, ARCHITECTURE.md, DEVELOPMENT.md, CHANGELOG.md, git-status-baseline.txt, git-diff-baseline.patch). Мой delta против baseline — C:\Users\nickk\AppData\Local\Temp\opencode\task2-delta\*.patch.
- Build: dotnet build ScreenIt.sln -c Release -p:BaseOutputPath=C:\Users\nickk\Documents\ScreenIt\artifacts\task2-build\out\ → успешно, 0 warnings, 0 errors. Обычный bin\Release заблокирован запущенной пользовательской копией ScreenIt.App (PID 26632), процесс не завершался; сборка выведена в изолированный ignored output.
- Узкие проверки: artifacts\task2-build\out\Release\net10.0-windows\ScreenIt.Verification.exe --notices-only → PASS 545 checks, exit 0, два последовательных прогона. Отчёт artifacts/notices-verification.json, PNG artifacts/notices/clear-*.png и toast-*.png.
- До фикса та же проверка падала: "First visible frame is already centered information \\.\DISPLAY1@120 rect=PxRect { X = 0, Y = 0, Width = 650, Height = 345 } work=... 3440x1380" — это и есть доказательство бага и того, что регрессия ловится тестом.
- После фикса: first visible frame 1395,517,650,345 = центр work area; «first rendered frame already final», «no visible reposition after first frame» (frames=1); width 650 при dpi=120 (520 DIP * 1.25); long RU/EN → 650x436; Clear dialog → 650x390; modal discard → 650x345, все кадры финальные.
- Дополнительно: hidden prompt HWND отдаёт actual DPI каждого монитора (DISPLAY1 120, DISPLAY2 96, DISPLAY3 96 с отрицательным origin), Centered проверен для scale 1.0/1.25/1.5/2.0 и work area каждого монитора, а также для отрицательного origin, узкой work area (clamp) и 1x1 frame.
- Не запускались: полный suite и Smoke.ps1 (вне разрешённого scope, меняют clipboard/settings/hotkeys), реальная визуальная приёмка пользователем.

Ограничения и риски.
- Live-проверка первого кадра выполняется на мониторе, который реально владеет foreground (здесь DISPLAY1 125%): Windows foreground lock не позволяет тестовому процессу выбирать монитор. DISPLAY2/DISPLAY3 (96%, отрицательный origin) покрыты общей математикой Centered и проверкой actual DPI скрытого prompt HWND, но не живым первым кадром — это не физическая DPI-приёмка.
- Визуальная приёмка (реальный глаз пользователя, mixed-DPI перетаскивание, IME/High Contrast, реальный receiver) остаётся за пользователем; автоматика подтверждает только rect/размеры/DPI в текущем окружении.
- Сборка и отчёты сделаны в artifacts\task2-build\out\ (ignored) из-за блокировки обычного bin\Release запущенной пользовательской копией; после закрытия пользовательской копии обычный dotnet build повторится без изменений.
- Полный suite/Smoke не запускались по условиям задачи; Clear guards/opt-out и paste/session semantics не изменялись, поэтому их регрессия не проверялась этим прогоном.

Этап: готово к пользовательской проверке после независимого ревью Sol. Статус In Progress до визуальной приёмки.

Финальный этап: принято пользователем. Ожидание визуальной приёмки снято явным подтверждением соответствия результата замыслу. Sol переводит задачу в Done; прежние этапы сохранены как история.
<!-- SECTION:NOTES:END -->

## Comments

<!-- COMMENTS:BEGIN -->
author: @flash-worker
created: 2026-11-12T20:40:00Z

---

Взял TASK-2 в работу (@flash-worker, In Progress). Исследовал lifecycle PromptWindow и все call sites, зафиксировал baseline копии затрагиваемых файлов и git diff в approved TEMP, записал план в задачу. Работаю в разрешённом scope: позиционирование PromptWindow в App, узкие notices-проверки и точечные docs; без commit/push, без завершения пользовательского ScreenIt и без изменений clipboard/settings/hotkeys.

---

author: @flash-worker
created: 2026-11-12T20:40:00Z

---

Готово к проверке Sol. Этап: готово к проверке Sol.
Корневая причина: первый видимый кадр PromptWindow оказывался в work origin, потому что Prepare ставил HWND 1x1 в угол, а центрирование выполнялось только в ContentRendered.
Решение: Prepare оставляет окно скрытым, снимает actual DPI целевого монитора и ставит финальный центрированный physical frame до Show; ContentRendered-коррекция стала no-op; общая математика Centered().
Изменены файлы (delta против baseline, baseline НЕ включает мои правки): src/ScreenIt.App/UtilityUi.cs, verification/ScreenIt.Verification/NoticeChecks.cs, ARCHITECTURE.md, CHANGELOG.md, DEVELOPMENT.md.
Проверки: build (изолированный output, 0 warnings/0 errors) и --notices-only PASS 545 checks, exit 0, дважды; до фикса та же проверка падала с rect (0,0,650,345) вместо центра (1395,517). Отчёт artifacts/notices-verification.json и PNG artifacts/notices/*.png. Полный suite/Smoke не запускались; live первый кадр проверен на foreground-мониторе (125%), остальные мониторы — математикой и actual DPI probe. Визуальная приёмка остаётся пользователю. Done не ставлю.

---

author: @sol-orchestrator
created: 2026-11-12T20:42:00Z

---

Ревью Sol: код и пять патчей относительно исходной базы проверены, замечаний нет. Подтверждены вызовы Prepare перед Show и сохранение поведения кнопок. Прочитаны исходный FAIL и итоговый отчёт PASS 545. git diff --check без ошибок. Критерии подтверждены в пределах автоматики и ревью. Готово к пользовательской проверке, оставить In Progress до визуальной приёмки. Живой первый кадр проверен при 125 процентах на основном мониторе, остальные мониторы покрыты математикой и DPI probe. Редкий fallback при нулевом DPI сохраняет позднее центрирование и не проверен. Full suite и Smoke не запускались. Старый процесс не обновлён и не закрывался.

---

author: @sol-orchestrator
created: 2026-11-12T20:46:00Z

---

Пользователь явно подтвердил: исправление соответствует задуманному, и поручил перевести задачу в Done. Визуальная приёмка согласованного результата завершена. Основание закрытия: предыдущее независимое ревью Sol, успешная сборка, PASS 545 узких проверок и текущая пользовательская приёмка. Прежние записи об ожидании приёмки являются историческими. Новые тесты при закрытии не запускались; ограничения покрытия не превращаются в PASS. Задача остаётся на доске, без архивирования. Код, коммиты и публикация при закрытии не изменялись.

---
<!-- COMMENTS:END -->

## Final Summary

<!-- SECTION:FINAL_SUMMARY:BEGIN -->
Завершено и принято пользователем. Общие информационные окна и подтверждения позиционируются до первого показа, без видимого скачка из угла в центр. Сохранены выбор монитора, DPI, поведение кнопок и пассивных toast. Сборка успешна без предупреждений и ошибок; узкие проверки notices: PASS 545. Sol независимо проверил код, delta и отчёты, пользователь подтвердил соответствие результата задуманному. Полный suite и Smoke не запускались; живой первый кадр автоматикой проверен при 125 процентах на foreground-мониторе, остальные мониторы покрыты математикой и DPI probe. IME, High Contrast и редкий fallback при нулевом DPI отдельно не проверены. Пользовательская приёмка не расширяет эти технические свидетельства. При закрытии новые тесты, коммиты и публикация не выполнялись.
<!-- SECTION:FINAL_SUMMARY:END -->
