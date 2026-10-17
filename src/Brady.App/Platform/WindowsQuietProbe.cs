using Brady.Core.Quiet;

namespace Brady.App.Platform;

/// <summary>
/// Asks Windows whether now is a bad time for notifications (SHQueryUserNotificationState):
/// fullscreen apps and games, presentation mode, quiet time, or nobody there.
/// </summary>
/// <remarks>
/// Windows 11's Do not disturb / Focus is not reported here; only the documented states are.
/// </remarks>
public sealed class WindowsQuietProbe : IQuietProbe
{
    public QuietReason Probe()
    {
        if (NativeMethods.SHQueryUserNotificationState(out var state) != 0) return QuietReason.None;
        return state switch
        {
            NativeMethods.QUNS_BUSY or NativeMethods.QUNS_RUNNING_D3D_FULL_SCREEN or NativeMethods.QUNS_APP => QuietReason.FullScreen,
            NativeMethods.QUNS_PRESENTATION_MODE => QuietReason.Presentation,
            NativeMethods.QUNS_QUIET_TIME => QuietReason.WindowsQuietTime,
            NativeMethods.QUNS_NOT_PRESENT => QuietReason.NotPresent,
            _ => QuietReason.None,
        };
    }
}
