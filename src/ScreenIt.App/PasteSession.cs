using System.Diagnostics;

internal enum PasteState { Idle, Preparing, WaitingForHotkeyRelease, PastingImage, Waiting, PastingComments, Completed, Failed, Aborted }
internal enum PasteOutcome { Completed, Failed, Aborted, Empty, Busy }
internal readonly record struct PasteTarget(IntPtr Hwnd, uint ProcessId);
internal sealed record PasteStep(string? ImagePath, ClipboardPart[] Payload)
{
    public bool IsImage => ImagePath != null;
}
internal sealed record PastePlan(IReadOnlyList<PasteStep> Steps, int ImageCount)
{
    public static PastePlan Create(string[] files, string comments)
    {
        var steps = files.Select(path => new PasteStep(path, ClipboardPayload.Images([path]))).ToList();
        if (!string.IsNullOrWhiteSpace(comments)) steps.Add(new(null, ClipboardPayload.Comments(comments)));
        return new(steps.AsReadOnly(), files.Length);
    }
}
internal sealed record PasteOptions
{
    // Receiver acknowledgement is unavailable. These are conservative starting values, not proven optima.
    public TimeSpan ImagePasteDelay { get; init; } = TimeSpan.FromMilliseconds(350);
    public TimeSpan FinalTextPasteDelay { get; init; } = TimeSpan.FromMilliseconds(350);
    public TimeSpan HotkeyReleaseTimeout { get; init; } = TimeSpan.FromSeconds(5);
    public TimeSpan KeyPollInterval { get; init; } = TimeSpan.FromMilliseconds(20);
    public TimeSpan ReleaseQuietDelay { get; init; } = TimeSpan.FromMilliseconds(40);
}
internal sealed record PasteResult(PasteOutcome Outcome, int ImagesSent, int TotalImages, string Reason)
{
    public bool PasteAttempted { get; init; }
    public string Status => Outcome switch
    {
        PasteOutcome.Completed => $"Pasted {ImagesSent} {(ImagesSent == 1 ? "screenshot" : "screenshots")}",
        PasteOutcome.Empty => "Session is empty; nothing pasted",
        PasteOutcome.Busy => "Paste Session is already running",
        _ => $"Paste stopped after {ImagesSent} of {TotalImages} screenshots. Your ScreenIt session is unchanged. {Reason}"
    };
}

// Only delivery boundaries are injectable. No domain, capture or receiver integration lives here.
internal interface IPasteDelivery
{
    bool IsTargetCurrent(PasteTarget target);
    bool IsOwnTarget(PasteTarget target);
    bool KeysReleased();
    Task ProbeAsync(CancellationToken token, Action guard);
    Task PublishAsync(ClipboardPart[] payload, CancellationToken token, Action guard);
    void SendPaste(PasteTarget target);
    Task Delay(TimeSpan duration, CancellationToken token);
}

internal sealed class PasteAbortedException(string reason) : Exception(reason);
internal sealed class PasteSequencer(IPasteDelivery delivery, PasteOptions? options = null)
{
    private readonly PasteOptions timing = options ?? new();
    public bool IsActive { get; private set; }
    public PasteState State { get; private set; } = PasteState.Idle;
    public event Action<PasteState, int, int>? Progress;
    private void SetState(PasteState state, int index, int total) { State = state; Progress?.Invoke(state, index, total); }
    public async Task<PasteResult> Run(PasteTarget target, int imageCount, Func<Task<PastePlan>> prepare, CancellationToken token = default)
    {
        if (IsActive) return new(PasteOutcome.Busy, 0, imageCount, "");
        if (imageCount == 0) return new(PasteOutcome.Empty, 0, 0, "");
        IsActive = true; int sent = 0; bool attempted = false;
        void Guard()
        {
            token.ThrowIfCancellationRequested();
            if (target.Hwnd == IntPtr.Zero || target.ProcessId == 0 || delivery.IsOwnTarget(target)) throw new PasteAbortedException("Focus a receiver and invoke Ctrl+Alt+V.");
            if (!delivery.IsTargetCurrent(target)) throw new PasteAbortedException("Foreground target changed.");
        }
        try
        {
            Guard(); SetState(PasteState.Preparing, 0, imageCount);
            var plan = await prepare();
            if (plan.ImageCount != imageCount || plan.Steps.Count(s => s.IsImage) != imageCount) throw new InvalidOperationException("Incomplete paste plan.");
            Guard(); await delivery.ProbeAsync(token, Guard);
            SetState(PasteState.WaitingForHotkeyRelease, 0, imageCount);
            var releaseTimer = Stopwatch.StartNew();
            while (true)
            {
                Guard();
                if (releaseTimer.Elapsed >= timing.HotkeyReleaseTimeout) throw new PasteAbortedException("Hotkey release timed out.");
                if (delivery.KeysReleased())
                {
                    await delivery.Delay(timing.ReleaseQuietDelay, token); Guard();
                    if (delivery.KeysReleased()) break;
                }
                else await delivery.Delay(timing.KeyPollInterval, token);
            }
            for (int stepIndex = 0; stepIndex < plan.Steps.Count; stepIndex++)
            {
                var step = plan.Steps[stepIndex];
                Guard();
                if (!delivery.KeysReleased()) throw new PasteAbortedException("Keyboard modifiers are held.");
                SetState(step.IsImage ? PasteState.PastingImage : PasteState.PastingComments, step.IsImage ? sent + 1 : sent, imageCount);
                await delivery.PublishAsync(step.Payload, token, Guard);
                Guard();
                if (!delivery.KeysReleased()) throw new PasteAbortedException("Keyboard modifiers are held.");
                attempted = true; delivery.SendPaste(target);
                if (step.IsImage)
                {
                    sent++;
                    if (stepIndex + 1 < plan.Steps.Count)
                    {
                        SetState(PasteState.Waiting, sent, imageCount);
                        bool textNext = !plan.Steps[stepIndex + 1].IsImage;
                        await delivery.Delay(textNext ? timing.FinalTextPasteDelay : timing.ImagePasteDelay, token);
                        Guard();
                    }
                }
            }
            SetState(PasteState.Completed, sent, imageCount); return new(PasteOutcome.Completed, sent, imageCount, "") { PasteAttempted = attempted };
        }
        catch (PasteAbortedException ex) { SetState(PasteState.Aborted, sent, imageCount); return new(PasteOutcome.Aborted, sent, imageCount, ex.Message) { PasteAttempted = attempted }; }
        catch (OperationCanceledException) { SetState(PasteState.Aborted, sent, imageCount); return new(PasteOutcome.Aborted, sent, imageCount, "Operation cancelled.") { PasteAttempted = attempted }; }
        catch (Exception) { SetState(PasteState.Failed, sent, imageCount); return new(PasteOutcome.Failed, sent, imageCount, "Preparation, clipboard or input failed; the last request may be incomplete.") { PasteAttempted = attempted }; }
        finally { IsActive = false; }
    }
}
