using Brady.App.Theme;

namespace Brady.App.Hosting;

/// <summary>Command-line switches. Everything except the theme override is a hidden developer flag.</summary>
public sealed record LaunchOptions
{
    /// <summary><c>--demo</c>: cycles every reminder kind through Done, Snooze, Skip, timeout and a stacked reflow.</summary>
    public bool Demo { get; init; }

    /// <summary><c>--loop</c>: repeat the demo until closed (otherwise it exits after one cycle).</summary>
    public bool Loop { get; init; }

    /// <summary><c>--fps</c>: frame-time overlay plus a per-second dropped-frame log.</summary>
    public bool Fps { get; init; }

    /// <summary><c>--reduced-motion</c>: preview the reduced-motion path without changing Windows settings.</summary>
    public bool ReducedMotion { get; init; }

    /// <summary>
    /// <c>--hardware</c>: render on the GPU. Off by default: for Brady's small layered windows WPF's
    /// GPU path renders and then reads back anyway, and the graphics driver it loads costs ~80 MB
    /// and ~50 threads for no gain in smoothness (measured in phase 1).
    /// </summary>
    public bool HardwareRendering { get; init; }

    /// <summary><c>--snapshot &lt;dir&gt;</c>: render the reference-screen grid in light and dark to PNGs, then exit.</summary>
    public string? SnapshotDirectory { get; init; }

    /// <summary><c>--theme light|dark</c>: override the Windows app theme.</summary>
    public ThemePreference Theme { get; init; } = ThemePreference.System;

    public static LaunchOptions Parse(IReadOnlyList<string> args)
    {
        var options = new LaunchOptions();
        for (var i = 0; i < args.Count; i++)
        {
            options = args[i].ToLowerInvariant() switch
            {
                "--demo" => options with { Demo = true },
                "--loop" => options with { Loop = true },
                "--fps" => options with { Fps = true },
                "--reduced-motion" => options with { ReducedMotion = true },
                "--hardware" => options with { HardwareRendering = true },
                "--snapshot" when i + 1 < args.Count => options with { SnapshotDirectory = args[++i] },
                "--theme" when i + 1 < args.Count => options with { Theme = Enum.Parse<ThemePreference>(args[++i], ignoreCase: true) },
                _ => options,
            };
        }
        return options;
    }
}
