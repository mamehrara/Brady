using Brady.Core.Activity;
using Brady.Core.Quiet;
using Brady.Core.Reminders;
using Microsoft.Extensions.Time.Testing;

namespace Brady.Core.Tests.Reminders;

public sealed class ReminderEngineTests
{
    private readonly FakeTimeProvider _time = new(SchedulerSim.Start);

    [Fact]
    public async Task The_engine_sleeps_until_a_reminder_is_due_then_raises_it()
    {
        _time.SetLocalTimeZone(TimeZoneInfo.Utc);
        using var engine = new ReminderEngine(_time, new AlwaysTyping(_time), new NeverQuiet(), SchedulerSim.Only((ReminderKind.Eyes, 20)));
        var raised = new TaskCompletionSource<ReminderDue>(TaskCreationOptions.RunContinuationsAsynchronously);
        engine.ReminderDue += (_, due) => raised.TrySetResult(due);
        engine.Start();

        // Walk the clock forward a minute at a time, letting the loop react between steps.
        for (var minute = 0; minute < 19; minute++)
        {
            await Settle();
            _time.Advance(TimeSpan.FromMinutes(1));
        }
        await Settle();
        Assert.False(raised.Task.IsCompleted);

        _time.Advance(TimeSpan.FromMinutes(1));
        var due = await raised.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        Assert.Equal(ReminderKind.Eyes, due.Kind);
        await engine.StopAsync();
    }

    [Fact]
    public async Task Commands_wake_the_loop_and_update_the_snapshot()
    {
        using var engine = new ReminderEngine(_time, new AlwaysTyping(_time), new NeverQuiet(), ReminderRules.Default);
        engine.Start();
        await Settle();

        var until = _time.GetUtcNow().AddMinutes(30);
        engine.Pause(until);
        Assert.Equal(until, engine.Snapshot().PausedUntil);

        engine.Resume();
        Assert.Null(engine.Snapshot().PausedUntil);
        await engine.StopAsync();
    }

    // The loop runs on the thread pool; give it a moment to reach its next await.
    private static Task Settle() => Task.Delay(20, TestContext.Current.CancellationToken);

    private sealed class AlwaysTyping(TimeProvider time) : IActivitySource
    {
        public DateTimeOffset LastInputAt => time.GetUtcNow();
    }

    private sealed class NeverQuiet : IQuietProbe
    {
        public QuietReason Probe() => QuietReason.None;
    }
}
