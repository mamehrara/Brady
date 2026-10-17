using Brady.Core.Quiet;

namespace Brady.Core.Reminders;

/// <summary>How one kind of reminder is scheduled.</summary>
/// <param name="Interval">Active time between reminders; for Move, continuous sitting.</param>
/// <param name="Snooze">How long Snooze pushes the reminder back.</param>
public sealed record KindRule(bool Enabled, TimeSpan Interval, TimeSpan Snooze);

/// <summary>Everything the scheduler needs to know from the user's settings.</summary>
public sealed record ReminderRules(
    IReadOnlyDictionary<ReminderKind, KindRule> Kinds,
    TimeSpan IdleThreshold,
    TimeSpan MinGap,
    QuietHours QuietHours)
{
    public static ReminderRules Default { get; } = new(
        new Dictionary<ReminderKind, KindRule>
        {
            [ReminderKind.Posture] = new(true, TimeSpan.FromMinutes(30), TimeSpan.FromMinutes(10)),
            [ReminderKind.Move] = new(true, TimeSpan.FromMinutes(50), TimeSpan.FromMinutes(10)),
            [ReminderKind.Eyes] = new(true, TimeSpan.FromMinutes(20), TimeSpan.FromMinutes(10)),
            [ReminderKind.Stretch] = new(true, TimeSpan.FromMinutes(90), TimeSpan.FromMinutes(10)),
            [ReminderKind.Water] = new(false, TimeSpan.FromMinutes(60), TimeSpan.FromMinutes(10)),
        },
        IdleThreshold: TimeSpan.FromMinutes(5),
        MinGap: TimeSpan.FromMinutes(4),
        QuietHours.Default);

    public KindRule this[ReminderKind kind] => Kinds[kind];

    public ReminderRules With(ReminderKind kind, KindRule rule) =>
        this with { Kinds = new Dictionary<ReminderKind, KindRule>(Kinds) { [kind] = rule } };
}
