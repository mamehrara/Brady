using System.Windows;
using System.Windows.Threading;
using Brady.App.Dev;
using Brady.App.Hosting;
using Brady.App.Platform;
using Brady.App.Theme;
using Brady.App.Toasts;
using Brady.App.Toasts.Motion;
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
        MotionSpec.ForceReducedMotion = _options.ReducedMotion;

        var theme = _services.GetRequiredService<ThemeManager>();
        theme.Preference = _options.Theme;
        theme.Start();

        if (_options.SnapshotDirectory is { } directory)
        {
            await _services.GetRequiredService<SnapshotRenderer>().RenderAsync(directory);
            Shutdown();
            return;
        }

        // Until the tray arrives (phase 2), launching Brady again is how a running instance is quit.
        _instance?.Listen(() => Dispatcher.BeginInvoke(() =>
        {
            _logger.LogInformation("Second launch: shutting down (Settings arrive in phase 3)");
            Shutdown();
        }));

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

        if (!_options.Demo)
        {
            _logger.LogInformation("Idle: warmed up and waiting (reminders arrive in phase 2)");
            return;
        }

        await _services.GetRequiredService<DemoDriver>().RunAsync(_options.Loop);
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _frameMeter?.Dispose();
        base.OnExit(e);
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        _logger.LogError(e.Exception, "Unhandled UI exception");
        e.Handled = true;
    }
}
