using Brady.Core.Quiet;
using Brady.Core.Stats;

namespace Brady.Core.Reminders;

/// <summary>A reminder that is due now.</summary>
/// <param name="SittingFor">Active time since the kind was last reset (the Move meta line: "52 min still").</param>
public sealed record ReminderDue(ReminderKind Kind, TimeSpan SittingFor);

/// <summary>A kind's next reminder; <see cref="DueAt"/> is null while the user is away.</summary>
public sealed record UpcomingReminder(ReminderKind Kind, DateTimeOffset? DueAt);

/// <summary>The scheduler's answer to "what now?".</summary>
/// <param name="Show">The reminder to show now, if any.</param>
/// <param name="Quiet">Why reminders that came due were dropped instead of shown, if they were.</param>
/// <param name="WakeAt">When to call <see cref="ReminderScheduler.Tick"/> again at the latest.</param>
public sealed record SchedulerDecision(ReminderDue? Show, QuietReason Quiet, DateTimeOffset WakeAt);

/// <summary>
/// Brady's scheduling rules as a pure state machine: no threads, no timers, no clock of its own.
/// Every method takes "now", so the tests drive it with a fake clock.
/// <list type="bullet">
/// <item>Time only counts while the user is present. Locking, sleeping or 5+ minutes without input
///   means they probably got up: the sitting-based kinds reset, Water just pauses.</item>
/// <item>Never two reminders within the minimum gap; when several are due, the highest priority
///   shows (Move &gt; Stretch &gt; Posture &gt; Eyes &gt; Water) and the rest wait.</item>
/// <item>During quiet (fullscreen, presentation, quiet hours, paused) due reminders are dropped and
///   their timers restart, so nothing piles up to catch up on afterwards.</item>
/// </list>
/// </summary>
public sealed class ReminderScheduler
{
    /// <summary>While away by idleness, how often to look for the user coming back.</summary>
    public static readonly TimeSpan AwayPoll = TimeSpan.FromSeconds(30);

    /// <summary>Longest sleep between ticks, so a clock change is never missed for long.</summary>
    public static readonly TimeSpan MaxSleep = TimeSpan.FromHours(1);

    private readonly Dictionary<ReminderKind, KindState> _kinds = [];
    private readonly TimeZoneInfo _zone;
    private ReminderRules _rules;
    private bool _idle, _locked, _asleep;
    private DateTimeOffset _idleSince;
    private DateTimeOffset _returnedAt;
    private DateTimeOffset? _nextAllowedAt;

    public ReminderScheduler(ReminderRules rules, DateTimeOffset now, TimeZoneInfo? zone = null)
    {
        _rules = rules;
        _zone = zone ?? TimeZoneInfo.Local;
        foreach (var kind in Enum.GetValues<ReminderKind>()) _kinds[kind] = new KindState { RunningSince = now };
        Today = DailyStats.For(LocalDate(now));
    }

    public ReminderRules Rules => _rules;

    public bool IsPresent => !_idle && !_locked && !_asleep;

    public DateTimeOffset? PausedUntil { get; private set; }

    public DailyStats Today { get; private set; }

    /// <summary>
    /// Advances the state to <paramref name="now"/>: notices the user leaving or returning, drops
    /// reminders that come due during quiet, and picks at most one reminder to show.
    /// </summary>
    public SchedulerDecision Tick(DateTimeOffset now, DateTimeOffset lastInputAt, QuietReason systemQuiet)
    {
        // Unlocking or waking the PC is input too, even if Windows didn't count it as such.
        if (_returnedAt > lastInputAt) lastInputAt = _returnedAt;
        UpdateIdle(now, lastInputAt);
        if (PausedUntil is { } until && now >= until) Resume(now);

        if (!IsPresent)
        {
            var poll = now + (_idle ? AwayPoll : MaxSleep);
            return new SchedulerDecision(null, QuietReason.None, NotBefore(now, Min(poll, PausedUntil ?? poll)));
        }

        var quiet = CurrentQuiet(now, systemQuiet);
        var due = DueKinds(now);
        ReminderDue? show = null;
        var dropped = QuietReason.None;

        if (due.Count > 0)
        {
            if (quiet != QuietReason.None)
            {
                foreach (var kind in due) Reset(kind, now);
                dropped = quiet;
            }
            else if (_nextAllowedAt is not { } allowed || now >= allowed)
            {
                show = Begin(due[0], now);
            }
        }

        // Wake for the next due reminder, the moment the user would count as away, or the end of a pause.
        var wake = Min(NextDue() ?? now + MaxSleep, lastInputAt + _rules.IdleThreshold);
        if (PausedUntil is { } pauseEnd) wake = Min(wake, pauseEnd);
        return new SchedulerDecision(show, dropped, NotBefore(now, Min(wake, now + MaxSleep)));
    }

    /// <summary>"Take a break now" from the tray: shows the reminder that is closest to due, right away.</summary>
    public ReminderDue? TakeBreakNow(DateTimeOffset now)
    {
        var kind = _kinds
            .Where(pair => _rules[pair.Key].Enabled && !pair.Value.InFlight)
            .OrderBy(pair => DueAt(pair.Key, pair.Value) ?? DateTimeOffset.MaxValue)
            .ThenBy(pair => ReminderKinds.Priority(pair.Key))
            .Select(pair => (ReminderKind?)pair.Key)
            .FirstOrDefault();
        return kind is { } k ? Begin(k, now) : null;
    }

    /// <summary>Records what the user did with a shown reminder.</summary>
    public void Report(ReminderKind kind, ReminderOutcome outcome, DateTimeOffset now)
    {
        var state = _kinds[kind];
        state.InFlight = false;
        Today = Today.Record(LocalDate(now), outcome);

        if (outcome == ReminderOutcome.Snoozed)
        {
            state.SnoozedUntil = now + _rules[kind].Snooze;
            return;
        }
        // Done, Skip, dismiss and missed all start the kind's interval over.
        Reset(kind, now);
    }

    public void SessionLocked(DateTimeOffset now) => SetAway(now, () => _locked = true);

    public void SessionUnlocked(DateTimeOffset now) => SetAway(now, () => _locked = false);

    public void Suspended(DateTimeOffset now) => SetAway(now, () => _asleep = true);

    public void Resumed(DateTimeOffset now) => SetAway(now, () => _asleep = false);

    /// <summary>Silences Brady until <paramref name="until"/>; everything restarts fresh afterwards.</summary>
    public void Pause(DateTimeOffset until) => PausedUntil = until;

    public void Resume(DateTimeOffset now)
    {
        PausedUntil = null;
        foreach (var kind in _kinds.Keys) Reset(kind, now);
    }

    /// <summary>Applies new settings. A kind that is switched on starts its interval from now.</summary>
    public void UpdateRules(ReminderRules rules, DateTimeOffset now)
    {
        var previous = _rules;
        _rules = rules;
        foreach (var kind in _kinds.Keys)
        {
            if (rules[kind].Enabled && !previous[kind].Enabled) Reset(kind, now);
        }
    }

    /// <summary>Every enabled kind's next due time, soonest first (for the tray tooltip and flyout).</summary>
    public IReadOnlyList<UpcomingReminder> Upcoming(DateTimeOffset now) =>
        _kinds
            .Where(pair => _rules[pair.Key].Enabled)
            .Select(pair => new UpcomingReminder(pair.Key, IsPresent ? Gated(DueAt(pair.Key, pair.Value)) : null))
            .OrderBy(r => r.DueAt ?? DateTimeOffset.MaxValue)
            .ThenBy(r => ReminderKinds.Priority(r.Kind))
            .ToList();

    /// <summary>Active time since <paramref name="kind"/> was last reset.</summary>
    public TimeSpan SittingFor(ReminderKind kind, DateTimeOffset now)
    {
        var state = _kinds[kind];
        var running = state.RunningSince is { } since && now > since ? now - since : TimeSpan.Zero;
        return state.Accrued + running;
    }

    private ReminderDue Begin(ReminderKind kind, DateTimeOffset now)
    {
        _kinds[kind].InFlight = true;
        _nextAllowedAt = now + _rules.MinGap;
        return new ReminderDue(kind, SittingFor(kind, now));
    }

    private List<ReminderKind> DueKinds(DateTimeOffset now) =>
        ReminderKinds.ByPriority
            .Where(kind => _rules[kind].Enabled && !_kinds[kind].InFlight && DueAt(kind, _kinds[kind]) <= now)
            .ToList();

    private DateTimeOffset? NextDue() =>
        _kinds
            .Where(pair => _rules[pair.Key].Enabled && !pair.Value.InFlight)
            .Select(pair => Gated(DueAt(pair.Key, pair.Value)))
            .Where(due => due is not null)
            .Min();

    // A due time can't be earlier than the minimum gap after the last reminder.
    private DateTimeOffset? Gated(DateTimeOffset? due) =>
        due is { } d && _nextAllowedAt is { } allowed && allowed > d ? allowed : due;

    private DateTimeOffset? DueAt(ReminderKind kind, KindState state)
    {
        if (state.SnoozedUntil is { } snoozed) return snoozed;
        if (state.RunningSince is not { } since) return null;
        return since + (_rules[kind].Interval - state.Accrued);
    }

    private QuietReason CurrentQuiet(DateTimeOffset now, QuietReason systemQuiet)
    {
        if (PausedUntil is not null) return QuietReason.Paused;
        if (systemQuiet != QuietReason.None) return systemQuiet;
        var local = TimeOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, _zone).DateTime);
        return _rules.QuietHours.Contains(local) ? QuietReason.QuietHours : QuietReason.None;
    }

    // Away by idleness starts at the last input, not when it was noticed: the idle minutes don't count.
    private void UpdateIdle(DateTimeOffset now, DateTimeOffset lastInputAt)
    {
        if (!_idle && IsPresent && now - lastInputAt >= _rules.IdleThreshold)
        {
            _idleSince = lastInputAt;
            Leave(lastInputAt);
            _idle = true;
        }
        else if (_idle && lastInputAt > _idleSince && now - lastInputAt < _rules.IdleThreshold)
        {
            _idle = false;
            if (IsPresent) Return(lastInputAt);
        }
    }

    private void SetAway(DateTimeOffset now, Action change)
    {
        var wasPresent = IsPresent;
        change();
        if (wasPresent && !IsPresent) Leave(now);
        else if (!wasPresent && IsPresent)
        {
            _idle = false;
            Return(now);
        }
    }

    // The user got up: stop counting. Sitting-based kinds start over; Water only pauses.
    private void Leave(DateTimeOffset at)
    {
        foreach (var (kind, state) in _kinds)
        {
            if (state.RunningSince is { } since && at > since) state.Accrued += at - since;
            state.RunningSince = null;
            state.SnoozedUntil = null;
            if (kind != ReminderKind.Water) state.Accrued = TimeSpan.Zero;
        }
    }

    private void Return(DateTimeOffset at)
    {
        _returnedAt = at;
        foreach (var state in _kinds.Values) state.RunningSince = at;
    }

    private void Reset(ReminderKind kind, DateTimeOffset now)
    {
        var state = _kinds[kind];
        state.Accrued = TimeSpan.Zero;
        state.SnoozedUntil = null;
        state.RunningSince = IsPresent ? now : null;
    }

    // Never ask to be woken in the past: at least a second from now.
    private static DateTimeOffset NotBefore(DateTimeOffset now, DateTimeOffset wake)
    {
        var floor = now + TimeSpan.FromSeconds(1);
        return wake < floor ? floor : wake;
    }

    private static DateTimeOffset Min(DateTimeOffset a, DateTimeOffset b) => a < b ? a : b;

    private DateOnly LocalDate(DateTimeOffset now) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, _zone).DateTime);

    private sealed class KindState
    {
        /// <summary>Active time counted before the current run (kept across Water's pauses).</summary>
        public TimeSpan Accrued { get; set; }

        /// <summary>When counting (re)started; null while the user is away.</summary>
        public DateTimeOffset? RunningSince { get; set; }

        public DateTimeOffset? SnoozedUntil { get; set; }

        /// <summary>Shown and waiting for the user's answer.</summary>
        public bool InFlight { get; set; }
    }
}
