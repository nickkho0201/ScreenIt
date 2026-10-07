using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

internal static class UpdateChecks
{
    internal static async Task Run(Action<bool,string> check)
    {
        using(var source=new Source())
        {
            bool identified=false;using var flow=new UpdateFlow(source,()=> { identified=true;throw new IOException("test installation identity"); },()=>Task.FromResult(false),_=>{},()=>{},_=>{});
            check(!identified && flow.State.Phase==UpdatePhase.Idle,"Opening Settings does not query installation identity or start updater work");
            await flow.Check();check(identified && flow.State.Phase==UpdatePhase.Failed,"Installation identity failure is contained in explicit update check");
        }
        var values=new List<DownloadProgress>();using(var output=new MemoryStream())
        {
            await GithubUpdateSource.LimitedCopy(new MemoryStream(new byte[10]),output,20,CancellationToken.None,new Immediate<DownloadProgress>(values.Add),10);
            check(values.First()==new DownloadProgress(0,10) && values.Last()==new DownloadProgress(10,10) && output.Length==10,"Download bytes report real 0% and 100% with known length");
        }
        values.Clear();using(var output=new MemoryStream())
        {
            await GithubUpdateSource.LimitedCopy(new MemoryStream(new byte[10]),output,20,CancellationToken.None,new Immediate<DownloadProgress>(values.Add));
            check(values.Last()==new DownloadProgress(10,null),"Missing Content-Length reports bytes without fake total");
        }
        foreach(long expected in new long[]{5,15})
        {
            bool rejected=false;using var output=new MemoryStream();try { await GithubUpdateSource.LimitedCopy(new MemoryStream(new byte[10]),output,20,CancellationToken.None,expectedLength:expected); }catch(Exception ex) when(ex is IOException or InvalidDataException) { rejected=true; }
            check(rejected,"Overlong/truncated HTTP body cannot become a completed download "+expected);
        }
        using(var output=new MemoryStream())
        {
            await GithubUpdateSource.LimitedCopy(new MemoryStream(),output,20,CancellationToken.None,expectedLength:0);
            check(new UpdateState(UpdatePhase.Downloading,TotalBytes:0,BytesDownloaded:0).Progress==null,"Zero-length response never divides by zero or claims a download percentage");
        }
        check(new UpdateState(UpdatePhase.Downloading,BytesDownloaded:15,TotalBytes:10).Progress==1 && new UpdateState(UpdatePhase.Downloading,BytesDownloaded:0,TotalBytes:-1).Progress==null,"Progress stays bounded and invalid totals cannot produce NaN or Infinity");
        foreach(var phase in new[]{UpdatePhase.Verifying,UpdatePhase.PreparingInstall,UpdatePhase.LaunchingInstaller})
        {
            using var source=new Source();bool launched=false,closed=false;
            using var flow=new UpdateFlow(source,()=>true,()=>Task.FromResult(true),_=>launched=true,()=>closed=true,_=>{});
            await flow.Check();flow.Changed+=()=> { if(flow.State.Phase==phase) flow.Dispose(); };await flow.Download();await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            check(!launched && !closed && flow.State.Phase==phase,"Lifecycle cancellation at "+phase+" prevents handoff without reporting a network error");
        }
        foreach(var failure in new[]{UpdateError.Download,UpdateError.Verification,UpdateError.Launch,UpdateError.Preparation})
        {
            using var source=new Source { Failure=failure };bool launched=false,closed=false;
            using var flow=new UpdateFlow(source,()=>true,()=>Task.FromResult(true),_=> { if(source.Failure==UpdateError.Launch) throw new IOException("test launch");launched=true; },()=>closed=true,_=>throw new InvalidOperationException("No browser allowed"));
            var phases=new List<UpdateState>();flow.Changed+=()=>phases.Add(flow.State);
            await flow.Check();check(flow.State.Phase==UpdatePhase.Available,"Check yields available update "+failure);
            if(failure==UpdateError.Preparation) source.Failure=null;
            using var preparationFlow=failure==UpdateError.Preparation ? new UpdateFlow(source,()=>source.Installed,()=>Task.FromResult(true),_=>launched=true,()=>closed=true,_=>{}) : null;
            var target=preparationFlow ?? flow;
            if(preparationFlow!=null) { await target.Check();source.Installed=false; }
            await target.Download();await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            check(target.State is { Phase:UpdatePhase.Failed,Progress:null,BytesDownloaded:null } && target.State.Error==failure && !closed && !launched,"Failure classification clears stale progress and prevents handoff "+failure);
            source.Failure=null;source.Installed=true;await target.Retry();await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            check(target.State.Phase==UpdatePhase.InstallerLaunched && target.State.Progress==null && launched && closed,"Retry creates clean successful flow without install percent "+failure);
            if(preparationFlow==null) check(phases.Any(s=>s.Phase==UpdatePhase.Downloading && s.Error==null && s.BytesDownloaded==0) && phases.Any(s=>s.Progress==.5) && phases.Any(s=>s.Phase==UpdatePhase.Verifying) && phases.Any(s=>s.Phase==UpdatePhase.PreparingInstall),"Real phase/progress transitions reset stale error "+failure);
        }
        using(var source=new Source { Wait=true })
        using(var flow=new UpdateFlow(source,()=>true,()=>Task.FromResult(true),_=>throw new InvalidOperationException("No installer"),()=>throw new InvalidOperationException("No shutdown"),_=>{}))
        {
            await flow.Check();var run=flow.Download();await source.Entered.Task;await flow.Download();
            check(source.InstallerCalls==1 && flow.State.Active,"Repeated click cannot start a second transfer");
            flow.Dispose();await run;
            check(source.Folders.All(f=>!Directory.Exists(f)),"Cancelled partial generation is removed; disposed flow cannot launch");
        }
        using(var source=new Source())
        using(var flow=new UpdateFlow(source,()=>true,()=>Task.FromResult(true),_=>throw new InvalidOperationException("No installer"),()=>{},_=>{}))
        {
            await flow.Check();flow.Changed+=()=> { if(flow.State.Phase==UpdatePhase.Downloading) flow.Dispose(); };await flow.Download();
            check(source.Folders.All(f=>!Directory.Exists(f)),"Immediate close at download transition is safe before the first await");
        }
        using(var source=new Source())
        using(var flow=new UpdateFlow(source,()=>true,()=>Task.FromResult(false),_=>throw new InvalidOperationException("No installer"),()=>throw new InvalidOperationException("No shutdown"),_=>{}))
        {
            await flow.Check();await flow.Download();check(flow.State.Phase==UpdatePhase.Available && !flow.State.Active,"Declined RAM-loss confirmation returns to available state");
        }
        using(var source=new Source())
        {
            bool closed=false;using var flow=new UpdateFlow(source,()=>true,()=>Task.FromResult(true),_=>{},()=>closed=true,_=>{});
            flow.Changed+=()=>throw new InvalidOperationException("test presentation failure");await flow.Check();await flow.Download();
            check(closed && flow.State.Phase==UpdatePhase.InstallerLaunched,"Presentation failure cannot undo successful installer handoff");
        }
        using(var source=new Source { Wait=true })
        using(var owner=new Coordinator(false,false))
        using(var flow=new UpdateFlow(source,()=>true,()=>Task.FromResult(true),_=>throw new InvalidOperationException("No installer"),()=>throw new InvalidOperationException("No shutdown"),_=>{}))
        {
            var settings=new SettingsWindow(owner,updateFlow:flow);settings.Show();settings.SelectPage(2);await flow.Check();var run=flow.Download();await source.Entered.Task;
            settings.Hide();settings.Show();settings.Activate();await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            check(flow.State.Phase==UpdatePhase.Downloading && Find<TextBlock>((DependencyObject)settings.Content).Any(t=>t.Text==UpdateText.Status(flow.State)),"Reopening hidden real Settings reflects active model state");
            settings.Close();await run;
            check(source.Folders.All(f=>!Directory.Exists(f)),"Closing real Settings cancels transfer and removes partial generation");
        }
        var staticBar=new UpdateProgress { Width=200,Height=6 };staticBar.Set(null,true,false);
        check(!staticBar.AnimationActive,"Reduced motion uses static accent track for unknown work");staticBar.Set(null,true,true);check(staticBar.AnimationActive,"Animation policy enables indeterminate accent movement");staticBar.Stop();
        var recovery=new UpdateUiHarness();recovery.Show(10);await recovery.Retry();for(int i=0;i<9;i++) recovery.Move(1);
        check(recovery.State.Phase==UpdatePhase.InstallerLaunched,"Manual harness Retry resets progress and can reach successful handoff without system actions");
        var oldLanguage=L.Language;var oldTheme=Appearance.Current;
        try
        {
            foreach(var language in new[]{"ru","en"}) foreach(var theme in new[]{UiTheme.Light,UiTheme.Dark})
            {
                L.Select(language);Appearance.Select(theme);
                check(UpdateText.Bytes(21_200_000)==(language=="ru" ? "21,2 МБ" : "21.2 MB"),"Localized decimal byte formatting "+language+theme);
                var flow=new UpdateUiHarness();var window=UpdateUiHarness.Window(flow,false);window.Show();
                try
                {
                    foreach(int step in new[]{0,1,3,5,6,7,8,9,10,11,12})
                    {
                        flow.Show(step);await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                        var state=flow.State;var root=(DependencyObject)window.Content;
                        check(Find<TextBlock>(root).Any(t=>t.Text==UpdateText.Status(state)),"Shared renderer exact phase/localization "+language+theme+step);
                        check(!state.Active || !Find<Button>(root).Any(b=>b.IsEnabled && b.Visibility==Visibility.Visible),"Active phase replaces actionable buttons "+language+theme+step);
                        check(state.Phase!=UpdatePhase.Failed || Find<Button>(root).Any(b=>Equals(b.Content,L.T("Retry"))),"Failure renders localized Retry "+language+theme+step);
                        check(Find<TextBlock>(root).All(t=>t.ActualWidth<=552.5) && Find<UpdateProgress>(root).All(p=>p.ActualWidth<=552.5),"Updater text wraps and progress fits the Settings column "+language+theme+step);
                        if(step is 0 or 3 or 7 or 10 or 11 or 12) UpdateUiHarness.Snapshot(window,$"{language}-{theme.ToString().ToLowerInvariant()}-{step}");
                    }
                    flow.Show(3);window.Hide();flow.Show(7);window.Show();await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                    check(Find<TextBlock>((DependencyObject)window.Content).Any(t=>t.Text==UpdateText.Status(flow.State)),"Re-show renderer uses current real model state "+language+theme);
                }
                finally { window.Close(); }
            }
            L.Select("en");check(UpdateText.Status(new(UpdatePhase.Downloading,UpdateUiHarness.Release,25_200_000,50_400_000))=="Downloading update — 50%","Intermediate percentage comes from bytes");
            check(new UpdateState(UpdatePhase.Verifying,UpdateUiHarness.Release,25,100).Progress==null && new UpdateState(UpdatePhase.LaunchingInstaller,UpdateUiHarness.Release,90,100).Progress==null,"Verification and launch never expose fake determinate percentage");
        }
        finally { L.Select(oldLanguage);Appearance.Select(oldTheme); }
    }
    private sealed class Immediate<T>(Action<T> report) : IProgress<T> { public void Report(T value)=>report(value); }
    private sealed class Source : IUpdateSource,IDisposable
    {
        internal UpdateError? Failure;
        internal bool Wait,Installed=true;
        internal int InstallerCalls;
        internal readonly TaskCompletionSource Entered=new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly HashSet<string> Folders=[];
        public Task<UpdateRelease?> Check(CancellationToken token)=>Task.FromResult<UpdateRelease?>(UpdateUiHarness.Release);
        public async Task Download(Uri uri,string path,long maxBytes,CancellationToken token,IProgress<DownloadProgress>? progress=null)
        {
            Folders.Add(Path.GetDirectoryName(path)!);token.ThrowIfCancellationRequested();
            if(Path.GetFileName(path)=="SHA256SUMS.txt") { await File.WriteAllTextAsync(path,(Failure==UpdateError.Verification ? new string('0',64) : Convert.ToHexString(SHA256.HashData(new byte[]{1,2,3,4})))+"  ScreenIt-Setup-0.2.1.exe",token);return; }
            InstallerCalls++;await File.WriteAllBytesAsync(path,new byte[]{1,2},token);progress?.Report(new(0,4));Entered.TrySetResult();
            if(Wait) await Task.Delay(Timeout.Infinite,token);
            if(Failure==UpdateError.Download) throw new IOException("test connection closed");
            progress?.Report(new(2,4));await Task.Delay(15,token);await File.WriteAllBytesAsync(path,new byte[]{1,2,3,4},token);progress?.Report(new(4,4));
        }
        public void Dispose()
        {
            foreach(string folder in Folders) if(Directory.Exists(folder)) { foreach(string name in new[]{"ScreenIt-Setup-0.2.1.exe","SHA256SUMS.txt",".screenit-update"}) { string file=Path.Combine(folder,name);if(File.Exists(file)) File.Delete(file); }Directory.Delete(folder); }
        }
    }
    private static IEnumerable<T> Find<T>(DependencyObject root) where T:DependencyObject
    {
        if(root is T item) yield return item;
        foreach(var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>()) foreach(var descendant in Find<T>(child)) yield return descendant;
    }
}
