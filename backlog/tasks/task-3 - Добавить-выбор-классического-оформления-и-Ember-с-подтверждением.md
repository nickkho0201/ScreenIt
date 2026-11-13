---
id: TASK-3
title: Добавить выбор классического оформления и Ember с подтверждением
status: In Progress
assignee:
  - '@flash-worker'
created_date: '2026-11-12T20:52:00Z'
updated_date: '2026-11-12T21:51:00Z'
labels: []
dependencies: []
priority: medium
type: feature
ordinal: 3000
---

## Description

<!-- SECTION:DESCRIPTION:BEGIN -->
Пользователь хочет сохранить синий интерфейс актуального релиза v0.2.3 как Классический и сделать текущий незакоммиченный CONSTRUCT-редизайн альтернативой Ember. В Основных настройках нужен отдельный выпадающий список оформления, независимый от System/Dark/Light. Выбор сначала показывает предупреждение с подтверждением и только после согласия меняет оформление. Источник классического оформления: git tag v0.2.3. Ember: текущая dirty-база codex/design-refresh с принятыми TASK-1/TASK-2. Существующие изменения пользователя сохранять; перед правками снять baseline для независимого delta-review. Область: appearance/preferences, Settings/localization, UI/render-потребители темы, связанные verification и документация. Не менять Core, capture/paste semantics, input/interop ownership, installer/updater, версии, release assets и OpenCode. Разрешено совместимое добавление поля оформления в preferences с сохранением неизвестных полей и старых настроек; без destructive migration. Не применять изменения к реальным пользовательским settings, clipboard/hotkeys и не закрывать работающий ScreenIt. Без commit/push/deploy. Локальные зависимости и изолированные build/tests разрешены. Backlog является постоянным источником задания, плана и evidence. После самопроверки передать Sol, затем пользователю на визуальную приёмку; не закрывать задачу.
<!-- SECTION:DESCRIPTION:END -->

## Acceptance Criteria
<!-- AC:BEGIN -->
- [x] #1 В Основных есть отдельный список Классический / Ember (Classic / Ember для EN), независимо от System/Dark/Light.
- [x] #2 Классический воспроизводит синий интерфейс релиза v0.2.3 во всех относящихся к оформлению поверхностях; Ember сохраняет текущий редизайн и палитру TASK-1. Семантика flows и исправление TASK-2 сохранены.
- [x] #3 Выбор другого оформления сначала показывает локализованное предупреждение; только явное подтверждение применяет и сохраняет его. Cancel/Escape/X и безопасный Enter ничего не меняют; повторный выбор текущего оформления не вызывает подтверждения.
- [x] #4 Выбор сохраняется между запусками; старые файлы без нового поля получают Классический, неизвестное значение безопасно нормализуется. Старые preferences и неизвестные поля сохранены; ошибка сохранения не рассогласует UI и persisted состояние.
- [x] #5 Сборка и изолированные проверки покрывают оба оформления, режимы, старые файлы, подтверждение/отмену и save failure. Результаты и ограничения записаны; full suite/smoke без безопасных предусловий не запускать.
- [x] #6 Документация RU/EN и Unreleased отражает выбор и подтверждение; подготовлен delta относительно dirty-базы и путь к сборке для ручной проверки.
<!-- AC:END -->

## Implementation Plan

<!-- SECTION:PLAN:BEGIN -->
1. Модель и персистенция: добавить DesignPreference { Classic, Ember } в Preferences (schemaVersion 2, поле design: classic/ember). Отсутствующее/неизвестное значение => Classic; Save сохраняет unknown JSON fields и старые theme/language/hotkeys/annotationModifier/disableClearConfirmation. Coordinator.ChangeDesign: сначала Save, только при успехе применить Appearance.ChooseDesign; при IOException/UnauthorizedAccessException откатить in-memory значение и вернуть false (UI и persisted state не рассогласуются). Program загружает design до первого окна.
2. Палитра/токены: Appearance хранит Design, Classic использует точные палитры v0.2.3 (dark #202733/#F3F5F8/#B0B8C6/#3B4757/#141922/#A6B5FF/#F2B285; light #FFFFFF/#1E293B/#64748B/#CBD5E1/#FFFFFF/#4255C5/#975514) и accent #4255C5; Ember — текущие CONSTRUCT-палитры и Signal. ConstructDesign становится design-aware для шрифтов (Commissioner/Unbounded vs системный), Signal/Acid/Focus/OnAccent/Sidebar/Hover/Pressed; UtilityUi.Accent следует выбранному оформлению. High Contrast остаётся системным в обоих.
3. Поверхности: SettingsWindow — отдельный список «Оформление/Design» (Classic/Ember) в Основных, независимо от System/Dark/Light; смена через UtilityUi.Confirm (Cancel/Esc/X/safe Enter сохраняют старое, no-op без вопроса); classic-раскладка v0.2.3 (780x620, margin 22, sidebar 170, прямые ComboBox, старые hover/pressed/switch-цвета, corner 6) и Ember-раскладка (880x660, карточки, Display, corner 4). AnnotationOverlay: classic toolbar v0.2.3 (● Marker/↗/▭/✎, Done внутри панели, старый редактор, синий selection, Painter-бейджи) и Ember (capture bar, Done dock, MarkerSurface, CommentEditorCard/link). PromptWindow: classic 390 DIP стандартное окно/panel, Ember 520 DIP NoticeSurface. Toast: classic radius 15/старые surface-цвета/Segoe UI, Ember NoticeSurface/radius 4. Painter: Classic #4255C5 + белый контур и старый бейдж, Ember Signal #F04D32 + Paper и новый бейдж; preview и export идут через один Draw.
4. Локализация: Design/Оформление, Classic/Классический, Ember, заголовок/описание/кнопка предупреждения на EN/RU; L.T покрывает новые ключи.
5. Verification: новый режим --design-selection-only и DesignSelectionChecks: persistence (missing/unknown/old/unknown fields/save failure), оба design × Dark/Light палитры и accent, Painter preview/export PNG, Settings (3 списка, RU/EN, подтверждение cancel/accept/no-op, live apply), overlay/prompt/toast/comment classic vs ember, synthetic PNG artifacts/design-selection/. Обновить существующие SettingsChecks (3 ComboBox), DesignChecks/AppearanceChecks/ToastChecks/MarkerEditorChecks под явный выбор design, чтобы full suite оставался валидным; full suite/Smoke не запускать.
6. Docs: README.md/README.ru.md, ARCHITECTURE, CHANGELOG Unreleased, DEVELOPMENT (новый режим), DESIGN (Classic/Ember). Baseline/delta и build в ignored artifacts/task3-build; self-review diff/status; evidence и критерии в Backlog, этап «готово к проверке Sol», не Done.
<!-- SECTION:PLAN:END -->

## Implementation Notes

<!-- SECTION:NOTES:BEGIN -->
Этап: выполнение прервано, реализация неполная. Исполнитель достиг лимита шагов, независимое ревью Sol и пользовательская приёмка не выполнены. По отчёту исполнителя подготовлены ветвления Classic/Ember в Preferences, Appearance, ConstructDesign, UtilityUi, SettingsWindow, AnnotationOverlay, Painter, ToastService, Coordinator, Program и Localization; промежуточная Release-сборка успешна без предупреждений и ошибок в artifacts/task3-build/out. Это сообщение о сборке ещё не подтверждено независимым просмотром лога. Проверки после изменений не запускались; новый набор DesignSelectionChecks и режим design-selection-only не созданы, существующие проверки не полностью адаптированы. Документация не обновлена, delta/self-review не завершены. Все критерии остаются неотмеченными. Baseline: C:\Users\nickk\AppData\Local\Temp\opencode\task3-baseline. Промежуточную сборку не считать готовой к использованию. Следующий шаг: продолжить реализацию проверок, адаптацию regression checks и документации, выполнить изолированные прогоны, затем саморевью и независимое ревью Sol. Пользовательские файлы и изменения не откатывались; задача остаётся In Progress.

Продолжение по поручению пользователя: исполнитель добавил DesignSelectionChecks и режим design-selection-only, сообщил build PASS 0/0 и PASS 98. Sol пока не принимал эти результаты. Исполнитель обнаружил незакрытый дефект: classic toolbar имеет 0x0 и отсутствует на synthetic PNG, несмотря на PASS. Также не завершены адаптация старых checks, документация, удаление Stage-диагностики и delta/self-review. Этот проход вновь достиг лимита шагов; работа остаётся неполной. Следующий шаг: исправить toolbar с проверкой ненулевых размеров/видимости, адаптировать regressions и закончить docs/evidence. Новый набор проверок и JSON присутствуют на диске; PASS не означает приёмку известного дефекта.

Этап: готово к проверке Sol (реализация завершена, независимое ревью и пользовательская визуальная приёмка остаются за Sol/пользователем).

Реализация.
- Preferences: DesignPreference { Classic, Ember }, поле design (classic/ember) в schemaVersion 2. Отсутствующее/неизвестное/нестроковое значение => Classic; старые theme/language/hotkeys/annotationModifier/disableClearConfirmation и unknown JSON fields сохраняются. Save пишет design; при IO/UnauthorizedAccessException Coordinator.ChangeDesign откатывает in-memory значение и не применяет design. Program загружает design до окон.
- Appearance/UiDesign/ConstructDesign: точные палитры Classic dark/light из v0.2.3 (#202733/#F3F5F8/#B0B8C6/#3B4757/#141922/#A6B5FF/#F2B285 и #FFFFFF/#1E293B/#64748B/#CBD5E1/#FFFFFF/#4255C5/#975514), accent #4255C5; Ember — текущие CONSTRUCT-палитры и Signal #F04D32. Classic использует системный шрифт; Ember — Commissioner/Unbounded. High Contrast остаётся системным.
- Settings: отдельный список «Оформление/Design» (Classic/Классический, Ember) в Основных, независимо от темы; RU-подпись темы изменена на «Тема», чтобы не конфликтовать с «Оформление». Смена: UtilityUi.Confirm -> только accept вызывает ChangeDesign (Save, затем Appearance.ChooseDesign); Cancel/Escape/X/safe Enter сохраняют старое; no-op без вопроса. Classic-раскладка 780x620/margin 22/sidebar 170/прямые ComboBox/corner 6/старые switch-цвета; Ember 880x660/карточки/Display/corner 4. При смене design окно перестраивает размер.
- AnnotationOverlay: Classic — toolbar 0.2.3 с ● Marker/↗/▭/✎, Done внутри панели, старый редактор, синий selection, Painter-бейджи, status inlines; Ember — capture bar, split Done dock, MarkerSurface, CommentEditorCard/link. Исправлен реальный дефект: classic toolbar измерялся как 12x12/0x0 из-за stale measure; теперь invalidate tools/panel/bar и размер 548x88 в synthetic 640px.
- PromptWindow: Classic 390 DIP, стандартное окно, plain StackPanel, Cancel default; Ember 520 DIP NoticeSurface. Для Classic PlaceBeforeShow учитывает non-client frame и фиксирует Height/SizeToContent, поэтому первый видимый кадр сразу центрирован (TASK-2). Toast: Classic radius 15/старые surface-цвета/Segoe UI; Ember NoticeSurface/radius 4.
- Painter: Classic #4255C5 + белый контур + старый rounded badge; Ember Signal/Paper + новый badge; preview и export идут через один Draw.
- Локализация: Design/Оформление, Classic/Классический, Switch design?/Сменить оформление?, описание, Switch/Сменить; Appearance/Тема.

Проверки (рабочий каталог C:\Users\nickk\Documents\ScreenIt; локальный SDK 10.0.404 из TEMP; изолированный output artifacts\task3-build\out).
- dotnet build ScreenIt.sln -c Release -p:BaseOutputPath=...\artifacts\task3-build\out\ => EXIT=0, 0 warnings, 0 errors (после последних правок кода; docs менялись после).
- ScreenIt.Verification.exe --design-selection-only => EXIT=0, PASS 98. Отчёт artifacts/design-selection-verification.json; PNG artifacts/design-selection/ (21 файл: settings classic EN/RU Dark/Light, settings ember, overlay/comment classic/ember Dark/Light, painter classic/ember, prompt, toast).
- --notices-only => EXIT=0, PASS 545 (Classic default; first frame 488x227 centered; modal/enter/escape/close/cancel/accept).
- --settings-visual-only => PASS 508; --settings-motion-only => PASS 8; --toast-static => PASS 428; --marker-editor-only => PASS 466; --design-only => PASS 366; --annotation-palette-only => PASS 20; --comments-only => PASS 11; --pencil-only => PASS 33; --annotation-toolbar-only => PASS 32; --clear-preferences-only => PASS 33.
- git diff --check => EXIT=0 (только предупреждения LF/CRLF).
- Full suite и Smoke.ps1 НЕ ЗАПУСКАЛИСЬ: требуют подготовленного desktop/clipboard/settings/hotkeys и меняют пользовательские данные; WindowCaptureChecks (--window-only) не запускался по той же причине.

Baseline/delta.
- Baseline: C:\Users\nickk\AppData\Local\Temp\opencode\task3-baseline (не перезаписывался): копии затрагиваемых файлов + git-status-baseline.txt, git-diff-baseline.patch, git-head.txt.
- Delta: C:\Users\nickk\AppData\Local\Temp\opencode\task3-delta\*.patch (git diff --no-index baseline/current для скопированных файлов), git-status-final.txt, new-files.txt, evidence-png-list.txt, untracked-no-baseline.txt. Baseline не содержал pre-existing untracked NoticeChecks/Discoverability/MarkerEditor/WindowCapture; их точные правки перечислены в untracked-no-baseline.txt. Новый файл задачи — verification/ScreenIt.Verification/DesignSelectionChecks.cs.
- Сборка для ручной проверки: C:\Users\nickk\Documents\ScreenIt\artifacts\task3-build\out\Release\net10.0-windows\ScreenIt.App.exe. Пользовательский ScreenIt.App.exe (PID 30288, artifacts\task2-build) не завершался; clipboard/settings/hotkeys не затрагивались.

Ограничения/риски.
- Synthetic PNG и проверки не заменяют ручную визуальную приёмку Classic/Ember на физическом desktop, mixed-DPI, High Contrast, IME и реальном receiver. Classic prompt/overlay проверены на synthetic/скрытых окнах и на foreground-мониторе 125%; физические 150/200% не проверялись.
- --window-only, полный suite и Smoke не запускались; их preconditions не создавались. SettingsChecks/AppearanceChecks/Discoverability/WindowCapture адаптированы под design и компилируются, но в полном suite не прогонялись.
- Пользовательские dirty changes сохранены; commit/push/tag/release/installer не выполнялись. Sol выполняет независимое ревью; пользовательская визуальная приёмка остаётся.

Исправление findings Sol #4 (2026-11-12).
1. SettingsWindow.FontFamily: Refresh() теперь выставляет FontFamily=ConstructDesign.Body при применении дизайна, поэтому открытое окно и его Text()/ComboBox не сохраняют шрифт прежнего оформления. Добавлены проверки CheckSettingsFonts: базовый FontFamily окна, фактический TextBlock «Start with Windows», ComboBox Design; Classic→Ember→Classic и обратно; сравнение с freshly constructed SettingsWindow в текущем дизайне. --design-selection-only: PASS 114 (было 98), включая 10 font assertions.
2. README features RU/EN: строка 0.2.0 оставлена только для System/Dark/Light, языка и shortcuts; Classic/Ember вынесен отдельной строкой с пометкой Unreleased (README.md:27, README.ru.md:29).

Повторные прогоны после исправления: build PASS 0 warnings/0 errors; --design-selection-only PASS 114; --settings-visual-only PASS 508; --settings-motion-only PASS 8; --design-only PASS 366; --marker-editor-only PASS 466; --notices-only PASS 545; --toast-static PASS 428; git diff --check PASS. PNG artifacts/design-selection/ и отчёт design-selection-verification.json перегенерированы; delta-патчи в task3-delta обновлены. Full suite/Smoke/--window-only по-прежнему не запускались; ручная визуальная приёмка остаётся пользователю. AC2 отмечен повторно по новым font-regression evidence; независимая проверка Sol сохраняется.
<!-- SECTION:NOTES:END -->

## Comments

<!-- COMMENTS:BEGIN -->
author: @flash-worker
created: 2026-11-12T20:58:00Z

---

Беру TASK-3 в работу (@flash-worker). Baseline затрагиваемых файлов, git status и git diff сохранены в C:\Users\nickk\AppData\Local\Temp\opencode\task3-baseline до правок. Использую локальный SDK 10.0.404 из TEMP и изолированный output artifacts\task3-build: пользовательский ScreenIt.App.exe (PID 30288) из artifacts\task2-build не завершаю, его clipboard/settings/hotkeys не трогаю. План записан в задачу.

---

author: @sol-orchestrator
created: 2026-11-12T21:04:00Z

---

Проверка передачи Sol: прочитаны фактическая TASK-3, план, комментарии и git status. В ledger отсутствовали результаты промежуточного выполнения; они добавлены как сведения из отчёта исполнителя, а не как подтверждённый PASS. Работа не принята: отсутствуют проверки, актуальная документация и reviewable delta; готовность к пользовательскому тестированию не объявляется. Блокер текущего прохода: лимит шагов исполнителя.

---

author: @flash-worker
created: 2026-11-12T21:45:00Z

---

Готово к проверке Sol (@flash-worker). Реализация и evidence записаны; AC 1–6 отмечены по фактическим прогонам. Build PASS 0/0; --design-selection-only PASS 98; --notices-only PASS 545; --settings-visual-only PASS 508; --settings-motion-only PASS 8; --toast-static PASS 428; --marker-editor-only PASS 466; --design-only PASS 366; --annotation-palette-only PASS 20; --comments-only PASS 11; --pencil-only PASS 33; --annotation-toolbar-only PASS 32; --clear-preferences-only PASS 33; git diff --check PASS. Отчёт/PNG: artifacts/design-selection-verification.json и artifacts/design-selection/. Baseline: C:\Users\nickk\AppData\Local\Temp\opencode\task3-baseline; delta: C:\Users\nickk\AppData\Local\Temp\opencode\task3-delta. Сборка: artifacts\task3-build\out\Release\net10.0-windows\ScreenIt.App.exe. Full suite/Smoke/--window-only не запускались; ручная визуальная приёмка остаётся пользователю. Статус In Progress, Done не ставится.

---

author: @sol-orchestrator
created: 2026-11-12T21:47:00Z

---

Ревью Sol, требуются исправления. 1. SettingsWindow.cs:60 задаёт FontFamily только в конструкторе; Refresh():287 и далее меняет AppliedDesign/размеры, но не FontFamily. Text() и ComboBox наследуют шрифт окна. Поэтому Classic→Ember в уже открытом Settings оставляет системный body-font, а Ember→Classic оставляет Commissioner, вопреки AC2 и сохранению оформления. Исправить обновление базового шрифта при применении дизайна; добавить assertions фактического FontFamily на окне и наследуемых контролах в обе стороны, включая отличие от freshly constructed окна. Пересобрать и повторить design-selection и связанные settings visual checks. 2. README RU/EN features строка про новые Classic/Ember всё ещё помечена 0.2.0: устранить ложное впечатление появления нового списка в выпущенной 0.2.0, явно оставить Unreleased-контекст. Проверено: фактический delta App/settings/overlay/preferences/painter/prompt/toast, новый набор DesignSelectionChecks, PNG classic toolbar и Settings обоих дизайнов, JSON PASS 98. Панель Classic на PNG теперь видна и имеет ненулевой размер. До исправления AC2 не принят; остальные отметки остаются предварительными.

---

author: @flash-worker
created: 2026-11-12T21:49:00Z

---

Исправления по findings #4 (@flash-worker). 1) SettingsWindow.Refresh обновляет FontFamily=ConstructDesign.Body при применении дизайна; добавлены regression assertions на базовый шрифт окна, TextBlock «Start with Windows» и ComboBox Design в Classic→Ember→Classic и обратно, включая сравнение с freshly constructed SettingsWindow. 2) README.md/README.ru.md: Classic/Ember вынесен отдельной строкой Unreleased, 0.2.0 больше не приписывается новому списку. Повторно: build PASS 0/0; --design-selection-only PASS 114; --settings-visual-only PASS 508; --settings-motion-only PASS 8; --design-only PASS 366; --marker-editor-only PASS 466; --notices-only PASS 545; --toast-static PASS 428; git diff --check PASS. PNG/JSON и delta обновлены. AC2 отмечен повторно по новым evidence; независимое ревью Sol сохраняется, Done не ставится. Full suite/Smoke/--window-only не запускались.

---

author: @sol-orchestrator
created: 2026-11-12T21:51:00Z

---

Ревью Sol завершено. Замечания 4 исправлены. Независимый запуск design-selection-only: PASS 114, exit 0. Проверены код, delta, PNG и git diff --check без ошибок. Критерии 1-6 подтверждены в пределах реализации и изолированных проверок. Готово к пользовательскому тестированию. Статус In Progress до визуальной приемки. Full suite, Smoke, window-only и физические DPI не проверены. Старый процесс не обновлялся.

---
<!-- COMMENTS:END -->

## Final Summary

<!-- SECTION:FINAL_SUMMARY:BEGIN -->
Выбор Классический / Ember добавлен в Основные независимо от System/Dark/Light. Перед сменой и сохранением требуется подтверждение; отмена сохраняет оформление. Классический возвращает синий UI v0.2.3, Ember сохраняет текущий редизайн. Старые preferences совместимы, ошибка сохранения откатывает выбор. Исправлены невидимый toolbar, first-frame Classic и шрифт открытого Settings. Ревью Sol выполнено: код, delta, PNG, независимый PASS 114 и git diff --check без ошибок. Release build и связанные scoped проверки успешны по evidence исполнителя. Готово к пользовательскому тестированию, In Progress до визуальной приемки. Full suite, Smoke, window-only, физические DPI, High Contrast, IME и receiver не проверены. Сборка: artifacts/task3-build/out/Release/net10.0-windows/ScreenIt.App.exe. Старый процесс не заменялся; commit/push/deploy не выполнялись.
<!-- SECTION:FINAL_SUMMARY:END -->
