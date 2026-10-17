using Brady.Core.Reminders;

namespace Brady.Core.Settings;

/// <summary>The choices Settings offers, in one place for the UI and for clamping hand-edited files.</summary>
public static class SettingsChoices
{
    public const int MinIntervalMinutes = 5;
    public const int MaxIntervalMinutes = 240;

    public const int MinIdleThresholdMinutes = 1;
    public const int MaxIdleThresholdMinutes = 30;
    public const int DefaultIdleThresholdMinutes = 5;

    public const int MinGapMinutes = 1;
    public const int MaxGapMinutes = 30;
    public const int DefaultGapMinutes = 4;

    public const int DefaultSnoozeMinutes = 10;

    /// <summary>Interval presets per kind; anything else is "Custom".</summary>
    public static IReadOnlyList<int> IntervalPresets(ReminderKind kind) => kind switch
    {
        ReminderKind.Posture => [20, 30, 45, 60],
        ReminderKind.Eyes => [20, 30, 40],
        ReminderKind.Move => [30, 45, 50, 60, 90],
        ReminderKind.Stretch => [60, 90, 120],
        ReminderKind.Water => [30, 45, 60, 90],
        _ => [],
    };

    public static IReadOnlyList<int> SnoozeMinutes { get; } = [5, 10, 15, 30];

    /// <summary>Guided-break lengths in seconds; empty for kinds without a guided break.</summary>
    public static IReadOnlyList<int> GuidedSeconds(ReminderKind kind) => kind switch
    {
        ReminderKind.Eyes => [20, 30],
        ReminderKind.Stretch => [15, 30, 45],
        _ => [],
    };

    public static int? DefaultGuidedSeconds(ReminderKind kind) =>
        GuidedSeconds(kind) is { Count: > 0 } choices ? choices[0] : null;

    /// <summary>The allowed value closest to <paramref name="value"/> (ties go to the smaller one).</summary>
    public static int Nearest(IReadOnlyList<int> allowed, int value) =>
        allowed.OrderBy(a => Math.Abs(a - value)).ThenBy(a => a).First();
}
