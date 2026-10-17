using System.Globalization;
using Brady.App.Controls;
using Brady.Core.Copy;
using Brady.Core.Reminders;
using Brady.Core.Settings;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Brady.App.Settings;

/// <summary>One reminder kind's card: on/off, interval (preset or custom), snooze, guided length, status.</summary>
public sealed partial class KindViewModel : ObservableObject
{
    /// <summary>The interval choice that means "Custom".</summary>
    public const int Custom = -1;

    private readonly Action<ReminderKind, Action<KindSettings>> _write;
    private bool _loading;

    public KindViewModel(ReminderKind kind, Action<ReminderKind, Action<KindSettings>> write)
    {
        Kind = kind;
        _write = write;
        IntervalChoices = [.. SettingsChoices.IntervalPresets(kind).Select(m => new Choice(m, $"{m} min")), new Choice(Custom, "Custom")];
        SnoozeChoices = [.. SettingsChoices.SnoozeMinutes.Select(m => new Choice(m, $"{m} min"))];
        GuidedChoices = [.. SettingsChoices.GuidedSeconds(kind).Select(s => new Choice(s, $"{s} s"))];
    }

    /// <summary>Asks the window to reset this card (it offers Undo).</summary>
    public event EventHandler? ResetRequested;

    public ReminderKind Kind { get; }

    public string Title => Kind switch
    {
        ReminderKind.Posture => "Posture checks",
        ReminderKind.Move => "Walk breaks",
        ReminderKind.Eyes => "Eye rest",
        ReminderKind.Stretch => "Stretch breaks",
        _ => "Water reminders",
    };

    /// <summary>Move counts continuous sitting, not clock time, so its label says so.</summary>
    public string IntervalLabel => Kind == ReminderKind.Move ? "After sitting for" : "Every";

    public IReadOnlyList<Choice> IntervalChoices { get; }

    public IReadOnlyList<Choice> SnoozeChoices { get; }

    public IReadOnlyList<Choice> GuidedChoices { get; }

    public bool HasGuidedBreak => GuidedChoices.Count > 0;

    [ObservableProperty]
    public partial bool Enabled { get; set; }

    /// <summary>The selected preset in minutes, or <see cref="Custom"/>.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCustom))]
    public partial object IntervalChoice { get; set; } = Custom;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Description))]
    public partial int IntervalMinutes { get; set; }

    [ObservableProperty]
    public partial object SnoozeChoice { get; set; } = SettingsChoices.DefaultSnoozeMinutes;

    [ObservableProperty]
    public partial object? GuidedChoice { get; set; }

    /// <summary>"Next in 12 min", "Off" or "Paused".</summary>
    [ObservableProperty]
    public partial string Status { get; set; } = "";

    public bool IsCustom => Equals(IntervalChoice, Custom);

    /// <summary>The schedule in plain words, under the card's title.</summary>
    public string Description => Kind switch
    {
        ReminderKind.Posture => $"Every {Minutes(IntervalMinutes)} while you type",
        ReminderKind.Move => $"After {Minutes(IntervalMinutes)} without leaving the desk",
        ReminderKind.Eyes => $"Every {Minutes(IntervalMinutes)}, look far away for a moment",
        ReminderKind.Stretch => $"Every {Minutes(IntervalMinutes)}, a slow stretch",
        _ => $"Every {Minutes(IntervalMinutes)}, quietly",
    };

    public void Load(KindSettings settings)
    {
        _loading = true;
        Enabled = settings.Enabled;
        IntervalMinutes = settings.IntervalMinutes;
        IntervalChoice = SettingsChoices.IntervalPresets(Kind).Contains(settings.IntervalMinutes) ? settings.IntervalMinutes : Custom;
        SnoozeChoice = settings.SnoozeMinutes;
        GuidedChoice = settings.GuidedSeconds;
        _loading = false;
    }

    public void UpdateStatus(EngineSnapshot snapshot)
    {
        if (!Enabled) Status = "Off";
        else if (snapshot.PausedUntil is not null) Status = "Paused";
        else if (snapshot.Upcoming.FirstOrDefault(u => u.Kind == Kind)?.DueAt is { } due) Status = "Next in " + StatusText.Duration(due - snapshot.Now);
        else Status = "Waiting for you to come back";
    }

    [RelayCommand]
    private void Reset() => ResetRequested?.Invoke(this, EventArgs.Empty);

    partial void OnEnabledChanged(bool value) => Write(k => k.Enabled = value);

    partial void OnIntervalChoiceChanged(object value)
    {
        if (_loading || value is not int minutes || minutes == Custom) return; // Custom keeps the current minutes
        IntervalMinutes = minutes;
    }

    partial void OnIntervalMinutesChanged(int value) => Write(k => k.IntervalMinutes = value);

    partial void OnSnoozeChoiceChanged(object value)
    {
        if (value is int minutes) Write(k => k.SnoozeMinutes = minutes);
    }

    partial void OnGuidedChoiceChanged(object? value)
    {
        if (value is int seconds) Write(k => k.GuidedSeconds = seconds);
    }

    private void Write(Action<KindSettings> change)
    {
        if (!_loading) _write(Kind, change);
    }

    private static string Minutes(int minutes) => minutes switch
    {
        60 => "hour",
        _ when minutes % 60 == 0 => $"{minutes / 60} hours",
        _ => string.Create(CultureInfo.InvariantCulture, $"{minutes} minutes"),
    };
}
