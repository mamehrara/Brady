namespace Brady.Core.Reminders;

/// <summary>The five things Brady nudges you about.</summary>
public enum ReminderKind
{
    Posture,
    Move,
    Eyes,
    Stretch,
    Water,
}

/// <summary>The brand tone a kind is drawn in (chip and countdown ring).</summary>
public enum Tone
{
    Moss,
    Bloom,
    Sky,
    Fur,
}

public static class ReminderKinds
{
    /// <summary>All kinds in coalescing priority order: when several are due at once, the first wins.</summary>
    public static IReadOnlyList<ReminderKind> ByPriority { get; } =
        [ReminderKind.Move, ReminderKind.Stretch, ReminderKind.Posture, ReminderKind.Eyes, ReminderKind.Water];

    public static Tone ToneOf(this ReminderKind kind) => kind switch
    {
        ReminderKind.Posture => Tone.Moss,
        ReminderKind.Move => Tone.Bloom,
        ReminderKind.Eyes or ReminderKind.Water => Tone.Sky,
        ReminderKind.Stretch => Tone.Fur,
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    /// <summary>The word on the kind's chip.</summary>
    public static string Label(this ReminderKind kind) => kind.ToString();
}
