using Brady.App.Toasts;
using Brady.Core.Copy;
using Brady.Core.Reminders;
using Microsoft.Extensions.Logging;

namespace Brady.App.Dev;

/// <summary>
/// <c>--demo</c>: drives every reminder kind through every exit path so the motion can be tuned:
/// Done, Snooze, Skip, timeout, and two stacked toasts with a reflow. Clicks are simulated through
/// the same code path as real ones; real clicks work too.
/// </summary>
public sealed partial class DemoDriver(ToastStackManager toasts, CopyDeck copy, ILogger<DemoDriver> logger)
{
    private static readonly TimeSpan Read = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan Pause = TimeSpan.FromSeconds(1.2);

    public async Task RunAsync(bool loop)
    {
        var defaultTimeout = toasts.Timeout;
        toasts.Timeout = TimeSpan.FromSeconds(5); // so the timeout path shows up quickly
        try
        {
            do
            {
                await Step(ReminderKind.Posture, "Every 30 min", ToastAction.Done, allowSkip: true);
                await Step(ReminderKind.Eyes, "20-20-20", ToastAction.Snooze);
                await Step(ReminderKind.Move, "52 min still", ToastAction.Skip, allowSkip: true);
                await Step(ReminderKind.Stretch, "Every 90 min", action: null);
                await StackedStep();
            }
            while (loop);
        }
        finally
        {
            toasts.Timeout = defaultTimeout;
        }
    }

    private async Task Step(ReminderKind kind, string meta, ToastAction? action, bool allowSkip = false)
    {
        LogStep(kind.ToString(), action?.ToString() ?? "timeout");
        var shown = toasts.ShowAsync(Content(kind, meta, allowSkip));
        if (action is { } a)
        {
            await Task.Delay(Read);
            toasts.Newest?.Invoke(a);
        }
        await shown;
        await Task.Delay(Pause);
    }

    // Two toasts at once: the first slides up when the second arrives, and back down when it leaves.
    private async Task StackedStep()
    {
        LogStep("Water + Posture", "stacked reflow");
        var water = toasts.ShowAsync(Content(ReminderKind.Water, "Every hour", allowSkip: false));
        await Task.Delay(TimeSpan.FromSeconds(1.5));
        var posture = toasts.ShowAsync(Content(ReminderKind.Posture, "Every 30 min", allowSkip: true));
        await Task.Delay(Read);
        toasts.Newest?.Invoke(ToastAction.Done);
        await posture;
        await Task.Delay(TimeSpan.FromSeconds(1));
        toasts.Newest?.Invoke(ToastAction.Close);
        await water;
        await Task.Delay(Pause);
    }

    private ToastContent Content(ReminderKind kind, string meta, bool allowSkip) =>
        new(kind, copy.Draw(kind), meta, allowSkip, CopyDeck.AfterDone(kind, DemoInterval(kind)));

    private static TimeSpan DemoInterval(ReminderKind kind) => TimeSpan.FromMinutes(kind switch
    {
        ReminderKind.Posture => 30,
        ReminderKind.Move => 50,
        ReminderKind.Eyes => 20,
        ReminderKind.Stretch => 90,
        _ => 60,
    });

    [LoggerMessage(Level = LogLevel.Information, Message = "Demo step: {Kind} -> {Path}")]
    private partial void LogStep(string kind, string path);
}
