using System.Windows;
using System.Windows.Threading;
using Brady.App.Dev;
using Brady.App.Hosting;
using Brady.App.Platform;
using Brady.App.Reminders;
using Brady.App.Settings;
using Brady.App.Theme;
using Brady.App.Toasts;
using Brady.App.Toasts.Motion;
using Brady.App.Tray;
using Brady.Core.Reminders;
using Brady.Core.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Brady.App;

public partial class App : Application
{
    private readonly IServiceProvider _services;
    private readonly LaunchOptions _options;
    private readonly SingleInstance? _instance;
    private readonly ILogger<App> _logger;
    private FrameMeter? _frameMeter;
    private ReminderEngine? _engine;
    private TrayController? _tray;
    private SystemEventsBridge? _systemEvents;

    public App(IServiceProvider services, LaunchOptions options, SingleInstance? instance)
    {
        _services = services;
        _options = options;
        _instance = instance;
        _logger = services.GetRequiredService<ILogger<App>>();
        DispatcherUnhandledException += OnDispatcherUnhandledException;
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        try
        {
            await StartAsync();
        }
        catch (Exception ex)
        {
            // Never linger as an invisible process: a failed start is logged and ends the app.
            _logger.LogCritical(ex, "Brady failed to start");
            Shutdown(1);
        }
    }

    private async Task StartAsync()
    {
        MotionSpec.ForceReducedMotion = _options.ReducedMotion;
        var settings = _services.GetRequiredService<SettingsService>();

        var theme = _services.GetRequiredService<ThemeManager>();
        theme.Preference = _options.Theme != ThemePreference.System ? _options.Theme : ToPreference(settings.Current.Theme);
        theme.Start();

        if (_options.SnapshotDirectory is { } directory)
        {
            await _services.GetRequiredService<SnapshotRenderer>().RenderAsync(directory);
            Shutdown();
            return;
        }

        var toasts = _services.GetRequiredService<ToastStackManager>();
        await toasts.WarmUpAsync();

        var trimmer = _services.GetRequiredService<IdleTrimmer>();
        toasts.Showing += (_, _) => trimmer.Cancel();
        toasts.Emptied += (_, _) => trimmer.Schedule();
        trimmer.Schedule();

        if (_options.Fps)
        {
            _frameMeter = _services.GetRequiredService<FrameMeter>();
            _frameMeter.Start();
        }

        if (_options.Demo)
        {
            _instance?.Listen(() => { }, () => Dispatcher.BeginInvoke(Shutdown));
            await _services.GetRequiredService<DemoDriver>().RunAsync(_options.Loop);
            Shutdown();
            return;
        }

        StartReminders(settings);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        StartExitWatchdog(e.ApplicationExitCode);
        _frameMeter?.Dispose();
        _tray?.Dispose();
        _systemEvents?.Dispose();
        _engine?.StopAsync().GetAwaiter().GetResult();
        _services.GetService<SettingsService>()?.Dispose(); // flushes a pending save
        base.OnExit(e);
    }

    private void StartReminders(SettingsService settings)
    {
        _engine = _services.GetRequiredService<ReminderEngine>();
        var now = _services.GetRequiredService<TimeProvider>().GetUtcNow();
        if (settings.Current.PausedUntil is { } until)
        {
            if (until > now)
            {
                _engine.Pause(until);
                _logger.LogInformation("Still paused until {Until}", until);
            }
            else settings.Update(s => s.PausedUntil = null);
        }

        _services.GetRequiredService<ReminderPresenter>().Start();
        _systemEvents = _services.GetRequiredService<SystemEventsBridge>();
        _systemEvents.Start();

        _tray = _services.GetRequiredService<TrayController>();
        _tray.QuitRequested += (_, _) => Shutdown();
        _tray.Start();

        _engine.Start();

        // Launching Brady while it's running opens the flyout (Settings, from phase 3); --quit ends it.
        _instance?.Listen(
            onActivate: () => Dispatcher.BeginInvoke(() => _tray.ShowFlyout()),
            onQuit: () => Dispatcher.BeginInvoke(() =>
            {
                _logger.LogInformation("Quit requested by another launch");
                Shutdown();
            }));
        _logger.LogInformation("Brady is running in the tray");
    }

    /// <summary>
    /// A tray app must never linger invisibly. If WPF's shutdown stalls (it can, for example while a
    /// popup is being torn down), end the process a few seconds after OnExit anyway.
    /// </summary>
    private static void StartExitWatchdog(int exitCode)
    {
        var watchdog = new Thread(() =>
        {
            Thread.Sleep(TimeSpan.FromSeconds(5));
            Environment.Exit(exitCode);
        })
        { IsBackground = true, Name = "Brady exit watchdog" };
        watchdog.Start();
    }

    private static ThemePreference ToPreference(ThemeChoice choice) => choice switch
    {
        ThemeChoice.Light => ThemePreference.Light,
        ThemeChoice.Dark => ThemePreference.Dark,
        _ => ThemePreference.System,
    };

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        _logger.LogError(e.Exception, "Unhandled UI exception");
        e.Handled = true;
    }
}
