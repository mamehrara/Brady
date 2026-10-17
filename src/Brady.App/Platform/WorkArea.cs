namespace Brady.App.Platform;

/// <summary>A monitor's work area (taskbar excluded) in physical pixels, plus its DPI scale.</summary>
internal readonly record struct WorkArea(int Left, int Top, int Right, int Bottom, double Scale)
{
    /// <summary>The work area of the monitor the mouse cursor is on.</summary>
    public static WorkArea UnderCursor()
    {
        NativeMethods.GetCursorPos(out var cursor);
        var monitor = NativeMethods.MonitorFromPoint(cursor, NativeMethods.MONITOR_DEFAULTTONEAREST);
        var info = new NativeMethods.MONITORINFO { cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.MONITORINFO>() };
        NativeMethods.GetMonitorInfo(monitor, ref info);
        var dpi = NativeMethods.GetDpiForMonitor(monitor, NativeMethods.MDT_EFFECTIVE_DPI, out var dpiX, out _) == 0 ? dpiX : 96u;
        var w = info.rcWork;
        return new WorkArea(w.Left, w.Top, w.Right, w.Bottom, dpi / 96.0);
    }
}
