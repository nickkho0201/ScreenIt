---
id: TASK-4
title: Выпустить 0.3.0 в main с возможностью отката к 0.2.3
status: Done
assignee:
  - '@sol'
created_date: '2026-11-13T04:26:00Z'
updated_date: '2026-11-13T04:39:00Z'
labels: []
dependencies: []
type: chore
ordinal: 4000
---

## Description

<!-- SECTION:DESCRIPTION:BEGIN -->
Пользователь разрешил commit, merge/push в main и публикацию релиза из текущего диффа; требуется сохранить baseline 0.2.3 для ручного отката. Реальную пользовательскую установку не менять.
<!-- SECTION:DESCRIPTION:END -->

## Acceptance Criteria
<!-- AC:BEGIN -->
- [x] #1 Release metadata проверены, сборка и доступные проверки выполнены с описанием ограничений
- [x] #2 Тег и assets 0.2.3 сохранены, checksums baseline проверены, инструкция отката подготовлена
- [x] #3 main и v0.3.0 опубликованы без переписывания истории; release содержит installer, portable и проверенные SHA256SUMS
<!-- AC:END -->

## Implementation Plan

<!-- SECTION:PLAN:BEGIN -->
1. Review diff/scripts/baseline. 2. Update metadata/docs, rollback instructions и old-loader check. 3. Build/full checks/smoke. 4. Preserve baseline, build/verify packages. 5. Commit/main/push/tag/release, remote hash verification.
<!-- SECTION:PLAN:END -->

## Implementation Notes

<!-- SECTION:NOTES:BEGIN -->
2026-11-13: пользователь закрыл ScreenIt и разрешил full suite/smoke с тестовыми данными. SDK local 10.0.404, build PASS 0 warnings/errors. Full suite PASS 4005 (первый run упал на несогласованном synthetic installer checksum filename после version bump; fixture исправлен, повтор PASS). Design-selection PASS 114; notices PASS 545. Self-contained 0.3.0 smoke defaults/custom PASS 22/22; официальный 0.2.3 baseline smoke после 0.3.0 PASS 22. ZIP audit всех файлов PASS: 477/479. Actual 0.2.3 assembly loader <-> 0.3.0 Save roundtrip PASS на synthetic settings, включая design/unknown/Unicode/hotkeys. Старые assets совпали с GitHub digest; artifacts/release перемещён без удаления в artifacts/release-0.2.3-preserved. Новые packages в artifacts/release, installer SHA256 57b9f6f7044434f144910221c5e864a0085088a78de77515eaf058deea2950e3, ZIP 972c4613b252c5b42324f68b893e5da21cd63f5fa1c302704c2ac0a5a5f14ce3. Metadata/docs 0.3.0, installer/ROLLBACK.md, version-bound updater fixtures обновлены; AppId/mutex/settings/schema2 сохранены. Installer/upgrade/downgrade execution не выполнялись: пользовательская installation не менялась, disposable account/VM не использовался. Физические 150/200%, accessibility, manual receiver/physical input не подтверждены этим прогоном. Публикация пока впереди; AC3 остаётся открыт.

Publication: release commit 8191fdd7185c021444f2097676d8ccabc51c36d8, main fast-forward then ordinary push; annotated v0.3.0 pushed, GitHub Release published as Latest with all 3 assets. Remote main/tag peeled SHA checked. v0.2.3 still points to efe304393ebab3822e3177bc5fd37a93721a1520; original remote installer/ZIP digests unchanged. Actual old updater Parse recognizes live latest metadata as 0.3.0; current updater rejects same-version offer. GitHub asset digest/state match local SHA; re-download check pending. Review: staged code/scripts diff --check PASS. Global staged diff --check flags existing supplied DESIGN Markdown hard-break spaces, original upstream OFL line whitespace, and final blank line in historical CONSTRUCT-REVIEW; left user design/history/upstream licenses untouched, not presented as global PASS.

Final remote verification PASS: all 3 v0.3.0 assets downloaded back to artifacts/release-0.3.0-remote-verified; installer/portable SHA256 match locally built packages and downloaded SHA256SUMS.txt is byte-identical. Live GithubUpdateSource request + design-selection gate PASS 115, public latest equals 0.3.0. Added exact reviewed-revert example for release commit to installer/ROLLBACK.md (not executed). Release URL https://github.com/nickkho0201/ScreenIt/releases/tag/v0.3.0. Final diff/status reviewed; follow-up commit contains only rollback documentation and this evidence, no runtime/package changes.
<!-- SECTION:NOTES:END -->

## Final Summary

<!-- SECTION:FINAL_SUMMARY:BEGIN -->
Опубликованы main (release commit 8191fdd) и annotated v0.3.0, stable Latest release с installer/portable/SHA256SUMS. Все assets скачаны обратно и проверены. Build PASS 0/0, full suite 4005, design selection 114, notices 545, production smoke 22/22, live update 115. v0.2.3/tag/assets сохранены; old portable smoke 22 и настоящий old-loader roundtrip подтверждают fallback без потери settings. Installer/ROLLBACK.md содержит manual rollback и exact revert. Установка пользователя не менялась; installer downgrade, receiver, physical DPI/accessibility не заявлены проверенными.
<!-- SECTION:FINAL_SUMMARY:END -->
