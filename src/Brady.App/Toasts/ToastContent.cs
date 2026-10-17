using Brady.Core.Copy;
using Brady.Core.Reminders;

namespace Brady.App.Toasts;

/// <summary>What a toast shows when it arrives.</summary>
/// <param name="Kind">Sets the chip (icon, word and tone).</param>
/// <param name="Copy">Title and one-sentence body.</param>
/// <param name="Meta">Optional caption next to the chip, such as "Every 30 min" or "52 min still".</param>
/// <param name="AllowSkip">Shows the quiet Skip button.</param>
/// <param name="DoneCopy">The confirmation Brady cross-fades to after Done.</param>
public sealed record ToastContent(
    ReminderKind Kind,
    ReminderCopy Copy,
    string? Meta,
    bool AllowSkip,
    ReminderCopy DoneCopy);

/// <summary>What the user did with a toast (or that they didn't).</summary>
public enum ToastOutcome
{
    Done,
    Snoozed,
    Skipped,
    Dismissed,
    TimedOut,
}

/// <summary>Buttons on the toast.</summary>
public enum ToastAction
{
    Done,
    Snooze,
    Skip,
    Close,
}
