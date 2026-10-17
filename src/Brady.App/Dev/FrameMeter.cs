using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Brady.App.Toasts;
using Microsoft.Extensions.Logging;

namespace Brady.App.Dev;

/// <summary>
/// <c>--fps</c>: measures UI-thread frame pacing from <see cref="CompositionTarget.Rendering"/> while
/// toasts are on screen. A frame counts as dropped when it arrives more than 1.5 frame intervals
/// after the previous one. Logs a line per second with activity and a total at exit, and shows a
/// small overlay in the host window. Log lines are buffered and written at exit, so the meter does
/// no file I/O on the UI thread while it measures. Subscribing forces WPF to render every frame,
/// so this is a dev-only tool and never on by default.
/// </summary>
public sealed partial class FrameMeter(ToastHostWindow host, ILogger<FrameMeter> logger) : IDisposable
{
    private const double FrameMs = 1000.0 / 60;

    private readonly TextBlock _overlay = new()
    {
        FontSize = 11,
        Foreground = Brushes.White,
        Background = new SolidColorBrush(Color.FromArgb(0xB0, 0, 0, 0)),
        Padding = new Thickness(6, 2, 6, 2),
        IsHitTestVisible = false,
    };

    private readonly List<string> _buffer = [];
    private TimeSpan _last;
    private TimeSpan _windowStart;
    private int _frames, _dropped, _totalFrames, _totalDropped;
    private double _worstMs, _totalWorstMs;
    private bool _running;

    public void Start()
    {
        if (_running) return;
        _running = true;
        Canvas.SetLeft(_overlay, 4);
        Canvas.SetTop(_overlay, 4);
        host.Stack.Children.Add(_overlay);
        CompositionTarget.Rendering += OnRendering;
    }

    public void Dispose()
    {
        if (!_running) return;
        _running = false;
        CompositionTarget.Rendering -= OnRendering;
        foreach (var line in _buffer) LogBuffered(line);
        LogTotal(_totalFrames, _totalDropped, _totalWorstMs);
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        var now = ((RenderingEventArgs)e).RenderingTime;
        if (now == _last) return; // Rendering can fire more than once per frame
        var delta = (now - _last).TotalMilliseconds;
        _last = now;
        if (!host.IsVisible || delta > 1000) { _windowStart = now; return; }

        _frames++;
        _totalFrames++;
        _worstMs = Math.Max(_worstMs, delta);
        _totalWorstMs = Math.Max(_totalWorstMs, delta);
        if (delta > FrameMs * 1.5)
        {
            var missed = (int)Math.Round(delta / FrameMs) - 1;
            _dropped += missed;
            _totalDropped += missed;
            _buffer.Add($"{DateTime.Now:HH:mm:ss.fff} hitch {delta:0.0} ms");
        }

        var elapsed = (now - _windowStart).TotalSeconds;
        if (elapsed < 1) return;
        var fps = _frames / elapsed;
        _overlay.Text = $"{fps:0} fps  dropped {_dropped}  worst {_worstMs:0.0} ms";
        _buffer.Add($"{DateTime.Now:HH:mm:ss.fff} {fps:0} fps, dropped {_dropped}, worst {_worstMs:0.0} ms");
        _windowStart = now;
        _frames = _dropped = 0;
        _worstMs = 0;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Frames: {Line}")]
    private partial void LogBuffered(string line);

    [LoggerMessage(Level = LogLevel.Information, Message = "Frame total: {Frames} frames, dropped {Dropped}, worst {WorstMs:0.0} ms")]
    private partial void LogTotal(int frames, int dropped, double worstMs);
}
