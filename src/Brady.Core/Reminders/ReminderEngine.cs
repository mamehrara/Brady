using Brady.Core.Activity;
using Brady.Core.Quiet;
using Brady.Core.Stats;

namespace Brady.Core.Reminders;

/// <summary>
/// Runs the <see cref="ReminderScheduler"/>: one loop that sleeps until the scheduler's next wake
/// time or until something happens (lock, unlock, sleep, a toast's outcome, a settings change),
/// whichever is first. There is no polling: idle CPU is zero between wake-ups. All scheduler access
/// goes through one lock, so the UI thread and the loop never race.
/// </summary>
public sealed class ReminderEngine : IDisposable
{
    private readonly TimeProvider _time;
    private readonly IActivitySource _activity;
    private readonly IQuietProbe _quiet;
    private readonly ReminderScheduler _scheduler;
    private readonly Lock _gate = new();
    private TaskCompletionSource _wake = NewWake();
    private CancellationTokenSource? _stop;
    private Task? _loop;

    public ReminderEngine(TimeProvider time, IActivitySource activity, IQuietProbe quiet, ReminderRules rules)
    {
        _time = time;
        _activity = activity;
        _quiet = quiet;
        _scheduler = new ReminderScheduler(rules, time.GetUtcNow(), time.LocalTimeZone);
    }

    /// <summary>A reminder is due. Raised on the engine's loop (a pool thread); marshal to the UI yourself.</summary>
    public event EventHandler<ReminderDue>? ReminderDue;

    /// <summary>Raised after every tick and command, so the tray can refresh its tooltip.</summary>
    public event EventHandler? Changed;

    /// <summary>The last reason due reminders were dropped (for logging).</summary>
    public event EventHandler<QuietReason>? Suppressed;

    public void Start()
    {
        if (_loop is not null) return;
        _stop = new CancellationTokenSource();
        _loop = Task.Run(() => RunAsync(_stop.Token));
    }

    public async Task StopAsync()
    {
        if (_stop is null || _loop is null) return;
        await _stop.CancelAsync().ConfigureAwait(false);
        try
        {
            await _loop.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
    }

    public ReminderRules Rules
    {
        get
        {
            lock (_gate) return _scheduler.Rules;
        }
    }

    public EngineSnapshot Snapshot()
    {
        lock (_gate)
        {
            var now = _time.GetUtcNow();
            return new EngineSnapshot(now, _scheduler.Upcoming(now), _scheduler.IsPresent, _scheduler.PausedUntil, _scheduler.Today);
        }
    }

    public void Report(ReminderKind kind, ReminderOutcome outcome) => Command(now => _scheduler.Report(kind, outcome, now));

    public void SessionLocked() => Command(_scheduler.SessionLocked);

    public void SessionUnlocked() => Command(_scheduler.SessionUnlocked);

    public void Suspended() => Command(_scheduler.Suspended);

    public void Resumed() => Command(_scheduler.Resumed);

    public void Pause(DateTimeOffset until) => Command(_ => _scheduler.Pause(until));

    public void Resume() => Command(_scheduler.Resume);

    public void UpdateRules(ReminderRules rules) => Command(now => _scheduler.UpdateRules(rules, now));

    /// <summary>Re-evaluates now (for example after the system clock changed).</summary>
    public void Poke() => Command(_ => { });

    /// <summary>"Take a break now": the reminder closest to due, shown immediately.</summary>
    public ReminderDue? TakeBreakNow()
    {
        ReminderDue? due;
        lock (_gate) due = _scheduler.TakeBreakNow(_time.GetUtcNow());
        Changed?.Invoke(this, EventArgs.Empty);
        return due;
    }

    public void Dispose()
    {
        _stop?.Cancel();
        _stop?.Dispose();
    }

    private void Command(Action<DateTimeOffset> change)
    {
        lock (_gate)
        {
            change(_time.GetUtcNow());
            _wake.TrySetResult();
        }
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private async Task RunAsync(CancellationToken cancel)
    {
        while (!cancel.IsCancellationRequested)
        {
            SchedulerDecision decision;
            DateTimeOffset now;
            Task woken;
            lock (_gate)
            {
                now = _time.GetUtcNow();
                decision = _scheduler.Tick(now, _activity.LastInputAt, _quiet.Probe());
                if (_wake.Task.IsCompleted) _wake = NewWake();
                woken = _wake.Task;
            }

            if (decision.Quiet != QuietReason.None) Suppressed?.Invoke(this, decision.Quiet);
            if (decision.Show is { } due) ReminderDue?.Invoke(this, due);
            Changed?.Invoke(this, EventArgs.Empty);

            using var sleeping = CancellationTokenSource.CreateLinkedTokenSource(cancel);
            var delay = decision.WakeAt - now;
            var timer = Task.Delay(delay, _time, sleeping.Token);
            await Task.WhenAny(timer, woken).ConfigureAwait(false);
            await sleeping.CancelAsync().ConfigureAwait(false);
        }
    }

    private static TaskCompletionSource NewWake() => new(TaskCreationOptions.RunContinuationsAsynchronously);
}

/// <summary>A consistent read of the engine's state for the tray.</summary>
public sealed record EngineSnapshot(
    DateTimeOffset Now,
    IReadOnlyList<UpcomingReminder> Upcoming,
    bool IsPresent,
    DateTimeOffset? PausedUntil,
    DailyStats Today);
