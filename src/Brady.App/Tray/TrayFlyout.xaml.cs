using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Brady.App.Controls;
using Brady.App.Platform;
using Brady.App.Toasts.Motion;
using Brady.Core.Copy;
using Brady.Core.Reminders;

namespace Brady.App.Tray;

/// <summary>One row of "Coming up".</summary>
public sealed record UpcomingRow(ReminderKind Kind, string When);

public sealed partial class TrayFlyout : Window
{
    /// <summary>Gap between the panel and the work area's edges.</summary>
    private const double Inset = 12;

    private bool _paused;
    private bool _closing;

    public TrayFlyout()
    {
        InitializeComponent();
        SourceInitialized += (_, _) => WindowStyles.MakeToolWindow(this);
        Deactivated += (_, _) => CloseQuietly();
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) CloseQuietly();
        };
    }

    public event EventHandler? TakeBreakRequested;

    public event EventHandler? PauseRequested;

    public event EventHandler? ResumeRequested;

    public event EventHandler? SettingsRequested;

    public void Update(EngineSnapshot snapshot, TimeZoneInfo zone)
    {
        _paused = snapshot.PausedUntil is not null;
        Head.Source = MascotArt.Get(MascotPose.Head, _paused ? MascotMood.Sleepy : MascotMood.Awake);
        var headline = StatusText.Headline(snapshot, zone);
        Status.Text = char.ToUpperInvariant(headline[0]) + headline[1..];

        var rows = snapshot.Upcoming
            .Select(u => new UpcomingRow(u.Kind, u.DueAt is { } due ? "in " + StatusText.Duration(due - snapshot.Now) : "later"))
            .ToList();
        Upcoming.ItemsSource = rows;
        Empty.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        BreakButton.IsEnabled = rows.Count > 0;
        PauseButton.Content = _paused ? "Resume" : "Pause 30 min";
    }

    /// <summary>Opens in the bottom-right corner of the monitor under the cursor, above the taskbar.</summary>
    public void Open()
    {
        var area = WorkArea.UnderCursor();
        Root.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var size = Root.DesiredSize;
        var margin = Root.Margin;
        Left = area.Right / area.Scale - Inset - size.Width - margin.Left;
        Top = area.Bottom / area.Scale - Inset - size.Height - margin.Top;

        Show();
        Activate();

        if (MotionSpec.FullMotion)
        {
            Anim.To(Rise, TranslateTransform.YProperty, 0, MotionSpec.Hover, MotionSpec.Settle, from: 8);
            Anim.To(Root, OpacityProperty, 1, MotionSpec.Hover, MotionSpec.Settle, from: 0);
        }
        else
        {
            Anim.To(Root, OpacityProperty, 1, MotionSpec.ReducedFade, from: 0);
        }
    }

    private void CloseQuietly()
    {
        if (_closing) return;
        _closing = true;
        Close();
    }

    private void OnBreakClick(object sender, RoutedEventArgs e)
    {
        TakeBreakRequested?.Invoke(this, EventArgs.Empty);
        CloseQuietly();
    }

    private void OnSettingsClick(object sender, RoutedEventArgs e)
    {
        SettingsRequested?.Invoke(this, EventArgs.Empty);
        CloseQuietly();
    }

    private void OnPauseClick(object sender, RoutedEventArgs e)
    {
        (_paused ? ResumeRequested : PauseRequested)?.Invoke(this, EventArgs.Empty);
        CloseQuietly();
    }
}
