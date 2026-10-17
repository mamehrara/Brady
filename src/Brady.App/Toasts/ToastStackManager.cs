using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Brady.App.Controls;
using Brady.App.Theme;
using Brady.App.Toasts.Motion;
using Brady.Core.Copy;
using Microsoft.Extensions.Logging;

namespace Brady.App.Toasts;

/// <summary>
/// Shows toasts in the host window: at most two, stacked upward 12px apart, newest at the bottom.
/// The host window is shown once during warm-up and then stays up, empty and transparent when idle.
/// Owns each toast's lifetime (enter, 20s timeout paused on hover, Done/Snooze replies, exit) and the
/// FLIP reflow that slides the others into place. Views are pooled and pre-warmed, so the first
/// toast after startup renders exactly like the hundredth.
/// </summary>
public sealed partial class ToastStackManager
{
    private const int PoolSize = ToastLayout.MaxVisible + 1; // one more for a toast still leaving

    private readonly ToastHostWindow _host;
    private readonly ThemeManager _theme;
    private readonly ILogger<ToastStackManager> _logger;
    private readonly Stack<ToastView> _pool = new();
    private readonly List<ActiveToast> _active = []; // index 0 = newest = bottom

    public ToastStackManager(ToastHostWindow host, ThemeManager theme, ILogger<ToastStackManager> logger)
    {
        _host = host;
        _theme = theme;
        _logger = logger;
        _theme.Changed += (_, _) => RefreshShadows();
    }

    /// <summary>Raised as a toast starts to show.</summary>
    public event EventHandler? Showing;

    /// <summary>Raised when the last toast has left the screen.</summary>
    public event EventHandler? Emptied;

    /// <summary>How long a toast waits before it leaves on its own and counts as missed.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(20);

    /// <summary>The bottom (newest) toast that is not leaving, if any.</summary>
    public ToastView? Newest => _active.FirstOrDefault(t => !t.Leaving)?.View;

    /// <summary>
    /// Builds the view pool and renders it once off-screen: fonts, drawings, shadow caches and the
    /// animation code paths are all warm before the first real reminder.
    /// </summary>
    public async Task WarmUpAsync()
    {
        var stopwatch = Stopwatch.StartNew();
        for (var i = 0; i < PoolSize; i++)
        {
            var view = new ToastView();
            view.ApplyShadowTokens();
            _pool.Push(view);
        }

        var sample = new ToastContent(Core.Reminders.ReminderKind.Posture, new ReminderCopy("Sit tall for a sec", "Warming up."),
            "Every 30 min", AllowSkip: true, new ReminderCopy("Nice.", "Logged."));
        _host.PlaceOffscreen();
        foreach (var view in _pool)
        {
            view.Present(sample);
            view.ShowSettled();
            _host.Stack.Children.Add(view);
        }
        _host.Show();
        await Frames.NextAsync(2);

        // Exercise every animation path once (enter, resolve, shift, exit) while nobody can see it.
        var first = _pool.Peek();
        var motion = first.PlayEnterAsync();
        await first.PlayShiftAsync(-10);
        await motion;
        _ = first.PlayResolveAsync(ToastOutcome.Done);
        await Frames.NextAsync(12);
        await first.PlayExitAsync();

        // From here on the window stays shown, empty and fully transparent (so click-through) while
        // idle. Show() is never on the critical path of a toast's first frames.
        _host.Stack.Children.Clear();
        await Frames.NextAsync(1);
        _host.PlaceOnCursorMonitor();

        LogWarmedUp(stopwatch.ElapsedMilliseconds);
    }

    /// <summary>Shows a toast and completes with what the user did once it has fully left the screen.</summary>
    public async Task<ToastOutcome> ShowAsync(ToastContent content)
    {
        Showing?.Invoke(this, EventArgs.Empty);

        // A third toast pushes out the oldest, which counts as missed.
        var visible = _active.Where(t => !t.Leaving).ToList();
        if (visible.Count >= ToastLayout.MaxVisible) visible[^1].Resolve(null);

        var view = _pool.Count > 0 ? _pool.Pop() : CreateView();
        view.Present(content);
        view.FadeShadowBelow = true;

        if (_active.Count == 0) _host.PlaceOnCursorMonitor();

        // Lay out and render the new card while it is still invisible (opacity 0): text layout and
        // the shadow cache cost a frame or two, and they must not land on the first frames of motion.
        // Measure through a real layout pass in the tree: a pooled view's own Measure() would
        // short-circuit and report the previous toast's height, since a changed TextBlock only
        // invalidates its ancestors during layout.
        Canvas.SetLeft(view, ToastLayout.Gutter);
        Canvas.SetTop(view, ToastLayout.WindowHeight);
        _host.Stack.Children.Add(view);
        _host.Stack.UpdateLayout();
        var height = view.ActualHeight;
        Canvas.SetTop(view, ToastLayout.WindowHeight - ToastLayout.EdgeInset - height);
        var toast = new ActiveToast(view, height, Timeout);
        await Frames.NextAsync(2);

        _active.Insert(0, toast);
        Reflow(newcomer: toast);
        _ = view.PlayEnterAsync();
        toast.StartTimer();
        LogShown(content.Kind.ToString(), height);

        var action = await toast.Action;
        var outcome = action switch
        {
            ToastAction.Done => ToastOutcome.Done,
            ToastAction.Snooze => ToastOutcome.Snoozed,
            ToastAction.Skip => ToastOutcome.Skipped,
            ToastAction.Close => ToastOutcome.Dismissed,
            _ => ToastOutcome.TimedOut,
        };

        if (outcome is ToastOutcome.Done or ToastOutcome.Snoozed)
            await view.PlayResolveAsync(outcome);

        toast.Leaving = true;
        await view.PlayExitAsync();

        _active.Remove(toast);
        _host.Stack.Children.Remove(view);
        toast.Detach();
        _pool.Push(view);
        LogLeft(content.Kind.ToString(), outcome);

        if (_active.Count > 0) Reflow(newcomer: null);
        else Emptied?.Invoke(this, EventArgs.Empty);

        return outcome;
    }

    // Bottom-up placement. Moved cards jump to their new Canvas.Top in the same frame as their
    // StackShift is set to the old offset, then slide to 0: one layout pass, transform-only frames.
    private void Reflow(ActiveToast? newcomer)
    {
        var bottom = ToastLayout.WindowHeight - ToastLayout.EdgeInset;
        foreach (var toast in _active)
        {
            var top = bottom - toast.Height;
            if (toast == newcomer || double.IsNaN(toast.Top))
            {
                Canvas.SetTop(toast.View, top);
            }
            else if (Math.Abs(toast.Top - top) > 0.5)
            {
                Canvas.SetTop(toast.View, top);
                _ = toast.View.PlayShiftAsync(toast.Top - top);
            }
            toast.Top = top;
            bottom = top - ToastLayout.StackGap;
        }
    }

    private ToastView CreateView()
    {
        var view = new ToastView();
        view.ApplyShadowTokens();
        return view;
    }

    private void RefreshShadows()
    {
        foreach (var view in _pool) view.ApplyShadowTokens();
        foreach (var toast in _active) toast.View.ApplyShadowTokens();
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Toast host warmed up in {ElapsedMs} ms")]
    private partial void LogWarmedUp(long elapsedMs);

    [LoggerMessage(Level = LogLevel.Information, Message = "Toast shown: {Kind} ({Height} DIP tall)")]
    private partial void LogShown(string kind, double height);

    [LoggerMessage(Level = LogLevel.Information, Message = "Toast left: {Kind} -> {Outcome}")]
    private partial void LogLeft(string kind, ToastOutcome outcome);

    /// <summary>A toast on screen: its slot in the stack, its pausable timeout and the pending user action.</summary>
    private sealed class ActiveToast
    {
        private readonly TaskCompletionSource<ToastAction?> _action = new();
        private readonly DispatcherTimer _timer = new(DispatcherPriority.Normal);
        private readonly Stopwatch _running = new();
        private TimeSpan _remaining;

        public ActiveToast(ToastView view, double height, TimeSpan timeout)
        {
            View = view;
            Height = height;
            _remaining = timeout;
            _timer.Tick += OnTimeout;
            View.ActionInvoked += OnAction;
            View.HoverChanged += OnHover;
        }

        public ToastView View { get; }

        public double Height { get; }

        public double Top { get; set; } = double.NaN;

        public bool Leaving { get; set; }

        /// <summary>The user's action, or null when the toast timed out or was pushed out.</summary>
        public Task<ToastAction?> Action => _action.Task;

        public void StartTimer()
        {
            _timer.Interval = _remaining;
            _timer.Start();
            _running.Restart();
        }

        public void Resolve(ToastAction? action)
        {
            _timer.Stop();
            _action.TrySetResult(action);
        }

        public void Detach()
        {
            _timer.Stop();
            _timer.Tick -= OnTimeout;
            View.ActionInvoked -= OnAction;
            View.HoverChanged -= OnHover;
        }

        private void OnAction(object? sender, ToastAction action) => Resolve(action);

        private void OnTimeout(object? sender, EventArgs e) => Resolve(null);

        // Hovering pauses the countdown; leaving resumes it with whatever time was left.
        private void OnHover(object? sender, bool hovering)
        {
            if (_action.Task.IsCompleted) return;
            if (hovering)
            {
                _timer.Stop();
                _remaining -= _running.Elapsed;
                if (_remaining < TimeSpan.FromSeconds(1)) _remaining = TimeSpan.FromSeconds(1);
            }
            else
            {
                StartTimer();
            }
        }
    }
}

/// <summary>Awaitable rendered frames.</summary>
internal static class Frames
{
    public static async Task NextAsync(int count)
    {
        for (var i = 0; i < count; i++)
        {
            var tcs = new TaskCompletionSource();
            void OnRendering(object? sender, EventArgs e)
            {
                CompositionTarget.Rendering -= OnRendering;
                tcs.TrySetResult();
            }
            CompositionTarget.Rendering += OnRendering;
            await tcs.Task;
        }
    }
}
