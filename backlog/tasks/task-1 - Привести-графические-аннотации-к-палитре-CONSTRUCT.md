---
id: TASK-1
title: Привести графические аннотации к палитре CONSTRUCT
status: Done
assignee:
  - '@flash-worker'
created_date: '2026-11-12T19:49:00Z'
updated_date: '2026-11-12T20:22:00Z'
labels: []
dependencies: []
priority: medium
type: enhancement
ordinal: 1000
---

## Description

<!-- SECTION:DESCRIPTION:BEGIN -->
Пользователь показал синие стрелку, рамку и карандаш на фоне обновлённого CONSTRUCT. Заменить синий на Signal Red #F04D32 со светлым контуром Paper #FBFEF8, одинаково в preview и итоговом bitmap. Область: renderer/overlay, связанные проверки и точечная документация. Сохранить метки, комментарии, толщины, геометрию, жесты, Undo/Redo/Delete и physical-pixel/fitted-window semantics. Не добавлять controls или persistence. Существующий незакоммиченный редизайн сохранять. Не менять Core, input/interop, installer, версии и OpenCode. Не закрывать пользовательский процесс и не менять clipboard/settings. Не коммитить и не публиковать. SDK разрешён только локально. Backlog является источником задания и результатов.
<!-- SECTION:DESCRIPTION:END -->

## Acceptance Criteria
<!-- AC:BEGIN -->
- [x] #1 Стрелка, рамка и карандаш используют Signal Red со светлым контуром Paper вместо старого синего.
- [x] #2 Gesture preview, draft preview и bitmap согласованы в обеих темах и fitted-window; геометрия и управление сохранены.
- [x] #3 Метки и комментарии не изменены; новые настройки отсутствуют.
- [x] #4 Сборка и связанные проверки выполнены либо конкретные блокеры записаны; подготовлены synthetic примеры на светлом и тёмном фоне.
- [x] #5 Устаревшие обещания сохранения синего цвета исправлены; существующий редизайн сохранён.
<!-- AC:END -->

## Implementation Plan

<!-- SECTION:PLAN:BEGIN -->
1. Инвентаризация пути рендера: Painter.Draw используется и для gesture/draft preview (AnnotationOverlay.Surface.OnRender через VisibleGlyphs), и для экспорта (Painter.Render). Отдельных цветовых настроек нет; менять только палитру stroked-глифов, не Marker/Box/Arrow geometry, не Core и не input.
2. В Painter: AnnotationAccent = Signal Red 240/77/50, контур = Paper 251/254/248 вместо Brushes.White; вынести прежние ширины 2.5/4.5 в константы и использовать для arrow/rectangle/pencil и в preview, и в bitmap. Не связывать с theme-dependent UtilityUi.Accent.
3. Verification: заменить устаревший assert #4255C5 в DesignChecks на точные Signal/Paper/width asserts; добавить синтетический raster-тест (arrow/box/pencil) на Paper и Ink фоне, проверку live gesture preview = committed preview = export по цвету/координатам; PNG-сэмплы в artifacts/annotation-palette. Сохранить существующие checks меток/комментариев/fitted-window.
4. Docs: исправить обещания сохранения синего/прежнего цвета и описание export accent в ARCHITECTURE, README/README.ru (CONSTRUCT), CHANGELOG Unreleased, DESIGN 10.3, DEVELOPMENT, CONSTRUCT-REVIEW; исторические release-записи не переписывать.
5. Сборка локальным SDK 10.0.404 и запуск синтетических --design-only/--pencil-only (без desktop capture, clipboard, settings, hotkeys); full suite/smoke не запускать из-за работающей пользовательской копии. Проверить PNG, сделать self-review git diff/status.
6. Записать evidence, критерии и итог в Backlog; оставить In Progress с этапом «готово к проверке Sol».
<!-- SECTION:PLAN:END -->

## Implementation Notes

<!-- SECTION:NOTES:BEGIN -->
Этап: готово к проверке Sol. Реализация завершена, критерии отмечены по фактическим прогонам; Done не ставится.

Код: Painter.cs задаёт явную export-палитру Signal Red #F04D32 (240,77,50) с контуром Paper #FBFEF8 (251,254,248); ширины сохранены как AnnotationStrokeWidth=2.5 / AnnotationOutlineWidth=4.5. Контур Brushes.White заменён на Paper для arrow, rectangle и pencil. Painter.Draw остаётся единственным путём для gesture/draft preview (AnnotationOverlay.Surface.OnRender → VisibleGlyphs) и экспорта (Painter.Render), поэтому preview и bitmap согласованы без второго renderer-а. Geometry, hit testing, жесты, Undo/Redo/Delete, Marker/Badge, комментарии, SourceScale/fitted-window и Core/input/interop/settings не менялись.

Verification: DesignChecks заменяет устаревший assert #4255C5 точной проверкой палитры/ширин и добавляет метод AnnotationPalette. Проверяются точные RGB, независимость от Light/Dark, отсутствие синих пикселей, Paper halo на тёмном фоне, совпадение live gesture → committed draft → bitmap, а также synthetic fitted-window через ChooseBitmap (960×640 source на 480×320 monitor): SourceScale<1, полное физическое разрешение экспорта, красный gesture preview и та же палитра в bitmap. Добавлен узкий режим --annotation-palette-only и отчёт artifacts/annotation-palette-verification.json. PNG: artifacts/annotation-palette/arrow-box-pencil-light.png и -dark.png; визуально просмотрены.

Фактические команды (рабочий каталог C:\Users\nickk\Documents\ScreenIt, локальный SDK 10.0.404 из TEMP, не глобальный): dotnet build ScreenIt.sln -c Release → EXIT=0, 0 warnings / 0 errors; ScreenIt.Verification.exe --annotation-palette-only → EXIT=0, PASS 20; --design-only → EXIT=0, PASS 366; --pencil-only → EXIT=0, PASS 33; --comments-only → EXIT=0, PASS 11. Palette-only и design-only перезапущены после последнего изменения кода; pencil/comments прогоны выполнены до последней правки только DesignChecks (их код не затрагивался).

Документация: ARCHITECTURE (export-палитра и независимость от темы), README.md/README.ru.md (CONSTRUCT вместо обещания сохранения цветов), CHANGELOG Unreleased, DESIGN §2.4/§10.3/§13, DEVELOPMENT (design-only и --annotation-palette-only), CONSTRUCT-REVIEW (историческая запись + раздел Annotation palette 2026-11-12). Исторические release-записи 0.2.3 не переписаны.

Ограничения: full suite и Smoke.ps1 НЕ ЗАПУСКАЛИСЬ, потому что пользовательский ScreenIt работает из artifacts/construct-preview-20261108-notice-details; реальный Windows.Graphics.Capture fitted-window прогон и ручная приёмка на физических мониторах/DPI НЕ ВЫПОЛНЕНЫ. Fitted-window покрыт только synthetic ChooseBitmap-проверкой без захвата desktop. Физические 150%/200%, High Contrast, реальный receiver и субъективная визуальная приёмка остаются за Sol/пользователем. Пользовательский процесс, clipboard, settings и hotkeys не затрагивались; commit/push/tag/release/installer не выполнялись. Пользовательская dirty-база редизайна сохранена; мой delta ограничен перечисленными файлами.

Уточнение по окружению: на момент финальной записи процесс ScreenIt.App.exe в списке не обнаружен; я его не завершал, не перезапускал и не менял его clipboard/settings. Full suite и Smoke.ps1 не запускал: они меняют clipboard/settings и требуют подготовленного desktop, а brief ограничивает такие прогоны при работающей пользовательской копии. Эти проверки остаются NOT RUN, а не PASS. Реальный WGC fitted-window и ручная приёмка также NOT RUN.

Этап: готово к пользовательской проверке. Sol проверил diff Painter относительно исходного редизайна, новые проверки и отчеты PASS 20 и 366. Просмотрены PNG на светлом и темном фоне. Геометрия и метки сохранены. git diff --check без ошибок. Реальная визуальная приемка остается за пользователем, статус In Progress. Старый portable preview не обновлен. Новая сборка: src/ScreenIt.App/bin/Release/net10.0-windows/ScreenIt.App.exe.

Финальный этап: принято пользователем, разрешен перевод в Done. Предыдущие записи об ожидании проверки являются историческими. Явная приемка пользователя и независимое ревью Sol достаточны для закрытия согласованного объема; невыполненные дополнительные сценарии остаются ограничениями evidence.
<!-- SECTION:NOTES:END -->

## Comments

<!-- COMMENTS:BEGIN -->
author: @flash-worker
created: 2026-11-12T19:53:00Z

---

Беру задачу в работу. Контекст: пользовательская копия ScreenIt запущена из artifacts/construct-preview-20261108-notice-details; её не завершаю, clipboard/settings/hotkeys не трогаю. Глобального .NET SDK нет (только runtimes 10.0.11), поэтому использую локально установленный в TEMP SDK 10.0.404.

---

author: @flash-worker
created: 2026-11-12T20:15:00Z

---

Готово к проверке Sol. Реализация: Painter экспортная палитра Signal #F04D32 + Paper #FBFEF8 для arrow/rectangle/pencil, ширины 2.5/4.5 без изменений; preview и bitmap через один Painter.Draw. Evidence: build PASS 0/0, --annotation-palette-only PASS 20, --design-only PASS 366, --pencil-only PASS 33, --comments-only PASS 11; PNG artifacts/annotation-palette/, отчёт artifacts/annotation-palette-verification.json. Ограничения: full suite/smoke и реальный WGC fitted-window не запускались из-за работающей пользовательской копии; fitted-window покрыт synthetic ChooseBitmap; физические DPI/High Contrast/ручная приёмка не выполнялись. Пользовательский процесс, clipboard, settings и hotkeys не затрагивались; commit/push/release не выполнялись. Задача оставлена In Progress с этапом готово к проверке Sol, не Done.

---

author: @sol-orchestrator
created: 2026-11-12T20:18:00Z

---

Проверка Sol: критерии подтверждены в пределах просмотра кода и synthetic evidence. Существенных замечаний нет. Готово к пользовательскому тестированию. До проверки реального capture и визуального результата задача остается In Progress, не Done.

---

author: @sol-orchestrator
created: 2026-11-12T20:22:00Z

---

Пользователь явно принял TASK-1 и поручил перевести в Done после обновления контекста. Финальная сводка актуализирована; прежняя история проверок и ограничения сохранены. Новые тесты при закрытии не запускались.

---
<!-- COMMENTS:END -->

## Final Summary

<!-- SECTION:FINAL_SUMMARY:BEGIN -->
Завершено и принято пользователем по явному сообщению: TASK-1 принимаю. Стрелки, рамки и карандаш используют Signal Red #F04D32 с контуром Paper #FBFEF8 в preview и bitmap. Толщины, геометрия, управление, метки и комментарии сохранены. Документация обновлена. Release build PASS без предупреждений и ошибок; палитра PASS 20, дизайн PASS 366, карандаш PASS 33, комментарии PASS 11. Sol независимо проверил код, отчеты и PNG на светлом и темном фоне. Пользовательская приемка снимает ожидание ручного согласования результата; конкретные дополнительные сценарии пользователь не перечислял. Full suite, Smoke.ps1, реальный WGC fitted-window, физические 150/200 процентов DPI, High Contrast и receiver не проверялись в рамках задачи и не объявляются PASS. Новая сборка: src/ScreenIt.App/bin/Release/net10.0-windows/ScreenIt.App.exe; старый portable preview не обновлен. Коммит, push и публикация не выполнялись.
<!-- SECTION:FINAL_SUMMARY:END -->
