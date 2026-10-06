using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

internal static class ShortcutRecordingChecks
{
    internal static void State(Action<bool,string> check)
    {
        foreach(var mods in new uint[][] { [0x5B,0xA0],[0x5C,0xA3],[0x5B,0xA4],[0x5C],[0x5B,0xA2,0xA5,0xA1] })
        {
            var state=new ShortcutRecordingState();uint expected=8|(mods.Contains(0xA0u)||mods.Contains(0xA1u) ? 4u : 0)|(mods.Contains(0xA2u)||mods.Contains(0xA3u) ? 2u : 0)|(mods.Contains(0xA4u)||mods.Contains(0xA5u) ? 1u : 0);
            foreach(uint key in mods) check(state.Process(key,true,false).Suppress,"Recorder owns fresh modifier down "+key);
            check(state.Process(0x58,true,false) is { Suppress:true,Complete:false },"Recorder suppresses main key and waits for releases "+expected);
            check(!state.Process(0x58,true,false).Complete && state.Process(0x58,false,false).Suppress,"Main repeat/up paired without duplicate result "+expected);
            for(int i=mods.Length-1;i>=0;i--) { var result=state.Process(mods[i],false,false);check(result.Suppress && (i!=0 ? !result.Complete : result.Complete && result.Key==new Hotkey(expected,0x58)),"Win preserved across modifier release order "+expected+":"+i); }
            check(!state.Process(0x41,true,false).Suppress,"Completed recorder no longer suppresses keys");
        }
        var seeded=new ShortcutRecordingState();seeded.Seed(0xA2);seeded.Process(0x5B,true,false);seeded.Process(0x58,true,false);seeded.Process(0x58,false,false);seeded.Process(0x5B,false,false);
        check(seeded.Process(0xA2,false,false) is { Suppress:false,Complete:true,Key: { Modifiers:10,Key:0x58 } },"Already held Ctrl is recorded but its forwarded release is not suppressed");
        var cancel=new ShortcutRecordingState();cancel.Process(0x5B,true,false);cancel.Process(0x1B,true,false);cancel.Process(0x1B,false,false);
        check(cancel.Process(0x5B,false,false) is { Suppress:true,Complete:true,Key:null },"Esc cancels with paired Win release and no chord");
        var empty=new ShortcutRecordingState();empty.Process(0x5B,true,false);check(empty.Process(0x5B,false,false) is { Suppress:true,Complete:false },"Win-only press/release does not complete or leak Start gesture");
        check(!empty.Process(0x5B,true,true).Suppress && !empty.Process(0x56,true,true).Complete,"Production recording ignores injected input");
        var modifierFirst=new ShortcutRecordingState();modifierFirst.Process(0x5C,true,false);modifierFirst.Process(0xA1,true,false);modifierFirst.Process(0x58,true,false);
        check(!modifierFirst.Process(0x5C,false,false).Complete && !modifierFirst.Process(0xA1,false,false).Complete && modifierFirst.Process(0x58,false,false) is { Suppress:true,Complete:true,Key: { Modifiers:12,Key:0x58 } },"Modifiers released before main key preserve the captured chord");
    }
    internal static async Task Ui(Action<bool,string> check,string path)
    {
        using var owner=new Coordinator(false,false) { PreferencesPath=path };
        owner.Preferences.Save(path);var attempted=new List<Hotkey>();int hooks=0;
        owner.HotkeyBindings.Register=(_,key)=> { attempted.Add(key);return key!=new Hotkey(12,0x56) && key!=new Hotkey(12,0x53); };
        owner.HotkeyBindings.InstallReserved=()=> { hooks++;return true; };
        owner.ShowSettings();var settings=owner.Settings!;settings.RecordInjectedInput=true;settings.SelectPage(1);
        foreach(var action in new[]{GlobalAction.Paste,GlobalAction.Clear})
        {
            Begin(settings,owner.Preferences.Hotkeys[action]);check(settings.RecorderInstalled,"All actions install temporary recorder "+action);
            Send([0x5B,0xA0,0x58]);await Idle();
            check(!settings.Recording && !settings.RecorderInstalled && attempted.Last()==new Hotkey(12,0x58) && owner.Preferences.Hotkeys[action]==new Hotkey(12,0x58) && Preferences.Load(path).Hotkeys[action]==new Hotkey(12,0x58),"Native hook records full Win+Shift+X into registration/UI/persistence "+action);
            check(owner.ChangeHotkeys(Hotkey.Defaults()),"Reset between recorder action checks "+action);settings.SelectPage(1);
            foreach(uint main in new uint[]{0x56,0x53})
            {
                byte[] before=System.IO.File.ReadAllBytes(path);Begin(settings,owner.Preferences.Hotkeys[action]);Send([0x5C,0xA1,main]);await Idle();
                var chord=new Hotkey(12,main);
                check(attempted.Last()==chord && settings.ValidationError.Contains(chord.ToString(),StringComparison.Ordinal) && !settings.Recording && !settings.RecorderInstalled && owner.Preferences.Hotkeys[action]==Hotkey.Defaults()[action] && before.SequenceEqual(System.IO.File.ReadAllBytes(path)) && hooks==0,"Native unavailable Win chord reaches ordinary registration and restores field/settings "+action+chord);
            }
        }
        foreach(var keys in new uint[][] { [0x5B,0xA2,0x58],[0x5C,0xA4,0x58],[0x5B,0x58] })
        {
            Begin(settings,owner.Preferences.Hotkeys[GlobalAction.Paste]);Send(keys);await Idle();
            check(owner.Preferences.Hotkeys[GlobalAction.Paste].Modifiers==(keys.Length==2 ? 8u : keys[1]==0xA2 ? 10u : 9u) && !settings.RecorderInstalled,"Native Win+Ctrl/Alt/bare Win chord preserves modifiers "+keys.Length+keys[1]);
        }
        Begin(settings,owner.Preferences.Hotkeys[GlobalAction.Capture]);Send([0x5B,0xA0,0x53]);await Idle();
        check(owner.Preferences.Hotkeys[GlobalAction.Capture]==new Hotkey(12,0x53) && hooks==1 && !settings.RecorderInstalled,"Recorded Capture Win+Shift+S retains special runtime backend");
        Begin(settings,owner.Preferences.Hotkeys[GlobalAction.Paste]);Send([0x5B,0x1B]);await Idle();check(!settings.Recording && !settings.RecorderInstalled,"Native Esc cancels recording and disposes hook");
        Begin(settings,owner.Preferences.Hotkeys[GlobalAction.Paste]);Send([0x5B]);await Idle();check(settings.Recording && settings.RecorderInstalled,"Native Win-only gesture remains pending without changing binding");
        settings.Hide();await Idle();check(!settings.Recording && !settings.RecorderInstalled,"Hide/focus loss removes pending recording hook");
        settings.Show();settings.Activate();settings.SelectPage(1);Begin(settings,owner.Preferences.Hotkeys[GlobalAction.Paste]);
        var other=new Window { Width=200,Height=100,ShowInTaskbar=false };try { other.Show();other.Activate();await Idle();check(!settings.Recording && !settings.RecorderInstalled,"Actual Settings deactivation removes pending recorder"); }finally { other.Close(); }
        settings.Activate();Begin(settings,owner.Preferences.Hotkeys[GlobalAction.Paste]);settings.Close();check(!settings.RecorderInstalled && !settings.Recording,"Settings close removes recording hook");
        // Repeat native install/uninstall without producing a chord. Source handles
        // and message threads are owned by each scope, including early cancellation.
        var host=new Window();var hwnd=new System.Windows.Interop.WindowInteropHelper(host).EnsureHandle();
        int beforeThreads=System.Diagnostics.Process.GetCurrentProcess().Threads.Count;
        for(int i=0;i<20;i++) { using var recorder=new ShortcutRecordingHook(hwnd,i); }
        check(System.Diagnostics.Process.GetCurrentProcess().Threads.Count<=beforeThreads+1,"Twenty recorder cancellation cycles leave no message threads");host.Close();
        owner.ShowSettings();settings=owner.Settings!;settings.RecordInjectedInput=true;settings.SelectPage(1);
        Begin(settings,owner.Preferences.Hotkeys[GlobalAction.Paste]);Begin(settings,owner.Preferences.Hotkeys[GlobalAction.Clear]);Send([0x5B,0x1B]);await Idle();
        check(!settings.Recording && !settings.RecorderInstalled,"Switching recording fields cancels the old scope and Esc cancels the new scope");
        Begin(settings,owner.Preferences.Hotkeys[GlobalAction.Paste]);Buttons((DependencyObject)settings.Content).Single(b=>Equals(b.Content,L.T("Reset defaults"))).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
        check(!settings.Recording && !settings.RecorderInstalled && owner.Preferences.Hotkeys.All(p=>p.Value==Hotkey.Defaults()[p.Key]),"Reset defaults ends recording and displays the active defaults");
        Begin(settings,owner.Preferences.Hotkeys[GlobalAction.Paste]);SendEvents([0x5B,0xA2,0x58],false);
        try { settings.Close();check(!settings.RecorderInstalled && !settings.Recording,"Closing Settings with held keys disposes recording immediately"); }
        finally { SendEvents([0x58,0xA2,0x5B],true); }
        owner.ShowSettings();settings=owner.Settings!;settings.RecordInjectedInput=true;settings.SelectPage(1);Begin(settings,owner.Preferences.Hotkeys[GlobalAction.Paste]);SendEvents([0x5C,0xA1],false);
        try { owner.Dispose();check(!settings.RecorderInstalled && !settings.Recording,"App shutdown while modifiers are held removes recording scope"); }
        finally { SendEvents([0xA1,0x5C],true); }
    }
    private static void Begin(SettingsWindow settings,Hotkey current)
    {
        settings.SelectPage(1);
        Buttons((DependencyObject)settings.Content).Single(b=>Equals(b.Content,current.ToString())).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
    }
    private static IEnumerable<Button> Buttons(DependencyObject root)
    {
        if(root is Button button) yield return button;
        foreach(var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>()) foreach(var nested in Buttons(child)) yield return nested;
    }
    private static async Task Idle() { await Task.Delay(80);await Dispatcher.Yield(DispatcherPriority.ApplicationIdle); }
    private static void Send(uint[] keys)
    {
        var inputs=keys.Select(k=>Input(k,false)).Concat(keys.Reverse().Select(k=>Input(k,true))).ToArray();
        if(SendInput((uint)inputs.Length,inputs,Marshal.SizeOf<PasteInput.Input>())!=inputs.Length) throw new InvalidOperationException("Recorder verification SendInput failed");
    }
    private static void SendEvents(uint[] keys,bool up)
    {
        var inputs=keys.Select(k=>Input(k,up)).ToArray();
        if(SendInput((uint)inputs.Length,inputs,Marshal.SizeOf<PasteInput.Input>())!=inputs.Length) throw new InvalidOperationException("Recorder verification key events failed");
    }
    private static PasteInput.Input Input(uint key,bool up) => new() { Type=1,Data=new() { Keyboard=new() { Vk=(ushort)key,Flags=(up ? 2u : 0)|(key is 0x5B or 0x5C ? 1u : 0) } } };
    [DllImport("user32.dll",SetLastError=true)] private static extern uint SendInput(uint count,PasteInput.Input[] inputs,int size);
}
