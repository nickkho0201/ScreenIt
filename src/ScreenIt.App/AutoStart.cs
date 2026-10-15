using Microsoft.Win32;
using System.Security;

// Optional "Start with Windows" integration for the per-user Run key.
// HKCU\...\Run is the single source of truth; the setting is never duplicated into settings.json.
// A registration that belongs to another ScreenIt copy or to an unrelated application is never
// silently removed. StartupApproved is undocumented, so it is interpreted conservatively and a
// non-confirmed state is never presented as confirmed Windows blocking.
internal enum AutoStartRegistration { Missing, Valid, Stale, Foreign, Unreadable }
internal enum AutoStartApproval { NotPresent, Enabled, Disabled, Unknown }

internal readonly record struct AutoStartStatus(AutoStartRegistration Registration, AutoStartApproval Approval, string? Command)
{
    // Enabled means exactly our command is registered and Windows is not blocking it.
    internal bool Enabled => Registration == AutoStartRegistration.Valid && Approval is AutoStartApproval.NotPresent or AutoStartApproval.Enabled;
    // Observed Windows-blocked approval states. Undocumented, so interpreted only for a registration we own.
    internal bool ExternallyDisabled => Registration == AutoStartRegistration.Valid && Approval == AutoStartApproval.Disabled;
    // An unread approval blob must never be presented as confirmed blocking.
    internal bool ApprovalUnknown => Registration == AutoStartRegistration.Valid && Approval == AutoStartApproval.Unknown;
}

internal enum AutoStartFailureKind { ConflictOtherApplication, ConflictOtherCopy, PathTooLong, Unreadable, Registry, Rollback, RollbackConflict }
internal sealed record AutoStartFailure(AutoStartFailureKind Kind)
{
    internal string Message => Kind switch
    {
        AutoStartFailureKind.ConflictOtherApplication => L.T("Another application already uses the ScreenIt startup entry. It was left unchanged."),
        AutoStartFailureKind.ConflictOtherCopy => L.T("Another ScreenIt copy owns this startup entry. It was left unchanged."),
        AutoStartFailureKind.PathTooLong => L.T("The installation path is too long for Windows startup. Move ScreenIt to a shorter folder."),
        AutoStartFailureKind.Unreadable => L.T("The Windows startup setting could not be read."),
        AutoStartFailureKind.Rollback => L.T("The startup setting is only partially applied. Check Windows startup apps."),
        AutoStartFailureKind.RollbackConflict => L.T("Windows changed the startup entry during the change. It was left as is."),
        _ => L.T("The Windows startup setting could not be changed.")
    };
}

internal interface IAutoStart
{
    AutoStartStatus Read();
    // null means the requested state was applied and confirmed by readback.
    AutoStartFailure? Enable();
    AutoStartFailure? Disable();
}

internal static class AutoStart
{
    // Verification seam, mirrors Appearance.ReadSystem. Production always uses the real registry.
    internal static Func<IAutoStart> Create { get; set; } = static () => new RegistryAutoStart();
}

// A registry value together with its actual type. The type is required to restore a previous value
// faithfully: inferring it from the CLR object cannot distinguish REG_SZ from REG_EXPAND_SZ.
internal readonly record struct RegistryValue(object Data, RegistryValueKind Kind);

// Minimal indirection over the registry so deterministic verification can supply an isolated or in-memory store.
internal interface IRegistryStore
{
    RegistryValue? Get(string subKey, string name);
    void Set(string subKey, string name, object value, RegistryValueKind kind);
    void Delete(string subKey, string name);
}

internal sealed class WindowsRegistryStore : IRegistryStore
{
    public RegistryValue? Get(string subKey, string name)
    {
        using var key = Registry.CurrentUser.OpenSubKey(subKey);
        if (key is null) return null;
        // DoNotExpandEnvironmentNames keeps a raw REG_EXPAND_SZ value unexpanded for faithful comparison and restore.
        var data = key.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
        return data is null ? null : new RegistryValue(data, key.GetValueKind(name));
    }
    public void Set(string subKey, string name, object value, RegistryValueKind kind)
    {
        using var key = Registry.CurrentUser.CreateSubKey(subKey, writable: true) ?? throw new IOException("Registry key is unavailable.");
        key.SetValue(name, value, kind);
    }
    public void Delete(string subKey, string name)
    {
        using var key = Registry.CurrentUser.OpenSubKey(subKey, writable: true);
        key?.DeleteValue(name, throwOnMissingValue: false);
    }
}

internal sealed class RegistryAutoStart : IAutoStart
{
    internal const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    internal const string ApprovalKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";
    internal const string ValueName = "ScreenIt";
    internal const string ExecutableName = "ScreenIt.App.exe";
    // Microsoft documents a 260-character maximum for Run command lines.
    internal const int MaxCommandLength = 260;

    private readonly IRegistryStore store;
    private readonly string runKey;
    private readonly string approvalKey;
    private readonly Func<string?> executablePath;

    internal RegistryAutoStart() : this(new WindowsRegistryStore(), RunKey, ApprovalKey) { }
    internal RegistryAutoStart(IRegistryStore store, string runKey, string approvalKey, Func<string?>? executablePath = null)
    {
        this.store = store; this.runKey = runKey; this.approvalKey = approvalKey;
        this.executablePath = executablePath ?? (() => Environment.ProcessPath);
    }

    public AutoStartStatus Read()
    {
        RegistryValue? stored;
        try { stored = store.Get(runKey, ValueName); }
        catch (Exception ex) when (IsRegistryFailure(ex)) { return new(AutoStartRegistration.Unreadable, AutoStartApproval.NotPresent, null); }
        if (stored is not RegistryValue value)
            return new(AutoStartRegistration.Missing, AutoStartApproval.NotPresent, null);
        if (value.Data is not string text)
            return new(AutoStartRegistration.Foreign, ReadApproval(), null);
        var registration = Owns(text)
            ? AutoStartRegistration.Valid
            : IsScreenItPath(Unquote(text)) ? AutoStartRegistration.Stale : AutoStartRegistration.Foreign;
        return new(registration, ReadApproval(), text);
    }

    public AutoStartFailure? Enable()
    {
        var command = Command();
        if (command == null) return new(AutoStartFailureKind.Registry);
        if (command.Length > MaxCommandLength) return new(AutoStartFailureKind.PathTooLong);

        AutoStartStatus current;
        try { current = Read(); }
        catch (Exception ex) when (IsRegistryFailure(ex)) { return new(AutoStartFailureKind.Unreadable); }
        if (current.Registration == AutoStartRegistration.Foreign) return new(AutoStartFailureKind.ConflictOtherApplication);
        if (current.Registration == AutoStartRegistration.Unreadable) return new(AutoStartFailureKind.Unreadable);

        Snapshot snapshot;
        try { snapshot = Capture(); }
        catch (Exception ex) when (IsRegistryFailure(ex)) { return new(AutoStartFailureKind.Registry); }

        try
        {
            store.Set(runKey, ValueName, command, RegistryValueKind.String);
            try { store.Delete(approvalKey, ValueName); } catch (Exception ex) when (IsRegistryFailure(ex)) { } // best effort; confirmed below
        }
        catch (Exception ex) when (IsRegistryFailure(ex)) { return Restore(snapshot, command); }

        // Success is decided by readback, not by the write calls.
        if (Read().Enabled) return null;
        return Restore(snapshot, command);
    }

    public AutoStartFailure? Disable()
    {
        AutoStartStatus current;
        try { current = Read(); }
        catch (Exception ex) when (IsRegistryFailure(ex)) { return new(AutoStartFailureKind.Unreadable); }
        if (current.Registration == AutoStartRegistration.Missing) return null;
        if (current.Registration == AutoStartRegistration.Foreign) return new(AutoStartFailureKind.ConflictOtherApplication);
        if (current.Registration == AutoStartRegistration.Stale) return new(AutoStartFailureKind.ConflictOtherCopy);
        if (current.Registration == AutoStartRegistration.Unreadable) return new(AutoStartFailureKind.Unreadable);

        // Revalidate ownership immediately before deleting. There is no atomic compare-and-delete for a
        // Run value, so an external re-registration in the gap between this check and Delete cannot be
        // fully prevented; the recheck only narrows that window.
        try
        {
            var recheck = store.Get(runKey, ValueName);
            if (recheck is not RegistryValue candidate) return null; // the entry disappeared on its own
            if (candidate.Data is not string text || !Owns(text)) return OwnershipConflict(candidate.Data as string);
            store.Delete(runKey, ValueName);
        }
        catch (Exception ex) when (IsRegistryFailure(ex)) { return new(AutoStartFailureKind.Registry); }

        // The approval record is meaningful only for the registration we just removed.
        try { if (store.Get(runKey, ValueName) is null) store.Delete(approvalKey, ValueName); }
        catch (Exception ex) when (IsRegistryFailure(ex)) { }

        return Read().Registration == AutoStartRegistration.Missing ? null : new(AutoStartFailureKind.Registry);
    }

    private string? Executable()
    {
        var path = executablePath();
        if (string.IsNullOrEmpty(path)) path = Path.Combine(AppContext.BaseDirectory, ExecutableName);
        return string.IsNullOrEmpty(path) ? null : path;
    }
    private string? Command() { var path = Executable(); return path == null ? null : "\"" + path + "\""; }
    private bool Owns(string stored) { var executable = Executable(); return executable != null && PathEquals(Unquote(stored), executable); }
    private static AutoStartFailure OwnershipConflict(string? stored) => stored != null && IsScreenItPath(Unquote(stored))
        ? new(AutoStartFailureKind.ConflictOtherCopy)
        : new(AutoStartFailureKind.ConflictOtherApplication);

    // Snapshot keeps the value and its real RegistryValueKind so compensation restores both.
    private readonly record struct Snapshot(RegistryValue? Run, RegistryValue? Approval);
    private Snapshot Capture() => new(store.Get(runKey, ValueName), store.Get(approvalKey, ValueName));

    // Compensating rollback: restore the previous values, but never overwrite a change made externally
    // during the operation. Values are compared structurally (bytes by content, plus the exact type).
    private AutoStartFailure Restore(Snapshot snapshot, string command)
    {
        bool conflict = false, failed = false;
        try
        {
            var run = store.Get(runKey, ValueName);
            // "Ours" is the exact value and type this Enable wrote; anything else is treated as external.
            if (run is RegistryValue current && current.Kind == RegistryValueKind.String && current.Data is string text && text == command)
            {
                if (snapshot.Run is RegistryValue previous) store.Set(runKey, ValueName, previous.Data, previous.Kind);
                else store.Delete(runKey, ValueName);
            }
            else if (!Same(run, snapshot.Run)) conflict = true;
        }
        catch (Exception ex) when (IsRegistryFailure(ex)) { failed = true; }

        try
        {
            var approval = store.Get(approvalKey, ValueName);
            if (approval is null)
            {
                if (snapshot.Approval is RegistryValue previous) store.Set(approvalKey, ValueName, previous.Data, previous.Kind);
            }
            else if (!Same(approval, snapshot.Approval)) conflict = true;
        }
        catch (Exception ex) when (IsRegistryFailure(ex)) { failed = true; }

        if (conflict) return new(AutoStartFailureKind.RollbackConflict);
        if (failed) return new(AutoStartFailureKind.Rollback);
        return new(AutoStartFailureKind.Registry);
    }

    private AutoStartApproval ReadApproval()
    {
        RegistryValue? stored;
        try { stored = store.Get(approvalKey, ValueName); }
        catch (Exception ex) when (IsRegistryFailure(ex)) { return AutoStartApproval.Unknown; }
        if (stored is not RegistryValue approval) return AutoStartApproval.NotPresent;
        // StartupApproved is undocumented. Only the commonly observed 12-byte blobs are interpreted:
        // 0x02/0x06 = enabled, 0x03/0x07 = disabled. Anything else stays Unknown.
        if (approval.Data is not byte[] bytes || bytes.Length != 12) return AutoStartApproval.Unknown;
        return bytes[0] switch { 0x02 or 0x06 => AutoStartApproval.Enabled, 0x03 or 0x07 => AutoStartApproval.Disabled, _ => AutoStartApproval.Unknown };
    }

    private static bool IsRegistryFailure(Exception ex) => ex is SecurityException or UnauthorizedAccessException or IOException;
    private static bool Same(RegistryValue? a, RegistryValue? b) => (a, b) switch
    {
        (null, null) => true,
        (RegistryValue x, RegistryValue y) => x.Kind == y.Kind && SameData(x.Data, y.Data),
        _ => false
    };
    private static bool SameData(object? a, object? b) => (a, b) switch { (null, null) => true, (byte[] x, byte[] y) => x.AsSpan().SequenceEqual(y), _ => Equals(a, b) };
    private static string Unquote(string value) { var trimmed = value.Trim(); return trimmed.Length >= 2 && trimmed[0] == '"' && trimmed[^1] == '"' ? trimmed[1..^1].Trim() : trimmed; }
    private static bool IsScreenItPath(string path) { try { return string.Equals(Path.GetFileName(path), ExecutableName, StringComparison.OrdinalIgnoreCase); } catch (ArgumentException) { return false; } }
    private static bool PathEquals(string path, string expected)
    {
        try { return string.Equals(Path.GetFullPath(path).TrimEnd('\\'), Path.GetFullPath(expected).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase); }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException or IOException) { return false; }
    }
}
