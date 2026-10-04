using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;

// The English source phrase is the canonical resource key. Technical IDs/content are never translated.
internal static class L
{
    internal static string Language { get; private set; }="en";
    internal static event Action? Changed;
    internal static readonly Dictionary<string,string> Russian=Catalog.Split('\n',StringSplitOptions.RemoveEmptyEntries).Select(s=>s.TrimEnd('\r').Replace("\\n","\n").Split('|',2)).ToDictionary(p=>p[0],p=>p[1],StringComparer.Ordinal);
    internal static IReadOnlyCollection<string> EnglishKeys => Russian.Keys;
    private static readonly Dictionary<string,Regex> patterns=Russian.SelectMany(p=>new[]{p.Key,p.Value}).Where(s=>s.Contains("{0}",StringComparison.Ordinal)).Distinct().ToDictionary(s=>s,s=>new Regex("^"+Regex.Replace(Regex.Escape(s),@"\\\{(\d+)}",m=>"(?<p"+m.Groups[1].Value+">.+?)").Replace(@"снимка\(ов\)","(?:снимок|снимка|снимков|снимка\\(ов\\))")+"$",RegexOptions.Singleline|RegexOptions.CultureInvariant,TimeSpan.FromMilliseconds(100)));
    internal static string T(string value)
    {
        if(Language=="en") return Reverse(value);
        value=Reverse(value);
        if(Russian.TryGetValue(value,out var exact)) return exact;
        foreach(var (key,translation) in Russian.Where(p=>p.Key.Contains("{0}",StringComparison.Ordinal)))
            if(Match(key,value) is { } args) return Plural(string.Format(System.Globalization.CultureInfo.CurrentCulture,translation,args));
        return value;
    }
    private static string Plural(string text) => Regex.Replace(text,@"(\d+) снимка\(ов\)",m=> { int.TryParse(m.Groups[1].Value,out int n);string word=n%10==1 && n%100!=11 ? "снимок" : n%10 is >=2 and <=4 && n%100 is not (>=12 and <=14) ? "снимка" : "снимков";return m.Groups[1].Value+" "+word; });
    private static object[]? Match(string template,string value)
    {
        var match=patterns[template].Match(value);
        if(!match.Success) return null;
        int count=Regex.Matches(template,@"\{\d+\}").Count;return Enumerable.Range(0,count).Select(i=>(object)match.Groups["p"+i].Value).ToArray();
    }
    private static string Reverse(string value)
    {
        if(Russian.ContainsKey(value)) return value;
        foreach(var (key,translation) in Russian)
        { if(value==translation) return key;if(translation.Contains("{0}",StringComparison.Ordinal) && Match(translation,value) is { } args) return string.Format(System.Globalization.CultureInfo.InvariantCulture,key,args); }
        return value;
    }
    internal static void Select(string language)
    {
        Language=language is "ru" ? "ru" : "en";
        if(Application.Current!=null)
            foreach(var window in Application.Current.Windows.Cast<Window>().ToArray())
            { if(window is AnnotationOverlay overlay) overlay.ApplyTheme();else if(window is ToastWindow toast) toast.ApplyTheme();else if(window is PromptWindow prompt) prompt.ApplyTheme();else if(window is SettingsWindow settings) settings.Refresh(); }
        Changed?.Invoke();
    }
    internal static void Tree(DependencyObject root)
    {
        if(root is TextBox) return; // Never translate comments or typed values.
        if(root is TextBlock text) text.Text=T(text.Text);
        if(root is Button button)
        { if(button.Content is string label) button.Content=T(label);if(button.ToolTip is ToolTip tip && tip.Content is string hint) { tip.Content=T(hint);AutomationProperties.SetName(button,T(hint)); } }
        if(root is RadioButton radio && radio.Content is string caption) radio.Content=T(caption);
        foreach(var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>()) Tree(child);
    }
    private const string Catalog="""
Settings|Настройки
General|Основные
Hotkeys|Клавиши
About|О программе
Appearance|Оформление
System|Как в Windows
Dark|Тёмная
Light|Светлая
Language|Язык интерфейса
Capture|Сделать снимок
Paste Session|Вставить сессию
Clear Session|Очистить сессию
Copy Session Images|Копировать снимки
Copy Session Comments|Копировать комментарии
More|Дополнительно
Exit|Выход
Marker|Маркер
Arrow|Стрелка
Rectangle|Рамка
● Marker|● Маркер
↗ Arrow|↗ Стрелка
▭ Rectangle|▭ Рамка
✓ ● Marker|✓ ● Маркер
✓ ↗ Arrow|✓ ↗ Стрелка
✓ ▭ Rectangle|✓ ▭ Рамка
Marker (M)|Маркер (M)
Arrow (A)|Стрелка (A)
Rectangle (R)|Рамка (R)
Edit|Изменить
Edit comment (E)|Изменить комментарий (E)
Delete|Удалить
Delete selected (Delete)|Удалить выбранное (Delete)
Undo (Ctrl+Z)|Отменить (Ctrl+Z)
Redo (Ctrl+Y)|Повторить (Ctrl+Y)
Done|Готово
Done (Ctrl+Enter)|Готово (Ctrl+Enter)
Cancel|Отмена
Keep session|Сохранить сессию
Clear|Очистить
Clear session|Очистить сессию
Clear session — ScreenIt|Очистка сессии — ScreenIt
Clear current session?|Очистить текущую сессию?
{0} screenshot and their comments will be discarded.|{0} снимок и комментарии будут удалены.
{0} screenshots and their comments will be discarded.|{0} снимка(ов) и комментарии будут удалены.
Discard this screenshot?|Отменить этот снимок?
Its annotations and comments will be discarded.|Пометки и комментарии будут удалены.
Discard|Удалить
Exit ScreenIt?|Выйти из ScreenIt?
The current session will be discarded.|Текущая сессия будет удалена.
Enter save · Shift+Enter newline · Esc cancel|Enter — сохранить · Shift+Enter — новая строка · Esc — отменить
Drag a region · Space full monitor · Esc cancel|Выделите область · Space — весь монитор · Esc — отменить
Continue on the selected monitor|Продолжайте на выбранном мониторе
Save or cancel the comment first.|Сначала сохраните или отмените комментарий.
Enter saves the comment · Esc cancels this edit|Enter — сохранить комментарий · Esc — отменить редактирование
Type a comment, or press Esc to cancel.|Введите комментарий или нажмите Esc для отмены.
Select a marker, then press E or double-click it.|Выберите маркер и нажмите E или щёлкните дважды.
Save or cancel the comment before undoing annotations.|Сохраните или отмените комментарий перед отменой пометок.
Save or cancel the comment before finishing the screenshot.|Сохраните или отмените комментарий перед завершением снимка.
Display scaling changed. Cancel this capture and start again.|Масштаб экрана изменился. Отмените снимок и повторите захват.
Display configuration changed.|Конфигурация мониторов изменилась.
Display configuration is unavailable.|Конфигурация мониторов недоступна.
Windows session or power state changed.|Состояние Windows или питания изменилось.
Capture suspended|Захват приостановлен
Capture is suspended. Cancel it and start a fresh capture.|Захват приостановлен. Отмените его и сделайте новый снимок.
Your screenshot is still available. Use Capture to cancel and start again.|Снимок сохранён. Нажмите «Сделать снимок», чтобы отменить захват и начать заново.
Not enough memory.|Недостаточно памяти.
Clipboard is busy.|Буфер обмена занят.
Operation failed.|Не удалось выполнить действие.
Capture could not complete.|Не удалось сделать снимок.
Screenshot could not be committed.|Не удалось сохранить снимок.
Images could not be copied. Session retained; retry Copy.|Не удалось скопировать снимки. Сессия сохранена; повторите копирование.
Comments could not be copied. Session retained; retry Copy.|Не удалось скопировать комментарии. Сессия сохранена; повторите копирование.
Paste Session is running|Сессия вставляется
Pasting comments…|Вставка комментариев…
Pasting session… {0}/{1}|Вставка сессии… {0}/{1}
Focus the target composer, then press {0}.|Перейдите в поле ввода и нажмите {0}.
Finish the capture, focus the receiver, then press {0}.|Завершите снимок, перейдите в поле ввода и нажмите {0}.
Close the Clear Session confirmation first|Сначала закройте подтверждение очистки сессии
Finish or cancel the current screenshot before clearing the session|Завершите или отмените снимок перед очисткой сессии
Session: {0} screenshot|Сессия: {0} снимок
Session: {0} screenshots|Сессия: {0} снимка(ов)
Session · {0} screenshot|Сессия · {0} снимок
Session · {0} screenshots|Сессия · {0} снимка(ов)
Screenshot {0} added|Снимок {0} добавлен
Session cleared|Сессия очищена
Session is already empty|Сессия уже пуста
Session unchanged|Сессия не изменена
Could not open confirmation|Не удалось открыть подтверждение
Comments copied|Комментарии скопированы
{0} screenshots copied|Снимки скопированы: {0}
Session pasted|Сессия вставлена
{0} screenshot|{0} снимок
{0} screenshots|{0} снимка(ов)
{0} screenshot + comments|{0} снимок + комментарии
{0} screenshots + comments|{0} снимка(ов) + комментарии
Paste interrupted|Вставка прервана
Paste failed|Не удалось вставить сессию
Check the receiving app\nSession unchanged|Проверьте принимающее приложение\nСессия не изменена
Nothing was pasted · Session unchanged|Ничего не вставлено · Сессия не изменена
{0} of {1} screenshots pasted\nSession unchanged|Вставлено снимков: {0} из {1}\nСессия не изменена
Pasted {0} screenshot|Вставлен {0} снимок
Pasted {0} screenshots|Вставлено снимков: {0}
Session is empty; nothing pasted|Сессия пуста; ничего не вставлено
Paste Session is already running|Сессия уже вставляется
Paste stopped after {0} of {1} screenshots. Your ScreenIt session is unchanged. {2}|Вставка остановлена после {0} из {1} снимков. Сессия ScreenIt не изменена. {2}
Focus a receiver and invoke Paste Session.|Перейдите в поле ввода и вызовите вставку сессии.
Foreground target changed.|Активное окно изменилось.
Hotkey release timed out.|Не удалось дождаться отпускания клавиш.
Keyboard modifiers are held.|Клавиши-модификаторы удерживаются.
Operation cancelled.|Действие отменено.
Preparation, clipboard or input failed; the last request may be incomplete.|Ошибка подготовки или вставки; последнее действие могло выполниться частично.
Clipboard changed before paste.|Буфер обмена изменился перед вставкой.
Settings could not be saved. Please retry.|Не удалось сохранить настройки. Повторите попытку.
Press shortcut…|Нажмите сочетание…
Use Ctrl, Alt, Shift or Win with a letter, number, F1–F12 or navigation key.|Используйте Ctrl, Alt, Shift или Win с буквой, цифрой, F1–F12 или клавишей навигации.
Shortcut unavailable or invalid. Previous shortcuts remain active.|Сочетание недоступно или неверно. Прежние сочетания продолжают работать.
Reset defaults|Восстановить стандартные
Local screenshot annotation utility for visual feedback.|Локальная утилита для снимков экрана и наглядных комментариев.
Open repository|Открыть репозиторий
Updates|Обновления
Check for updates|Проверить обновления
Checking…|Проверка…
You are up to date.|Установлена актуальная версия.
Version {0} is available.|Доступна версия {0}.
Download and install|Скачать и установить
Open release page|Открыть страницу выпуска
Portable copy: install updates manually from the release page.|Переносная версия: установите обновление вручную со страницы выпуска.
Downloading and verifying…|Загрузка и проверка…
Update check failed. Please retry.|Не удалось проверить обновления. Повторите попытку.
Update download or verification failed. Nothing was installed.|Не удалось скачать или проверить обновление. Ничего не установлено.
Install update?|Установить обновление?
ScreenIt will close to install the update. Your current screenshot session is stored only in memory and will be lost. Continue?|ScreenIt закроется для установки обновления. Текущая сессия снимков хранится только в оперативной памяти и будет потеряна. Продолжить?
The installer is unsigned. Windows SmartScreen may show a warning.|Установщик не подписан. Windows SmartScreen может показать предупреждение.
Install|Установить
Could not start the installer. Your session is unchanged.|Не удалось запустить установщик. Сессия не изменена.
Could not open the page.|Не удалось открыть страницу.
An interaction could not complete. Cancel/restart this capture.|Не удалось завершить действие. Отмените и повторите захват.
ScreenIt could not complete the interaction. Existing session data remain in memory.|Не удалось завершить действие. Текущая сессия осталась в памяти.
ScreenIt could not start. Check the desktop runtime and whether the capture hotkey is available.|Не удалось запустить ScreenIt. Проверьте среду выполнения и доступность горячих клавиш.
Some configured shortcuts were unavailable. Available defaults are active; open Settings.|Некоторые сочетания недоступны. Используются доступные стандартные; откройте настройки.
""";
}
