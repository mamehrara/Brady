using Brady.Core.Reminders;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace Brady.App.Platform;

/// <summary>
/// Forwards the Windows events the engine cares about: lock and unlock, sleep and wake, and clock
/// changes. Locking or sleeping means the user is away, so the sitting timers reset.
/// </summary>
public sealed partial class SystemEventsBridge(ReminderEngine engine, ILogger<SystemEventsBridge> logger) : IDisposable
{
    private bool _started;

    public void Start()
    {
        if (_started) return;
        _started = true;
        SystemEvents.SessionSwitch += OnSessionSwitch;
        SystemEvents.PowerModeChanged += OnPowerModeChanged;
        SystemEvents.TimeChanged += OnTimeChanged;
    }

    public void Dispose()
    {
        if (!_started) return;
        SystemEvents.SessionSwitch -= OnSessionSwitch;
        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        SystemEvents.TimeChanged -= OnTimeChanged;
    }

    private void OnSessionSwitch(object? sender, SessionSwitchEventArgs e)
    {
        LogEvent(e.Reason.ToString());
        switch (e.Reason)
        {
            case SessionSwitchReason.SessionLock or SessionSwitchReason.ConsoleDisconnect or SessionSwitchReason.RemoteDisconnect:
                engine.SessionLocked();
                break;
            case SessionSwitchReason.SessionUnlock or SessionSwitchReason.ConsoleConnect or SessionSwitchReason.RemoteConnect:
                engine.SessionUnlocked();
                break;
        }
    }

    private void OnPowerModeChanged(object? sender, PowerModeChangedEventArgs e)
    {
        LogEvent(e.Mode.ToString());
        if (e.Mode == PowerModes.Suspend) engine.Suspended();
        else if (e.Mode == PowerModes.Resume) engine.Resumed();
    }

    private void OnTimeChanged(object? sender, EventArgs e) => engine.Poke();

    [LoggerMessage(Level = LogLevel.Information, Message = "System event: {Event}")]
    private partial void LogEvent(string @event);
}
