using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows.Input;

internal enum ThemePreference { System, Dark, Light }
internal enum GlobalAction { Capture = 1, Paste = 2, Clear = 3 }
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
    internal bool Valid => Modifiers is >0 and <=15 && MainKeys.ContainsKey(Key) && !(Modifiers==1 && Key==0x73) && !(Modifiers==2 && Key==0x56) && !(Modifiers==3 && Key==0x2E) && Modifiers!=8 && !(Modifiers==12 && Key==0x53) && !(Modifiers==10 && Key is 0x44 or 0x46);
    internal static Dictionary<GlobalAction,Hotkey> Defaults() => new() { [GlobalAction.Capture]=new(3,0x53),[GlobalAction.Paste]=new(3,0x56),[GlobalAction.Clear]=new(3,0x58) };
}
internal sealed class Preferences
{
    internal ThemePreference Theme { get; set; }=ThemePreference.System;
    internal string Language { get; set; }=DefaultLanguage(CultureInfo.CurrentUICulture);
    internal Dictionary<GlobalAction,Hotkey> Hotkeys { get; set; }=Hotkey.Defaults();
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
        var keys=root["hotkeys"] as JsonObject ?? new JsonObject();root["hotkeys"]=null;
        foreach(var (action,key) in Hotkeys) keys[action.ToString().ToLowerInvariant()]=new JsonObject { ["modifiers"]=key.Modifiers,["key"]=key.Key };
        root["hotkeys"]=keys;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);string tmp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
        try { File.WriteAllText(tmp,root.ToJsonString(new JsonSerializerOptions { WriteIndented=true })+"\n");File.Move(tmp,path,true);original=root; }
        finally { if(File.Exists(tmp)) File.Delete(tmp); }
    }
}
internal sealed class HotkeyRegistration(IntPtr hwnd, bool native=true) : IDisposable
{
    private readonly Dictionary<GlobalAction,(int Id,Hotkey Key)> entries=[];
    private int nextId=100;
    internal Func<int,Hotkey,bool> Register { get; set; }=(id,key)=> !native || Native.RegisterHotKey(hwnd,id,0x4000|key.Modifiers,key.Key);
    internal Action<int> Unregister { get; set; }=id=> { if(native) Native.UnregisterHotKey(hwnd,id); };
    internal GlobalAction? ActionFor(int id) => entries.Where(p=>p.Value.Id==id).Select(p=>(GlobalAction?)p.Key).FirstOrDefault();
    internal bool Start(Dictionary<GlobalAction,Hotkey> keys)
    {
        bool complete=true;
        foreach(var action in Enum.GetValues<GlobalAction>())
        {
            var key=keys[action];int id=(int)action;
            if(!Register(id,key))
            {
                complete=false;key=Hotkey.Defaults()[action];keys[action]=key;
                if(!Register(id,key)) continue; // Tray remains usable even when Windows owns the default.
            }
            entries[action]=(id,key);
        }
        return complete;
    }
    internal bool Replace(IReadOnlyDictionary<GlobalAction,Hotkey> keys,Func<bool>? commit=null)
    {
        if(keys.Count!=3 || keys.Values.Any(k=>!k.Valid) || keys.Values.Distinct().Count()!=3) return false;
        var staged=new Dictionary<GlobalAction,(int Id,Hotkey Key)>();
        foreach(var (action,key) in keys)
        {
            if(entries.TryGetValue(action,out var old) && old.Key==key) { staged[action]=old;continue; }
            // Reuse an existing registration when resetting/swapping bindings atomically.
            var reused=entries.Values.Where(v=>v.Key==key).ToArray();
            if(reused.Length==1) { staged[action]=reused[0];continue; }
            int id=entries.Count==0 ? (int)action : nextId++;
            if(!Register(id,key)) { foreach(var v in staged.Values.Where(v=>!entries.Values.Any(e=>e.Id==v.Id))) Unregister(v.Id);return false; }
            staged[action]=(id,key);
        }
        if(commit!=null && !commit()) { foreach(var v in staged.Values.Where(v=>!entries.Values.Any(e=>e.Id==v.Id))) Unregister(v.Id);return false; }
        foreach(var old in entries.Values.Where(v=>!staged.Values.Any(s=>s.Id==v.Id))) Unregister(old.Id);
        entries.Clear();foreach(var pair in staged) entries[pair.Key]=pair.Value;return true;
    }
    public void Dispose() { foreach(var v in entries.Values) Unregister(v.Id);entries.Clear(); }
}
