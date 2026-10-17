using Brady.Core.Copy;
using Brady.Core.Reminders;
using Brady.Core.Stats;

namespace Brady.Core.Tests.Copy;

public sealed class StatusTextTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 19, 14, 0, 0, TimeSpan.Zero);

    private static EngineSnapshot Snapshot(params UpcomingReminder[] upcoming) =>
        new(Now, upcoming, IsPresent: true, PausedUntil: null, DailyStats.For(new DateOnly(2026, 10, 19)));

    [Fact]
    public void Names_the_next_reminder()
    {
        var snapshot = Snapshot(new UpcomingReminder(ReminderKind.Eyes, Now.AddMinutes(6.2)), new(ReminderKind.Posture, Now.AddMinutes(20)));
        Assert.Equal("Brady · next: Eyes in 7 min", StatusText.Tooltip(snapshot, TimeZoneInfo.Utc));
    }

    [Fact]
    public void Says_when_paused()
    {
        var today = Snapshot(new UpcomingReminder(ReminderKind.Eyes, Now.AddMinutes(5))) with { PausedUntil = Now.AddMinutes(30) };
        var tomorrow = today with { PausedUntil = new DateTimeOffset(2026, 10, 20, 8, 0, 0, TimeSpan.Zero) };
        Assert.Equal("Brady · paused until 14:30", StatusText.Tooltip(today, TimeZoneInfo.Utc));
        Assert.Equal("Brady · paused until tomorrow, 08:00", StatusText.Tooltip(tomorrow, TimeZoneInfo.Utc));
    }

    [Fact]
    public void Says_when_away_or_everything_is_off()
    {
        Assert.Equal("Brady · see you when you're back", StatusText.Tooltip(Snapshot() with { IsPresent = false }, TimeZoneInfo.Utc));
        Assert.Equal("Brady · all reminders are off", StatusText.Tooltip(Snapshot(), TimeZoneInfo.Utc));
    }

    [Theory]
    [InlineData(0.2, "1 min")]
    [InlineData(1, "1 min")]
    [InlineData(59.5, "1 h")]
    [InlineData(60, "1 h")]
    [InlineData(95, "1 h 35 min")]
    public void Durations_round_up(double minutes, string expected) =>
        Assert.Equal(expected, StatusText.Duration(TimeSpan.FromMinutes(minutes)));

    [Fact]
    public void The_tooltip_refreshes_when_the_minute_rolls_over()
    {
        var snapshot = Snapshot(new UpcomingReminder(ReminderKind.Eyes, Now.AddMinutes(6).AddSeconds(12)));
        Assert.Equal(TimeSpan.FromSeconds(12.05), StatusText.NextChange(snapshot));
    }
}
