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
/// odd values gets clamped by <see cref="Normalize"/>, never rejected. Whether Brady starts with
/// Windows is deliberately not stored here: the registry's Run entry is the only truth.
/// </summary>
public sealed class BradySettings
{
    public const int CurrentVersion = 2;

    public int Version { get; set; } = CurrentVersion;

    public Dictionary<ReminderKind, KindSettings> Kinds { get; set; } = DefaultKinds();

    public QuietHoursSettings QuietHours { get; set; } = new();

    /// <summary>"Count me as away after": minutes without input before the sitting timers reset.</summary>
    public int IdleThresholdMinutes { get; set; } = SettingsChoices.DefaultIdleThresholdMinutes;

    /// <summary>Never two reminders closer together than this.</summary>
    public int MinGapMinutes { get; set; } = SettingsChoices.DefaultGapMinutes;

    public ThemeChoice Theme { get; set; } = ThemeChoice.System;

    public bool Sound { get; set; }

    /// <summary>Set while paused from the tray, so "until tomorrow" survives a restart.</summary>
    public DateTimeOffset? PausedUntil { get; set; }

    /// <summary>The one-time "Start with Windows?" question has been asked (uninstalled builds only).</summary>
    public bool StartupOffered { get; set; }

    public static BradySettings CreateDefault() => new();

    public static KindSettings DefaultsFor(ReminderKind kind)
    {
        var rule = ReminderRules.Default[kind];
        return new KindSettings
        {
            Enabled = rule.Enabled,
            IntervalMinutes = (int)rule.Interval.TotalMinutes,
            SnoozeMinutes = SettingsChoices.DefaultSnoozeMinutes,
            GuidedSeconds = SettingsChoices.DefaultGuidedSeconds(kind),
        };
    }

    /// <summary>Fills in anything missing and clamps anything out of range.</summary>
    public BradySettings Normalize()
    {
        Version = CurrentVersion;
        Kinds ??= [];
        foreach (var unknown in Kinds.Keys.Where(k => !Enum.IsDefined(k)).ToList()) Kinds.Remove(unknown);
        foreach (var kind in Enum.GetValues<ReminderKind>())
        {
            if (!Kinds.TryGetValue(kind, out var settings) || settings is null)
            {
                Kinds[kind] = DefaultsFor(kind);
                continue;
            }
            settings.IntervalMinutes = Math.Clamp(settings.IntervalMinutes, SettingsChoices.MinIntervalMinutes, SettingsChoices.MaxIntervalMinutes);
            settings.SnoozeMinutes = SettingsChoices.Nearest(SettingsChoices.SnoozeMinutes, settings.SnoozeMinutes);
            var guided = SettingsChoices.GuidedSeconds(kind);
            settings.GuidedSeconds = guided.Count == 0
                ? null
                : SettingsChoices.Nearest(guided, settings.GuidedSeconds ?? guided[0]);
        }

        QuietHours ??= new QuietHoursSettings();
        if (!TryParseTime(QuietHours.Start, out _)) QuietHours.Start = QuietHoursSettings.DefaultStart;
        if (!TryParseTime(QuietHours.End, out _)) QuietHours.End = QuietHoursSettings.DefaultEnd;
        IdleThresholdMinutes = Math.Clamp(IdleThresholdMinutes, SettingsChoices.MinIdleThresholdMinutes, SettingsChoices.MaxIdleThresholdMinutes);
        MinGapMinutes = Math.Clamp(MinGapMinutes, SettingsChoices.MinGapMinutes, SettingsChoices.MaxGapMinutes);
        if (!Enum.IsDefined(Theme)) Theme = ThemeChoice.System;
        return this;
    }

    public ReminderRules ToRules()
    {
        var kinds = Kinds.ToDictionary(
            pair => pair.Key,
            pair => new KindRule(
                pair.Value.Enabled,
                TimeSpan.FromMinutes(pair.Value.IntervalMinutes),
                TimeSpan.FromMinutes(pair.Value.SnoozeMinutes)));
        TryParseTime(QuietHours.Start, out var start);
        TryParseTime(QuietHours.End, out var end);
        return new ReminderRules(
            kinds,
            TimeSpan.FromMinutes(IdleThresholdMinutes),
            TimeSpan.FromMinutes(MinGapMinutes),
            new QuietHours(QuietHours.Enabled, start, end));
    }

    /// <summary>The "General" card's values back to their defaults (reminder cards untouched).</summary>
    public void ResetGeneral()
    {
        QuietHours = new QuietHoursSettings();
        IdleThresholdMinutes = SettingsChoices.DefaultIdleThresholdMinutes;
        MinGapMinutes = SettingsChoices.DefaultGapMinutes;
        Theme = ThemeChoice.System;
        Sound = false;
    }

    public BradySettings Clone() => new()
    {
        Version = Version,
        Kinds = Kinds.ToDictionary(pair => pair.Key, pair => pair.Value.Clone()),
        QuietHours = new QuietHoursSettings { Enabled = QuietHours.Enabled, Start = QuietHours.Start, End = QuietHours.End },
        IdleThresholdMinutes = IdleThresholdMinutes,
        MinGapMinutes = MinGapMinutes,
        Theme = Theme,
        Sound = Sound,
        PausedUntil = PausedUntil,
        StartupOffered = StartupOffered,
    };

    private static Dictionary<ReminderKind, KindSettings> DefaultKinds() =>
        Enum.GetValues<ReminderKind>().ToDictionary(kind => kind, DefaultsFor);

    internal static bool TryParseTime(string? text, out TimeOnly time) =>
        TimeOnly.TryParseExact(text, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out time);
}

public sealed class KindSettings
{
    public bool Enabled { get; set; }

    public int IntervalMinutes { get; set; }

    public int SnoozeMinutes { get; set; } = SettingsChoices.DefaultSnoozeMinutes;

    /// <summary>Guided-break length in seconds (Eyes and Stretch only; used from phase 4).</summary>
    public int? GuidedSeconds { get; set; }

    public KindSettings Clone() => (KindSettings)MemberwiseClone();
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
