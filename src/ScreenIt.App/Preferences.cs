using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows.Input;

internal enum ThemePreference { System, Dark, Light }
internal enum GlobalAction { Capture = 1, Paste = 2, Clear = 3 }
internal enum AnnotationModifier { Ctrl, Shift, Alt }
internal sealed record Hotkey(uint Modifiers, uint Key)
{
    private static readonly IReadOnlyDictionary<uint,string> MainKeys = BuildMainKeys();
    private static Dictionary<uint,string> BuildMainKeys()
    {
        var keys=new Dictionary<uint,string> { [0x08]="Backspace",[0x2E]="Delete",[0x2D]="Insert",[0x24]="Home",[0x23]="End",[0x21]="PageUp",[0x22]="PageDown",[0x25]="Left",[0x27]="Right",[0x26]="Up",[0x28]="Down",[0x20]="Space" };
        for(uint key=0x30;key<=0x39;key++) keys[key]=((char)key).ToString();
        for(uint key=0x41;key<=0x5A;key++) keys[key]=((char)key).ToString();
        for(uint key=0x70;key<=0x7B;key++) keys[key]="F"+(key-0x6F);
        return keys;
    }
    public override string ToString() => string.Join("+", new[] { (2u,"Ctrl"),(1u,"Alt"),(8u,"Win"),(4u,"Shift") }.Where(x=>(Modifiers & x.Item1)!=0).Select(x=>x.Item2).Append(MainKeys.TryGetValue(Key,out var name) ? name : KeyInterop.KeyFromVirtualKey((int)Key).ToString()));
    internal bool ReservedCapture => Modifiers==12 && Key==0x53;
    internal bool Valid => Modifiers is >0 and <=15 && MainKeys.ContainsKey(Key);
    internal bool UsesReservedHook(GlobalAction action) => action==GlobalAction.Capture && ReservedCapture;
    internal static Dictionary<GlobalAction,Hotkey> Defaults() => new() { [GlobalAction.Capture]=new(3,0x53),[GlobalAction.Paste]=new(3,0x56),[GlobalAction.Clear]=new(3,0x58) };
}
internal sealed class Preferences
{
    internal ThemePreference Theme { get; set; }=ThemePreference.System;
    internal string Language { get; set; }=DefaultLanguage(CultureInfo.CurrentUICulture);
    internal Dictionary<GlobalAction,Hotkey> Hotkeys { get; set; }=Hotkey.Defaults();
    internal AnnotationModifier AnnotationModifier { get; set; }=AnnotationModifier.Ctrl;
    private JsonObject original=new();
    internal static string DefaultLanguage(CultureInfo culture) => culture.TwoLetterISOLanguageName=="ru" ? "ru" : "en";
    internal static Preferences Load(string path)
    {
        var p=new Preferences();
        try
        {
            if(!File.Exists(path) || new FileInfo(path).Length>65536) return p;
            p.original=JsonNode.Parse(File.ReadAllText(path),documentOptions:new JsonDocumentOptions { MaxDepth=12 }) as JsonObject ?? new();
            if(p.original["theme"] is JsonValue t && t.TryGetValue<string>(out var theme) && theme is "system" or "dark" or "light") p.Theme=Enum.Parse<ThemePreference>(theme,true);
            if(p.original["language"] is JsonValue l && l.TryGetValue<string>(out var lang)) p.Language=lang is "en" or "ru" ? lang : "en";
            if(p.original["annotationModifier"] is JsonValue a && a.TryGetValue<string>(out var modifier) && modifier is "ctrl" or "shift" or "alt") p.AnnotationModifier=Enum.Parse<AnnotationModifier>(modifier,true);
            if(p.original["hotkeys"] is JsonObject keys)
                foreach(var action in Enum.GetValues<GlobalAction>())
                    if(keys[action.ToString().ToLowerInvariant()] is JsonObject key && key["modifiers"] is JsonValue m && m.TryGetValue<uint>(out uint modifiers) && key["key"] is JsonValue k && k.TryGetValue<uint>(out uint vk))
                    { var candidate=new Hotkey(modifiers,vk);if(candidate.Valid) p.Hotkeys[action]=candidate; }
            for(int i=0;i<3 && p.Hotkeys.Values.Distinct().Count()!=3;i++)
                foreach(var action in p.Hotkeys.GroupBy(pair=>pair.Value).Where(group=>group.Count()>1).SelectMany(group=>group.Select(pair=>pair.Key)).ToArray()) p.Hotkeys[action]=Hotkey.Defaults()[action];
        }
        catch(Exception e) when(e is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException) { }
        return p;
    }
    internal void Save(string path)
    {
        var root=(JsonObject)original.DeepClone();root["schemaVersion"]=2;root["theme"]=Theme.ToString().ToLowerInvariant();root["language"]=Language;
        root["annotationModifier"]=AnnotationModifier.ToString().ToLowerInvariant();
        var keys=root["hotkeys"] as JsonObject ?? new JsonObject();root["hotkeys"]=null;
        foreach(var (action,key) in Hotkeys) keys[action.ToString().ToLowerInvariant()]=new JsonObject { ["modifiers"]=key.Modifiers,["key"]=key.Key };
        root["hotkeys"]=keys;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);string tmp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
        try { File.WriteAllText(tmp,root.ToJsonString(new JsonSerializerOptions { WriteIndented=true })+"\n");File.Move(tmp,path,true);original=root; }
        finally { if(File.Exists(tmp)) File.Delete(tmp); }
    }
}
internal enum HotkeyFailureKind { Invalid, Duplicate, Unavailable, ReservedHook, Persistence, Busy }
internal sealed record HotkeyFailure(HotkeyFailureKind Kind,Hotkey? Key=null,GlobalAction? OtherAction=null)
{
    internal string Message => Kind switch
    {
        HotkeyFailureKind.Invalid => Key==null ? L.T("Shortcut set is invalid.") : string.Format(L.T("{0} is not supported by ScreenIt."),Key),
        HotkeyFailureKind.Duplicate => string.Format(L.T("{0} is already assigned to “{1}”."),Key,L.T(OtherAction==GlobalAction.Paste ? "Paste Session" : OtherAction==GlobalAction.Clear ? "Clear Session" : "Capture")),
        HotkeyFailureKind.Unavailable => string.Format(L.T("{0} is unavailable. It is already used by Windows or another application."),Key),
        HotkeyFailureKind.ReservedHook => string.Format(L.T("{0} interception could not be enabled. Previous shortcuts remain active."),Key),
        HotkeyFailureKind.Persistence => L.T("Settings could not be saved. Previous shortcuts remain active."),
        _ => L.T("Finish the current operation before changing shortcuts.")
    };
}
internal sealed class HotkeyRegistration(IntPtr hwnd, bool native=true) : IDisposable
{
    private readonly Dictionary<GlobalAction,(int Id,Hotkey Key,bool Reserved)> entries=[];
    private int nextId=100;
    private ReservedCaptureHook? reserved;
    private readonly HashSet<int> reservedIds=[];
    internal bool ReservedHookInstalled => reserved!=null;
    internal HotkeyFailure? LastFailure { get; private set; }
    internal List<HotkeyFailure> StartupFailures { get; }=[];
    internal Func<bool>? InstallReserved { get; set; }
    internal Func<int,Hotkey,bool> Register { get; set; }=null!;
    internal Action<int> Unregister { get; set; }=null!;
    private bool EnsureReserved()
    {
        if(!native) return InstallReserved?.Invoke() ?? true;
        if(reserved!=null) return true;
        try { reserved=new(hwnd);return true; }
        catch(System.ComponentModel.Win32Exception ex) { System.Diagnostics.Trace.TraceError("ScreenIt reserved shortcut installation failed: {0}",ex.NativeErrorCode);return false; }
    }
    private bool RegisterBinding(GlobalAction action,int id,Hotkey key)
    {
        bool special=key.UsesReservedHook(action);
        if(special ? !EnsureReserved() : !Register(id,key)) { LastFailure=new(special ? HotkeyFailureKind.ReservedHook : HotkeyFailureKind.Unavailable,key);return false; }
        if(special) reservedIds.Add(id);
        return true;
    }
    private void UnregisterKey(int id)
    {
        if(reservedIds.Remove(id)) { RefreshReserved();return; }
        Unregister(id);
    }
    private void RefreshReserved()
    {
        int id=entries.Values.Where(v=>v.Reserved && reservedIds.Contains(v.Id)).Select(v=>v.Id).FirstOrDefault();
        reserved?.Configure(id);
        if(reservedIds.Count==0) { reserved?.Dispose();reserved=null; }
    }
    private void InitializeCallbacks()
    {
        Register ??=(id,key)=>!native || Native.RegisterHotKey(hwnd,id,0x4000|key.Modifiers,key.Key);
        Unregister ??=id=> { if(native) Native.UnregisterHotKey(hwnd,id); };
    }
    internal GlobalAction? ActionFor(int id) => entries.Where(p=>p.Value.Id==id).Select(p=>(GlobalAction?)p.Key).FirstOrDefault();
    internal bool Start(Dictionary<GlobalAction,Hotkey> keys)
    {
        InitializeCallbacks();LastFailure=null;StartupFailures.Clear();
        bool complete=true;
        foreach(var action in Enum.GetValues<GlobalAction>())
        {
            var key=keys[action];int id=(int)action;
            if(!RegisterBinding(action,id,key))
            {
                StartupFailures.Add(LastFailure!);
                complete=false;key=Hotkey.Defaults()[action];keys[action]=key;
                if(!RegisterBinding(action,id,key)) continue; // Tray remains usable even when Windows owns the default.
            }
            entries[action]=(id,key,key.UsesReservedHook(action));
        }
        RefreshReserved();return complete;
    }
    internal bool Replace(IReadOnlyDictionary<GlobalAction,Hotkey> keys,Func<bool>? commit=null)
    {
        InitializeCallbacks();LastFailure=null;
        if(keys.Count!=3 || Enum.GetValues<GlobalAction>().Any(a=>!keys.ContainsKey(a))) { LastFailure=new(HotkeyFailureKind.Invalid);return false; }
        foreach(var (action,key) in keys)
        {
            if(!key.Valid) { LastFailure=new(HotkeyFailureKind.Invalid,key);return false; }
            var duplicates=keys.Where(p=>p.Key!=action && p.Value==key).ToArray();
            if(duplicates.Length!=0)
            {
                // Prefer naming the already-bound action rather than the action being edited.
                var existing=duplicates.FirstOrDefault(p=>entries.TryGetValue(p.Key,out var entry) && entry.Key==key);
                var other=entries.TryGetValue(action,out var current) && current.Key==key ? action : existing.Value!=null ? existing.Key : duplicates[0].Key;
                LastFailure=new(HotkeyFailureKind.Duplicate,key,other);return false;
            }
        }
        var staged=new Dictionary<GlobalAction,(int Id,Hotkey Key,bool Reserved)>();
        foreach(var (action,key) in keys)
        {
            if(entries.TryGetValue(action,out var old) && old.Key==key) { staged[action]=old;continue; }
            // Reuse an existing registration when resetting/swapping bindings atomically.
            var reused=entries.Values.Where(v=>v.Key==key && v.Reserved==key.UsesReservedHook(action)).ToArray();
            if(reused.Length==1) { staged[action]=reused[0];continue; }
            int id=entries.Count==0 ? (int)action : nextId++;
            if(!RegisterBinding(action,id,key)) { foreach(var v in staged.Values.Where(v=>!entries.Values.Any(e=>e.Id==v.Id))) UnregisterKey(v.Id);return false; }
            staged[action]=(id,key,key.UsesReservedHook(action));
        }
        if(commit!=null && !commit()) { LastFailure=new(HotkeyFailureKind.Persistence);foreach(var v in staged.Values.Where(v=>!entries.Values.Any(e=>e.Id==v.Id))) UnregisterKey(v.Id);return false; }
        foreach(var old in entries.Values.Where(v=>!staged.Values.Any(s=>s.Id==v.Id))) UnregisterKey(old.Id);
        entries.Clear();foreach(var pair in staged) entries[pair.Key]=pair.Value;RefreshReserved();return true;
    }
    public void Dispose() { InitializeCallbacks();reserved?.Configure(0);foreach(var v in entries.Values) UnregisterKey(v.Id);entries.Clear();reserved?.Dispose();reserved=null; }
}
