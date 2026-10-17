using System.IO;
using Brady.App.Dev;
using Brady.App.Platform;
using Brady.App.Reminders;
using Brady.App.Settings;
using Brady.App.Theme;
using Brady.App.Toasts;
using Brady.App.Tray;
using Brady.Core.Activity;
using Brady.Core.Copy;
using Brady.Core.Quiet;
using Brady.Core.Reminders;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;

namespace Brady.App.Hosting;

/// <summary>
/// A deliberately lean generic host: DI and Serilog to a rolling file, and nothing else
/// (no console, EventLog or JSON configuration providers), to keep the idle footprint small.
/// </summary>
public static class BradyHost
{
    public static string LogDirectory { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Brady", "logs");

    public static IHost Build(LaunchOptions options)
    {
        var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings
        {
            ApplicationName = "Brady",
            DisableDefaults = true,
        });

        var logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .WriteTo.File(
                Path.Combine(LogDirectory, "brady-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
            .CreateLogger();
        builder.Services.AddSerilog(logger, dispose: true);

        builder.Services.AddSingleton(options);
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<CopyDeck>();
        builder.Services.AddSingleton<SettingsService>();

        // Reminders: the Core engine fed by Windows' input and notification state.
        builder.Services.AddSingleton<IActivitySource, WindowsActivitySource>();
        builder.Services.AddSingleton<IQuietProbe, WindowsQuietProbe>();
        builder.Services.AddSingleton(sp => new ReminderEngine(
            sp.GetRequiredService<TimeProvider>(),
            sp.GetRequiredService<IActivitySource>(),
            sp.GetRequiredService<IQuietProbe>(),
            Rules(sp.GetRequiredService<SettingsService>(), options)));
        builder.Services.AddSingleton<SystemEventsBridge>();
        builder.Services.AddSingleton<ReminderPresenter>();
        builder.Services.AddSingleton<TrayController>();

        builder.Services.AddSingleton<ThemeManager>();
        builder.Services.AddSingleton<IdleTrimmer>();
        builder.Services.AddSingleton<ToastHostWindow>();
        builder.Services.AddSingleton<ToastStackManager>();
        builder.Services.AddSingleton<DemoDriver>();
        builder.Services.AddSingleton<FrameMeter>();
        builder.Services.AddSingleton<SnapshotRenderer>();

        return builder.Build();
    }

    private static ReminderRules Rules(SettingsService settings, LaunchOptions options)
    {
        var rules = settings.Current.ToRules();
        if (!options.Fast) return rules;
        var seconds = rules.Kinds.ToDictionary(
            pair => pair.Key,
            pair => pair.Value with { Interval = pair.Value.Interval / 60, Snooze = pair.Value.Snooze / 60 });
        return rules with { Kinds = seconds, MinGap = rules.MinGap / 60 };
    }
}
