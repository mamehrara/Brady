using Brady.Core.Quiet;
using Brady.Core.Reminders;

namespace Brady.Core.Tests.Reminders;

/// <summary>
/// Drives a <see cref="ReminderScheduler"/> the way the engine does: the clock moves in 10-second
/// steps, the user types or sits idle, and Tick runs only when the scheduler asked to be woken
/// (or right after a command). Shown reminders are answered immediately by <see cref="Respond"/>.
/// </summary>
internal sealed class SchedulerSim
{
    public static readonly DateTimeOffset Start = new(2026, 10, 19, 9, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Step = TimeSpan.FromSeconds(10);

    private DateTimeOffset _wakeAt;

    public SchedulerSim(ReminderRules? rules = null, DateTimeOffset? start = null)
    {
        Now = start ?? Start;
        LastInput = Now;
        Scheduler = new ReminderScheduler(rules ?? ReminderRules.Default, Now, TimeZoneInfo.Utc);
        _wakeAt = Now;
    }

    public ReminderScheduler Scheduler { get; }

    public DateTimeOffset Now { get; private set; }

    public DateTimeOffset LastInput { get; private set; }

    /// <summary>What Windows reports (fullscreen, presentation...).</summary>
    public QuietReason SystemQuiet { get; set; }

    public Func<ReminderDue, ReminderOutcome> Respond { get; set; } = _ => ReminderOutcome.Done;

    public List<Shown> History { get; } = [];

    public List<(DateTimeOffset At, QuietReason Reason)> Dropped { get; } = [];

    public static ReminderRules Only(params (ReminderKind Kind, int Minutes)[] kinds)
    {
        var rules = ReminderRules.Default;
        foreach (var kind in Enum.GetValues<ReminderKind>())
            rules = rules.With(kind, rules[kind] with { Enabled = false });
        foreach (var (kind, minutes) in kinds)
            rules = rules.With(kind, rules[kind] with { Enabled = true, Interval = TimeSpan.FromMinutes(minutes) });
        return rules;
    }

    /// <summary>The user is at the keyboard for this long.</summary>
    public SchedulerSim Type(TimeSpan duration) => Run(duration, typing: true);

    /// <summary>No input for this long (they may or may not have left).</summary>
    public SchedulerSim Idle(TimeSpan duration) => Run(duration, typing: false);

    public SchedulerSim Type(int minutes) => Type(TimeSpan.FromMinutes(minutes));

    public SchedulerSim Idle(int minutes) => Idle(TimeSpan.FromMinutes(minutes));

    /// <summary>Applies a command to the scheduler at the current time and ticks right after.</summary>
    public SchedulerSim Do(Action<ReminderScheduler, DateTimeOffset> command)
    {
        command(Scheduler, Now);
        _wakeAt = Now;
        TickIfDue();
        return this;
    }

    public IEnumerable<Shown> ShownOf(ReminderKind kind) => History.Where(s => s.Kind == kind);

    /// <summary>Minutes since <see cref="Start"/> at which each reminder was shown.</summary>
    public IReadOnlyList<double> MinutesOf(ReminderKind kind) =>
        ShownOf(kind).Select(s => (s.At - Start).TotalMinutes).ToList();

    private SchedulerSim Run(TimeSpan duration, bool typing)
    {
        var end = Now + duration;
        while (Now < end)
        {
            var step = end - Now < Step ? end - Now : Step;
            Now += step;
            if (typing) LastInput = Now;
            TickIfDue();
        }
        return this;
    }

    private void TickIfDue()
    {
        if (Now < _wakeAt) return;
        var decision = Scheduler.Tick(Now, LastInput, SystemQuiet);
        _wakeAt = decision.WakeAt;
        if (decision.Quiet != QuietReason.None) Dropped.Add((Now, decision.Quiet));
        if (decision.Show is { } due)
        {
            History.Add(new Shown(due.Kind, Now, due.SittingFor));
            Scheduler.Report(due.Kind, Respond(due), Now);
            _wakeAt = Now; // the engine is poked after every report
        }
    }

    public sealed record Shown(ReminderKind Kind, DateTimeOffset At, TimeSpan SittingFor);
}
