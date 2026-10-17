using Brady.Core.Reminders;

namespace Brady.Core.Copy;

/// <summary>
/// Brady's lines per kind, rotated round-robin so the same line never shows twice in a row.
/// Voice rules (BRAND.md): first person, sentence case, no exclamation marks, no emoji, no guilt.
/// </summary>
public sealed class CopyDeck
{
    private static readonly IReadOnlyDictionary<ReminderKind, ReminderCopy[]> Lines = new Dictionary<ReminderKind, ReminderCopy[]>
    {
        [ReminderKind.Posture] =
        [
            new("Sit tall for a sec", "Shoulders back, feet flat, screen at eye level."),
            new("Uncurl that spine", "Roll your shoulders twice, then sit back in the chair."),
            new("Quick posture check", "Chin back, elbows at ninety, wrists straight."),
        ],
        [ReminderKind.Move] =
        [
            new("Even I'd move by now", "You've been sitting a while. Take a three-minute walk."),
            new("Leg day, kind of", "Walk to the kitchen and back. Slowly is fine."),
            new("Time to wander", "Stand up and do a lap. I'll hold your seat."),
        ],
        [ReminderKind.Eyes] =
        [
            new("Look far away", "Twenty seconds on something across the room. I'll count."),
            new("Rest those eyes", "Find the farthest thing you can see. Stay there a bit."),
            new("Blink break", "Look out a window and blink slowly a few times."),
        ],
        [ReminderKind.Stretch] =
        [
            new("Reach for the branches", "Arms up, slow breath, hold for fifteen seconds."),
            new("Neck rolls", "Slow circles, three each way."),
            new("Wrist rescue", "Stretch each wrist back gently for ten seconds."),
        ],
        [ReminderKind.Water] =
        [
            new("Sip break", "A glass of water, then back to it."),
            new("Hydrate like a leaf", "Grab some water. I'll wait here."),
            new("Water o'clock", "A few sips now saves a headache later."),
        ],
    };

    private readonly Dictionary<ReminderKind, int> _next = [];

    public static IReadOnlyList<ReminderCopy> LinesFor(ReminderKind kind) => Lines[kind];

    /// <summary>Returns the next line for <paramref name="kind"/>, advancing the rotation.</summary>
    public ReminderCopy Draw(ReminderKind kind)
    {
        var lines = Lines[kind];
        var index = _next.GetValueOrDefault(kind);
        _next[kind] = (index + 1) % lines.Length;
        return lines[index];
    }

    /// <summary>What Brady says after Done; <paramref name="nextIn"/> is the kind's interval.</summary>
    public static ReminderCopy AfterDone(ReminderKind kind, TimeSpan nextIn) =>
        new($"Nice. See you in {FormatMinutes(nextIn)}.", kind switch
        {
            ReminderKind.Posture => "Logged. Stay tall out there.",
            ReminderKind.Move => "Logged. That lap counts.",
            ReminderKind.Eyes => "Logged. Your eyes thank you.",
            ReminderKind.Stretch => "Logged. Nicely stretched.",
            ReminderKind.Water => "Logged. Brady approves of your hydration.",
            _ => "Logged.",
        });

    /// <summary>What Brady says after Snooze.</summary>
    public static ReminderCopy AfterSnooze(TimeSpan snooze) =>
        new("Okay. I'll hang here.", $"Back in {snooze.TotalMinutes:0} minutes.");

    /// <summary>
    /// The caption beside the chip: the schedule in plain words, or for Move how long the user has
    /// been sitting ("52 min still").
    /// </summary>
    public static string Meta(ReminderKind kind, TimeSpan interval, TimeSpan sittingFor) => kind switch
    {
        ReminderKind.Move => $"{Math.Max(1, (int)sittingFor.TotalMinutes)} min still",
        ReminderKind.Eyes when interval == TimeSpan.FromMinutes(20) => "20-20-20",
        _ => interval == TimeSpan.FromHours(1) ? "Every hour" : $"Every {interval.TotalMinutes:0} min",
    };

    private static string FormatMinutes(TimeSpan span) =>
        span.TotalMinutes >= 60 && span.TotalMinutes % 60 == 0
            ? (span.TotalHours == 1 ? "an hour" : $"{span.TotalHours:0} hours")
            : $"{span.TotalMinutes:0}";
}
