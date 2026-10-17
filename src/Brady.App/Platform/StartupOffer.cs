using Brady.App.Settings;
using Brady.App.Toasts;
using Brady.Core.Copy;
using Brady.Core.Reminders;
using Microsoft.Extensions.Logging;

namespace Brady.App.Platform;

/// <summary>
/// For a Brady that was not installed by its installer (a dev or portable build): asks once,
/// shortly after the first start, whether Brady should start with Windows. The installer asks
/// the same question on its own Tasks page, so installed copies never see this.
/// </summary>
public sealed partial class StartupOffer(
    SettingsService settings,
    IStartupRegistration startup,
    ToastStackManager toasts,
    ILogger<StartupOffer> logger)
{
    private static readonly TimeSpan Delay = TimeSpan.FromSeconds(8);

    public async Task MaybeOfferAsync()
    {
        if (AppIdentity.IsInstalled || startup.IsEnabled || settings.Current.StartupOffered) return;

        await Task.Delay(Delay);
        if (startup.IsEnabled) return;
        settings.Update(s => s.StartupOffered = true); // asked once, whatever the answer

        var content = new ToastContent(
            ReminderKind.Posture, // not shown: offers have no chip
            new ReminderCopy("Start with Windows?", "I can wait in the tray each time you sign in."),
            Meta: null,
            AllowSkip: false,
            DoneCopy: new ReminderCopy("Done. See you at sign-in.", "Change it any time in Settings."),
            Snooze: ToastContent.DefaultSnooze,
            Offer: new ToastOffer(Yes: "Yes", No: "Not now"));

        var outcome = await toasts.ShowAsync(content);
        if (outcome == ToastOutcome.Done) startup.Enable();
        LogAnswered(outcome == ToastOutcome.Done ? "yes" : "not now");
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Start with Windows offer answered: {Answer}")]
    private partial void LogAnswered(string answer);
}
