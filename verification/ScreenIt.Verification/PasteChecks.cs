using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using ScreenIt.Core;

internal static class PasteChecks
{
    private static readonly PasteTarget Target = new(new IntPtr(123), 999);
    private static readonly PasteOptions Fast = new() { ImagePasteDelay = TimeSpan.Zero, FinalTextPasteDelay = TimeSpan.Zero, ReleaseQuietDelay = TimeSpan.Zero, KeyPollInterval = TimeSpan.Zero };
    private sealed class Fake : IPasteDelivery
    {
        public bool Current = true, Own, Released = true, ProbeFails;
        public int Sent, Probes, FailPublication = -1;
        public readonly List<ClipboardPart[]> Published = [];
        public readonly List<TimeSpan> Delays = [];
        public Action? OnDelay, OnSend;
        public bool IsTargetCurrent(PasteTarget target) => Current;
        public bool IsOwnTarget(PasteTarget target) => Own;
        public bool KeysReleased() => Released;
        public Task ProbeAsync(CancellationToken token, Action guard) { guard(); Probes++; if(ProbeFails) throw new IOException(); return Task.CompletedTask; }
        public Task PublishAsync(ClipboardPart[] payload, CancellationToken token, Action guard)
        { guard(); if(Published.Count==FailPublication) throw new IOException(); Published.Add(payload); return Task.CompletedTask; }
        public void SendPaste(PasteTarget target) { if(!Released) throw new Exception("Injection before key release"); Sent++; OnSend?.Invoke(); }
        public Task Delay(TimeSpan delay, CancellationToken token) { token.ThrowIfCancellationRequested(); Delays.Add(delay); OnDelay?.Invoke(); return Task.CompletedTask; }
    }
    public static async Task Run(Action<bool,string> check, Coordinator coordinator)
    {
        var files=new[]{"A","B","C"}.Select(letter=>Path.GetFullPath(Path.Combine(Path.GetTempPath(),"ScreenIt-sequence-fixture",letter+".png"))).ToArray();
        const string comments="Screenshot A\r\nA1 — русский\r\n     многострочный комментарий\r\n\r\nScreenshot B\r\nB1 — второй\r\n\r\nScreenshot C\r\nC1 — третий";
        var plan=PastePlan.Create(files,comments);
        check(plan.Steps.Select(s=>s.IsImage).SequenceEqual(new[]{true,true,true,false}),"Sequence plan Image A/B/C then Text");
        for(int i=0;i<3;i++)
        {
            var parts=plan.Steps[i].Payload;var drop=parts.Single(p=>p.Format==15).Bytes;
            var paths=Encoding.Unicode.GetString(drop,20,drop.Length-20).Split('\0',StringSplitOptions.RemoveEmptyEntries);
            check(paths.Length==1 && paths[0]==files[i],"Each planned HDROP contains one correct path " + i);
            check(parts.Length==3 && parts.All(p=>p.Format!=13 && p.Format!=8 && p.Format!=17),"No competing formats image stage " + i);
            check(parts.Where(p=>p.Format!=15).All(p=>p.Bytes.Length==4 && BitConverter.ToInt32(p.Bytes)==0),"Privacy zero per image stage " + i);
        }
        check(Encoding.Unicode.GetString(plan.Steps[^1].Payload.Single(p=>p.Format==13).Bytes)==comments+"\0","Exact planned Unicode text");
        var before=coordinator.Session.Screenshots.ToArray();var annotations=before.Select(s=>s.Annotations.ToArray()).ToArray();
        var fake=new Fake();var sequence=new PasteSequencer(fake,Fast);
        var states=new List<PasteState>(); sequence.Progress+=(state,_,_)=> { states.Add(state); if(state==PasteState.Completed) check(fake.Sent==4,"Success state occurs only after final comments paste"); };
        var success=await sequence.Run(Target,3,()=>Task.FromResult(plan));
        check(success.Outcome==PasteOutcome.Completed && success.ImagesSent==3 && fake.Sent==4 && fake.Published.Count==4,"Complete sequence injects four independent paste requests");
        check(sequence.State==PasteState.Completed && !sequence.IsActive && states.Contains(PasteState.Preparing) && states.Contains(PasteState.WaitingForHotkeyRelease) && states.Contains(PasteState.PastingComments),"Async state lifecycle");
        check(fake.Published[^1].Any(p=>p.Format==13) && fake.Published[^1].All(p=>p.Format!=15),"Successful final clipboard payload is comments");
        var timed=new Fake();await new PasteSequencer(timed).Run(Target,3,()=>Task.FromResult(plan));
        check(timed.Delays.SequenceEqual(new[]{TimeSpan.FromMilliseconds(40),TimeSpan.FromMilliseconds(350),TimeSpan.FromMilliseconds(350),TimeSpan.FromMilliseconds(350)}),"Isolated timing: release quiet then A/B/C/text intervals");
        var empty=new Fake();var emptyResult=await new PasteSequencer(empty,Fast).Run(Target,0,()=>throw new Exception());
        check(emptyResult.Outcome==PasteOutcome.Empty && empty.Probes==0 && empty.Sent==0 && empty.Published.Count==0,"Empty session leaves clipboard/input untouched");
        var failedPrepare=new Fake();var prepareResult=await new PasteSequencer(failedPrepare,Fast).Run(Target,3,()=>throw new IOException());
        check(prepareResult.Outcome==PasteOutcome.Failed && failedPrepare.Published.Count==0 && failedPrepare.Sent==0 && failedPrepare.Probes==0,"Preparation failure precedes first publication/injection");
        var probe=new Fake{ProbeFails=true};var probeResult=await new PasteSequencer(probe,Fast).Run(Target,3,()=>Task.FromResult(plan));
        check(probeResult.Outcome==PasteOutcome.Failed && probe.Sent==0 && probe.Published.Count==0,"Clipboard infrastructure preflight fails before paste");
        var pending=new TaskCompletionSource<PastePlan>(TaskCreationOptions.RunContinuationsAsynchronously);var concurrentFake=new Fake();var concurrent=new PasteSequencer(concurrentFake,Fast);
        var running=concurrent.Run(Target,3,()=>pending.Task);var repeated=await concurrent.Run(Target,3,()=>throw new Exception());
        check(repeated.Outcome==PasteOutcome.Busy && concurrent.IsActive && concurrentFake.Sent==0,"Repeated invocation cannot start a second sequence");pending.SetResult(plan);await running;
        var own=new Fake{Own=true};var ownResult=await new PasteSequencer(own,Fast).Run(Target,3,()=>throw new Exception());
        check(ownResult.Outcome==PasteOutcome.Aborted && own.Published.Count==0 && own.Probes==0,"ScreenIt foreground rejected before preparation");
        var changed=new Fake();changed.OnSend=()=>changed.Current=false;var changedResult=await new PasteSequencer(changed,Fast).Run(Target,3,()=>Task.FromResult(plan));
        check(changedResult.Outcome==PasteOutcome.Aborted && changedResult.ImagesSent==1 && changed.Sent==1 && changed.Published.Count==1,"Foreground switch after A aborts remaining B/C/text");
        check(changedResult.Status.Contains("after 1 of 3") && changedResult.Status.Contains("Your ScreenIt session is unchanged"),"Honest partial-progress status");
        var intermediate=new Fake{FailPublication=1};var intermediateResult=await new PasteSequencer(intermediate,Fast).Run(Target,3,()=>Task.FromResult(plan));
        check(intermediateResult.Outcome==PasteOutcome.Failed && intermediateResult.ImagesSent==1 && intermediate.Sent==1,"Intermediate clipboard failure stops after first request");
        var injectionFailure=new Fake();injectionFailure.OnSend=()=>throw new IOException();
        var injectionResult=await new PasteSequencer(injectionFailure,Fast).Run(Target,3,()=>Task.FromResult(plan));
        check(injectionResult.PasteAttempted && injectionResult.ImagesSent==0 && ToastMessage.Paste(injectionResult,true)?.Title=="Paste interrupted" && !ToastMessage.Paste(injectionResult,true)!.Detail.Contains("Nothing"),"First input failure does not falsely claim nothing was pasted");
        check(!prepareResult.PasteAttempted && ToastMessage.Paste(prepareResult,true)?.Detail.StartsWith("Nothing was pasted")==true,"Actual pre-input preparation failure reports nothing pasted");
        var textless=new Fake();var textlessResult=await new PasteSequencer(textless,Fast).Run(Target,3,()=>Task.FromResult(PastePlan.Create(files,"")));
        check(textlessResult.Outcome==PasteOutcome.Completed && textless.Sent==3 && textless.Published.All(p=>p.All(x=>x.Format!=13)),"Text-less plan skips empty paste");
        var release=new Fake{Released=false};int polls=0;release.OnDelay=()=>{check(release.Sent==0 && release.Published.Count==0,"No publication/injection while initial keys held");if(++polls>=2) release.Released=true;};
        release.OnSend=()=>release.OnDelay=null;
        var releaseResult=await new PasteSequencer(release,Fast).Run(Target,3,()=>Task.FromResult(plan));
        check(releaseResult.Outcome==PasteOutcome.Completed && polls>=2,"Injection begins only after Ctrl/Alt/V release");
        var held=new Fake{Released=false};var heldResult=await new PasteSequencer(held,Fast with{HotkeyReleaseTimeout=TimeSpan.Zero}).Run(Target,3,()=>Task.FromResult(plan));
        check(heldResult.Outcome==PasteOutcome.Aborted && held.Sent==0 && held.Published.Count==0,"Hotkey release timeout aborts without publication");
        var repress=new Fake();repress.OnSend=()=>repress.Released=false;var repressResult=await new PasteSequencer(repress,Fast).Run(Target,3,()=>Task.FromResult(plan));
        check(repressResult.Outcome==PasteOutcome.Aborted && repress.Sent==1,"Reheld modifiers stop subsequent paste");
        using var cancel=new CancellationTokenSource();var cancellation=new Fake();cancellation.OnSend=()=>cancel.Cancel();var cancelResult=await new PasteSequencer(cancellation,Fast).Run(Target,3,()=>Task.FromResult(plan),cancel.Token);
        check(cancelResult.Outcome==PasteOutcome.Aborted && cancellation.Sent==1,"Cancellation stops remaining sequence");
        check(coordinator.Session.Screenshots.SequenceEqual(before) && before.Select((s,i)=>s.Annotations.SequenceEqual(annotations[i])).All(x=>x),"Session remains immutable through delivery failure/abort/completion");
        var prepared=await coordinator.PreparePaste();
        check(prepared.ImageCount==3 && prepared.Steps.Where(s=>s.IsImage).All(s=>File.Exists(s.ImagePath)),"Production preparation materializes every PNG before delivery");
        check(prepared.Steps.Where(s=>s.IsImage).Select(s=>Path.GetFileName(s.ImagePath)).SequenceEqual(new[]{"ScreenIt-A.png","ScreenIt-B.png","ScreenIt-C.png"}),"Production preparation snapshot file order");
        check(prepared.Steps.Select(s=>s.IsImage).SequenceEqual(new[]{true,true,true,false}),"Production preparation stages are singleton images then comments");
        var chord=PasteInput.Chord();check(Marshal.SizeOf<PasteInput.Input>()==40 && Marshal.OffsetOf<PasteInput.Input>(nameof(PasteInput.Input.Data)).ToInt32()==8,"Native x64 INPUT layout");
        check(chord.Select(s=>s.Data.Keyboard.Vk).SequenceEqual(new ushort[]{0x11,0x56,0x56,0x11}) && chord.Select(s=>s.Data.Keyboard.Flags).SequenceEqual(new uint[]{0,0,2,2}),"SendInput chord Ctrl down V down V up Ctrl up");
        var window=new Window{ShowInTaskbar=false};var hwnd=new WindowInteropHelper(window).EnsureHandle();
        bool rejected=false;try{PasteInput.SendPaste(new(hwnd,(uint)Environment.ProcessId));}catch(PasteAbortedException){rejected=true;}
        check(rejected,"Native SendInput boundary rejects own HWND without injection");
        await ClipboardTransport.ProbeAsync(hwnd,CancellationToken.None,()=>{});
        var windowsDelivery=new WindowsPasteDelivery(hwnd);
        await windowsDelivery.PublishAsync(prepared.Steps[0].Payload,CancellationToken.None,()=>{});
        bool metadataPassed=false;try{windowsDelivery.SendPaste(new(hwnd,(uint)Environment.ProcessId));}catch(PasteAbortedException ex){metadataPassed=ex.Message!="Clipboard changed before paste.";}
        check(metadataPassed,"Native publication identity passes before own-target input rejection");
        ClipboardTransport.Publish(coordinator.ControlHandle,ClipboardPayload.Comments("synthetic replacement"));
        bool clipboardReplaced=false;try{windowsDelivery.SendPaste(Target);}catch(PasteAbortedException){clipboardReplaced=true;}
        check(clipboardReplaced,"Clipboard replacement aborts native delivery before SendInput");
        foreach(var stage in prepared.Steps)
        {
            await ClipboardTransport.PublishAsync(hwnd,stage.Payload,CancellationToken.None,()=>{});
            uint seq=GetClipboardSequenceNumber();check(GetClipboardOwner()==hwnd && ClipboardTransport.OpenClipboard(hwnd),"Async clipboard own publication guard");
            try
            {
                check(GetClipboardOwner()==hwnd && GetClipboardSequenceNumber()==seq,"Readback own sequence identity");
                if(stage.IsImage)
                {
                    var drop=GetClipboardData(15);var path=new StringBuilder(2048);uint count=DragQueryFileW(drop,uint.MaxValue,null,0);DragQueryFileW(drop,0,path,(uint)path.Capacity);
                    check(count==1 && path.ToString()==stage.ImagePath,"Actual single-file HDROP readback matches sequence stage");
                    check(!IsClipboardFormatAvailable(13),"Async image stage has no Unicode text");
                }
                else
                {
                    var text=GetClipboardData(13);var pointer=ClipboardTransport.GlobalLock(text);
                    try{check(Marshal.PtrToStringUni(pointer)==CommentFormatter.Format(coordinator.Session),"Async final Unicode comments exact readback");}finally{ClipboardTransport.GlobalUnlock(text);}
                    check(!IsClipboardFormatAvailable(15),"Final text stage has no file payload");
                }
                foreach(var flag in new[]{"CanUploadToCloudClipboard","CanIncludeInClipboardHistory"})
                {
                    var data=GetClipboardData(ClipboardPayload.RegisterClipboardFormatW(flag));check(data!=IntPtr.Zero,"Every-stage privacy flag present");var pointer=ClipboardTransport.GlobalLock(data);
                    try{check(pointer!=IntPtr.Zero && Marshal.ReadInt32(pointer)==0,"Every-stage privacy DWORD zero");}finally{ClipboardTransport.GlobalUnlock(data);}
                }
            }
            finally{ClipboardTransport.CloseClipboard();}
        }
        check(prepared.Steps.Where(s=>s.IsImage).All(s=>File.Exists(s.ImagePath)),"Sequence PNG generation survives all stage replacements");
        using(var withoutComments=new Coordinator(showTray:false,registerHotkey:false))
        {
            var draft=withoutComments.Session.CreateDraft(800,600);var shot=withoutComments.Session.Commit(draft);
            var image=coordinator.Rasters[before[0].Id].Original;withoutComments.Rasters.Add(shot.Id,new(image,image));
            var noTextPlan=await withoutComments.PreparePaste();
            check(noTextPlan.ImageCount==1 && noTextPlan.Steps.Count==1 && noTextPlan.Steps[0].IsImage,"Production screenshot-only session omits comments stage");
        }
        window.Close();
    }
    [DllImport("user32.dll")] private static extern IntPtr GetClipboardOwner();
    [DllImport("user32.dll")] private static extern uint GetClipboardSequenceNumber();
    [DllImport("user32.dll")] private static extern IntPtr GetClipboardData(uint format);
    [DllImport("user32.dll")] private static extern bool IsClipboardFormatAvailable(uint format);
    [DllImport("shell32.dll",CharSet=CharSet.Unicode)] private static extern uint DragQueryFileW(IntPtr drop,uint index,StringBuilder? path,uint size);
}
