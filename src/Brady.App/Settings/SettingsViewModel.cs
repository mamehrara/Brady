using System.Globalization;
using Brady.App.Controls;
using Brady.App.Platform;
using Brady.Core.Reminders;
using Brady.Core.Settings;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Brady.App.Settings;

/// <summary>
/// The Settings window's state. Every change is written straight to <see cref="SettingsService"/>
/// (saved after a short debounce and applied to the engine at once), so there is no Save button.
/// Resets keep a copy of what they replaced, so the window can offer Undo.
/// </summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly SettingsService _settings;
    private readonly IStartupRegistration _startup;
    private bool _loading;

    public SettingsViewModel(SettingsService settings, IStartupRegistration startup)
    {
        _settings = settings;
        _startup = startup;
        Kinds =
        [
            .. new[] { ReminderKind.Posture, ReminderKind.Move, ReminderKind.Eyes, ReminderKind.Stretch, ReminderKind.Water }
                .Select(kind => new KindViewModel(kind, (k, change) => _settings.Update(s => change(s.Kinds[k])))),
        ];
        foreach (var kind in Kinds) kind.ResetRequested += (_, _) => ResetKind(kind);
        Load();
    }

    /// <summary>Raised after a reset with a message and the action that undoes it.</summary>
    public event EventHandler<UndoOffer>? UndoOffered;

    public IReadOnlyList<KindViewModel> Kinds { get; }

    public IReadOnlyList<Choice> ThemeChoices { get; } =
        [new(ThemeChoice.System, "System"), new(ThemeChoice.Light, "Light"), new(ThemeChoice.Dark, "Dark")];

    [ObservableProperty]
    public partial bool QuietHoursEnabled { get; set; }

    /// <summary>Minutes since midnight.</summary>
    [ObservableProperty]
    public partial int QuietStart { get; set; }

    /// <summary>Minutes since midnight; may be earlier than the start (the window crosses midnight).</summary>
    [ObservableProperty]
    public partial int QuietEnd { get; set; }

    [ObservableProperty]
    public partial int IdleThresholdMinutes { get; set; }

    [ObservableProperty]
    public partial int MinGapMinutes { get; set; }

    [ObservableProperty]
    public partial object Theme { get; set; } = ThemeChoice.System;

    [ObservableProperty]
    public partial bool Sound { get; set; }

    /// <summary>Read from (and written to) the registry's Run entry, never from settings.json.</summary>
    [ObservableProperty]
    public partial bool StartWithWindows { get; set; }

    [ObservableProperty]
    public partial string TodayLine { get; set; } = "";

    public void Load()
    {
        _loading = true;
        var s = _settings.Current;
        foreach (var kind in Kinds) kind.Load(s.Kinds[kind.Kind]);
        QuietHoursEnabled = s.QuietHours.Enabled;
        QuietStart = MinutesOfDay(s.QuietHours.Start);
        QuietEnd = MinutesOfDay(s.QuietHours.End);
        IdleThresholdMinutes = s.IdleThresholdMinutes;
        MinGapMinutes = s.MinGapMinutes;
        Theme = s.Theme;
        Sound = s.Sound;
        StartWithWindows = _startup.IsEnabled;
        _loading = false;
    }

    public void UpdateStatus(EngineSnapshot snapshot)
    {
        foreach (var kind in Kinds) kind.UpdateStatus(snapshot);
        var taken = snapshot.Today.Taken;
        TodayLine = taken == 1 ? "Today: 1 break taken" : $"Today: {taken} breaks taken";
    }

    private void ResetKind(KindViewModel kind)
    {
        var before = _settings.Current.Kinds[kind.Kind].Clone();
        _settings.Update(s => s.Kinds[kind.Kind] = BradySettings.DefaultsFor(kind.Kind));
        kind.Load(_settings.Current.Kinds[kind.Kind]);
        UndoOffered?.Invoke(this, new UndoOffer($"{kind.Title} reset to defaults.", () =>
        {
            _settings.Update(s => s.Kinds[kind.Kind] = before);
            kind.Load(_settings.Current.Kinds[kind.Kind]);
        }));
    }

    [RelayCommand]
    private void ResetGeneral()
    {
        var before = _settings.Current.Clone();
        _settings.Update(s => s.ResetGeneral());
        Load();
        UndoOffered?.Invoke(this, new UndoOffer("General settings reset to defaults.", () => Restore(before)));
    }

    [RelayCommand]
    private void ResetAll()
    {
        var before = _settings.Current.Clone();
        _settings.Update(s =>
        {
            foreach (var kind in Enum.GetValues<ReminderKind>()) s.Kinds[kind] = BradySettings.DefaultsFor(kind);
            s.ResetGeneral();
        });
        Load();
        UndoOffered?.Invoke(this, new UndoOffer("Everything reset to defaults.", () => Restore(before)));
    }

    private void Restore(BradySettings before)
    {
        _settings.Update(s =>
        {
            foreach (var (kind, settings) in before.Kinds) s.Kinds[kind] = settings.Clone();
            s.QuietHours = before.QuietHours;
            s.IdleThresholdMinutes = before.IdleThresholdMinutes;
            s.MinGapMinutes = before.MinGapMinutes;
            s.Theme = before.Theme;
            s.Sound = before.Sound;
        });
        Load();
    }

    partial void OnQuietHoursEnabledChanged(bool value) => Write(s => s.QuietHours.Enabled = value);

    partial void OnQuietStartChanged(int value) => Write(s => s.QuietHours.Start = Clock(value));

    partial void OnQuietEndChanged(int value) => Write(s => s.QuietHours.End = Clock(value));

    partial void OnIdleThresholdMinutesChanged(int value) => Write(s => s.IdleThresholdMinutes = value);

    partial void OnMinGapMinutesChanged(int value) => Write(s => s.MinGapMinutes = value);

    partial void OnThemeChanged(object value)
    {
        if (value is ThemeChoice theme) Write(s => s.Theme = theme);
    }

    partial void OnSoundChanged(bool value) => Write(s => s.Sound = value);

    partial void OnStartWithWindowsChanged(bool value)
    {
        if (_loading) return;
        if (value) _startup.Enable();
        else _startup.Disable();
    }

    private void Write(Action<BradySettings> change)
    {
        if (!_loading) _settings.Update(change);
    }

    private static int MinutesOfDay(string time) =>
        TimeOnly.TryParseExact(time, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var t) ? t.Hour * 60 + t.Minute : 0;

    private static string Clock(int minutes) => $"{minutes / 60:00}:{minutes % 60:00}";
}

/// <summary>A short message with an Undo action, shown as a small toast in the Settings window.</summary>
public sealed record UndoOffer(string Message, Action Undo);
