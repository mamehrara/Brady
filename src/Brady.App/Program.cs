using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Brady.App.Hosting;
using Microsoft.Extensions.Hosting;

namespace Brady.App;

public static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        var options = LaunchOptions.Parse(args);

        // Snapshots are a dev tool and may run beside a live Brady; everything else is single-instance.
        using var instance = options.SnapshotDirectory is null ? SingleInstance.TryAcquire() : null;
        if (instance is null && options.SnapshotDirectory is null) return 0;

        // Must run before any Timeline exists: cap every animation clock at 60fps.
        Timeline.DesiredFrameRateProperty.OverrideMetadata(typeof(Timeline), new FrameworkPropertyMetadata { DefaultValue = 60 });

        if (!options.HardwareRendering) RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;

        using var host = BradyHost.Build(options);
        host.Start();

        var app = new App(host.Services, options, instance);
        app.InitializeComponent();
        var exitCode = app.Run();

        host.StopAsync().GetAwaiter().GetResult();
        return exitCode;
    }
}
