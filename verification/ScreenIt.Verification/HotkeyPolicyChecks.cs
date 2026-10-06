using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

internal static class HotkeyPolicyChecks
{
    internal static void State(Action<bool,string> check)
    {
        foreach(var action in Enum.GetValues<GlobalAction>())
        {
            using var keys=new HotkeyRegistration(IntPtr.Zero,false);int ordinaryCalls=0,hookCalls=0;
            keys.Register=(_,key)=> { ordinaryCalls++;return key!=new Hotkey(12,0x56); };
            keys.InstallReserved=()=> { hookCalls++;return true; };
            keys.Start(Hotkey.Defaults());var proposed=Hotkey.Defaults();proposed[action]=new(12,0x58);
            bool saved=false;check(keys.Replace(proposed,()=>saved=true) && saved && hookCalls==0,"Available Win+Shift+X uses RegisterHotKey for "+action);
            proposed[action]=new(12,0x56);saved=false;int before=ordinaryCalls;
            check(!keys.Replace(proposed,()=>saved=true) && !saved && ordinaryCalls==before+1 && hookCalls==0,"Unavailable Win+Shift+V rejected by ordinary registration for "+action);
            check(keys.LastFailure is { Kind:HotkeyFailureKind.Unavailable,Key: { Modifiers:12,Key:0x56 } },"Global conflict classification retains exact chord "+action);
            foreach(string language in new[]{"en","ru"}) { L.Select(language);check(keys.LastFailure!.Message.Contains("Win+Shift+V",StringComparison.Ordinal) && keys.LastFailure.Message.Contains(language=="ru" ? "Windows или другим приложением" : "Windows or another application",StringComparison.Ordinal),"Conflict message names chord and does not guess owner "+language+action); }
        }
        L.Select("en");
        foreach(var action in new[]{GlobalAction.Paste,GlobalAction.Clear})
        {
            using var keys=new HotkeyRegistration(IntPtr.Zero,false);int hooks=0,calls=0;bool available=false;
            keys.InstallReserved=()=> { hooks++;return true; };keys.Register=(_,key)=> { if(key.ReservedCapture) { calls++;return available; }return true; };
            keys.Start(Hotkey.Defaults());var proposed=Hotkey.Defaults();proposed[action]=new(12,0x53);
            check(!keys.Replace(proposed) && calls==1 && hooks==0 && keys.LastFailure?.Kind==HotkeyFailureKind.Unavailable,"Non-Capture Win+Shift+S attempts RegisterHotKey and never hooks "+action);
            available=true;check(keys.Replace(proposed) && calls==2 && hooks==0,"Non-Capture Win+Shift+S accepted when ordinary registration succeeds "+action);
        }
        using(var keys=new HotkeyRegistration(IntPtr.Zero,false))
        {
            int hooks=0;bool ordinaryS=false;var calls=new List<Hotkey>();var live=new HashSet<int>();
            keys.InstallReserved=()=> { hooks++;return true; };keys.Register=(id,key)=> { calls.Add(key);return (!key.ReservedCapture || ordinaryS) && live.Add(id); };keys.Unregister=id=>live.Remove(id);
            var reserved=Hotkey.Defaults();reserved[GlobalAction.Capture]=new(12,0x53);keys.Start(reserved);
            check(hooks==1 && calls.Count==2,"Capture Win+Shift+S bypasses ordinary registration exclusively");
            var moved=Hotkey.Defaults();moved[GlobalAction.Capture]=new(6,0x51);moved[GlobalAction.Clear]=new(12,0x53);
            bool saved=false;check(!keys.Replace(moved,()=>saved=true) && !saved && hooks==1 && keys.ActionFor(1)==GlobalAction.Capture && live.Count==2 && calls.Last().ReservedCapture,"Reserved-to-ordinary action move cannot reuse hook; failure rolls back staged registrations");
            ordinaryS=true;check(keys.Replace(moved) && keys.ActionFor(1)==null && hooks==1,"Accepted Capture rebind releases reserved backend");
            check(keys.Replace(reserved) && hooks==2,"Ordinary-to-reserved action move installs hook instead of reusing ordinary ID");
            var duplicate=new Dictionary<GlobalAction,Hotkey>(reserved) { [GlobalAction.Clear]=reserved[GlobalAction.Paste] };int before=calls.Count;
            check(!keys.Replace(duplicate) && calls.Count==before && keys.LastFailure is { Kind:HotkeyFailureKind.Duplicate,OtherAction:GlobalAction.Paste },"Duplicate classified before native registration with existing action name");
            L.Select("ru");check(keys.LastFailure!.Message.Contains("Ctrl+Alt+V",StringComparison.Ordinal) && keys.LastFailure.Message.Contains("Вставить сессию",StringComparison.Ordinal),"Duplicate message names localized existing action");L.Select("en");
            var invalid=new Dictionary<GlobalAction,Hotkey>(reserved) { [GlobalAction.Clear]=new(0,0x53) };
            check(!keys.Replace(invalid) && keys.LastFailure?.Kind==HotkeyFailureKind.Invalid && calls.Count==before,"Invalid structural shortcut is distinct from global conflict");
            check(!keys.Replace(Hotkey.Defaults(),()=>false) && keys.LastFailure?.Kind==HotkeyFailureKind.Persistence,"Save failure is not reported as Windows conflict");
        }
        foreach(var chord in new[]{new Hotkey(8,0x4C),new Hotkey(10,0x44),new Hotkey(10,0x46),new Hotkey(1,0x73),new Hotkey(2,0x56),new Hotkey(3,0x2E)}) check(chord.Valid,"No hardcoded OS chord blacklist: "+chord);
    }
    internal static async Task Ui(Action<bool,string> check,string path,Action<Window,string> preview)
    {
        using var owner=new Coordinator(false,false) { PreferencesPath=path };
        owner.Preferences.Save(path);var original=File.ReadAllBytes(path);
        owner.HotkeyBindings.Register=(_,key)=>key!=new Hotkey(12,0x56);
        owner.ShowSettings();var settings=owner.Settings!;
        foreach(var theme in new[]{ThemePreference.Dark,ThemePreference.Light}) foreach(var language in new[]{"ru","en"})
        {
            Appearance.Choose(theme);L.Select(language);settings.SelectPage(1);
            Find<Button>((DependencyObject)settings.Content).Single(b=>b.Content is string text && text=="Ctrl+Alt+V").RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
            settings.RecordShortcut(new(12,0x56));await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            check(settings.ValidationError.Contains("Win+Shift+V",StringComparison.Ordinal) && Find<TextBlock>((DependencyObject)settings.Content).Any(t=>t.Text==settings.ValidationError),"Settings immediately displays exact rejected chord "+theme+language);
            check(owner.Preferences.Hotkeys[GlobalAction.Paste]==new Hotkey(3,0x56) && original.SequenceEqual(File.ReadAllBytes(path)) && owner.HotkeyBindings.ActionFor(2)==GlobalAction.Paste,"Failed UI rebind retains working mapping and persisted bytes "+theme+language);
            preview(settings,"conflict-"+theme.ToString().ToLowerInvariant()+"-"+language);
        }
        settings.SelectPage(1);
        Find<Button>((DependencyObject)settings.Content).Single(b=>b.Content is string text && text=="Ctrl+Alt+V").RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));settings.RecordShortcut(new(12,0x58));
        check(Preferences.Load(path).Hotkeys[GlobalAction.Paste]==new Hotkey(12,0x58) && !settings.Recording,"Accepted ordinary UI rebind persists after prior conflicts");
        settings.Close();L.Select("en");Appearance.Choose(ThemePreference.Dark);
    }
    private static IEnumerable<T> Find<T>(DependencyObject root) where T:DependencyObject
    {
        if(root is T item) yield return item;
        foreach(var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>()) foreach(var descendant in Find<T>(child)) yield return descendant;
    }
}
