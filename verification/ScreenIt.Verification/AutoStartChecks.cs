using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Microsoft.Win32;

// Deterministic autostart coverage. Default isolation is fakes or a private HKCU subkey; the
// real per-user Run/StartupApproved values are only read, as an additional before/after safeguard.
internal static class AutoStartChecks
{
    private const string Exe = @"C:\ScreenItVerification\ScreenIt.App.exe";
    private const string ExpandableExe = @"%LOCALAPPDATA%\Portable\ScreenIt.App.exe";
    private static readonly string Command = "\"" + Exe + "\"";

    // Verification never constructs the real registry-backed implementation through the factory.
    internal static void UseFake() => AutoStart.Create = static () => new FakeAutoStart();

    internal static Task Run(Action<bool,string> check)
    {
        var before = RealValues();
        string basePath = @"Software\ScreenIt\Verification\" + Guid.NewGuid().ToString("N");
        var store = new WindowsRegistryStore();
        string runKey = basePath + @"\Run", approvalKey = basePath + @"\StartupApproved\Run";
        IAutoStart subject = new RegistryAutoStart(store, runKey, approvalKey, () => Exe);
        try
        {
            IsolatedRegistry(check, subject, store, runKey, approvalKey);
            ValueTypePreservation(check, store, runKey, approvalKey);
            Rollback(check);
            DisableRevalidation(check);
            CoordinatorAndSettings(check);
            check(RealValues().SequenceEqual(before), "Autostart verification never writes the real per-user startup configuration");
        }
        finally
        {
            try { Registry.CurrentUser.DeleteSubKeyTree(basePath, false); } catch (Exception) { }
            // Remove the now-empty isolated parents so verification leaves no registry litter. DeleteSubKey
            // throws when a parent still has subkeys, so an unrelated value is never removed.
            foreach (var parent in new[] { @"Software\ScreenIt\Verification", @"Software\ScreenIt" })
                try { Registry.CurrentUser.DeleteSubKey(parent, false); } catch (Exception) { }
        }
        return Task.CompletedTask;
    }

    private static void IsolatedRegistry(Action<bool,string> check, IAutoStart subject, IRegistryStore store, string runKey, string approvalKey)
    {
        store.Delete(runKey, "ScreenIt"); store.Delete(approvalKey, "ScreenIt");
        check(subject.Read() is { Registration: AutoStartRegistration.Missing, Approval: AutoStartApproval.NotPresent, Enabled: false }, "Autostart: absent registration is Missing and disabled");

        store.Set(runKey, "ScreenIt", Command, RegistryValueKind.String);
        check(subject.Read() is { Registration: AutoStartRegistration.Valid, Approval: AutoStartApproval.NotPresent, Enabled: true }, "Autostart: own quoted command is Valid and enabled without an approval record");

        store.Set(runKey, "ScreenIt", "\"D:\\Portable\\ScreenIt.App.exe\"", RegistryValueKind.String);
        check(subject.Read() is { Registration: AutoStartRegistration.Stale, Enabled: false }, "Autostart: another ScreenIt copy is Stale, never enabled");

        store.Set(runKey, "ScreenIt", "\"C:\\Windows\\notepad.exe\"", RegistryValueKind.String);
        check(subject.Read() is { Registration: AutoStartRegistration.Foreign, Enabled: false }, "Autostart: unrelated command is Foreign, never enabled");
        store.Set(runKey, "ScreenIt", 1234, RegistryValueKind.DWord);
        check(subject.Read() is { Registration: AutoStartRegistration.Foreign, Enabled: false }, "Autostart: a non-string value is Foreign, never enabled");

        foreach (byte flag in new byte[] { 0x02, 0x06 })
        {
            store.Set(runKey, "ScreenIt", Command, RegistryValueKind.String);
            store.Set(approvalKey, "ScreenIt", Approval(flag), RegistryValueKind.Binary);
            check(subject.Read() is { Registration: AutoStartRegistration.Valid, Approval: AutoStartApproval.Enabled, Enabled: true }, "Autostart: observed enabled approval byte " + flag);
        }
        foreach (byte flag in new byte[] { 0x03, 0x07 })
        {
            store.Set(runKey, "ScreenIt", Command, RegistryValueKind.String);
            store.Set(approvalKey, "ScreenIt", Approval(flag), RegistryValueKind.Binary);
            check(subject.Read() is { Registration: AutoStartRegistration.Valid, Approval: AutoStartApproval.Disabled, Enabled: false, ExternallyDisabled: true }, "Autostart: observed Windows-blocked approval byte " + flag);
        }
        store.Set(approvalKey, "ScreenIt", new byte[4], RegistryValueKind.Binary);
        check(subject.Read() is { Approval: AutoStartApproval.Unknown, Enabled: false, ApprovalUnknown: true, ExternallyDisabled: false }, "Autostart: unexpected approval length is Unknown, never confirmed blocking");
        store.Set(approvalKey, "ScreenIt", Approval(0x09), RegistryValueKind.Binary);
        check(subject.Read() is { Approval: AutoStartApproval.Unknown, Enabled: false, ExternallyDisabled: false }, "Autostart: unrecognized approval byte is Unknown, never confirmed blocking");

        store.Set(runKey, "ScreenIt", Command, RegistryValueKind.String);
        check(subject.Enable() == null && subject.Read().Enabled, "Autostart enable writes the quoted executable and confirms by readback");
        store.Set(approvalKey, "ScreenIt", Approval(0x07), RegistryValueKind.Binary);
        check(subject.Enable() == null && subject.Read() is { Registration: AutoStartRegistration.Valid, Approval: AutoStartApproval.NotPresent, Enabled: true }, "Autostart enable clears a Windows-blocked approval record");

        store.Set(runKey, "ScreenIt", "\"C:\\Windows\\notepad.exe\"", RegistryValueKind.String);
        check(subject.Enable() is { Kind: AutoStartFailureKind.ConflictOtherApplication } && (string?)store.Get(runKey, "ScreenIt")?.Data == "\"C:\\Windows\\notepad.exe\"", "Autostart enable refuses and preserves an unrelated application entry");

        string longPath = new string('x', 300) + @"\ScreenIt.App.exe";
        check(new RegistryAutoStart(store, runKey, approvalKey, () => longPath).Enable() is { Kind: AutoStartFailureKind.PathTooLong }, "Autostart enable refuses a path beyond the documented Run limit");
        check(RegistryAutoStart.MaxCommandLength == 260, "Autostart uses the documented 260-character Run limit");

        store.Set(runKey, "ScreenIt", Command, RegistryValueKind.String); store.Set(approvalKey, "ScreenIt", Approval(0x02), RegistryValueKind.Binary);
        check(subject.Disable() == null && subject.Read().Registration == AutoStartRegistration.Missing && store.Get(approvalKey, "ScreenIt") is null, "Autostart disable removes own registration and its approval record");

        store.Set(runKey, "ScreenIt", "\"C:\\Windows\\notepad.exe\"", RegistryValueKind.String); store.Set(approvalKey, "ScreenIt", Approval(0x02), RegistryValueKind.Binary);
        check(subject.Disable() is { Kind: AutoStartFailureKind.ConflictOtherApplication } && (string?)store.Get(runKey, "ScreenIt")?.Data == "\"C:\\Windows\\notepad.exe\"" && store.Get(approvalKey, "ScreenIt")?.Data is byte[], "Autostart disable refuses an unrelated entry and keeps its approval record");

        store.Set(runKey, "ScreenIt", "\"D:\\Portable\\ScreenIt.App.exe\"", RegistryValueKind.String);
        check(subject.Disable() is { Kind: AutoStartFailureKind.ConflictOtherCopy } && (string?)store.Get(runKey, "ScreenIt")?.Data == "\"D:\\Portable\\ScreenIt.App.exe\"", "Autostart disable refuses another ScreenIt copy");

        store.Delete(runKey, "ScreenIt"); store.Delete(approvalKey, "ScreenIt");
        check(subject.Disable() == null, "Autostart disable with no registration is harmless");
    }

    // The snapshot carries the real RegistryValueKind; a REG_EXPAND_SZ value must round-trip unexpanded
    // and be restored with its original type, not re-created as REG_SZ.
    private static void ValueTypePreservation(Action<bool,string> check, IRegistryStore store, string runKey, string approvalKey)
    {
        store.Set(runKey, "ScreenIt", ExpandableExe, RegistryValueKind.ExpandString);
        check(store.Get(runKey, "ScreenIt") is RegistryValue stored && stored.Kind == RegistryValueKind.ExpandString && (string?)stored.Data == ExpandableExe, "Autostart registry store preserves REG_EXPAND_SZ type and unexpanded data");

        // A path that changes between the write and the readback forces a compensating rollback.
        int lookups = 0;
        var switching = new RegistryAutoStart(store, runKey, approvalKey, () => ++lookups <= 2 ? Exe : @"C:\Other\ScreenIt.App.exe");
        var failure = switching.Enable();
        check(failure is { Kind: AutoStartFailureKind.Registry } && store.Get(runKey, "ScreenIt") is RegistryValue restored && restored.Kind == RegistryValueKind.ExpandString && (string?)restored.Data == ExpandableExe, "Autostart rollback restores a previous REG_EXPAND_SZ value with its exact type");
        store.Delete(runKey, "ScreenIt"); store.Delete(approvalKey, "ScreenIt");
    }

    private static void Rollback(Action<bool,string> check)
    {
        var memory = new MemoryStore();
        memory.Set("approval", "ScreenIt", Approval(0x03), RegistryValueKind.Binary);
        memory.SuppressDelete = (sub, _) => sub == "approval";
        var failure = new RegistryAutoStart(memory, "run", "approval", () => Exe).Enable();
        check(failure is { Kind: AutoStartFailureKind.Registry } && memory.Get("run", "ScreenIt") is null && memory.Get("approval", "ScreenIt")?.Data is byte[], "Autostart rollback restores an absent previous Run and keeps the approval record");

        memory = new MemoryStore();
        memory.Set("run", "ScreenIt", ExpandableExe, RegistryValueKind.ExpandString);
        memory.Set("approval", "ScreenIt", Approval(0x03), RegistryValueKind.Binary);
        memory.SuppressDelete = (sub, _) => sub == "approval";
        failure = new RegistryAutoStart(memory, "run", "approval", () => Exe).Enable();
        check(failure is { Kind: AutoStartFailureKind.Registry } && memory.Get("run", "ScreenIt") is RegistryValue previous && previous.Kind == RegistryValueKind.ExpandString && (string?)previous.Data == ExpandableExe, "Autostart rollback restores the previous value and registry type");

        memory = new MemoryStore();
        memory.SuppressDelete = (sub, _) => sub == "approval";
        memory.BeforeDelete = (sub, _) => { if (sub == "approval") memory.Set("run", "ScreenIt", "\"C:\\Windows\\notepad.exe\"", RegistryValueKind.String); };
        failure = new RegistryAutoStart(memory, "run", "approval", () => Exe).Enable();
        check(failure is { Kind: AutoStartFailureKind.RollbackConflict } && (string?)memory.Get("run", "ScreenIt")?.Data == "\"C:\\Windows\\notepad.exe\"", "Autostart rollback never overwrites an external change made during the operation");

        memory = new MemoryStore();
        memory.Set("run", "ScreenIt", "\"D:\\Portable\\ScreenIt.App.exe\"", RegistryValueKind.String);
        memory.Set("approval", "ScreenIt", Approval(0x03), RegistryValueKind.Binary);
        memory.SuppressDelete = (sub, _) => sub == "approval";
        int writes = 0;
        memory.SuppressSet = (sub, _) => sub == "run" && ++writes >= 2;
        failure = new RegistryAutoStart(memory, "run", "approval", () => Exe).Enable();
        check(failure is { Kind: AutoStartFailureKind.Rollback } && (string?)memory.Get("run", "ScreenIt")?.Data == Command, "Autostart reports a partial result when the previous value cannot be restored");

        memory = new MemoryStore();
        memory.Set("run", "ScreenIt", "\"D:\\Portable\\ScreenIt.App.exe\"", RegistryValueKind.String);
        memory.Set("approval", "ScreenIt", Approval(0x03), RegistryValueKind.Binary);
        memory.SkipSet = (sub, _) => sub == "run";
        memory.SuppressSet = (sub, _) => sub == "approval";
        failure = new RegistryAutoStart(memory, "run", "approval", () => Exe).Enable();
        check(failure is { Kind: AutoStartFailureKind.Rollback } && memory.Get("approval", "ScreenIt") is null && (string?)memory.Get("run", "ScreenIt")?.Data == "\"D:\\Portable\\ScreenIt.App.exe\"", "Autostart reports a partial result when the previous approval record cannot be restored");
    }

    // Ownership is revalidated immediately before deletion, so an entry replaced in the gap is preserved.
    private static void DisableRevalidation(Action<bool,string> check)
    {
        var memory = new MemoryStore();
        memory.Set("run", "ScreenIt", Command, RegistryValueKind.String);
        memory.AfterGet = (sub, _) => { if (sub == "run") memory.Set("run", "ScreenIt", "\"C:\\Windows\\notepad.exe\"", RegistryValueKind.String); };
        check(new RegistryAutoStart(memory, "run", "approval", () => Exe).Disable() is { Kind: AutoStartFailureKind.ConflictOtherApplication } && (string?)memory.Get("run", "ScreenIt")?.Data == "\"C:\\Windows\\notepad.exe\"", "Autostart disable revalidates ownership and preserves an entry replaced before deletion");

        memory = new MemoryStore();
        memory.Set("run", "ScreenIt", Command, RegistryValueKind.String);
        memory.AfterGet = (sub, _) => { if (sub == "run") memory.Set("run", "ScreenIt", "\"D:\\Portable\\ScreenIt.App.exe\"", RegistryValueKind.String); };
        check(new RegistryAutoStart(memory, "run", "approval", () => Exe).Disable() is { Kind: AutoStartFailureKind.ConflictOtherCopy }, "Autostart disable revalidation distinguishes another ScreenIt copy");
    }

    private static void CoordinatorAndSettings(Action<bool,string> check)
    {
        var fake = new FakeAutoStart();
        using var owner = new Coordinator(showTray: false, registerHotkey: false, autoStart: fake);
        check(owner.AutoStartStatus is { Registration: AutoStartRegistration.Missing, Enabled: false }, "Autostart Coordinator exposes the injected registration state");
        check(owner.ChangeAutoStart(true) && fake.Enabled && owner.AutoStartStatus.Enabled && owner.LastAutoStartFailure == null, "Autostart enable is routed through the Coordinator");
        check(owner.ChangeAutoStart(false) && !fake.Enabled && owner.AutoStartStatus.Registration == AutoStartRegistration.Missing, "Autostart disable is routed through the Coordinator");

        fake.EnableFailure = new(AutoStartFailureKind.ConflictOtherApplication);
        check(!owner.ChangeAutoStart(true) && !fake.Enabled && owner.LastAutoStartFailure is { Kind: AutoStartFailureKind.ConflictOtherApplication }, "Autostart conflict is reported and leaves the state unchanged");

        foreach (var language in new[] { "en", "ru" })
        {
            fake.Status = new(AutoStartRegistration.Missing, AutoStartApproval.NotPresent, null);
            fake.EnableFailure = null; fake.DisableFailure = null; fake.Enabled = false;
            L.Select(language);
            owner.ShowSettings();
            var settings = owner.Settings!;
            settings.SelectPage(0);
            var toggles = Descendants<ToggleButton>((DependencyObject)settings.Content).ToArray();
            check(toggles.Length == 1 && toggles[0].IsChecked == false && AutomationProperties.GetName(toggles[0]) == L.T("Start with Windows") && AutomationProperties.GetHelpText(toggles[0]) == L.T("Launch ScreenIt automatically when you sign in."), "Settings shows one localized startup switch " + language);
            check(Descendants<TextBlock>((DependencyObject)settings.Content).Any(t => t.Text == L.T("Start with Windows")), "Settings shows the localized startup section " + language);
            toggles[0].ApplyTemplate();
            check(toggles[0].Template.FindName("SwitchTrack", toggles[0]) is Border && toggles[0].Template.FindName("SwitchThumb", toggles[0]) is Border, "Startup switch uses the production toggle template " + language);

            fake.Status = new(AutoStartRegistration.Valid, AutoStartApproval.NotPresent, fake.Command);
            settings.SelectPage(0);
            check(Descendants<ToggleButton>((DependencyObject)settings.Content).Single().IsChecked == true, "Startup switch reflects an enabled registration " + language);

            fake.Status = new(AutoStartRegistration.Valid, AutoStartApproval.Disabled, fake.Command);
            settings.SelectPage(0);
            var blocked = Descendants<ToggleButton>((DependencyObject)settings.Content).Single();
            check(blocked.IsChecked == false && blocked.IsEnabled && Descendants<TextBlock>((DependencyObject)settings.Content).Any(t => t.Text == L.T("Windows is currently blocking this startup entry. Enable it in Windows startup apps.")), "Startup switch reflects external Windows blocking " + language);

            fake.Status = new(AutoStartRegistration.Valid, AutoStartApproval.Unknown, fake.Command);
            settings.SelectPage(0);
            var notes = Descendants<TextBlock>((DependencyObject)settings.Content).Select(t => t.Text).ToArray();
            check(notes.Contains(L.T("Windows manages this startup entry; ScreenIt cannot confirm its state.")) && !notes.Contains(L.T("Windows is currently blocking this startup entry. Enable it in Windows startup apps.")), "Unknown approval is never presented as confirmed blocking " + language);

            fake.Status = new(AutoStartRegistration.Unreadable, AutoStartApproval.NotPresent, null);
            settings.SelectPage(0);
            var unavailable = Descendants<ToggleButton>((DependencyObject)settings.Content).Single();
            check(!unavailable.IsEnabled && unavailable.IsChecked == false && Descendants<TextBlock>((DependencyObject)settings.Content).Any(t => t.Text == L.T("The Windows startup setting could not be read.")), "Startup switch is unavailable and explained when unreadable " + language);

            fake.Status = new(AutoStartRegistration.Missing, AutoStartApproval.NotPresent, null);
            settings.SelectPage(0);
            ToggleButton toggle = Descendants<ToggleButton>((DependencyObject)settings.Content).Single();
            toggle.IsChecked = true; toggle.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            check(fake.Enabled && owner.AutoStartStatus.Enabled, "Settings startup switch click enables autostart " + language);

            fake.EnableFailure = new(AutoStartFailureKind.PathTooLong);
            fake.Enabled = false;
            owner.ShowSettings();
            settings.SelectPage(0);
            toggle = Descendants<ToggleButton>((DependencyObject)settings.Content).Single();
            toggle.IsChecked = true; toggle.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            check(!fake.Enabled && settings.ValidationError == L.T("The installation path is too long for Windows startup. Move ScreenIt to a shorter folder."), "Settings shows the localized startup failure inline " + language);
            settings.Close();
        }
        L.Select("en");
    }

    private static byte[] Approval(byte flag) { var bytes = new byte[12]; bytes[0] = flag; return bytes; }
    private static object? Real(string subKey) { using var key = Registry.CurrentUser.OpenSubKey(subKey); return key?.GetValue("ScreenIt", null, RegistryValueOptions.DoNotExpandEnvironmentNames); }
    private static string[] RealValues() => new[] { Describe(Real(RegistryAutoStart.RunKey)), Describe(Real(RegistryAutoStart.ApprovalKey)) };
    private static string Describe(object? value) => value switch { null => "absent", byte[] bytes => Convert.ToHexString(bytes), _ => value.ToString() ?? "unknown" };
    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        if (root is T item) yield return item;
        foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>()) foreach (var descendant in Descendants<T>(child)) yield return descendant;
    }

    private sealed class FakeAutoStart : IAutoStart
    {
        internal AutoStartStatus Status = new(AutoStartRegistration.Missing, AutoStartApproval.NotPresent, null);
        internal AutoStartFailure? EnableFailure, DisableFailure;
        internal string Command = "\"" + Exe + "\"";
        internal bool Enabled;
        public AutoStartStatus Read() => Status;
        public AutoStartFailure? Enable() { if (EnableFailure != null) return EnableFailure; Enabled = true; Status = new(AutoStartRegistration.Valid, AutoStartApproval.NotPresent, Command); return null; }
        public AutoStartFailure? Disable() { if (DisableFailure != null) return DisableFailure; Enabled = false; Status = new(AutoStartRegistration.Missing, AutoStartApproval.NotPresent, null); return null; }
    }

    // In-memory registry substitute for deterministic rollback/type/revalidation coverage.
    private sealed class MemoryStore : IRegistryStore
    {
        private readonly Dictionary<string,(object Value,RegistryValueKind Kind)> values = new(StringComparer.Ordinal);
        internal Action<string,string>? BeforeDelete;
        internal Action<string,string>? AfterGet;
        internal Func<string,string,bool>? SuppressDelete;
        internal Func<string,string,bool>? SuppressSet;
        internal Func<string,string,bool>? SkipSet;
        private static string Key(string subKey,string name) => subKey + "\u0000" + name;
        public RegistryValue? Get(string subKey,string name)
        {
            var found = values.TryGetValue(Key(subKey,name),out var entry) ? new RegistryValue(entry.Value,entry.Kind) : (RegistryValue?)null;
            AfterGet?.Invoke(subKey,name);
            return found;
        }
        public void Set(string subKey,string name,object value,RegistryValueKind kind)
        {
            if (SkipSet?.Invoke(subKey,name) == true) return;
            if (SuppressSet?.Invoke(subKey,name) == true) throw new IOException("Synthetic registry write failure.");
            values[Key(subKey,name)] = (value,kind);
        }
        public void Delete(string subKey,string name) { BeforeDelete?.Invoke(subKey,name); if (SuppressDelete?.Invoke(subKey,name) == true) return; values.Remove(Key(subKey,name)); }
    }
}
