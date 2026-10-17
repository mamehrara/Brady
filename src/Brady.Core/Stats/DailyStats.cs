using Brady.Core.Reminders;

namespace Brady.Core.Stats;

/// <summary>
/// Today's tally. Only "breaks taken" is ever shown; missed reminders are counted silently and
/// never announced (no streaks, no guilt).
/// </summary>
public sealed record DailyStats(DateOnly Date, int Taken, int Snoozed, int Missed)
{
    public static DailyStats For(DateOnly date) => new(date, 0, 0, 0);

    /// <summary>Counts an outcome, starting a fresh tally when the local date has changed.</summary>
    public DailyStats Record(DateOnly today, ReminderOutcome outcome)
    {
        var current = today == Date ? this : For(today);
        return outcome switch
        {
            ReminderOutcome.Done => current with { Taken = current.Taken + 1 },
            ReminderOutcome.Snoozed => current with { Snoozed = current.Snoozed + 1 },
            ReminderOutcome.Missed => current with { Missed = current.Missed + 1 },
            _ => current,
        };
    }
}
