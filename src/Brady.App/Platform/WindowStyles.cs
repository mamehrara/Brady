using System.Windows;
using System.Windows.Interop;

namespace Brady.App.Platform;

internal static class WindowStyles
{
    /// <summary>Keeps a popup-like window out of Alt-Tab and the taskbar (it may still take focus).</summary>
    public static void MakeToolWindow(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        var ex = NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE);
        NativeMethods.SetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE, (ex | NativeMethods.WS_EX_TOOLWINDOW) & ~NativeMethods.WS_EX_APPWINDOW);
    }

    /// <summary>
    /// Makes a window a non-activating tool window: it never takes focus (not even on click)
    /// and never appears in Alt-Tab or the taskbar.
    /// </summary>
    public static void MakeNonActivatingToolWindow(Window window)
    {
        // During SourceInitialized the visual isn't attached to its source yet, so go through the handle.
        var hwnd = new WindowInteropHelper(window).Handle;
        var source = HwndSource.FromHwnd(hwnd);
        var ex = NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE);
        ex = (ex | NativeMethods.WS_EX_NOACTIVATE | NativeMethods.WS_EX_TOOLWINDOW) & ~NativeMethods.WS_EX_APPWINDOW;
        NativeMethods.SetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE, ex);

        source.AddHook(static (nint _, int msg, nint _, nint _, ref bool handled) =>
        {
            if (msg != NativeMethods.WM_MOUSEACTIVATE) return 0;
            handled = true;
            return NativeMethods.MA_NOACTIVATE;
        });
    }
}
