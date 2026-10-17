using Brady.Core.Quiet;
using Brady.Core.Reminders;
using static Brady.Core.Reminders.ReminderKind;

namespace Brady.Core.Tests.Reminders;

public sealed class ReminderSchedulerTests
{
    // ---- intervals and active time -------------------------------------------------------

    [Fact]
    public void A_kind_fires_after_its_interval_of_active_time()
    {
        var sim = new SchedulerSim(SchedulerSim.Only((Posture, 30))).Type(95);
        Assert.Equal([30, 60, 90], sim.MinutesOf(Posture));
    }

    [Fact]
    public void With_the_defaults_eyes_comes_first()
    {
        var sim = new SchedulerSim().Type(21);
        Assert.Equal(Eyes, Assert.Single(sim.History).Kind);
        Assert.Equal(20, (sim.History[0].At - SchedulerSim.Start).TotalMinutes);
    }

    [Fact]
    public void Disabled_kinds_never_fire()
    {
        var sim = new SchedulerSim().Type(300);
        Assert.Empty(sim.ShownOf(Water)); // off by default
    }

    // ---- away: idle and lock --------------------------------------------------------------

    [Fact]
    public void Five_minutes_idle_resets_the_sitting_timers()
    {
        var sim = new SchedulerSim(SchedulerSim.Only((Posture, 30)))
            .Type(25)
            .Idle(5)  // 25..30: away, counted from minute 25
            .Type(31); // back at 30: a fresh 30 minutes
        AssertMinutes([60], sim.MinutesOf(Posture));
    }

    [Fact]
    public void Less_than_five_minutes_idle_is_still_sitting()
    {
        var sim = new SchedulerSim(SchedulerSim.Only((Posture, 30)))
            .Type(20)
            .Idle(TimeSpan.FromSeconds(4 * 60 + 50))
            .Type(10);
        Assert.Equal([30], sim.MinutesOf(Posture));
    }

    [Fact]
    public void Idle_minutes_before_leaving_do_not_count()
    {
        // Typed 0..20, idle 20..40. Away from minute 20, back at 40: Posture is due at 70, not 60.
        var sim = new SchedulerSim(SchedulerSim.Only((Posture, 30))).Type(20).Idle(20).Type(31);
        AssertMinutes([70], sim.MinutesOf(Posture));
    }

    [Fact]
    public void Locking_resets_the_sitting_timers()
    {
        var sim = new SchedulerSim(SchedulerSim.Only((Move, 50)))
            .Type(45)
            .Do((s, now) => s.SessionLocked(now))
            .Idle(2)
            .Do((s, now) => s.SessionUnlocked(now))
            .Type(51);
        Assert.Equal([97], sim.MinutesOf(Move));
    }

    [Fact]
    public void Sleep_and_resume_count_as_away()
    {
        var sim = new SchedulerSim(SchedulerSim.Only((Eyes, 20)))
            .Type(15)
            .Do((s, now) => s.Suspended(now))
            .Idle(60)
            .Do((s, now) => s.Resumed(now))
            .Type(21);
        Assert.Equal([95], sim.MinutesOf(Eyes));
    }

    [Fact]
    public void Water_pauses_while_away_instead_of_resetting()
    {
        var sim = new SchedulerSim(SchedulerSim.Only((Water, 60)))
            .Type(40)
            .Do((s, now) => s.SessionLocked(now))
            .Idle(20)
            .Do((s, now) => s.SessionUnlocked(now))
            .Type(21);
        // 40 active minutes before the lock, 20 after unlocking at 60: due at 80.
        Assert.Equal([80], sim.MinutesOf(Water));
    }

    [Fact]
    public void Nothing_fires_while_away()
    {
        var sim = new SchedulerSim(SchedulerSim.Only((Eyes, 20))).Type(10).Idle(120);
        Assert.Empty(sim.History);
    }

    // ---- never nag: coalescing and the minimum gap ----------------------------------------

    [Fact]
    public void When_several_are_due_the_highest_priority_shows_and_the_rest_wait_the_gap()
    {
        var sim = new SchedulerSim(SchedulerSim.Only((Posture, 50), (Move, 50), (Eyes, 50))).Type(60);
        Assert.Equal([Move, Posture, Eyes], sim.History.Select(s => s.Kind));
        Assert.Equal([50, 54, 58], sim.History.Select(s => (s.At - SchedulerSim.Start).TotalMinutes));
    }

    [Fact]
    public void Two_reminders_are_never_within_four_minutes()
    {
        var sim = new SchedulerSim().Type(8 * 60);
        var times = sim.History.Select(s => s.At).Order().ToList();
        Assert.True(times.Count > 10);
        Assert.All(times.Zip(times.Skip(1)), pair => Assert.True(pair.Second - pair.First >= TimeSpan.FromMinutes(4)));
    }

    [Fact]
    public void A_kind_due_inside_the_gap_is_pushed_back_not_dropped()
    {
        var sim = new SchedulerSim(SchedulerSim.Only((Eyes, 20), (Posture, 22))).Type(30);
        Assert.Equal([20], sim.MinutesOf(Eyes));
        Assert.Equal([24], sim.MinutesOf(Posture));
    }

    // ---- outcomes --------------------------------------------------------------------------

    [Fact]
    public void Snooze_refires_after_the_snooze_length()
    {
        var sim = new SchedulerSim(SchedulerSim.Only((Eyes, 20)));
        var first = true;
        sim.Respond = _ =>
        {
            var outcome = first ? ReminderOutcome.Snoozed : ReminderOutcome.Done;
            first = false;
            return outcome;
        };
        sim.Type(55);
        Assert.Equal([20, 30, 50], sim.MinutesOf(Eyes));
    }

    [Theory]
    [InlineData(ReminderOutcome.Done)]
    [InlineData(ReminderOutcome.Skipped)]
    [InlineData(ReminderOutcome.Dismissed)]
    [InlineData(ReminderOutcome.Missed)]
    public void Every_other_outcome_restarts_the_interval(ReminderOutcome outcome)
    {
        var sim = new SchedulerSim(SchedulerSim.Only((Posture, 30))) { Respond = _ => outcome };
        sim.Type(61);
        Assert.Equal([30, 60], sim.MinutesOf(Posture));
    }

    [Fact]
    public void Breaks_taken_and_missed_are_counted_separately()
    {
        var n = 0;
        var sim = new SchedulerSim(SchedulerSim.Only((Eyes, 20)))
        {
            Respond = _ => n++ % 2 == 0 ? ReminderOutcome.Done : ReminderOutcome.Missed,
        };
        sim.Type(81);
        Assert.Equal(2, sim.Scheduler.Today.Taken);
        Assert.Equal(2, sim.Scheduler.Today.Missed);
    }

    [Fact]
    public void Move_reports_how_long_the_user_has_been_sitting()
    {
        var sim = new SchedulerSim(SchedulerSim.Only((Move, 50))).Type(51);
        Assert.Equal(TimeSpan.FromMinutes(50), Assert.Single(sim.History).SittingFor);
    }

    // ---- quiet conditions, no catching up ----------------------------------------------------

    [Theory]
    [InlineData(QuietReason.FullScreen)]
    [InlineData(QuietReason.Presentation)]
    [InlineData(QuietReason.WindowsQuietTime)]
    public void Windows_quiet_drops_due_reminders_without_catching_up(QuietReason reason)
    {
        var sim = new SchedulerSim(SchedulerSim.Only((Eyes, 20))).Type(15);
        sim.SystemQuiet = reason;
        sim.Type(10); // 15..25: Eyes comes due at 20 and is dropped
        sim.SystemQuiet = QuietReason.None;
        sim.Type(20);
        Assert.Equal([40], sim.MinutesOf(Eyes)); // not at 25: no catch-up
        Assert.Contains(sim.Dropped, d => d.Reason == reason);
    }

    [Fact]
    public void Quiet_hours_across_midnight_stay_silent_and_do_not_pile_up()
    {
        var evening = new DateTimeOffset(2026, 10, 19, 21, 0, 0, TimeSpan.Zero);
        var sim = new SchedulerSim(start: evening).Type(12 * 60); // 21:00 to 09:00

        bool InQuiet(DateTimeOffset t) => t.Hour >= 22 || t.Hour < 8;
        Assert.DoesNotContain(sim.History, s => InQuiet(s.At));
        Assert.Contains(sim.History, s => s.At.Hour == 21);
        Assert.Contains(sim.Dropped, d => d.Reason == QuietReason.QuietHours);

        // At 08:00 nothing bursts out: at most one reminder in the first few minutes, as usual.
        var morning = new DateTimeOffset(2026, 10, 20, 8, 0, 0, TimeSpan.Zero);
        Assert.True(sim.History.Count(s => s.At >= morning && s.At < morning.AddMinutes(4)) <= 1);
    }

    [Fact]
    public void Pausing_silences_until_the_pause_ends_then_starts_fresh()
    {
        var sim = new SchedulerSim(SchedulerSim.Only((Eyes, 20)))
            .Type(5)
            .Do((s, now) => s.Pause(now.AddMinutes(30)))
            .Type(60);
        Assert.Equal([55], sim.MinutesOf(Eyes)); // pause ends at 35, then a full 20 minutes
    }

    [Fact]
    public void Resuming_early_starts_fresh()
    {
        var sim = new SchedulerSim(SchedulerSim.Only((Eyes, 20)))
            .Type(5)
            .Do((s, now) => s.Pause(now.AddHours(1)))
            .Type(10)
            .Do((s, now) => s.Resume(now))
            .Type(21);
        Assert.Equal([35], sim.MinutesOf(Eyes));
    }

    // ---- tray actions and settings -----------------------------------------------------------

    [Fact]
    public void Take_a_break_now_shows_the_kind_closest_to_due()
    {
        var sim = new SchedulerSim().Type(15);
        var due = sim.Scheduler.TakeBreakNow(sim.Now);
        Assert.Equal(Eyes, due?.Kind); // Eyes is 5 minutes from due, the others further
    }

    [Fact]
    public void Switching_a_kind_on_starts_its_interval_from_now()
    {
        var sim = new SchedulerSim(SchedulerSim.Only((Eyes, 20))).Type(100);
        sim.Do((s, now) => s.UpdateRules(s.Rules.With(Water, s.Rules[Water] with { Enabled = true }), now)).Type(65);
        // Due at 160, but Eyes is due then too and outranks Water, so Water waits out the gap.
        Assert.Equal([164], sim.MinutesOf(Water));
    }

    [Fact]
    public void Upcoming_lists_enabled_kinds_soonest_first()
    {
        var sim = new SchedulerSim().Type(1);
        var upcoming = sim.Scheduler.Upcoming(sim.Now);
        Assert.Equal([Eyes, Posture, Move, Stretch], upcoming.Select(u => u.Kind));
        Assert.Equal(SchedulerSim.Start.AddMinutes(20), upcoming[0].DueAt);
    }

    [Fact]
    public void Upcoming_has_no_times_while_away()
    {
        var sim = new SchedulerSim().Type(1).Do((s, now) => s.SessionLocked(now));
        Assert.All(sim.Scheduler.Upcoming(sim.Now), u => Assert.Null(u.DueAt));
    }

    // A return from idleness is noticed by the next away poll (30s), so its start can lag by that much.
    private static void AssertMinutes(double[] expected, IReadOnlyList<double> actual)
    {
        Assert.Equal(expected.Length, actual.Count);
        foreach (var (e, a) in expected.Zip(actual))
            Assert.InRange(a, e, e + ReminderScheduler.AwayPoll.TotalMinutes);
    }

    [Fact]
    public void The_scheduler_sleeps_until_something_can_happen()
    {
        var scheduler = new ReminderScheduler(SchedulerSim.Only((Stretch, 90)), SchedulerSim.Start, TimeZoneInfo.Utc);
        var decision = scheduler.Tick(SchedulerSim.Start, SchedulerSim.Start, QuietReason.None);
        // Nothing is due for 90 minutes; the next thing to check is whether the user went idle.
        Assert.Equal(SchedulerSim.Start.AddMinutes(5), decision.WakeAt);
    }
}
