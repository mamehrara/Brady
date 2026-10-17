using System.Windows.Threading;
using Brady.App.Toasts;
using Brady.Core.Copy;
using Brady.Core.Reminders;
using Microsoft.Extensions.Logging;

namespace Brady.App.Reminders;

/// <summary>
/// Turns the engine's due reminders into toasts and reports what the user did back to the engine.
/// The engine raises events on its own thread; everything here runs on the UI thread.
/// </summary>
public sealed partial class ReminderPresenter(
    ReminderEngine engine,
    ToastStackManager toasts,
    CopyDeck copy,
    ILogger<ReminderPresenter> logger)
{
    private Dispatcher? _dispatcher;

    public void Start()
    {
        _dispatcher = Dispatcher.CurrentDispatcher;
        engine.ReminderDue += (_, due) => _dispatcher.BeginInvoke(() => _ = PresentAsync(due));
        engine.Suppressed += (_, reason) => LogSuppressed(reason.ToString());
    }

    /// <summary>"Take a break now" from the tray.</summary>
    public void TakeBreakNow()
    {
        if (engine.TakeBreakNow() is { } due) _ = PresentAsync(due);
    }

    private async Task PresentAsync(ReminderDue due)
    {
        var rule = engine.Rules[due.Kind];
        var content = new ToastContent(
            due.Kind,
            copy.Draw(due.Kind),
            CopyDeck.Meta(due.Kind, rule.Interval, due.SittingFor),
            AllowSkip: true,
            CopyDeck.AfterDone(due.Kind, rule.Interval),
            rule.Snooze);

        var outcome = await toasts.ShowAsync(content);
        engine.Report(due.Kind, outcome switch
        {
            ToastOutcome.Done => ReminderOutcome.Done,
            ToastOutcome.Snoozed => ReminderOutcome.Snoozed,
            ToastOutcome.Skipped => ReminderOutcome.Skipped,
            ToastOutcome.Dismissed => ReminderOutcome.Dismissed,
            _ => ReminderOutcome.Missed,
        });
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Due reminders dropped while quiet: {Reason}")]
    private partial void LogSuppressed(string reason);
}
