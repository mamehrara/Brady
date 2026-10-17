using System.Drawing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Brady.App.Reminders;
using Brady.App.Settings;
using Brady.Core.Copy;
using Brady.Core.Reminders;
using H.NotifyIcon;
using Microsoft.Extensions.Logging;

namespace Brady.App.Tray;

internal enum TrayCommand
{
    Pause30,
    Pause60,
    PauseUntilTomorrow,
    Resume,
    Settings,
    Quit,
}

/// <summary>
/// Brady's notification-area icon: awake normally, sleepy while paused. Its tooltip says what's
/// next ("Brady · next: Eyes in 7 min") and refreshes only when that text would change. Left click
/// opens the flyout; right click opens the menu (pause, resume, settings, quit).
/// </summary>
public sealed partial class TrayController : IDisposable
{
    private readonly ReminderEngine _engine;
    private readonly ReminderPresenter _presenter;
    private readonly SettingsService _settings;
    private readonly TimeProvider _time;
    private readonly ILogger<TrayController> _logger;
    private readonly DispatcherTimer _tooltipTimer = new(DispatcherPriority.Background);
    private readonly Icon _awake = LoadIcon("tray-awake.ico");
    private readonly Icon _sleepy = LoadIcon("tray-sleepy.ico");
    private TaskbarIcon? _icon;
    private MenuItem? _resumeItem;
    private TrayFlyout? _flyout;
    private bool _showingSleepy;

    public TrayController(
        ReminderEngine engine,
        ReminderPresenter presenter,
        SettingsService settings,
        TimeProvider time,
        ILogger<TrayController> logger)
    {
        _engine = engine;
        _presenter = presenter;
        _settings = settings;
        _time = time;
        _logger = logger;
        _tooltipTimer.Tick += (_, _) => Refresh();
    }

    /// <summary>
    /// How long a closing menu popup needs to be torn down. Shutting WPF down while a popup window is
    /// still being destroyed can hang the dispatcher, leaving an invisible process behind.
    /// </summary>
    internal static readonly TimeSpan MenuCloseDelay = TimeSpan.FromMilliseconds(300);

    /// <summary>Quit was chosen from the menu.</summary>
    public event EventHandler? QuitRequested;

    /// <summary>Settings was chosen from the menu or the flyout.</summary>
    public event EventHandler? SettingsRequested;

    public void Start()
    {
        _icon = new TaskbarIcon
        {
            Icon = _awake,
            ToolTipText = "Brady",
            NoLeftClickDelay = true,
            ContextMenu = BuildMenu(),
        };
        _icon.TrayLeftMouseUp += (_, _) => ToggleFlyout();
        _icon.ForceCreate(enablesEfficiencyMode: false); // Efficiency Mode would throttle the toast animations

        var dispatcher = Dispatcher.CurrentDispatcher;
        _engine.Changed += (_, _) => dispatcher.BeginInvoke(Refresh, DispatcherPriority.Background);
        Refresh();
    }

    /// <summary>Opens the flyout (also used when Brady is launched a second time).</summary>
    public void ShowFlyout()
    {
        if (_flyout is not null)
        {
            _flyout.Activate();
            return;
        }

        _flyout = new TrayFlyout();
        _flyout.Update(_engine.Snapshot(), _time.LocalTimeZone);
        _flyout.TakeBreakRequested += (_, _) => _presenter.TakeBreakNow();
        _flyout.PauseRequested += (_, _) => PauseFor(TimeSpan.FromMinutes(30));
        _flyout.ResumeRequested += (_, _) => Resume();
        _flyout.SettingsRequested += (_, _) => SettingsRequested?.Invoke(this, EventArgs.Empty);
        _flyout.Closed += (_, _) => _flyout = null;
        _flyout.Open();
    }

    public void Dispose()
    {
        _tooltipTimer.Stop();
        _flyout?.Close();
        _icon?.Dispose();
        _awake.Dispose();
        _sleepy.Dispose();
    }

    private void ToggleFlyout()
    {
        if (_flyout is not null) _flyout.Close();
        else ShowFlyout();
    }

    private ContextMenu BuildMenu()
    {
        var menu = CreateMenu(command =>
        {
            switch (command)
            {
                case TrayCommand.Pause30: PauseFor(TimeSpan.FromMinutes(30)); break;
                case TrayCommand.Pause60: PauseFor(TimeSpan.FromHours(1)); break;
                case TrayCommand.PauseUntilTomorrow: PauseUntilTomorrow(); break;
                case TrayCommand.Resume: Resume(); break;
                case TrayCommand.Settings: SettingsRequested?.Invoke(this, EventArgs.Empty); break;
                case TrayCommand.Quit: QuitAfterMenuCloses(); break;
            }
        });
        _resumeItem = menu.Items.OfType<MenuItem>().First(i => Equals(i.Tag, TrayCommand.Resume));
        return menu;
    }

    /// <summary>The right-click menu. Static so <c>--snapshot</c> can render it too.</summary>
    internal static ContextMenu CreateMenu(Action<TrayCommand> run)
    {
        var menu = new ContextMenu { Style = (Style)Application.Current.FindResource("TrayMenu") };
        menu.Items.Add(Item("Pause for 30 minutes", TrayCommand.Pause30));
        menu.Items.Add(Item("Pause for 1 hour", TrayCommand.Pause60));
        menu.Items.Add(Item("Pause until tomorrow", TrayCommand.PauseUntilTomorrow));
        menu.Items.Add(Item("Resume", TrayCommand.Resume));
        menu.Items.Add(new Separator { Style = (Style)Application.Current.FindResource("TrayMenuSeparator") });
        menu.Items.Add(Item("Settings…", TrayCommand.Settings));
        menu.Items.Add(Item("Quit", TrayCommand.Quit));
        return menu;

        MenuItem Item(string header, TrayCommand command)
        {
            var item = new MenuItem { Header = header, Tag = command, Style = (Style)Application.Current.FindResource("TrayMenuItem") };
            item.Click += (_, _) => run(command);
            return item;
        }
    }

    private async void QuitAfterMenuCloses()
    {
        await Task.Delay(MenuCloseDelay);
        QuitRequested?.Invoke(this, EventArgs.Empty);
    }

    private void PauseFor(TimeSpan length) => Pause(_time.GetUtcNow() + length);

    // "Until tomorrow" means until the morning: the end of quiet hours (08:00 by default).
    private void PauseUntilTomorrow()
    {
        var zone = _time.LocalTimeZone;
        var localNow = TimeZoneInfo.ConvertTime(_time.GetUtcNow(), zone).DateTime;
        var morning = _engine.Rules.QuietHours with { Enabled = true };
        var end = morning.NextEnd(localNow);
        if (end - localNow < TimeSpan.FromHours(1)) end = end.AddDays(1);
        Pause(new DateTimeOffset(end, zone.GetUtcOffset(end)));
    }

    private void Pause(DateTimeOffset until)
    {
        _engine.Pause(until);
        _settings.Update(s => s.PausedUntil = until);
        LogPaused(until);
    }

    private void Resume()
    {
        _engine.Resume();
        _settings.Update(s => s.PausedUntil = null);
    }

    private void Refresh()
    {
        if (_icon is null) return;
        var snapshot = _engine.Snapshot();
        var zone = _time.LocalTimeZone;
        var paused = snapshot.PausedUntil is not null;

        _icon.ToolTipText = StatusText.Tooltip(snapshot, zone);
        if (paused != _showingSleepy)
        {
            _icon.Icon = paused ? _sleepy : _awake;
            _showingSleepy = paused;
        }
        if (_resumeItem is not null) _resumeItem.IsEnabled = paused;
        _flyout?.Update(snapshot, zone);

        // Sleep until the countdown text would change; no ticking in between.
        _tooltipTimer.Stop();
        _tooltipTimer.Interval = StatusText.NextChange(snapshot);
        _tooltipTimer.Start();
    }

    private static Icon LoadIcon(string file)
    {
        var uri = new Uri($"pack://application:,,,/Brady;component/Icons/{file}", UriKind.Absolute);
        using var stream = Application.GetResourceStream(uri)!.Stream;
        var edge = Platform.NativeMethods.GetSystemMetrics(Platform.NativeMethods.SM_CXSMICON);
        return new Icon(stream, edge, edge);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Paused until {Until}")]
    private partial void LogPaused(DateTimeOffset until);
}
