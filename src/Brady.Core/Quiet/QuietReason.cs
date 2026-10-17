namespace Brady.Core.Quiet;

/// <summary>Why Brady is staying silent right now.</summary>
public enum QuietReason
{
    None,

    /// <summary>A fullscreen app or game (QUNS_BUSY, QUNS_RUNNING_D3D_FULL_SCREEN, QUNS_APP).</summary>
    FullScreen,

    /// <summary>Windows presentation mode (QUNS_PRESENTATION_MODE).</summary>
    Presentation,

    /// <summary>Windows quiet time (QUNS_QUIET_TIME).</summary>
    WindowsQuietTime,

    /// <summary>The user isn't there to see it (QUNS_NOT_PRESENT: locked, screen saver).</summary>
    NotPresent,

    /// <summary>Brady's own quiet hours (default 22:00 to 08:00).</summary>
    QuietHours,

    /// <summary>The user paused Brady from the tray.</summary>
    Paused,
}

/// <summary>Reports whether Windows says now is a bad time for notifications.</summary>
public interface IQuietProbe
{
    QuietReason Probe();
}
