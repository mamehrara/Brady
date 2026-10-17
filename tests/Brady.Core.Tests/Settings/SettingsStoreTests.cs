using Brady.Core.Reminders;
using Brady.Core.Settings;
using Microsoft.Extensions.Time.Testing;

namespace Brady.Core.Tests.Settings;

public sealed class SettingsStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "brady-tests-" + Guid.NewGuid().ToString("N"));
    private readonly FakeTimeProvider _time = new();

    public SettingsStoreTests() => Directory.CreateDirectory(_dir);

    private string FilePath => Path.Combine(_dir, "settings.json");

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public void A_missing_file_gives_the_defaults()
    {
        using var store = new SettingsStore(FilePath, _time);
        var settings = store.Load();
        Assert.Equal(BradySettings.CurrentVersion, settings.Version);
        Assert.True(settings.Kinds[ReminderKind.Posture].Enabled);
        Assert.False(settings.Kinds[ReminderKind.Water].Enabled);
        Assert.Null(store.LastProblem);
    }

    [Fact]
    public void Settings_round_trip()
    {
        using var store = new SettingsStore(FilePath, _time);
        var settings = BradySettings.CreateDefault();
        settings.Kinds[ReminderKind.Eyes].IntervalMinutes = 40;
        settings.QuietHours.Start = "23:30";
        settings.Theme = ThemeChoice.Dark;
        store.Save(settings);

        var loaded = store.Load();
        Assert.Equal(40, loaded.Kinds[ReminderKind.Eyes].IntervalMinutes);
        Assert.Equal("23:30", loaded.QuietHours.Start);
        Assert.Equal(ThemeChoice.Dark, loaded.Theme);
        Assert.Contains("\"version\": 1", File.ReadAllText(FilePath), StringComparison.Ordinal);
    }

    [Fact]
    public void Hand_edited_nonsense_is_clamped_not_fatal()
    {
        File.WriteAllText(FilePath, """
            {
              // a comment, a trailing comma and some bad values
              "version": 1,
              "kinds": {
                "posture": { "enabled": true, "intervalMinutes": 1 },
                "eyes": { "enabled": true, "intervalMinutes": "9999" },
              },
              "quietHours": { "enabled": true, "start": "25:99", "end": "07:00" },
              "theme": "dark",
            }
            """);
        using var store = new SettingsStore(FilePath, _time);
        var settings = store.Load();

        Assert.Equal(BradySettings.MinIntervalMinutes, settings.Kinds[ReminderKind.Posture].IntervalMinutes);
        Assert.Equal(BradySettings.MaxIntervalMinutes, settings.Kinds[ReminderKind.Eyes].IntervalMinutes);
        Assert.Equal(50, settings.Kinds[ReminderKind.Move].IntervalMinutes); // missing: default
        Assert.Equal(QuietHoursSettings.DefaultStart, settings.QuietHours.Start);
        Assert.Equal("07:00", settings.QuietHours.End);
        Assert.Null(store.LastProblem);
    }

    [Fact]
    public void An_unreadable_file_is_set_aside_and_defaults_are_used()
    {
        File.WriteAllText(FilePath, "{ this is not json");
        using var store = new SettingsStore(FilePath, _time);
        var settings = store.Load();

        Assert.Equal(30, settings.Kinds[ReminderKind.Posture].IntervalMinutes);
        Assert.NotNull(store.LastProblem);
        Assert.True(File.Exists(FilePath + ".bad"));
    }

    [Fact]
    public void A_file_from_a_newer_brady_is_kept_aside_not_overwritten()
    {
        File.WriteAllText(FilePath, """{ "version": 99 }""");
        using var store = new SettingsStore(FilePath, _time);
        store.Load();
        Assert.Equal("""{ "version": 99 }""", File.ReadAllText(FilePath + ".bad"));
    }

    [Fact]
    public void Scheduled_saves_are_debounced_into_one_write()
    {
        using var store = new SettingsStore(FilePath, _time);
        var settings = BradySettings.CreateDefault();
        for (var i = 1; i <= 5; i++)
        {
            settings.Kinds[ReminderKind.Eyes].IntervalMinutes = 20 + i;
            store.ScheduleSave(settings);
            _time.Advance(TimeSpan.FromMilliseconds(100));
        }
        Assert.False(File.Exists(FilePath));

        _time.Advance(SettingsStore.SaveDelay);
        Assert.Equal(25, store.Load().Kinds[ReminderKind.Eyes].IntervalMinutes);
    }

    [Fact]
    public void Settings_become_scheduler_rules()
    {
        var settings = BradySettings.CreateDefault();
        settings.Kinds[ReminderKind.Water].Enabled = true;
        settings.Kinds[ReminderKind.Water].IntervalMinutes = 45;
        settings.QuietHours.Enabled = false;

        var rules = settings.ToRules();
        Assert.Equal(new KindRule(true, TimeSpan.FromMinutes(45), TimeSpan.FromMinutes(10)), rules[ReminderKind.Water]);
        Assert.False(rules.QuietHours.Enabled);
    }
}
