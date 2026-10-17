using System.IO;
using Brady.App.Dev;
using Brady.App.Platform;
using Brady.App.Theme;
using Brady.App.Toasts;
using Brady.Core.Copy;
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
        builder.Services.AddSingleton<ThemeManager>();
        builder.Services.AddSingleton<IdleTrimmer>();
        builder.Services.AddSingleton<ToastHostWindow>();
        builder.Services.AddSingleton<ToastStackManager>();
        builder.Services.AddSingleton<DemoDriver>();
        builder.Services.AddSingleton<FrameMeter>();
        builder.Services.AddSingleton<SnapshotRenderer>();

        return builder.Build();
    }
}
