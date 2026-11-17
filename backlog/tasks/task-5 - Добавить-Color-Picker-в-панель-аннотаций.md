---
id: TASK-5
title: Добавить Color Picker в панель аннотаций
status: Done
assignee:
  - '@flash-worker'
created_date: '2026-11-17T09:12:00Z'
updated_date: '2026-11-17T11:35:00Z'
labels: []
dependencies: []
priority: medium
type: feature
ordinal: 5000
---

## Description

<!-- SECTION:DESCRIPTION:BEGIN -->
Кнопка палитры с индикатором текущего цвета между инструментами и Undo/Redo. Popup по референсам пользователя: заголовок, восемь пресетов (красный, жёлтый, зелёный, бирюзовый, синий, фиолетовый, белый, чёрный), разделитель, SV-поле, hue-полоса, поля R/G/B и образец. Classic/Ember и Dark/Light, RU/EN, короткие конечные анимации с reduced motion. Цвет применяется к новым аннотациям; существующие объекты не перекрашивать. Рабочий цвет хранится в RAM между снимками процесса, без изменения preferences. До явного выбора сохранить штатные цвета оформлений. Метки согласовать с выбранным цветом без изменения текста, номеров и геометрии. Scope: annotation UI/model/render/runtime, scoped verification/docs. Сохранить contracts AGENTS. Не менять interop, updater/installer, версии, release artifacts, OpenCode. Не завершать пользовательский ScreenIt, не трогать реальные clipboard/settings/hotkeys. Без commit/push/merge/release. Ветка codex/annotation-color-picker, исходное дерево чистое. TASK-1 и TASK-3 содержат предыдущие результаты; их не менять. Backlog является источником задания, плана и evidence; обязательный onboarding. При неоднозначности семантики обратиться к Sol до правок зависимой части.
<!-- SECTION:DESCRIPTION:END -->

## Acceptance Criteria
<!-- AC:BEGIN -->
- [x] #1 Кнопка и Popup соответствуют референсам в Classic/Ember, Dark/Light и RU/EN; помещаются в узком viewport и доступны с клавиатуры.
- [x] #2 Пресеты, SV, hue и RGB синхронизированы; 0/255, пустой и невалидный ввод, чёрный/белый и быстрые изменения безопасны.
- [x] #3 Новые Marker/Arrow/Rectangle/Pencil используют выбранный цвет одинаково в gesture/draft и bitmap; старые объекты и Undo/Redo сохраняют собственный цвет.
- [x] #4 Popup не пропускает input в рисование/commit; Escape сначала закрывает picker; внешнее нажатие и закрытие overlay безопасны, shortcuts вне picker сохранены.
- [x] #5 Цвет хранится в RAM между снимками; штатные цвета сохранены до выбора; preferences не меняются.
- [x] #6 Плавные конечные анимации учитывают reduced motion и безопасны при быстром reopen/close и закрытии overlay.
- [x] #7 Release build и scoped checks выполнены; PNG обоих оформлений/тем и актуальные docs подготовлены; evidence и manual/DPI ограничения записаны.
- [x] #8 Клавиша C без модификаторов открывает picker в режиме аннотаций, не перехватывает ввод комментария или RGB; подсказка у кнопки показывает C.
- [x] #9 SV/hue/RGB редактируют только локальный кандидат; нажатие мини-превью применяет цвет и закрывает picker. Пресеты применяются сразу; Escape и внешний клик сохраняют прежний рабочий цвет.
- [x] #10 RGB цифры вертикально центрированы в Classic/Ember и Dark/Light; мини-превью доступно с клавиатуры и входит в циклическую Tab-навигацию.
- [x] #11 Ввод и вставка числа выше 255 в любой RGB канал дают 255 без усечения последней цифры; 0, 255, пустой и невалидный ввод обрабатываются безопасно.
<!-- AC:END -->

## Implementation Plan

<!-- SECTION:PLAN:BEGIN -->
1. Контракт изменён пользователем: кастомный SV/hue/RGB — локальный кандидат до явного нажатия square preview; пресеты применяются сразу; Escape/outside/deactivation/close кандидат не применяют. C (без modifiers) открывает picker только в Editable annotation mode вне Editing; в комментарии/RGB/popup не перехватывается; tooltip/automation содержат C.
2. AnnotationColorPicker: убрать live apply из plane/hue/RGB; preview сделать настоящей Button (tooltip/automation «Apply color»/«Применить цвет», cursor/focus feedback, Enter/Space через Button, UIA Invoke), Click применяет candidate к Coordinator и закрывает popup. RGB Enter не применяет/не закрывает. Пресеты: immediate apply + dismiss. FocusOrder = 8 пресетов, SV, hue, R, G, B, preview (14, cycle).
3. RGB: вертикальное центрирование (VerticalContentAlignment + проверка caret/actual template); снять MaxLength; clamp >255 для ввода/вставки без усечения цифры, длинные/overflow цифровые строки безопасно → 255; invalid/пусто/negative не меняют candidate; не перезаписывать валидный ввод (caret).
4. Overlay: C key в annotation mode (не Editing, не picker) → ToggleColorPicker; tooltip/automation «Annotation color (C)». Остальной routing/commit/Escape hierarchy не менять.
5. Tests: обновить старые live-apply assertions на candidate-vs-working; regressions C no-edit/no-toggle, preset immediate, preview click+keyboard/UIA apply, cancel/Escape/outside/interrupt без применения, candidate не меняет draft/Undo, Tab14 containment, RGB clamp/overflow/paste/invalid/0/255/alignment.
6. Verification: Release build, --annotation-color-only с обновлёнными regressions, --annotation-toolbar-only и при необходимости palette/pencil/comments; PNG artifacts/annotation-color перегенерировать, визуально просмотреть.
7. Docs: README/README.ru, CHANGELOG Unreleased, ARCHITECTURE, DEVELOPMENT и DESIGN — staged custom apply + C, без переписывания history. Baseline текущих затронутых файлов и diff-snapshot — в C:\Users\nenai\AppData\Local\Temp\opencode\task5-baseline2.
8. Ledger после блоков; build/tests/evidence/limitations; TASK-5 остаётся In Progress с этапом «готово к проверке Sol», не Done. Никаких commit/push, real clipboard/settings/hotkeys, пользовательский ScreenIt, TASK-3.
<!-- SECTION:PLAN:END -->

## Implementation Notes

<!-- SECTION:NOTES:BEGIN -->
План записан до правок кода. Локальный .NET SDK 10.0.404 установлен в C:\Users\nenai\AppData\Local\Temp\opencode\dotnet-sdk-10 (машинный SDK 10 отсутствует); сборка и проверки будут с изолированным output. TASK-3/TASK-1 не изменяются.

Sol: проход ses_e17379bbfffeaboUuv6vQ8E1kN остановлен по лимиту. Работа неполна: CS8602, проверки и PNG не выполнены, docs отсутствуют. AC не отмечены. Продолжение той же задачи. TASK-3 не трогать.

Sol: повторный проход ses_e17379bbfffeaboUuv6vQ8E1kN завершён по лимиту. По отчёту исполнителя build PASS 0/0, color PASS 952, palette 20, toolbar 32, pencil 34, comments 11, design-selection 114, marker-editor 466. Эти результаты пока не приняты независимо. PNG и частичные docs есть; DEVELOPMENT/DESIGN, регрессии design/settings/toast и запись evidence не завершены. Карточка отстаёт от диска; работа не принята, следующий проход ограничен завершением документации и evidence.

Фактический прогресс (не принято Sol; evidence исполнителя).

Реализация (файлы на диске):
- src/ScreenIt.Core/Color.cs: AnnotationColor/HsvColor, HSV<->RGB, clamp, Hex.
- src/ScreenIt.Core/Draft.cs: optional Annotation.Color, PencilStroke ctor с цветом, перегрузки CreateMarker/AddArrow/AddBox/AddPencil(color=null по умолчанию).
- src/ScreenIt.App/Painter.cs: цвет в glyph projection, design default (Classic #4255C5 / Ember #F04D32), контрастный halo и текст/контур метки, IsLight/Contrast/Luminance; без явного цвета вывод байт-идентичен прежнему.
- src/ScreenIt.App/MarkerBadgeUi.cs: цветной акцент UI-бейджа, геометрия/hit bounds не изменены.
- src/ScreenIt.App/UiMotion.cs: Reveal/Stop для popover, reduced motion => статичное состояние.
- src/ScreenIt.App/AnnotationColorPicker.cs: popup (заголовок, 8 пресетов, SV, hue, RGB 0-255, preview, notch), адаптивная ширина, тест-симы.
- src/ScreenIt.App/AnnotationOverlay.cs: кнопка палитры между инструментами и Undo, LayoutColorPicker, Esc-приоритет, outside-dismiss, закрытие при InterruptGesture/Deactivated/refresh/close, фиксация цвета жеста.
- src/ScreenIt.App/Coordinator.cs: RAM AnnotationColor?, EffectiveAnnotationColor следует design; preferences не тронуты.
- src/ScreenIt.App/Localization.cs: RU/EN ключи.
- verification/ScreenIt.Verification/AnnotationColorChecks.cs + режим --annotation-color-only и report annotation-color-verification.json (Program.cs).

Проверки (изолированный output artifacts/task5-build/out, локальный SDK 10.0.404 из TEMP):
- dotnet build ScreenIt.sln -c Release -p:BaseOutputPath=...\artifacts\task5-build\out\ => PASS 0 warnings / 0 errors (после последних правок кода; docs изменены позже).
- --annotation-color-only => PASS 952, exit 0.
- --annotation-palette-only => PASS 20; --annotation-toolbar-only => PASS 32; --pencil-only => PASS 34; --comments-only => PASS 11; --design-selection-only => PASS 114; --marker-editor-only => PASS 466 (изолированно; ранее в последовательном профиле был транзиентный FAIL "Sequence contains no elements", повторный изолированный прогон PASS; причина не установлена, к регрессии не отнесено).
- НЕ запускались на этот момент: --design-only, --settings-visual-only, --toast-static, full suite, Smoke, --window-only.
- PNG: artifacts/annotation-color/ (24 picker PNG Classic/Ember x Dark/Light x EN/RU x 1366/640/440, 8 popup PNG, 4 raster PNG), отчёт artifacts/annotation-color-verification.json. Визуально просмотрены popup/overlay/raster; исправлены: нулевой размер пресетов, позиция notch, перекрытие popup при переносе toolbar, границы невыбранных свотчей.
- Логи: C:\Users\nenai\AppData\Local\Temp\opencode\task5-build.log, C:\Users\nenai\AppData\Local\Temp\opencode\task5-color.log.

Ограничения: физический desktop/mixed DPI, реальный mouse capture SV/hue, reduced motion на системе, High Contrast, IME не проверялись; PNG синтетические. TASK-3 не изменялась, пользовательский ScreenIt (PID 23184) не завершался, commit/push не выполнялись.

FINDING Sol (этот проход): AnnotationColorPicker.Sync восстанавливает hue из RGB ToHsv, поэтому при белом/сером/чёрном HueBar.Changed теряет выбранный hue (ToHsv achromatic => hue 0), а drag SV по нижнему краю теряет исходный hue/насыщенность. Нужен отдельный editing HSV state, сохраняющий hue при achromatic и saturation для чёрного (V=0). Проверяю и исправляю в этом же проходе; regression: black/white -> hue blue -> SV вверх => blue без скачка; насыщенный blue -> SV V=0 -> V>0 сохраняет hue/S.

Sol findings исправлены в этом проходе. (1) Hue/saturation state: AnnotationColorPicker теперь хранит editing HSV отдельно от RGB; при achromatic (белый/серый/чёрный) hue сохраняется, для чёрного (V=0) сохраняется и saturation; внешние RGB/preset значения обновляют HSV, но не сбрасывают thumb. Regression добавлены (black/white -> hue 240 -> SV вверх => blue; насыщенный blue -> SV V=0 -> V>0 сохраняет hue/S). (2) Palette icon заменён на узнаваемый artist-palette outline с thumb notch/отверстием и тремя paint-точками; bounds/layout/dot+chevron сохранены. PNG artifacts/annotation-color перегенерированы. --annotation-color-only: PASS 1096 (было 952).

Финальные проверки этого прохода (изолированный output artifacts/task5-build/out, локальный SDK 10.0.404 из TEMP, рабочий каталог C:\Users\nenai\Documents\ScreenIt):
- dotnet build ScreenIt.sln -c Release -p:BaseOutputPath=...\artifacts\task5-build\out\ => PASS, 0 warnings / 0 errors (после последних правок кода; изменены только docs).
- ScreenIt.Verification.exe --annotation-color-only => PASS 1096, exit 0. PNG artifacts/annotation-color/ перегенерированы (24 picker, 8 popup, 4 raster); отчёт artifacts/annotation-color-verification.json.
- --design-only => PASS 374, exit 0 (регрессия toolbar/design после нового контрола).
- --settings-visual-only => PASS 508, exit 0.
- --toast-static => PASS 428, exit 0.
- Ранее в этом же проходе: --annotation-palette-only PASS 20, --annotation-toolbar-only PASS 32, --pencil-only PASS 34, --comments-only PASS 11, --design-selection-only PASS 114, --marker-editor-only PASS 466 (изолированно; единственный транзиентный FAIL "Sequence contains no elements" при последовательном профиле не воспроизвёлся, причина не установлена, к регрессии не отнесён).
- НЕ запускались: full suite, Smoke.ps1, SettingsHoverSmoke, --window-only, --autostart-only, --updates-only, live update check. Причины: desktop/clipboard/hotkeys/settings preconditions и риск для пользовательской копии.
- git diff --check => exit 0 (только предупреждения LF/CRLF). git status: изменены только файлы TASK-5 + docs; карточка TASK-3 изменена Sol ранее и не трогалась. Commit/push не выполнялись.
- Self-review: нет диагностических Trace, временных логов в коде и мусорных using; новый popup не рисует и не коммитит; пользовательский ScreenIt.App.exe (PID 23184) не завершался; фоновых verification-процессов не осталось.

Sol findings:
1. Hue/saturation: исправлено отдельным editing HSV state; regression black/white -> hue 240 -> SV вверх => blue; blue -> V=0 -> V>0 сохраняет hue/S. PASS в --annotation-color-only.
2. Palette icon: заменён на artist-palette outline с thumb notch/отверстием и тремя paint-точками; bounds/layout/dot+chevron сохранены; PNG перегенерированы и просмотрены.

Визуальный просмотр PNG: popup Classic/Ember Dark/Light EN/RU, picker 1366/640/440, raster обоих оформлений; найденные дефекты (размер пресетов, notch, перекрытие popup, границы свотчей, иконка) исправлены до финального прогона.

Этап: готово к проверке Sol (реализация и scoped checks завершены; независимое ревью и пользовательская визуальная приёмка остаются). AC 1–7 отмечены по фактическим evidence. Ограничение AC6: статичное reduced-motion состояние и очистка анимаций проверены синтетически (UiMotion.Reveal(false) + HasActiveMotion=false, 30 быстрых reopen/close), но физическая система с выключенными анимациями Windows не прогонялась.

Ревью Sol (AC4 не принят): AnnotationColorPicker не ограничивает клавиатурную навигацию. Overlay.Keys при открытом picker возвращается без Handled, поэтому Tab из последнего RGB-поля уходит на toolbar/Done, и Enter/Space может активировать внешнюю кнопку. Исправление в этом проходе: KeyboardNavigation.TabNavigation/CtrlTabNavigation=Cycle (+DirectionalNavigation=Contained) на picker, собственный Tab/Shift+Tab handler с циклическим внутренним порядком (8 пресетов -> SV -> hue -> R/G/B), seam для проверки traversal без живых окон. Regression: Tab/CtrlTab не выходят за picker, Enter/Space не меняют инструмент/не коммитят, Ctrl+Enter/Delete/Undo-маршруты не активируют внешние действия, порядок фокуса покрывает все контролы popup. Удаляется неиспользуемое поле colorPickerOffset.

Review repair AC4 (этот проход).
- Код: AnnotationColorPicker: KeyboardNavigation.TabNavigation=Cycle, ControlTabNavigation=Cycle, DirectionalNavigation=Contained; handler PreviewKeyDown на Tab/Shift+Tab циклически ведёт focus по внутреннему порядку (8 пресетов -> SV -> hue -> R -> G -> B); NextFocus/FocusOrder — тот же seam, что использует реальный ввод. Overlay.Keys по-прежнему возвращается до чтения modifiers, поэтому Ctrl+Enter/Ctrl+Z/Ctrl+Y не исполняются при открытом picker. Удалено неиспользуемое поле colorPickerOffset в AnnotationOverlay.
- Regression (в --annotation-color-only): Cycle-свойства; порядок из 13 focus targets и их принадлежность popup; переход last RGB -> первый preset и Shift+Tab обратно; RaiseEvent реального Tab handler с Handled=true; Enter/Space не меняют инструмент, не коммитят и не фокусируют toolbar/Done; Delete с выбранной аннотацией при открытом picker не удаляет её (маркер после проверки удалён). Физические modifier states не имитировались; Ctrl-маршруты проверены структурно (ранний return до modifiers) и ручной desktop-проверкой не закрыты.
- Результаты: build Release PASS 0 warnings/0 errors; --annotation-color-only PASS 1103, exit 0 (было 1096); --annotation-toolbar-only PASS 32, exit 0. PNG перегенерированы тем же color-only прогоном. AC4 восстановлен по этим evidence.

Sol: независимый финальный color-only PASS 1103, exit 0. Код и PNG проверены. Исправления приняты. Готово к пользовательской проверке; In Progress до приёмки.

Уточнение пользователя после визуальной приёмки: добавить C для вызова picker; кастомный SV/hue/RGB цвет сначала редактируется локально и применяется только нажатием мини-превью справа снизу (закрывает popup). Preset по-прежнему применяется сразу. Escape/внешнее закрытие не применяет кандидат. Выровнять RGB цифры вертикально по центру. Значения выше 255 ограничивать до 255, а не отбрасывать последнюю цифру; ввод и вставка больших чисел безопасны. Это новый согласованный контракт вместо прежнего live-применения кастомного цвета. Продолжаем TASK-5 в той же ветке; существующий diff является сохраняемой базой.

Новый контракт пользователя (AC8-11) принят в план; старый live custom apply и RGB Enter закрытие переопределены staged candidate + preview Apply. Базовый dirty diff сохраняется; перед правками снимаю baseline затронутых файлов в task5-baseline2.

Пользовательский контракт AC8–11 реализован (этот проход).
- C: в AnnotationOverlay.Keys добавлен только для Editable annotation mode вне Editing и вне открытого picker (ранние ветки возвращаются раньше); tooltip/automation кнопки — «Annotation color (C)». В комментарии C остаётся текстом (Keys возвращает без Handled), в RGB/popup не переключает.
- Staged custom apply: SV/hue/RGB больше не вызывают Coordinator. Квадратное превью — настоящая Button (44×44, SwatchTemplate focus/hover ring, cursor Hand, ToolTip и AutomationProperties «Apply color»/«Применить цвет», UIA Invoke pattern, Enter/Space через ButtonBase), Click применяет candidate и закрывает popup. Пресеты применяются сразу; Escape, внешний клик, InterruptGesture и закрытие candidate не применяют. RGB Enter больше не применяет/не закрывает. FocusOrder = 8 пресетов + SV + hue + R + G + B + preview = 14, cycle сохранён.
- RGB: VerticalContentAlignment=Center; убран MaxLength; TryChannel клампит >255 (в т.ч. длинные цифровые строки/overflow >int) до 255 без усечения, 0/255/пусто/невалидное/negative безопасны; валидный ввод не перезаписывается. Candidate и текст синхронизированы.
- Regression обновлены: candidate-vs-working на всех SV/hue/RGB путях, C open/no-toggle/no-edit, preset immediate, preview pointer+keyboard+UIA pattern, Escape/outside/interrupt cancel, Tab14, clamp/overflow/empty/invalid, vertical alignment через GetRectFromCharacterIndex.

Проверки (output artifacts/task5-build/final-out; пользовательский ScreenIt.App.exe PID 24964 из artifacts/task5-build/out не завершался, поэтому финальная сборка в отдельный output):
- build Release PASS 0 warnings / 0 errors.
- --annotation-color-only PASS 1515, exit 0 (5 последовательных PASS). Один промежуточный FAIL «Hue keyboard edits only the candidate and RGB» не воспроизвёлся в 5 прогонах; причина не установлена (похоже на внешнее состояние focus/modifiers), к регрессии не отнесён.
- --annotation-toolbar-only PASS 32; --annotation-palette-only PASS 20.
- PNG artifacts/annotation-color/ перегенерированы; визуально проверены popup Ember Light/Classic Dark RU и picker Ember Dark 1366 — preview-кнопка, центрирование цифр и notch на месте.
- Логи: C:\Users\nenai\AppData\Local\Temp\opencode\task5-build-final.log, task5-color-final-1.log, task5-color-final-2.log.
- Baseline/delta: C:\Users\nenai\AppData\Local\Temp\opencode\task5-baseline2 (baseline copies + working-diff-baseline.patch + git status), final\ (итоговые copies), delta-*.patch, working-diff-final.patch, git-status-final.txt. git diff --check exit 0.
- Docs обновлены: README/README.ru, CHANGELOG, ARCHITECTURE, DEVELOPMENT, DESIGN.

Этап: готово к проверке Sol (AC1–11 отмечены по evidence; независимое ревью и пользовательская приёмка остаются). Контракт AC8–11: C в annotation mode, staged custom candidate только через кнопку «Применить цвет», вертикальное центрирование RGB и clamp >255. Ограничения: физический desktop/mixed DPI, real mouse capture, HC/IME, системный reduced-motion, физические modifier states и actual focus traversal не прогонялись; один промежуточный hue-assertion FAIL не воспроизвёлся в 5 PASS.

Review repair AC11 (этот проход).
- RgbChanged больше не использует короткое ||: каждый канал парсится/нормализуется независимо (TryChannel + ClampField) до проверки «все валидны»; overflow клампится даже при пустом/невалидном соседнем поле, candidate меняется только когда все три канала валидны. NormalizeChannel при overflow=true также вызывает ClampField.
- C: условие изменено на Keyboard.Modifiers==ModifierKeys.None (учитывает Win и любые модификаторы, не только Ctrl/Shift/Alt).
- Regression: матрица для R/G/B overflow при пустом/невалидном соседнем канале (Green 999 при пустом Red, Red 999 при пустом Blue), candidate остаётся прежним до полной валидности, затем clamped candidate; подтверждение через Apply preview (owner=(255,100,200), popup закрыт); обычные valid/0/255/пусто/невалидные проверки сохранены.
- Preview keyboard: pointer activation проверена; клавиатурная активация подтверждена структурно (native Button, Focusable/IsTabStop, UIA Invoke pattern, 14-й Tab-stop) — синтетический Enter на скрытом окне не даёт достоверного focus и не используется как focus regression; actual desktop Enter/Space остаётся manual.
- Результаты: build Release PASS 0 warnings/0 errors (final-out); --annotation-color-only PASS 1683, exit 0, 4 последовательных прогона; git diff --check exit 0.
- Обновлены baseline delta (task5-baseline2/final, delta-*.patch, working-diff-final.patch, git-status-final.txt) и логи (task5-build-final.log, task5-color-final-6.log).
<!-- SECTION:NOTES:END -->

## Comments

<!-- COMMENTS:BEGIN -->
author: @flash-worker
created: 2026-11-17T09:13:00Z

---

Беру TASK-5 в работу (@flash-worker). Ветка codex/annotation-color-picker, исходное дерево чистое (только untracked карточка). Изучаю AGENTS, README/ARCHITECTURE/DEVELOPMENT/CHANGELOG и текущую реализацию annotation UI/painter; план запишу до правок кода. TASK-3 и TASK-1 не трогаю.

---

author: @flash-worker
created: 2026-11-17T10:20:00Z

---

Готово к проверке Sol (@flash-worker). Implementation/evidence в notes: build PASS 0/0; --annotation-color-only PASS 1096; --design-only PASS 374; --settings-visual-only PASS 508; --toast-static PASS 428; palette 20, toolbar 32, pencil 34, comments 11, design-selection 114, marker-editor 466 (изолированно). PNG artifacts/annotation-color/, JSON artifacts/annotation-color-verification.json. Исправлены findings hue/saturation и palette icon. Full suite/Smoke/--window-only не запускались; физический DPI/mouse capture/HC/IME и reduced-motion на системе не проверены. TASK-3 не тронута, commit/push нет. Статус In Progress, Done не ставится.

---

author: @sol-orchestrator
created: 2026-11-17T10:22:00Z

---

Ревью Sol: независимый color-only PASS 1096, exit 0; просмотрены код, docs, build log и PNG. Требуется исправление AC4: picker не ограничивает Tab navigation. Keys возвращается без Handled, поэтому Tab из последнего RGB-поля может перевести focus на toolbar/Done; Enter или Space активирует внешнюю кнопку при открытом picker. Задать циклическую Tab/CtrlTab-навигацию внутри picker и проверку, что focus не выходит к Done/инструментам при Tab/ShiftTab; Enter/Space работают только на текущем контроле popup. Проверить CtrlEnter/Undo/Delete с открытым picker, не менять системные modifier keys в тесте. AC4 до исправления не принят. Hue и artist-palette findings исправлены.

---

author: @flash-worker
created: 2026-11-17T10:26:00Z

---

AC4 review repair готов (@flash-worker). Добавлены Tab/CtrlTab Cycle + Directional Contained и собственный циклический Tab/Shift+Tab handler внутри popup; regression проверяет порядок/цикличность traversal, принадлежность всех focus targets popup, перехват Tab реальным handler, отсутствие активации toolbar/Done через Enter/Space и защиту Delete. Неиспользуемый colorPickerOffset удалён. build PASS 0/0; --annotation-color-only PASS 1103; --annotation-toolbar-only PASS 32. AC4 восстановлен по evidence; физические modifier states и actual desktop focus traversal остаются manual. Статус In Progress, готово к проверке Sol, Done не ставится.

---

author: @sol-orchestrator
created: 2026-11-17T10:28:00Z

---

Независимое ревью принято. Color-only PASS 1103. Готово к пользовательской проверке; ограничения сохранены в итоговой сводке.

---

author: @flash-worker
created: 2026-11-17T11:07:00Z

---

AC8–11 готово к проверке Sol (@flash-worker). C открывает picker только в annotation mode вне Editing и не перехватывается в комментарии/RGB/popup; tooltip/automation содержат C. Кастомный цвет staged: применяется только кнопкой «Применить цвет» (Button, Enter/Space/UIA), пресеты сразу, Escape/outside/interrupt не применяют; RGB Enter не подтверждает; preview — 14-й Tab-stop. RGB цифры вертикально центрированы; >255 и длинные числовые строки клампятся до 255 без усечения, 0/255/пусто/невалидное безопасны. build PASS 0/0; --annotation-color-only PASS 1515 (5×); toolbar 32; palette 20. PNG artifacts/annotation-color/; final output artifacts/task5-build/final-out (старый out занят пользовательским ScreenIt PID 24964, не завершался). Baseline/delta TEMP/opencode/task5-baseline2. Docs обновлены. AC2/4/7 восстановлены по evidence; AC8–11 отмечены. Один промежуточный hue-assertion FAIL не воспроизвёлся (риск отмечен). Статус In Progress, Done не ставится.

---

author: @sol-orchestrator
created: 2026-11-17T11:09:00Z

---

Ревью Sol: независимый color-only PASS 1515. Найден край AC11: RgbChanged возвращается до ClampField при пустом/невалидном другом канале. Например R пустой, G=999: G не нормализуется, а NormalizeChannel считает overflow валидным. Нормализовать каждый цифровой канал независимо до проверки полного RGB; candidate менять только когда все каналы валидны. Добавить regression с пустым соседним полем и overflow в каждом канале.

---

author: @flash-worker
created: 2026-11-17T11:16:00Z

---

AC11 review repair готов (@flash-worker). Per-channel normalize: overflow >255 клампится независимо от пустого/невалидного соседа; candidate меняется только при трёх валидных каналах; NormalizeChannel overflow исправлен. C требует Keyboard.Modifiers==None (Win учтён). Regression: каждый канал 999 с пустым соседом, candidate неизменен, затем clamped candidate и применение через preview; build PASS 0/0; --annotation-color-only PASS 1683 (4×); diffcheck 0. Delta/логи обновлены в TEMP/opencode/task5-baseline2 и task5-*-final*.log. Preview keyboard подтверждён структурно (native Button/UIA/Tab); actual Enter/Space/focus — manual. Остальные реализации не менялись, статус In Progress, готово к проверке Sol, Done не ставится.

---

author: @sol-orchestrator
created: 2026-11-17T11:17:00Z

---

Sol: доработки C, staged Apply, RGB alignment и независимый clamp приняты после просмотра delta, тестов, docs, build log и PNG. Независимый финальный color-only PASS 1683, exit 0; diff check PASS. AC1-11 подтверждены в рамках synthetic evidence. Готово к пользовательской проверке, In Progress до приёмки. Физические Enter/Space, focus, DPI и mouse capture остаются manual; прежние транзиентные failures не скрыты.

---
<!-- COMMENTS:END -->

## Final Summary

<!-- SECTION:FINAL_SUMMARY:BEGIN -->
Добавлен выбор цвета аннотаций. Компактная кнопка палитры (artist-palette outline, точка рабочего цвета, chevron) стоит между инструментами и Undo в обоих оформлениях; popup с notch содержит восемь пресетов, SV-поле, полосу оттенка, поля R/G/B 0–255 и образец. Classic использует системную типографику и синий accent, Ember — Commissioner/Signal. RU/EN, Dark/Light, адаптивная ширина 320–420 DIP, клампинг в viewport, клавиатурный доступ и notch по кнопке проверены на 1366/640/440.

Цвет применяется только к новым Marker/Arrow/Rectangle/Pencil одинаково в gesture/draft и итоговом bitmap; старые аннотации сохраняют собственный цвет при Undo/Redo/Delete/перемещении. Без явного выбора прежние палитры оформлений сохраняются байт-идентично. Выбранный цвет метки становится её акцентом с сохранением формы, номера, геометрии и читаемости; comment body не растеризуется. Рабочий цвет хранится только в RAM и переживает новые снимки; preferences/schema не изменены.

Popup изолирован от рисования и commit: Escape сначала закрывает его, RGB-поля не запускают инструменты/Delete/Undo/commit, внешний клик закрывает без аннотации, InterruptGesture/потеря capture/закрытие overlay корректно освобождают состояние. Анимация появления конечная, при отключённых анимациях Windows — статичная.

Исправлены findings Sol: (1) hue/saturation больше не сбрасываются при белом/сером/чёрном — editing HSV хранится отдельно от RGB, saturation сохраняется при V=0; добавлены regression black/white→blue и blue→V=0→V>0. (2) Иконка кнопки заменена на узнаваемую artist-palette с thumb notch и paint-точками.

Проверки (изолированный output artifacts/task5-build/out, локальный SDK 10.0.404 из TEMP): build Release PASS 0/0; --annotation-color-only PASS 1096; --design-only PASS 374; --settings-visual-only PASS 508; --toast-static PASS 428; --annotation-palette-only PASS 20; --annotation-toolbar-only PASS 32; --pencil-only PASS 34; --comments-only PASS 11; --design-selection-only PASS 114; --marker-editor-only PASS 466 (один транзиентный FAIL при последовательном профиле не воспроизвёлся). git diff --check exit 0.

PNG: artifacts/annotation-color/ (picker/popup Classic/Ember × Dark/Light × EN/RU × 1366/640/440, raster-образцы); отчёт artifacts/annotation-color-verification.json; build log C:\Users\nenai\AppData\Local\Temp\opencode\task5-build.log, color log task5-color.log. Docs обновлены: README.md, README.ru.md, CHANGELOG Unreleased, ARCHITECTURE, DEVELOPMENT (режим --annotation-color-only), DESIGN.

Ограничения: не проверялись физический desktop и mixed DPI, реальный mouse capture SV/hue, High Contrast, IME и система с выключенными анимациями; PNG синтетические. Full suite, Smoke, SettingsHoverSmoke, --window-only, --autostart-only, --updates-only и live update не запускались (preconditions/desktop, риск для пользовательской копии). Пользовательский ScreenIt не завершался; TASK-3 не изменялась; commit/push не выполнялись. Независимое ревью Sol и пользовательская визуальная приёмка остаются; задача остаётся In Progress, не Done.

Review repair AC4 (keyboard containment): AnnotationColorPicker получил KeyboardNavigation.TabNavigation/CtrlTabNavigation=Cycle и DirectionalNavigation=Contained, плюс собственный Tab/Shift+Tab handler, циклически перемещающий focus внутри popup (8 пресетов -> SV -> hue -> R/G/B). Regression проверяет порядок и цикличность traversal, что все focus targets принадлежат popup (не toolbar/Done), перехват Tab через реальный input handler, а Enter/Space при открытом picker не активируют внешние кнопки; Delete с выбранной аннотацией не удаляет её. Ctrl+Enter/Ctrl+Z маршруты не исполняются, потому что overlay возвращается до чтения modifiers; физическое состояние модификаторов в тесте не имитировалось. Неиспользуемое поле colorPickerOffset удалено.

Доработки пользователя (AC8–11): `C` без модификаторов открывает picker только в Editable annotation mode вне редактирования комментария; tooltip/automation кнопки показывают C. В тексте комментария, RGB-полях и открытом popup C остаётся обычным вводом. Кастомный SV/hue/RGB теперь только локальный кандидат: он не меняет рабочий цвет до нажатия квадратной кнопки «Применить цвет» (доступная Button, ToolTip/automation, cursor/focus feedback, Enter/Space, UIA Invoke), которая применяет цвет и закрывает popup. Пресеты применяются сразу; Escape, внешний клик, deactivation и закрытие кандидат не применяют; RGB Enter только редактирует. Мини-превью включено в циклическую Tab-навигацию как 14-й контрол. Цифры R/G/B вертикально центрированы, а ввод и вставка значений выше 255 (включая очень длинные числовые строки и overflow >int) клампятся до 255 без усечения; пустой/невалидный ввод безопасен. Hue/saturation editing state сохранён.

Проверки: build Release PASS 0/0 (output artifacts/task5-build/final-out — старый artifacts/task5-build/out занят запущенным пользователем ScreenIt.App.exe PID 24964, который не завершался); --annotation-color-only PASS 1515 (5 последовательных прогонов; один промежуточный FAIL hue-assertion не воспроизвёлся, причина не установлена); --annotation-toolbar-only PASS 32; --annotation-palette-only PASS 20. PNG artifacts/annotation-color/ обновлены и просмотрены. Baseline/delta: C:\Users\nenai\AppData\Local\Temp\opencode\task5-baseline2 (baseline + final copies, delta-*.patch, working-diff-final.patch). Логи: TEMP/opencode/task5-build-final.log, task5-color-final-1.log, task5-color-final-2.log.

Ограничения прежние: физический desktop/mixed DPI, реальный mouse capture SV/hue, High Contrast, IME, системный reduced-motion, физические modifier states и actual focus traversal не прогонялись; PNG синтетические. Full suite/Smoke/--window-only/--settings-visual-only/--toast-static/--design-only в этом проходе не повторялись (не затронуты контрактом; ранее PASS). TASK-3 не изменялась, commit/push нет. Задача остаётся In Progress, готово к проверке Sol; Done только Sol.

AC11 review repair: каждый RGB-канал теперь парсится и нормализуется независимо, поэтому значения выше 255 клампятся до 255 даже при пустом или невалидном соседнем поле; candidate меняется только после того, как все три канала валидны, и подтверждается кнопкой «Применить цвет». NormalizeChannel также исправляет overflow. C теперь требует Keyboard.Modifiers==ModifierKeys.None (включая Win). Regression-матрица покрывает overflow каждого канала с невалидным соседом, неизменность candidate до полной валидности и применение клампированного кандидата через preview. Проверки: build PASS 0/0; --annotation-color-only PASS 1683 (4 последовательных прогона); git diff --check exit 0. Delta обновлена в C:\Users\nenai\AppData\Local\Temp\opencode\task5-baseline2. Клавиатурная активация preview подтверждена структурно (native Button/UIA/Tab); реальный Enter/Space и focus traversal на desktop остаются manual.

Итог Sol после доработок: READY_FOR_USER_TESTING. Независимый annotation-color-only PASS 1683, exit 0; просмотрены delta, новые regressions, docs, build log и PNG. AC1-11 приняты в пределах synthetic evidence. Актуальная сборка: artifacts/task5-build/final-out/Release/net10.0-windows/ScreenIt.App.exe. Прежний out не обновлялся, пользовательская копия не завершалась. Ручные keyboard/focus/DPI/mouse capture проверки остаются; статус In Progress до приёмки.

Пользователь принял результат сообщением «Хорошо сделано» и поручил финальный аудит, push в main и подготовку 0.3.1. Пользовательская приёмка завершена. Прежние ограничения evidence сохранены; подготовка релиза ведётся отдельной задачей.
<!-- SECTION:FINAL_SUMMARY:END -->
