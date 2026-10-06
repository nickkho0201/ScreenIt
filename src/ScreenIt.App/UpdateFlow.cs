using System.Diagnostics;

internal enum UpdatePhase { Idle,Checking,UpToDate,Available,Downloading,Verifying,PreparingInstall,LaunchingInstaller,InstallerLaunched,Failed }
internal enum UpdateError { Check,Download,Verification,Preparation,Launch }
internal sealed record UpdateState(UpdatePhase Phase=UpdatePhase.Idle,UpdateRelease? Release=null,long? BytesDownloaded=null,long? TotalBytes=null,UpdateError? Error=null)
{
    internal Version? AvailableVersion => Release?.Version;
    internal double? Progress => Phase==UpdatePhase.Downloading && TotalBytes is >0 && BytesDownloaded.HasValue ? Math.Clamp((double)BytesDownloaded.Value/TotalBytes.Value,0,1) : null;
    internal bool Active => Phase is UpdatePhase.Checking or UpdatePhase.Downloading or UpdatePhase.Verifying or UpdatePhase.PreparingInstall or UpdatePhase.LaunchingInstaller;
}
internal interface IUpdateFlow
{
    UpdateState State { get; }
    bool CanInstall { get; }
    event Action? Changed;
    Task Check();
    Task Download();
    Task Retry();
    void OpenPage();
}

// Window-scoped operation owner, UI-free state. Async continuations stay on the
// caller's STA context; transport reports bytes at most every 100 ms.
internal sealed class UpdateFlow(IUpdateSource source,Func<bool> installed,Func<Task<bool>> confirm,Action<string> launch,Action close,Action<Uri> open) : IUpdateFlow,IDisposable
{
    private readonly CancellationTokenSource cancellation=new();
    private bool disposed;
    private int attempt;
    public UpdateState State { get; private set; }=new();
    public bool CanInstall { get; private set; }
    public event Action? Changed;
    private void Set(UpdateState state)
    {
        if(disposed) return;State=state;
        if(Changed is not { } changed) return;
        foreach(Action notify in changed.GetInvocationList())
            try { notify(); }catch(Exception ex) { Trace.TraceError("ScreenIt updater presentation failed: {0}",ex); }
    }
    public async Task Check()
    {
        if(disposed || State.Active) return;var token=cancellation.Token;Set(new(UpdatePhase.Checking));
        try { var release=await source.Check(token);token.ThrowIfCancellationRequested();CanInstall=release!=null && installed();Set(new(release==null ? UpdatePhase.UpToDate : UpdatePhase.Available,release)); }
        catch(OperationCanceledException) when(token.IsCancellationRequested) { }
        catch(Exception ex) { Fail(UpdateError.Check,ex); }
    }
    public async Task Download()
    {
        if(disposed || State.Active || State.Release is not { } release || !CanInstall) return;
        var token=cancellation.Token;
        int currentAttempt=++attempt;Set(new(UpdatePhase.Downloading,release,0));
        // Capture the operation's token before Dispose; queued Progress callbacks
        // must never overwrite a later phase or access a disposed CTS.
        var progress=new Progress<DownloadProgress>(value=> { if(!disposed && currentAttempt==attempt && !token.IsCancellationRequested && State.Phase==UpdatePhase.Downloading) Set(new(UpdatePhase.Downloading,release,value.BytesDownloaded,value.TotalBytes)); });
        try
        {
            string path=await new UpdateTransfer(source).Prepare(release,token,progress,()=>Set(new(UpdatePhase.Verifying,release)));
            token.ThrowIfCancellationRequested();
            // Preserve the existing re-verification immediately before confirmation.
            await UpdateTransfer.Verify(path,Path.Combine(Path.GetDirectoryName(path)!,"SHA256SUMS.txt"),token);
            Set(new(UpdatePhase.PreparingInstall,release));
            if(!installed()) throw new InvalidOperationException("Installation changed.");
            if(!await confirm()) { Set(new(UpdatePhase.Available,release));return; }
            token.ThrowIfCancellationRequested();
            Set(new(UpdatePhase.LaunchingInstaller,release));
            await System.Windows.Threading.Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.Render);
            token.ThrowIfCancellationRequested();launch(path);
            Set(new(UpdatePhase.InstallerLaunched,release));close();
        }
        catch(OperationCanceledException) when(token.IsCancellationRequested) { }
        catch(Exception ex)
        {
            Fail(State.Phase switch { UpdatePhase.Verifying=>UpdateError.Verification,UpdatePhase.PreparingInstall=>UpdateError.Preparation,UpdatePhase.LaunchingInstaller=>UpdateError.Launch,_=>UpdateError.Download },ex);
        }
    }
    private void Fail(UpdateError error,Exception ex)
    {
        Trace.TraceError("ScreenIt updater {0} failed: {1}",error,ex);
        Set(new(UpdatePhase.Failed,State.Release,Error:error));
    }
    public Task Retry() => State.Error==UpdateError.Check ? Check() : Download();
    public void OpenPage()
    {
        if(disposed || State.Active || State.Release==null) return;
        try { open(State.Release.Page); }
        catch(Exception ex) { Fail(UpdateError.Preparation,ex); }
    }
    public void Dispose() { if(disposed) return;disposed=true;cancellation.Cancel();cancellation.Dispose();Changed=null; }
}
