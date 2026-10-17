namespace Brady.Core.Reminders;

/// <summary>What happened to a reminder once it was shown.</summary>
public enum ReminderOutcome
{
    Done,
    Snoozed,
    Skipped,

    /// <summary>Closed with the X: treated like Skip.</summary>
    Dismissed,

    /// <summary>Timed out unseen; counted quietly, never announced.</summary>
    Missed,
}
