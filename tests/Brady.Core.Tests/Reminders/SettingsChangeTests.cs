using Brady.Core.Quiet;
using Brady.Core.Reminders;
using Brady.Core.Settings;
using static Brady.Core.Reminders.ReminderKind;

namespace Brady.Core.Tests.Reminders;

/// <summary>Settings change while Brady runs: only the affected kind moves, and nothing fires instantly.</summary>
public sealed class SettingsChangeTests
{
    private static Action<ReminderScheduler, DateTimeOffset> Set(ReminderKind kind, Func<KindRule, KindRule> change) =>
        (s, now) => s.UpdateRules(s.Rules.With(kind, change(s.Rules[kind])), now);

    private static Action<ReminderScheduler, DateTimeOffset> Interval(ReminderKind kind, int minutes) =>
        Set(kind, r => r with { Interval = TimeSpan.FromMinutes(minutes) });

    [Fact]
    public void A_longer_interval_mid_countdown_keeps_the_time_already_counted()
    {
        var sim = new SchedulerSim(SchedulerSim.Only((Posture, 30))).Type(10).Do(Interval(Posture, 45)).Type(50);
        Assert.Equal([45], sim.MinutesOf(Posture));
    }

    [Fact]
    public void A_shorter_interval_that_has_already_passed_fires_one_minute_later_not_immediately()
    {
        var sim = new SchedulerSim(SchedulerSim.Only((Posture, 30))).Type(25).Do(Interval(Posture, 20)).Type(10);
        Assert.Equal([26], sim.MinutesOf(Posture));
    }

    [Fact]
    public void An_interval_change_only_moves_that_kind()
    {
        var sim = new SchedulerSim(SchedulerSim.Only((Posture, 30), (Stretch, 60))).Type(10).Do(Interval(Posture, 45)).Type(61);
        Assert.Equal([45], sim.MinutesOf(Posture));
        Assert.Equal([60], sim.MinutesOf(Stretch));
    }

    [Fact]
    public void Disabling_stops_a_kind_and_re_enabling_starts_it_fresh()
    {
        var sim = new SchedulerSim(SchedulerSim.Only((Eyes, 20)))
            .Type(10)
            .Do(Set(Eyes, r => r with { Enabled = false }))
            .Type(30)  // 10..40: would have fired at 20
            .Do(Set(Eyes, r => r with { Enabled = true }))
            .Type(21);
        Assert.Equal([60], sim.MinutesOf(Eyes));
    }

    [Fact]
    public void Each_kind_snoozes_for_its_own_length()
    {
        var rules = SchedulerSim.Only((Eyes, 20), (Stretch, 60));
        rules = rules.With(Eyes, rules[Eyes] with { Snooze = TimeSpan.FromMinutes(5) });
        rules = rules.With(Stretch, rules[Stretch] with { Snooze = TimeSpan.FromMinutes(15) });
        var snoozed = new HashSet<ReminderKind>();
        var sim = new SchedulerSim(rules)
        {
            Respond = due => snoozed.Add(due.Kind) ? ReminderOutcome.Snoozed : ReminderOutcome.Done,
        };
        sim.Type(80);
        Assert.Equal([20, 25, 45, 65], sim.MinutesOf(Eyes));
        Assert.Equal([60, 75], sim.MinutesOf(Stretch));
    }

    [Fact]
    public void Custom_quiet_hours_across_midnight_from_settings()
    {
        var settings = BradySettings.CreateDefault();
        settings.QuietHours.Start = "23:30";
        settings.QuietHours.End = "06:15";
        var evening = new DateTimeOffset(2026, 10, 19, 22, 0, 0, TimeSpan.Zero);
        var sim = new SchedulerSim(settings.ToRules(), evening).Type(9 * 60); // 22:00 to 07:00

        bool Quiet(DateTimeOffset t) => t.TimeOfDay >= new TimeSpan(23, 30, 0) || t.TimeOfDay < new TimeSpan(6, 15, 0);
        Assert.DoesNotContain(sim.History, s => Quiet(s.At));
        Assert.Contains(sim.History, s => s.At.Hour == 23 && s.At.Minute < 30);
        Assert.Contains(sim.History, s => s.At.Hour == 6 && s.At.Minute >= 15);
    }

    [Fact]
    public void A_longer_idle_threshold_keeps_short_breaks_as_sitting()
    {
        var sim = new SchedulerSim(SchedulerSim.Only((Posture, 30)))
            .Do((s, now) => s.UpdateRules(s.Rules with { IdleThreshold = TimeSpan.FromMinutes(10) }, now))
            .Type(20)
            .Idle(7) // would reset with the default 5 minutes
            .Type(10);
        Assert.Equal([30], sim.MinutesOf(Posture));
    }

    [Fact]
    public void A_shorter_idle_threshold_takes_effect_at_once()
    {
        var sim = new SchedulerSim(SchedulerSim.Only((Posture, 30)))
            .Type(20)
            .Idle(2)
            .Do((s, now) => s.UpdateRules(s.Rules with { IdleThreshold = TimeSpan.FromMinutes(1) }, now))
            .Type(31);
        // Away since minute 20 (two minutes idle is now enough), so the countdown restarts on return.
        Assert.Single(sim.MinutesOf(Posture));
        Assert.InRange(sim.MinutesOf(Posture)[0], 52, 52.5);
    }

    [Fact]
    public void The_minimum_gap_follows_the_setting()
    {
        var rules = SchedulerSim.Only((Posture, 50), (Move, 50)) with { MinGap = TimeSpan.FromMinutes(1) };
        var sim = new SchedulerSim(rules).Type(55);
        Assert.Equal([50], sim.MinutesOf(Move));
        Assert.Equal([51], sim.MinutesOf(Posture));
    }
}
