using System.Globalization;
using Brady.Core.Quiet;
using Brady.Core.Reminders;

namespace Brady.Core.Settings;

public enum ThemeChoice
{
    System,
    Light,
    Dark,
}

/// <summary>
/// What's stored in %AppData%\Brady\settings.json. Plain and forgiving: a hand-edited file with
/// odd values gets clamped by <see cref="Normalize"/>, never rejected.
/// </summary>
public sealed class BradySettings
{
    public const int CurrentVersion = 1;
    public const int MinIntervalMinutes = 5;
    public const int MaxIntervalMinutes = 240;

    public int Version { get; set; } = CurrentVersion;

    public Dictionary<ReminderKind, KindSettings> Kinds { get; set; } = DefaultKinds();

    public QuietHoursSettings QuietHours { get; set; } = new();

    public ThemeChoice Theme { get; set; } = ThemeChoice.System;

    public bool Sound { get; set; }

    /// <summary>Set while paused from the tray, so "until tomorrow" survives a restart.</summary>
    public DateTimeOffset? PausedUntil { get; set; }

    public static BradySettings CreateDefault() => new();

    /// <summary>Fills in anything missing and clamps anything out of range.</summary>
    public BradySettings Normalize()
    {
        Version = CurrentVersion;
        Kinds ??= [];
        foreach (var (kind, fallback) in DefaultKinds())
        {
            if (!Kinds.TryGetValue(kind, out var settings) || settings is null)
            {
                Kinds[kind] = fallback;
                continue;
            }
            settings.IntervalMinutes = Math.Clamp(settings.IntervalMinutes, MinIntervalMinutes, MaxIntervalMinutes);
        }
        foreach (var unknown in Kinds.Keys.Where(k => !Enum.IsDefined(k)).ToList()) Kinds.Remove(unknown);

        QuietHours ??= new QuietHoursSettings();
        if (!TryParseTime(QuietHours.Start, out _)) QuietHours.Start = QuietHoursSettings.DefaultStart;
        if (!TryParseTime(QuietHours.End, out _)) QuietHours.End = QuietHoursSettings.DefaultEnd;
        if (!Enum.IsDefined(Theme)) Theme = ThemeChoice.System;
        return this;
    }

    public ReminderRules ToRules()
    {
        var defaults = ReminderRules.Default;
        var kinds = Kinds.ToDictionary(
            pair => pair.Key,
            pair => defaults[pair.Key] with
            {
                Enabled = pair.Value.Enabled,
                Interval = TimeSpan.FromMinutes(pair.Value.IntervalMinutes),
            });
        TryParseTime(QuietHours.Start, out var start);
        TryParseTime(QuietHours.End, out var end);
        return defaults with { Kinds = kinds, QuietHours = new QuietHours(QuietHours.Enabled, start, end) };
    }

    private static Dictionary<ReminderKind, KindSettings> DefaultKinds() =>
        ReminderRules.Default.Kinds.ToDictionary(
            pair => pair.Key,
            pair => new KindSettings { Enabled = pair.Value.Enabled, IntervalMinutes = (int)pair.Value.Interval.TotalMinutes });

    internal static bool TryParseTime(string? text, out TimeOnly time) =>
        TimeOnly.TryParseExact(text, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out time);
}

public sealed class KindSettings
{
    public bool Enabled { get; set; }

    public int IntervalMinutes { get; set; }
}

public sealed class QuietHoursSettings
{
    public const string DefaultStart = "22:00";
    public const string DefaultEnd = "08:00";

    public bool Enabled { get; set; } = true;

    /// <summary>Local time, "HH:mm".</summary>
    public string Start { get; set; } = DefaultStart;

    /// <summary>Local time, "HH:mm"; may be earlier than Start (the window crosses midnight).</summary>
    public string End { get; set; } = DefaultEnd;
}
