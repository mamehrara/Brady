using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using Brady.App.Platform;

namespace Brady.App.Toasts;

/// <summary>Fixed geometry of the toast stack, in DIPs.</summary>
internal static class ToastLayout
{
    public const double CardWidth = 368;

    /// <summary>Room left of and above the cards for the shadow and Brady's overhang.</summary>
    public const double Gutter = 44;

    /// <summary>Space between a card and the work area's right and bottom edges (16px above the taskbar).</summary>
    public const double EdgeInset = 16;

    /// <summary>space-3 between stacked toasts.</summary>
    public const double StackGap = 12;

    /// <summary>Tallest card the window reserves room for (a two-line title and body plus actions fit well inside).</summary>
    public const double MaxCardHeight = 240;

    public const int MaxVisible = 2;

    public static double WindowWidth => Gutter + CardWidth + EdgeInset;

    public static double WindowHeight => Gutter + MaxVisible * MaxCardHeight + StackGap + EdgeInset;
}

/// <summary>
/// The one long-lived window every toast lives in. It is created once at startup, never moves or
/// resizes while anything animates (only the cards inside it do), never activates, never shows in
/// Alt-Tab. When the stack is empty it is fully transparent: a static layered window costs no
/// rendering, and its transparent pixels are click-through because it is per-pixel-alpha layered.
/// </summary>
public sealed class ToastHostWindow : Window
{
    public ToastHostWindow()
    {
        Title = "Brady";
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        ShowActivated = false;
        Topmost = true;
        Focusable = false;
        UseLayoutRounding = true;
        SizeToContent = SizeToContent.Manual;
        Width = ToastLayout.WindowWidth;
        Height = ToastLayout.WindowHeight;
        WindowStartupLocation = WindowStartupLocation.Manual;
        Left = -32000;
        Top = -32000;

        Stack = new Canvas { Width = ToastLayout.WindowWidth, Height = ToastLayout.WindowHeight, ClipToBounds = false };
        Content = Stack;

        SourceInitialized += (_, _) => WindowStyles.MakeNonActivatingToolWindow(this);
        new WindowInteropHelper(this).EnsureHandle();
    }

    /// <summary>The surface the cards are placed on, in window DIPs.</summary>
    public Canvas Stack { get; }

    /// <summary>
    /// Anchors the window to the bottom-right of the work area of the monitor under the cursor.
    /// Called only while the stack is empty, so nothing on screen ever jumps.
    /// </summary>
    public void PlaceOnCursorMonitor()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var area = WorkArea.UnderCursor();
            var width = (int)Math.Ceiling(ToastLayout.WindowWidth * area.Scale);
            var height = (int)Math.Ceiling(ToastLayout.WindowHeight * area.Scale);
            var before = VisualTreeHelper.GetDpi(this).DpiScaleX;
            NativeMethods.SetWindowPos(hwnd, NativeMethods.HWND_TOPMOST, area.Right - width, area.Bottom - height, width, height,
                NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_NOOWNERZORDER);

            // Moving onto a monitor with another DPI makes WPF rescale the window; place it once more.
            if (Math.Abs(VisualTreeHelper.GetDpi(this).DpiScaleX - before) < 0.001) break;
        }
    }

    /// <summary>Moves the window far off-screen (used while warming up).</summary>
    public void PlaceOffscreen()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        NativeMethods.SetWindowPos(hwnd, NativeMethods.HWND_TOPMOST, -32000, -32000, 0, 0,
            NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_NOOWNERZORDER | NativeMethods.SWP_NOSIZE);
    }
}
