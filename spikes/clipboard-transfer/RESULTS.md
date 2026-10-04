# Spike 1 — Clipboard Transfer Contract: результаты

Обновлено: 2026-10-04. **Spike 1 CLOSED для выбора MVP transport.** Последний ручной Codex workflow 6 → paste → 12 → paste — PASS. Baseline принят: отдельная передача images через CF_HDROP и comments через CF_UNICODETEXT. Research evidence сохранены; production MVP теперь находится отдельно в src (см. root README).

## Environment и evidence

- Windows 11 x64, build 26200 (Environment.OSVersion: 10.0.26200.0).
- Локальный SDK 10.0.100, вне репозитория и системной dotnet installation.
- Build Release: PASS, 0 warnings, 0 errors.
- Полный self-test прошёл под SDK-local runtime 10.0.0 и отдельно под установленным Windows Desktop runtime 10.0.11; interactive/one-shot smoke — под 10.0.11.
- [Self-check evidence](artifacts/self-check.json): 24 publication/readback cases и две дополнительные группы проверок, 0 failures.
- [Installed-runtime self-check](artifacts/self-check-installed-runtime.json): те же 24 cases и две группы, 0 failures на runtime 10.0.11.
- [Interactive publication evidence](artifacts/interactive-smoke.json), [replacement evidence](artifacts/replacement-smoke.json).
- Binary: `bin/Release/net10.0-windows/ClipboardTransfer.exe`.
- Logs не содержат foreign clipboard data. Generated fixture directories этих автоматических запусков очищены; report paths внутри JSON — исторические, а не существующий file cache.
- Ручные результаты ниже предоставлены пользователем после реальных вставок Ctrl+V. Точные версии приложений, browser environment, дата выполнения и privacy state этих ручных тестов не записаны. Environment автоматических проверок выше не подменяет отсутствующие manual metadata; default Privacy ON сам по себе не доказывает режим выполненного теста.

## Локальная автоматическая проверка

PASS здесь означает только корректность sender/API readback, не paste сторонним приложением.

| Payload | Privacy OFF | Privacy ON | Проверено |
|---|---|---|---|
| 1: Single PNG | PASS | PASS | PNG/DIBV5 bytes and fixture validation |
| 2: Single DIBV5 | PASS | PASS | PNG/DIBV5 bytes and fixture validation |
| 3: PNG + DIBV5 | PASS | PASS | PNG/DIBV5 bytes and fixture validation |
| 4: PNG + Unicode text | PASS | PASS | Image bytes + exact Unicode |
| 5: PNG + DIBV5 + Unicode text | PASS | PASS | Image bytes + exact Unicode |
| 6: Multiple files (CF_HDROP) | PASS | PASS | CF_HDROP A/B/C readback; files exist |
| 7: Multiple files + Unicode text | PASS | PASS | CF_HDROP A/B/C readback; files exist |
| 8: HTML embedded data URI | PASS | PASS | UTF-8 HTML offsets, three PNG references, real comments |
| 9: HTML local file references | PASS | PASS | UTF-8 HTML offsets, three PNG references, real comments |
| 10: Competing: files first | PASS | PASS | All explicit formats; A/B/C list and Unicode |
| 11: Competing: image first | PASS | PASS | All explicit formats; A/B/C list and Unicode |
| 12: Unicode text only | PASS | PASS | Exact Unicode/multiline |

Дополнительные наблюдения:

- Privacy ON: оба flags присутствуют как DWORD 0 и читаются обратно; explicit payload bytes остаются корректными. Privacy OFF: flags отсутствуют в собственном clipboard state.
- Windows синтезировал CF_BITMAP/CF_DIB при публикации DIBV5.
- Windows синтезировал CF_LOCALE/CF_TEXT/CF_OEMTEXT при публикации Unicode text.
- Корректный HTML сформирован в двух вариантах: PNG data URI и local file URI. Наличие кириллицы в context проверяет byte offsets, а не character offsets.
- Negative controls для unexpected external path, broken HTML offsets и nonlocal image URI: PASS.
- Clipboard replacement не удалил synthetic PNG files: PASS.
- Interactive publish ID 7, message pumping и чистый выход: PASS.
- После публикации отдельным процессом ID 12 команда `v` первого процесса отказалась читать изменённый clipboard: PASS; third-party contents не читались.
- PNG files существовали после exit процесса: PASS.
- Safe cleanup конкретных synthetic run directories: выполнена успешно.
- Cleanup path вне application-owned root отвергнут: PASS; файлы репозитория не затронуты.
- Исходный UTF-16 text: 336 bytes с NUL terminator. A: 1024×640; B: 800×600; C: 640×960.
- Static review: production source не содержит HTTP/socket/upload calls; NuGet PackageReference отсутствуют. Packet-level runtime network trace не выполнялся.

## Manual compatibility matrix

Обновлены только реально проверенные пользователем комбинации: ChatGPT Web — 5/6/7/8/9/10/11; ChatGPT Desktop Windows — 5/6/7; Codex — 5/6/7 и 12 после 6 в одном composer. Остальные строки остаются NOT TESTED. Использованный receiver format не трассировался и остаётся unknown. Точный Codex interface/version и privacy state не записаны; не предполагается, что проверены все Codex interfaces.

Колонка One Ctrl+V означает, достигнута ли полная цель конкретного payload одной вставкой. Частичная вставка не считается YES; например, для payload 7 Desktop получил все три images одним Ctrl+V, но не comments.

| Application | Version / environment | Payload | Images received | Text received | Order preserved | One Ctrl+V | Notes |
|---|---|---|---|---|---|---|---|
| ChatGPT Web | NOT TESTED | 1: Single PNG | NOT TESTED | NOT TESTED | NOT TESTED | NOT TESTED | Status: NOT TESTED; privacy ON/OFF pending; format unknown |
| ChatGPT Web | NOT TESTED | 2: Single DIBV5 | NOT TESTED | NOT TESTED | NOT TESTED | NOT TESTED | Status: NOT TESTED; privacy ON/OFF pending; format unknown |
| ChatGPT Web | NOT TESTED | 3: PNG + DIBV5 | NOT TESTED | NOT TESTED | NOT TESTED | NOT TESTED | Status: NOT TESTED; privacy ON/OFF pending; format unknown |
| ChatGPT Web | NOT TESTED | 4: PNG + Unicode text | NOT TESTED | NOT TESTED | NOT TESTED | NOT TESTED | Status: NOT TESTED; privacy ON/OFF pending; format unknown |
| ChatGPT Web | Web composer; browser/version not recorded | 5: PNG + DIBV5 + Unicode text | 0 | Full Unicode comments; Cyrillic/multiline preserved | N/A: no images | NO: text only | Status: PARTIAL; image absent; privacy state not recorded; format unknown |
| ChatGPT Web | Web composer; browser/version not recorded | 6: Multiple files (CF_HDROP) | 1: A; B/C absent | N/A: payload contains no text | N/A: only A received | NO: incomplete image set | Status: FAIL for multi-image goal; only first screenshot; privacy state not recorded; format unknown |
| ChatGPT Web | Web composer; browser/version not recorded | 7: Multiple files + Unicode text | 1: A; B/C absent | None | N/A: only A received | NO: B/C and comments absent | Status: FAIL; adding Unicode text did not fix multi-image paste; privacy state not recorded; format unknown |
| ChatGPT Web | Web composer; browser/version not recorded | 8: HTML embedded data URI | 0 observed | None observed | N/A: no images | NO: no visible result | Status: FAIL; composer visually unchanged; no inference that Windows clipboard was empty; privacy state not recorded; format unknown |
| ChatGPT Web | Web composer; browser/version not recorded | 9: HTML local file references | 0 | HTML-derived text with comments; formatting transformed | N/A: no images | NO: text only | Status: PARTIAL; local image references did not become attachments; exact observed text below; privacy state not recorded; format unknown |
| ChatGPT Web | Web composer; browser/version not recorded | 10: Competing: files first | 1: A; B/C absent | None | N/A: only A received | NO: B/C and comments absent | Status: FAIL; same observed result as 11; privacy state not recorded; format unknown |
| ChatGPT Web | Web composer; browser/version not recorded | 11: Competing: image first | 1: A; B/C absent | None | N/A: only A received | NO: B/C and comments absent | Status: FAIL; publication order change did not change observed paste behavior versus 10; privacy state not recorded; format unknown |
| ChatGPT Web | NOT TESTED | 12: Unicode text only | NOT TESTED | NOT TESTED | NOT TESTED | NOT TESTED | Status: NOT TESTED; privacy ON/OFF pending; format unknown |
| ChatGPT Desktop Windows | NOT TESTED | 1: Single PNG | NOT TESTED | NOT TESTED | NOT TESTED | NOT TESTED | Status: NOT TESTED; privacy ON/OFF pending; format unknown |
| ChatGPT Desktop Windows | NOT TESTED | 2: Single DIBV5 | NOT TESTED | NOT TESTED | NOT TESTED | NOT TESTED | Status: NOT TESTED; privacy ON/OFF pending; format unknown |
| ChatGPT Desktop Windows | NOT TESTED | 3: PNG + DIBV5 | NOT TESTED | NOT TESTED | NOT TESTED | NOT TESTED | Status: NOT TESTED; privacy ON/OFF pending; format unknown |
| ChatGPT Desktop Windows | NOT TESTED | 4: PNG + Unicode text | NOT TESTED | NOT TESTED | NOT TESTED | NOT TESTED | Status: NOT TESTED; privacy ON/OFF pending; format unknown |
| ChatGPT Desktop Windows | Windows desktop composer; app version not recorded | 5: PNG + DIBV5 + Unicode text | 0 | Full Unicode comments; Cyrillic/multiline preserved | N/A: no images | NO: text only | Status: PARTIAL; image absent; privacy state not recorded; format unknown |
| ChatGPT Desktop Windows | Windows desktop composer; app version not recorded | 6: Multiple files (CF_HDROP) | 3: A, B, C | N/A: payload contains no text | YES: A → B → C | YES: all three images | Status: PASS for three-file goal; multi-image paste of CF_HDROP payload confirmed; privacy state not recorded; format unknown |
| ChatGPT Desktop Windows | Windows desktop composer; app version not recorded | 7: Multiple files + Unicode text | 3: A, B, C | None | YES: A → B → C | NO for full payload; YES for images | Status: PARTIAL; all three images in one Ctrl+V, comments not inserted; privacy state not recorded; format unknown |
| ChatGPT Desktop Windows | NOT TESTED | 8: HTML embedded data URI | NOT TESTED | NOT TESTED | NOT TESTED | NOT TESTED | Status: NOT TESTED; privacy ON/OFF pending; format unknown |
| ChatGPT Desktop Windows | NOT TESTED | 9: HTML local file references | NOT TESTED | NOT TESTED | NOT TESTED | NOT TESTED | Status: NOT TESTED; privacy ON/OFF pending; format unknown |
| ChatGPT Desktop Windows | NOT TESTED | 10: Competing: files first | NOT TESTED | NOT TESTED | NOT TESTED | NOT TESTED | Status: NOT TESTED; privacy ON/OFF pending; format unknown |
| ChatGPT Desktop Windows | NOT TESTED | 11: Competing: image first | NOT TESTED | NOT TESTED | NOT TESTED | NOT TESTED | Status: NOT TESTED; privacy ON/OFF pending; format unknown |
| ChatGPT Desktop Windows | NOT TESTED | 12: Unicode text only | NOT TESTED | NOT TESTED | NOT TESTED | NOT TESTED | Status: NOT TESTED; privacy ON/OFF pending; format unknown |
| Codex paste-capable interface | NOT TESTED | 1: Single PNG | NOT TESTED | NOT TESTED | NOT TESTED | NOT TESTED | Status: NOT TESTED; privacy ON/OFF pending; format unknown |
| Codex paste-capable interface | NOT TESTED | 2: Single DIBV5 | NOT TESTED | NOT TESTED | NOT TESTED | NOT TESTED | Status: NOT TESTED; privacy ON/OFF pending; format unknown |
| Codex paste-capable interface | NOT TESTED | 3: PNG + DIBV5 | NOT TESTED | NOT TESTED | NOT TESTED | NOT TESTED | Status: NOT TESTED; privacy ON/OFF pending; format unknown |
| Codex paste-capable interface | NOT TESTED | 4: PNG + Unicode text | NOT TESTED | NOT TESTED | NOT TESTED | NOT TESTED | Status: NOT TESTED; privacy ON/OFF pending; format unknown |
| Codex paste-capable interface | Codex paste-capable composer; exact interface/version not recorded | 5: PNG + DIBV5 + Unicode text | 0 | Full Unicode comments; Cyrillic/multiline preserved | N/A: no images | NO: text only | Status: PARTIAL; image absent; privacy state not recorded; format unknown |
| Codex paste-capable interface | Codex paste-capable composer; exact interface/version not recorded | 6: Multiple files (CF_HDROP) | 3: A, B, C | N/A: payload contains no text | YES: A → B → C | YES: all three images | Status: PASS for three-file goal; three separate images in one Ctrl+V; privacy state not recorded; format unknown |
| Codex paste-capable interface | Codex paste-capable composer; exact interface/version not recorded | 7: Multiple files + Unicode text | 3: A, B, C | None | YES: A → B → C | NO for full payload; YES for images | Status: PARTIAL; all three images in one Ctrl+V, comments not inserted; privacy state not recorded; format unknown |
| Codex paste-capable interface | NOT TESTED | 8: HTML embedded data URI | NOT TESTED | NOT TESTED | NOT TESTED | NOT TESTED | Status: NOT TESTED; privacy ON/OFF pending; format unknown |
| Codex paste-capable interface | NOT TESTED | 9: HTML local file references | NOT TESTED | NOT TESTED | NOT TESTED | NOT TESTED | Status: NOT TESTED; privacy ON/OFF pending; format unknown |
| Codex paste-capable interface | NOT TESTED | 10: Competing: files first | NOT TESTED | NOT TESTED | NOT TESTED | NOT TESTED | Status: NOT TESTED; privacy ON/OFF pending; format unknown |
| Codex paste-capable interface | NOT TESTED | 11: Competing: image first | NOT TESTED | NOT TESTED | NOT TESTED | NOT TESTED | Status: NOT TESTED; privacy ON/OFF pending; format unknown |
| Codex paste-capable interface | Previously tested Codex composer; exact interface/version not recorded | 12: Unicode text only | N/A: no images in payload; existing A/B/C retained | Full editable Unicode comments; Cyrillic/multiline preserved | YES: existing A → B → C unchanged | YES for text stage; full workflow uses two pastes | Status: PASS; tested after payload 6 in same composer, no attachments lost/duplicated; privacy state not recorded; format unknown |
| Claude Web | NOT TESTED | 1: Single PNG | NOT TESTED | NOT TESTED | NOT TESTED | NOT TESTED | Status: NOT TESTED; privacy ON/OFF pending; format unknown |
| Claude Web | NOT TESTED | 2: Single DIBV5 | NOT TESTED | NOT TESTED | NOT TESTED | NOT TESTED | Status: NOT TESTED; privacy ON/OFF pending; format unknown |
| Claude Web | NOT TESTED | 3: PNG + DIBV5 | NOT TESTED | NOT TESTED | NOT TESTED | NOT TESTED | Status: NOT TESTED; privacy ON/OFF pending; format unknown |
| Claude Web | NOT TESTED | 4: PNG + Unicode text | NOT TESTED | NOT TESTED | NOT TESTED | NOT TESTED | Status: NOT TESTED; privacy ON/OFF pending; format unknown |
| Claude Web | NOT TESTED | 5: PNG + DIBV5 + Unicode text | NOT TESTED | NOT TESTED | NOT TESTED | NOT TESTED | Status: NOT TESTED; privacy ON/OFF pending; format unknown |
| Claude Web | NOT TESTED | 6: Multiple files (CF_HDROP) | NOT TESTED | NOT TESTED | NOT TESTED | NOT TESTED | Status: NOT TESTED; privacy ON/OFF pending; format unknown |
| Claude Web | NOT TESTED | 7: Multiple files + Unicode text | NOT TESTED | NOT TESTED | NOT TESTED | NOT TESTED | Status: NOT TESTED; privacy ON/OFF pending; format unknown |
| Claude Web | NOT TESTED | 8: HTML embedded data URI | NOT TESTED | NOT TESTED | NOT TESTED | NOT TESTED | Status: NOT TESTED; privacy ON/OFF pending; format unknown |
| Claude Web | NOT TESTED | 9: HTML local file references | NOT TESTED | NOT TESTED | NOT TESTED | NOT TESTED | Status: NOT TESTED; privacy ON/OFF pending; format unknown |
| Claude Web | NOT TESTED | 10: Competing: files first | NOT TESTED | NOT TESTED | NOT TESTED | NOT TESTED | Status: NOT TESTED; privacy ON/OFF pending; format unknown |
| Claude Web | NOT TESTED | 11: Competing: image first | NOT TESTED | NOT TESTED | NOT TESTED | NOT TESTED | Status: NOT TESTED; privacy ON/OFF pending; format unknown |
| Claude Web | NOT TESTED | 12: Unicode text only | NOT TESTED | NOT TESTED | NOT TESTED | NOT TESTED | Status: NOT TESTED; privacy ON/OFF pending; format unknown |
| Claude Desktop | NOT TESTED | 1: Single PNG | NOT TESTED | NOT TESTED | NOT TESTED | NOT TESTED | Status: NOT TESTED; privacy ON/OFF pending; format unknown |
| Claude Desktop | NOT TESTED | 2: Single DIBV5 | NOT TESTED | NOT TESTED | NOT TESTED | NOT TESTED | Status: NOT TESTED; privacy ON/OFF pending; format unknown |
| Claude Desktop | NOT TESTED | 3: PNG + DIBV5 | NOT TESTED | NOT TESTED | NOT TESTED | NOT TESTED | Status: NOT TESTED; privacy ON/OFF pending; format unknown |
| Claude Desktop | NOT TESTED | 4: PNG + Unicode text | NOT TESTED | NOT TESTED | NOT TESTED | NOT TESTED | Status: NOT TESTED; privacy ON/OFF pending; format unknown |
| Claude Desktop | NOT TESTED | 5: PNG + DIBV5 + Unicode text | NOT TESTED | NOT TESTED | NOT TESTED | NOT TESTED | Status: NOT TESTED; privacy ON/OFF pending; format unknown |
| Claude Desktop | NOT TESTED | 6: Multiple files (CF_HDROP) | NOT TESTED | NOT TESTED | NOT TESTED | NOT TESTED | Status: NOT TESTED; privacy ON/OFF pending; format unknown |
| Claude Desktop | NOT TESTED | 7: Multiple files + Unicode text | NOT TESTED | NOT TESTED | NOT TESTED | NOT TESTED | Status: NOT TESTED; privacy ON/OFF pending; format unknown |
| Claude Desktop | NOT TESTED | 8: HTML embedded data URI | NOT TESTED | NOT TESTED | NOT TESTED | NOT TESTED | Status: NOT TESTED; privacy ON/OFF pending; format unknown |
| Claude Desktop | NOT TESTED | 9: HTML local file references | NOT TESTED | NOT TESTED | NOT TESTED | NOT TESTED | Status: NOT TESTED; privacy ON/OFF pending; format unknown |
| Claude Desktop | NOT TESTED | 10: Competing: files first | NOT TESTED | NOT TESTED | NOT TESTED | NOT TESTED | Status: NOT TESTED; privacy ON/OFF pending; format unknown |
| Claude Desktop | NOT TESTED | 11: Competing: image first | NOT TESTED | NOT TESTED | NOT TESTED | NOT TESTED | Status: NOT TESTED; privacy ON/OFF pending; format unknown |
| Claude Desktop | NOT TESTED | 12: Unicode text only | NOT TESTED | NOT TESTED | NOT TESTED | NOT TESTED | Status: NOT TESTED; privacy ON/OFF pending; format unknown |
| Notepad | NOT TESTED | 1: Single PNG | NOT TESTED | NOT TESTED | NOT TESTED | NOT TESTED | Status: NOT TESTED; privacy ON/OFF pending; format unknown |
| Notepad | NOT TESTED | 2: Single DIBV5 | NOT TESTED | NOT TESTED | NOT TESTED | NOT TESTED | Status: NOT TESTED; privacy ON/OFF pending; format unknown |
| Notepad | NOT TESTED | 3: PNG + DIBV5 | NOT TESTED | NOT TESTED | NOT TESTED | NOT TESTED | Status: NOT TESTED; privacy ON/OFF pending; format unknown |
| Notepad | NOT TESTED | 4: PNG + Unicode text | NOT TESTED | NOT TESTED | NOT TESTED | NOT TESTED | Status: NOT TESTED; privacy ON/OFF pending; format unknown |
| Notepad | NOT TESTED | 5: PNG + DIBV5 + Unicode text | NOT TESTED | NOT TESTED | NOT TESTED | NOT TESTED | Status: NOT TESTED; privacy ON/OFF pending; format unknown |
| Notepad | NOT TESTED | 6: Multiple files (CF_HDROP) | NOT TESTED | NOT TESTED | NOT TESTED | NOT TESTED | Status: NOT TESTED; privacy ON/OFF pending; format unknown |
| Notepad | NOT TESTED | 7: Multiple files + Unicode text | NOT TESTED | NOT TESTED | NOT TESTED | NOT TESTED | Status: NOT TESTED; privacy ON/OFF pending; format unknown |
| Notepad | NOT TESTED | 8: HTML embedded data URI | NOT TESTED | NOT TESTED | NOT TESTED | NOT TESTED | Status: NOT TESTED; privacy ON/OFF pending; format unknown |
| Notepad | NOT TESTED | 9: HTML local file references | NOT TESTED | NOT TESTED | NOT TESTED | NOT TESTED | Status: NOT TESTED; privacy ON/OFF pending; format unknown |
| Notepad | NOT TESTED | 10: Competing: files first | NOT TESTED | NOT TESTED | NOT TESTED | NOT TESTED | Status: NOT TESTED; privacy ON/OFF pending; format unknown |
| Notepad | NOT TESTED | 11: Competing: image first | NOT TESTED | NOT TESTED | NOT TESTED | NOT TESTED | Status: NOT TESTED; privacy ON/OFF pending; format unknown |
| Notepad | NOT TESTED | 12: Unicode text only | NOT TESTED | NOT TESTED | NOT TESTED | NOT TESTED | Status: NOT TESTED; privacy ON/OFF pending; format unknown |
| Paint | NOT TESTED | 1: Single PNG | NOT TESTED | NOT TESTED | NOT TESTED | NOT TESTED | Status: NOT TESTED; privacy ON/OFF pending; format unknown |
| Paint | NOT TESTED | 2: Single DIBV5 | NOT TESTED | NOT TESTED | NOT TESTED | NOT TESTED | Status: NOT TESTED; privacy ON/OFF pending; format unknown |
| Paint | NOT TESTED | 3: PNG + DIBV5 | NOT TESTED | NOT TESTED | NOT TESTED | NOT TESTED | Status: NOT TESTED; privacy ON/OFF pending; format unknown |
| Paint | NOT TESTED | 4: PNG + Unicode text | NOT TESTED | NOT TESTED | NOT TESTED | NOT TESTED | Status: NOT TESTED; privacy ON/OFF pending; format unknown |
| Paint | NOT TESTED | 5: PNG + DIBV5 + Unicode text | NOT TESTED | NOT TESTED | NOT TESTED | NOT TESTED | Status: NOT TESTED; privacy ON/OFF pending; format unknown |
| Paint | NOT TESTED | 6: Multiple files (CF_HDROP) | NOT TESTED | NOT TESTED | NOT TESTED | NOT TESTED | Status: NOT TESTED; privacy ON/OFF pending; format unknown |
| Paint | NOT TESTED | 7: Multiple files + Unicode text | NOT TESTED | NOT TESTED | NOT TESTED | NOT TESTED | Status: NOT TESTED; privacy ON/OFF pending; format unknown |
| Paint | NOT TESTED | 8: HTML embedded data URI | NOT TESTED | NOT TESTED | NOT TESTED | NOT TESTED | Status: NOT TESTED; privacy ON/OFF pending; format unknown |
| Paint | NOT TESTED | 9: HTML local file references | NOT TESTED | NOT TESTED | NOT TESTED | NOT TESTED | Status: NOT TESTED; privacy ON/OFF pending; format unknown |
| Paint | NOT TESTED | 10: Competing: files first | NOT TESTED | NOT TESTED | NOT TESTED | NOT TESTED | Status: NOT TESTED; privacy ON/OFF pending; format unknown |
| Paint | NOT TESTED | 11: Competing: image first | NOT TESTED | NOT TESTED | NOT TESTED | NOT TESTED | Status: NOT TESTED; privacy ON/OFF pending; format unknown |
| Paint | NOT TESTED | 12: Unicode text only | NOT TESTED | NOT TESTED | NOT TESTED | NOT TESTED | Status: NOT TESTED; privacy ON/OFF pending; format unknown |
| Word / rich-text receiver | NOT TESTED | 1: Single PNG | NOT TESTED | NOT TESTED | NOT TESTED | NOT TESTED | Status: NOT TESTED; privacy ON/OFF pending; format unknown |
| Word / rich-text receiver | NOT TESTED | 2: Single DIBV5 | NOT TESTED | NOT TESTED | NOT TESTED | NOT TESTED | Status: NOT TESTED; privacy ON/OFF pending; format unknown |
| Word / rich-text receiver | NOT TESTED | 3: PNG + DIBV5 | NOT TESTED | NOT TESTED | NOT TESTED | NOT TESTED | Status: NOT TESTED; privacy ON/OFF pending; format unknown |
| Word / rich-text receiver | NOT TESTED | 4: PNG + Unicode text | NOT TESTED | NOT TESTED | NOT TESTED | NOT TESTED | Status: NOT TESTED; privacy ON/OFF pending; format unknown |
| Word / rich-text receiver | NOT TESTED | 5: PNG + DIBV5 + Unicode text | NOT TESTED | NOT TESTED | NOT TESTED | NOT TESTED | Status: NOT TESTED; privacy ON/OFF pending; format unknown |
| Word / rich-text receiver | NOT TESTED | 6: Multiple files (CF_HDROP) | NOT TESTED | NOT TESTED | NOT TESTED | NOT TESTED | Status: NOT TESTED; privacy ON/OFF pending; format unknown |
| Word / rich-text receiver | NOT TESTED | 7: Multiple files + Unicode text | NOT TESTED | NOT TESTED | NOT TESTED | NOT TESTED | Status: NOT TESTED; privacy ON/OFF pending; format unknown |
| Word / rich-text receiver | NOT TESTED | 8: HTML embedded data URI | NOT TESTED | NOT TESTED | NOT TESTED | NOT TESTED | Status: NOT TESTED; privacy ON/OFF pending; format unknown |
| Word / rich-text receiver | NOT TESTED | 9: HTML local file references | NOT TESTED | NOT TESTED | NOT TESTED | NOT TESTED | Status: NOT TESTED; privacy ON/OFF pending; format unknown |
| Word / rich-text receiver | NOT TESTED | 10: Competing: files first | NOT TESTED | NOT TESTED | NOT TESTED | NOT TESTED | Status: NOT TESTED; privacy ON/OFF pending; format unknown |
| Word / rich-text receiver | NOT TESTED | 11: Competing: image first | NOT TESTED | NOT TESTED | NOT TESTED | NOT TESTED | Status: NOT TESTED; privacy ON/OFF pending; format unknown |
| Word / rich-text receiver | NOT TESTED | 12: Unicode text only | NOT TESTED | NOT TESTED | NOT TESTED | NOT TESTED | Status: NOT TESTED; privacy ON/OFF pending; format unknown |
| Explorer | NOT TESTED | 1: Single PNG | NOT TESTED | NOT TESTED | NOT TESTED | NOT TESTED | Status: NOT TESTED; privacy ON/OFF pending; format unknown |
| Explorer | NOT TESTED | 2: Single DIBV5 | NOT TESTED | NOT TESTED | NOT TESTED | NOT TESTED | Status: NOT TESTED; privacy ON/OFF pending; format unknown |
| Explorer | NOT TESTED | 3: PNG + DIBV5 | NOT TESTED | NOT TESTED | NOT TESTED | NOT TESTED | Status: NOT TESTED; privacy ON/OFF pending; format unknown |
| Explorer | NOT TESTED | 4: PNG + Unicode text | NOT TESTED | NOT TESTED | NOT TESTED | NOT TESTED | Status: NOT TESTED; privacy ON/OFF pending; format unknown |
| Explorer | NOT TESTED | 5: PNG + DIBV5 + Unicode text | NOT TESTED | NOT TESTED | NOT TESTED | NOT TESTED | Status: NOT TESTED; privacy ON/OFF pending; format unknown |
| Explorer | NOT TESTED | 6: Multiple files (CF_HDROP) | NOT TESTED | NOT TESTED | NOT TESTED | NOT TESTED | Status: NOT TESTED; privacy ON/OFF pending; format unknown |
| Explorer | NOT TESTED | 7: Multiple files + Unicode text | NOT TESTED | NOT TESTED | NOT TESTED | NOT TESTED | Status: NOT TESTED; privacy ON/OFF pending; format unknown |
| Explorer | NOT TESTED | 8: HTML embedded data URI | NOT TESTED | NOT TESTED | NOT TESTED | NOT TESTED | Status: NOT TESTED; privacy ON/OFF pending; format unknown |
| Explorer | NOT TESTED | 9: HTML local file references | NOT TESTED | NOT TESTED | NOT TESTED | NOT TESTED | Status: NOT TESTED; privacy ON/OFF pending; format unknown |
| Explorer | NOT TESTED | 10: Competing: files first | NOT TESTED | NOT TESTED | NOT TESTED | NOT TESTED | Status: NOT TESTED; privacy ON/OFF pending; format unknown |
| Explorer | NOT TESTED | 11: Competing: image first | NOT TESTED | NOT TESTED | NOT TESTED | NOT TESTED | Status: NOT TESTED; privacy ON/OFF pending; format unknown |
| Explorer | NOT TESTED | 12: Unicode text only | NOT TESTED | NOT TESTED | NOT TESTED | NOT TESTED | Status: NOT TESTED; privacy ON/OFF pending; format unknown |

Optional messengers: пока NOT TESTED; добавлять строки только для реально доступных приложений.

### Наблюдавшийся текст: payload 5, Web, Desktop и Codex

Пользователь сообщил полную вставку того же Unicode structured text во всех трёх receivers; кириллица и multiline сохранились:

```text
Screenshot A
A1 — тестовый комментарий
A2 — русский
     многострочный комментарий

Screenshot B
B1 — второй комментарий

Screenshot C
C1 — третий комментарий
```

### Наблюдавшийся текст: payload 9, ChatGPT Web

Изображений/attachments не было. Текстовая HTML-структура и comments вставились в следующем наблюдаемом виде:

```text
## Screenshot A

Screenshot A

## Screenshot B

Screenshot B

## Screenshot C

Screenshot C

Screenshot A A1 — тестовый комментарий A2 — русский
     многострочный комментарий Screenshot B B1 — второй комментарий Screenshot C C1 — третий комментарий
```

## Интерпретация статусов

- PASS: receiver принял всё, что этот конкретный payload должен передать, без потери структуры. Для ID 7 нужны три images и настоящий editable text.
- PARTIAL: передалась часть данных, изменился порядок/структура либо понадобилось дополнительное действие.
- FAIL: receiver не принял ожидаемый payload.
- NOT TESTED: реальный paste не выполнен.

Не угадывать выбранный clipboard format. Разница IDs 10/11 может показать зависимость от публикации, но сама по себе не доказывает внутренний paste handler. Числа attachments и editable text наблюдаются отдельно.

## Privacy / lifetime matrix

| Experiment | Status | Finding / next observation |
|---|---|---|
| Flag publication/readback + payload byte parity | PASS | 12 payloads × privacy ON/OFF |
| Windows Win+V exclusion | NOT TESTED | Compare same payload OFF/ON; do not infer from DWORD readback |
| Windows cloud synchronization exclusion | NOT TESTED | No cloud test/network transfer performed |
| Third-party clipboard managers | NOT TESTED | Not controlled by harness |
| Files survive replacement | PASS | Sender retains A/B/C until explicit cleanup |
| Files survive process exit | PASS | Files checked after interactive exit |
| Paste after process exit | NOT TESTED | Need real receiver observation; file existence is insufficient |
| Actual receiver ReadFile time | NOT TESTED | Trace specific synthetic directory if needed |
| Delete immediately after paste | NOT TESTED | Compare attachment readiness / late error |
| Delete after completed preview | NOT TESTED | Success is app/version-specific, not general acknowledgement |
| Receiver behavior after clipboard replacement | NOT TESTED | Observe existing attachments separately |

Instructions and isolated cleanup experiments: [README](README.md).

## Preliminary findings

**Sender:** harness публикует проверенные payload без OLE. Local readback сохраняет A → B → C и реальный Unicode text. Это отдельно от результатов paste; composite image не создаётся.

**ChatGPT Web:** payload 6 с CF_HDROP передал только A, а не всю multi-image session. Payload 7 с дополнительным Unicode text также передал только A и не вставил comments. Competing variants 10/11 дали тот же результат: A без B/C и comments. Изменение порядка публикации именно этих combinations не изменило наблюдаемое поведение; использованный receiver format неизвестен.

Payload 8 (embedded HTML) не дал видимого результата в composer; это не утверждение о пустоте Windows clipboard. Payload 9 (local HTML references) вставил текстовую структуру с comments, но не attachments. Payload 5 вставил полный Unicode text без image.

**ChatGPT Desktop Windows:** multi-image paste payload 6 с CF_HDROP практически подтверждён: три отдельных images, порядок A → B → C, один Ctrl+V. Payload 7 также передал три images за одну вставку, но не comments, поэтому full multi-image + text contract не подтверждён. Payload 5 вставил полный Unicode text без image.

**Codex, проверенный пользователем interface:** payload 6 — PASS: A/B/C отдельно, правильный порядок, одна вставка. Payload 7 — PARTIAL: те же три images за один Ctrl+V, но без comments. Payload 5 — PARTIAL: полный Unicode text с сохранённой кириллицей и multiline, без image. Это совпадает с наблюдениями ChatGPT Desktop Windows для 5/6/7.

**Receiving contracts различаются:** для того же file-list payload Desktop и проверенный Codex interface принимают A/B/C, Web — только A. Ни один из сообщённых tests не передал отдельные A/B/C и настоящий structured text вместе одним paste. Это вывод о проверенных combinations/environments, не доказательство невозможности для всех mechanisms или versions.

**Последний end-to-end Codex test — PASS:** после payload 6 вставились A/B/C; после payload 12 и второй вставки в тот же composer все три attachments остались, порядок сохранился, дубликатов нет. Добавился полный editable Unicode text, кириллица и multiline сохранены. Текст совпадает с fixture, приведённым выше. Источник — ручное наблюдение пользователя; версии/privacy не записаны.

**Принятое MVP-решение:** два явно разделённых clipboard действия — images-only CF_HDROP, затем comments-only CF_UNICODETEXT. Смешивать PNG/DIB/HTML с этим transport не требуется. Полный workflow подтверждён для проверенного Codex interface; утверждение не распространяется автоматически на другие приложения.

Внутренний алгоритм выбора formats ChatGPT/Codex не установлен: tracing/instrumentation отсутствует. Точные versions/interfaces, privacy state, повторяемость и фактический image resolution не записаны. Локальный flags roundtrip не доказывает history/cloud exclusion или влияние flags на manual receiver behavior.

File lifetime по-прежнему проверен только со стороны sender. Момент завершения чтения receiver, безопасное удаление после paste и production lifetime policy остаются открытыми.

## Decision gate

### CLOSED: end-to-end 6 → paste → 12 → paste в Codex — PASS

Пользователь выполнил последний назначенный эксперимент в том же ранее проверенном Codex interface. Зафиксированный workflow:

1. Payload 6 был опубликован и вставлен одним Ctrl+V: A/B/C появились в порядке A → B → C.
2. В том же composer после публикации payload 12 (только CF_UNICODETEXT) выполнен второй Ctrl+V.
3. Все три самостоятельных attachments остались, порядок не изменился, полный editable Unicode text с кириллицей/multiline появился, изображения не дублировались.
4. Exact interface/version и фактический privacy state не записаны — unknown. Дополнительные privacy/repeatability tests для этого закрытого gate не назначаются.

| Workflow | Receiver | Status | Observed result |
|---|---|---|---|
| Images-only 6 → comments-only 12 in the same composer | Previously tested Codex interface | PASS | A/B/C retained in order, no duplicates; full editable Unicode text with Cyrillic/multiline after exactly two pastes |

**Transfer-selection gate закрыт:** чистый text payload 12 принят после images payload 6 в том же composer; attachments сохранены без потери или дублирования. Отдельные images и настоящий structured text успешно объединены двумя явными вставками.

**Принято:** images-only CF_HDROP → comments-only CF_UNICODETEXT — baseline MVP contract для совместимых desktop receivers. Полный two-paste workflow подтверждён для проверенного Codex interface. Для ChatGPT Desktop Windows image stage и вставка comments из payload 5 подтверждены, но полный последовательный 6 → 12 в одном composer отдельно не выполнялся.

### Принятый MVP contract и compatibility boundaries

- Copy Session Images: три (или N) отдельных annotated PNG files, упорядоченный CF_HDROP; затем пользовательский Ctrl+V.
- Copy Session Comments: только настоящий человекочитаемый CF_UNICODETEXT со Screenshot A/B/C и A1/A2/B1 associations; второй пользовательский Ctrl+V.
- Оба действия явные. Без interception Ctrl+V, simulated input, автоматического переключения clipboard по попытке угадать paste, composite, HTML или image/text competition.
- Happy path: Copy Session Images → Ctrl+V → Copy Session Comments → Ctrl+V. Две явные команды копирования, две пользовательские вставки.
- Проверенный Codex interface: полный workflow подтверждён end-to-end.
- ChatGPT Desktop Windows: multi-image CF_HDROP и Unicode comments подтверждены отдельно; полный последовательный 6 → 12 остаётся NOT TESTED.
- ChatGPT Web: bulk multi-image clipboard transport не поддерживается проверенными payload; передача только A не должна считаться успехом session. Универсальная Web compatibility не обещается; новые Web fallback/mechanisms в этом spike не исследуются.
- Claude, остальные Codex interfaces и reference apps: NOT TESTED, без заявленной совместимости.
- Sender сохраняет original image dimensions; сохранение resolution receiver не измерено. Temporary PNG lifetime остаётся отдельным implementation constraint: files нельзя удалять сразу после copy, comments copy или Finish. Безопасный момент receiver read completion не установлен.
- Privacy flags проверены локально, но их влияние на receiving/history/cloud поведение не подтверждено. Неизвестные versions и отсутствие повторных тестов ограничивают claims наблюдаемыми environments.

Spike 1 завершён для выбора MVP transport. Дополнительные HTML/OLE, composite, Web fallback, receiver auto-detection и заполнение matrix ради полноты не требуются. Существующая архивная matrix и методика остаются для учёта неизвестного. File lifetime и privacy ограничения не превращаются в заявления о непроверенной совместимости.

Composite/export не реализованы и не выбраны как fallback. Следующий этап — отдельный disposable Spike 2; production ScreenIt не начат.

## Production delivery evolution — 2026-10-04

Historical PASS/FAIL observations and the CLOSED Spike 1 gate remain unchanged: bulk CF_HDROP works in the tested Desktop/Codex receivers, Web takes only the first file, and mixed image+text one-paste was not confirmed.

Subsequent production UX acceptance found that two manual tray-copy/paste operations did not satisfy ScreenIt’s core product goal. Delivery now uses explicit Ctrl+Alt+V Paste Session: RAM session snapshot → one-file CF_HDROP A/paste → B/paste → C/paste → Unicode comments/paste, with foreground/modifier guards. This is a production delivery change, not a new spike or a revision of historical evidence. New sequential Web/Desktop/Codex end-to-end acceptance remains NOT TESTED; the Web delivery blocker is OPEN. Current behavior and manual procedure: [root README](../../README.md).
