# Clipboard Transfer Contract — disposable spike

Только Windows clipboard harness. Это не ScreenIt: здесь нет capture, overlay, editor, tray, sessions, production abstractions или solution. Используется console + hidden WinForms NativeWindow для HWND владельца и message pumping. Runtime не делает сетевых запросов. NuGet dependencies отсутствуют; package sources очищены в `NuGet.Config`.

## Быстрый запуск на этой машине

Сборка уже выполнена. В PowerShell:

```powershell
# Run from the repository root.
Set-Location '.\spikes\clipboard-transfer'
.\bin\Release\net10.0-windows\ClipboardTransfer.exe
```

Нужен Windows 11 x64 и .NET 10 Windows Desktop runtime. На проверенной машине установлен runtime 10.0.11. EXE framework-dependent; это не installer и не self-contained release.

## Сборка

На машине с .NET 10 SDK:

```powershell
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
dotnet build -c Release --configfile NuGet.Config
dotnet run -c Release --no-build
```

Если SDK отсутствует в PATH, установите .NET 10 SDK или укажите путь к своему `dotnet.exe`.

SDK/runtime setup может требовать скачивания инструментов; установленный harness ничего не скачивает. `global.json` закрепляет minimum SDK family 10.0.100 с roll-forward на stable feature band .NET 10.

## Fixture

Каждый запуск создаёт уникальную папку `%TEMP%\ScreenItClipboardSpike\<GUID>` и показывает полный путь.

| File | Dimensions | Pattern |
|---|---|---|
| A.png | 1024×640 | Red banner, A1/A2 |
| B.png | 800×600 | Green banner, B1 |
| C.png | 640×960 | Blue banner, C1 |

Все PNG имеют 96 DPI, corner anchors, 32-pixel grid и 1-pixel checkerboard. Никакие реальные экраны не читаются. Буквы и marker-like badges нарисованы заранее; annotations editor отсутствует. Комментарии НЕ нарисованы в изображениях.

Отдельный Unicode text содержит:

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

## Payload menu

| ID | Explicit formats, в порядке публикации | Цель |
|---|---|---|
| 1 | PNG | Registered format, screenshot A |
| 2 | CF_DIBV5 | Native bitmap, screenshot A |
| 3 | PNG, CF_DIBV5 | Image representation competition |
| 4 | PNG, CF_UNICODETEXT | Image + real comments |
| 5 | PNG, CF_DIBV5, CF_UNICODETEXT | Image/text with native fallback |
| 6 | CF_HDROP | Three files A → B → C |
| 7 | CF_HDROP, CF_UNICODETEXT | Главный multi-image + text candidate |
| 8 | HTML Format | Three embedded PNG data URI + real HTML text |
| 9 | HTML Format | Three local `file:///` PNG references + real HTML text |
| 10 | CF_HDROP, CF_UNICODETEXT, PNG, CF_DIBV5 | Files-first competing representations |
| 11 | PNG, CF_DIBV5, CF_HDROP, CF_UNICODETEXT | Image-first competing representations |
| 12 | CF_UNICODETEXT | Text-only control |

PNG/DIBV5 в mixed payload представляют только A. HDROP и HTML представляют A/B/C. Это позволяет заметить receiver, который выбрал single-image representation и потерял B/C.

`PNG` — имя зарегистрированного format, а не стандартный Windows CF_* ID. Его поддержка приложением не предполагается. Числовые registered IDs могут отличаться на другой машине.

Privacy по умолчанию ON. В конце публикации добавляются два registered formats: `CanUploadToCloudClipboard` и `CanIncludeInClipboardHistory`, каждый с четырьмя нулевыми байтами (DWORD 0). Команда `p` переключает flags для СЛЕДУЮЩЕЙ публикации; сама clipboard не меняет.

Windows может дополнительно синтезировать CF_BITMAP/CF_DIB или CF_TEXT/CF_OEMTEXT/CF_LOCALE. Harness показывает и explicit SetClipboardData calls, и фактическую enumeration. Эти synthesized formats не считаются результатом выбора receiving application.

## Команды

- Число 1–12 + Enter: publish и немедленный self-check.
- `p`: privacy ON/OFF для следующих тестов.
- `v`: повторить проверку собственной последней записи. Если owner/sequence изменились, проверка отказывается читать чужие данные.
- `r`: заменить clipboard синтетическим text-only control; files остаются.
- `t`: показать только фиксированные тестовые комментарии.
- `f`: показать существование fixture files; это не мониторинг их чтения receiver.
- `d`: после подтверждения `DELETE` очистить собственный clipboard и удалить только files этого запуска.
- `q`: выход, files сохраняются. EOF также сохраняет files.

Clipboard заменяется явной публикацией. Предыдущее содержимое не читается, не сохраняется и не восстанавливается. `--self-test` также заменяет clipboard и в конце очищает собственную запись.

One-shot и report:

```powershell
.\bin\Release\net10.0-windows\ClipboardTransfer.exe --publish 7 --report artifacts\manual-run.json
.\bin\Release\net10.0-windows\ClipboardTransfer.exe --publish 7 --no-privacy
.\bin\Release\net10.0-windows\ClipboardTransfer.exe --self-test --report artifacts\self-check.json
```

Report сохраняет только technical metadata: варианты, flags, format names, environment, fixture paths/dimensions/hashes и self-check statuses. Он не заполняет receiver results автоматически.

## Что проверяет self-check

- Synthetic PNG files, dimensions, corner pixels и 1px pattern.
- Все explicit HGLOBAL payload bytes после `SetClipboardData` через `GetClipboardData`.
- Unicode exact match, включая кириллицу и CRLF.
- HDROP header (`pFiles=20`, `fWide=TRUE`), double NUL, order и собственные paths.
- HDROP через `DragQueryFileW` и наличие A/B/C files.
- DIBV5 header, masks/color space, top-down orientation и sampled PNG/DIB pixel parity.
- HTML UTF-8 byte offsets, fragment boundaries, comments, embedded PNG order или exact local URIs.
- Отсутствие remote URLs/unexpected external paths в generated payload.
- Privacy flag presence и DWORD 0 readback; OFF не публикует flags.
- Negative controls: неправильный path, broken HTML offset, nonlocal URI должны отвергаться.
- Files остаются после clipboard replacement.

Это проверка sender payload, не paste receiving application. Верификация history/cloud exclusion отдельно остаётся manual test.

## Manual compatibility protocol

**Spike 1 CLOSED для выбора MVP transport.** Наблюдения пользователя внесены в [RESULTS.md](RESULTS.md): Web — 5/6/7/8/9/10/11; Desktop Windows и Codex — 5/6/7; Codex дополнительно — успешный end-to-end 6 → paste → 12 → paste. Versions, точный Codex interface и privacy state не записаны; default ON не считается доказательством режима теста.

В последнем Codex test после первого paste вставились A/B/C; после text-only payload 12 все attachments сохранились в прежнем порядке без дубликатов, полный editable Unicode text появился с сохранёнными Cyrillic/multiline. Workflow — PASS.

**Зафиксированный MVP contract:** Copy Session Images публикует отдельные annotated PNG files A/B/C/… через CF_HDROP в порядке session → пользовательский Ctrl+V → Copy Session Comments публикует только человекочитаемый CF_UNICODETEXT → второй Ctrl+V. Без конкурирующих image/HTML formats, composite, OLE и автоматического перехвата paste.

Compatibility: проверенный Codex interface — end-to-end PASS. ChatGPT Desktop Windows — multi-image CF_HDROP и Unicode text подтверждены отдельно; последовательный 6 → 12 отдельно не выполнялся. ChatGPT Web — проверенные file lists дали только A, поэтому Web compatibility этого transport не обещается. Claude и остальные receivers — NOT TESTED.

Новых clipboard экспериментов не требуется. Общий protocol ниже — архивная справочная методика. File-read completion и безопасная cleanup остаются непроверенными implementation constraints; files нельзя удалять только на основании copy/Finish. Spikes 2/3 также CLOSED. Production MVP разрешён пользователем и находится отдельно от spikes (см. root README).

1. Открыть receiving application и пустой unsent composer/document.
2. Записать version, Windows/browser environment, test date и privacy ON/OFF.
3. Начать с ID 7, затем ID 6: это отличает влияние Unicode text на multi-file paste.
4. Выбрать test ID в harness; убедиться в `SELF-CHECK PASS`.
5. Перейти в receiver и нажать один `Ctrl+V`. Не отправлять сообщение.
6. Записать image count, A/B/C order, duplicates, editable text, multiline, resolution changes, popups и дополнительные действия.
7. Удалить вставленные test attachments/text перед следующим тестом.
8. Проверить остальные IDs, особенно 4/5, 8/9, 10/11; 1/2/3/12 — controls.
9. Повторить promising payload три раза в чистом composer, затем сравнить privacy ON/OFF.
10. Внести наблюдения в `RESULTS.md`. Не выводить использованный format только из наличия image/text; если tracing отсутствует, писать `format unknown`.

Смотреть количество attachments ДО отправки и финальный preview после подготовки. Preview scaling само по себе не доказывает потерю исходного resolution. Проверять фактические dimensions только если receiver позволяет inspect/save вложение; иначе отмечать UNKNOWN.

PASS означает достижение цели конкретного payload: например, ID 6 получает три отдельных images в порядке A/B/C; ID 7 требует также настоящий текст. PARTIAL — передана часть целевого содержимого, например images без text или text без image. FAIL — цель payload не достигнута, даже если пришёл только первый image вместо трёх. NOT TESTED — paste реально не выполнялся. В RESULTS статус 6 для Web — FAIL, для Desktop/Codex — PASS; статус 7 для Web — FAIL, для Desktop/Codex — PARTIAL. Статус 5 — PARTIAL во всех трёх receivers.

## Privacy manual test

Публиковать один и тот же payload с flags OFF/ON. Сравнить обычный paste и присутствие в Win+V, если history включена. Не менять системные settings автоматически. Если history выключена — записать NOT TESTED, а не PASS.

Harness не включает cloud sync и не отправляет данные для проверки. Cloud exclusion остаётся NOT TESTED без отдельного наблюдаемого эксперимента пользователя. Flags readback не доказывает поведение clipboard managers или receivers.

## Temporary files и lifetime experiments

В нормальных tests не удалять files до завершения чтения receiver. `q` сохраняет files специально. Нет автоматической TTL cleanup и нет production lifetime policy.

После всех tests удалить конкретную run directory:

```powershell
.\bin\Release\net10.0-windows\ClipboardTransfer.exe --cleanup 'ПОЛНЫЙ ПУТЬ ИЗ ВЫВОДА HARNESS'
```

Cleanup проверяет immediate-child path под собственным TEMP root, GUID, owner marker, known filenames, отсутствие directory/reparse entries. Никакого recursive delete. Не удаляет другие run directories. При unexpected files отказывает вместо широкого удаления.

Архивная методика отдельных lifetime tests, только с синтетическими files (не дополнительные задания текущего decision gate):

1. Publish ID 6/7, подождать и вставить при работающем harness.
2. Publish one-shot ID 6/7, дождаться выхода процесса, затем вставить. Files должны оставаться; paste result нужно наблюдать отдельно.
3. После появления attachments повторить paste без replacement: проверить, что исходные files всё ещё нужны или уже скопированы receiver.
4. В отдельном терминале выполнить `--cleanup` сразу после paste. Эта команда НЕ меняет clipboard, но намеренно удаляет synthetic files. Проверить late preview/error и повторный paste. Не использовать `d` для этого эксперимента: `d` также очищает собственный clipboard и смешивает два воздействия.
5. В новом запуске повторить с задержкой cleanup после готового preview.
6. Publish files, нажать `r` (clipboard replacement), убедиться через `f`, что files не удалились; наблюдать уже вставленные attachments.

Для точного момента ReadFile можно дополнительно использовать Microsoft Process Monitor с фильтром Path begins with конкретная run directory. [Process Monitor](https://learn.microsoft.com/en-us/sysinternals/downloads/procmon). Harness сам его не устанавливает/не запускает. LastAccessTime и FileSystemWatcher не являются надёжным подтверждением завершения чтения.

Даже успешное удаление после paste в одном app/version не является универсальным safe-delete acknowledgement. Если чтение не трассировалось, время фактического чтения остаётся UNKNOWN.

## Источники Microsoft

- [Standard formats: CF_HDROP, CF_UNICODETEXT, DIB/DIBV5](https://learn.microsoft.com/en-us/windows/win32/dataxchg/standard-clipboard-formats)
- [Multiple, registered, synthesized formats](https://learn.microsoft.com/en-us/windows/win32/dataxchg/clipboard-formats)
- [RegisterClipboardFormatW](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-registerclipboardformatw)
- [DROPFILES](https://learn.microsoft.com/en-us/windows/win32/api/shlobj_core/ns-shlobj_core-dropfiles)
- [DragQueryFileW](https://learn.microsoft.com/en-us/windows/win32/api/shellapi/nf-shellapi-dragqueryfilew)
- [BITMAPV5HEADER](https://learn.microsoft.com/en-us/windows/win32/api/wingdi/ns-wingdi-bitmapv5header)
- [HTML clipboard, UTF-8 offsets](https://learn.microsoft.com/en-us/windows/win32/dataxchg/html-clipboard-format)
- [SetClipboardData, ownership](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setclipboarddata)
- [Using clipboard, eager/delayed rendering](https://learn.microsoft.com/en-us/windows/win32/dataxchg/using-the-clipboard)
- [History/cloud privacy formats](https://learn.microsoft.com/ru-ru/windows/win32/dataxchg/clipboard-formats)

OLE/virtual files не реализованы и не требуются для выбранного baseline. Transfer-selection gate закрыт после успешного end-to-end 6 → paste → 12 → paste в Codex. Не продолжать clipboard testing ради полноты.

## Production delivery evolution — 2026-10-04

Historical PASS/FAIL observations and the CLOSED Spike 1 gate remain unchanged: bulk CF_HDROP works in the tested Desktop/Codex receivers, Web takes only the first file, and mixed image+text one-paste was not confirmed.

Subsequent production UX acceptance found that two manual tray-copy/paste operations did not satisfy ScreenIt’s core product goal. Delivery now uses explicit Ctrl+Alt+V Paste Session: RAM session snapshot → one-file CF_HDROP A/paste → B/paste → C/paste → Unicode comments/paste, with foreground/modifier guards. This is a production delivery change, not a new spike or a revision of historical evidence. New sequential Web/Desktop/Codex end-to-end acceptance remains NOT TESTED; the Web delivery blocker is OPEN. Current behavior and manual procedure: [root README](../../README.md).
