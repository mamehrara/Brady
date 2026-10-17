using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Brady.App.Platform;
using Brady.App.Theme;
using Brady.App.Toasts.Motion;
using Brady.Core.Reminders;

namespace Brady.App.Settings;

public sealed partial class SettingsWindow : Window
{
    private static readonly TimeSpan UndoShownFor = TimeSpan.FromSeconds(6);

    private readonly SettingsViewModel _viewModel;
    private readonly ReminderEngine _engine;
    private readonly ThemeManager _theme;
    private readonly DispatcherTimer _statusTimer = new(DispatcherPriority.Background) { Interval = TimeSpan.FromSeconds(30) };
    private readonly DispatcherTimer _undoTimer = new() { Interval = UndoShownFor };
    private Action? _undo;

    public SettingsWindow(SettingsViewModel viewModel, ReminderEngine engine, ThemeManager theme)
    {
        _viewModel = viewModel;
        _engine = engine;
        _theme = theme;
        InitializeComponent();
        DataContext = viewModel;

        _statusTimer.Tick += (_, _) => RefreshStatus();
        _undoTimer.Tick += (_, _) => HideUndo();
        _engine.Changed += OnEngineChanged;
        _theme.Changed += OnThemeChanged;
        _viewModel.UndoOffered += OnUndoOffered;

        SourceInitialized += (_, _) => TintTitleBar();
        Loaded += (_, _) => Dispatcher.BeginInvoke(Scroller.ScrollToTop, DispatcherPriority.Loaded);
        // A click focuses the button under the pointer, and WPF then scrolls it "into view", which
        // nudges the page under the user's mouse. Only keyboard navigation should scroll.
        Page.RequestBringIntoView += (_, e) =>
        {
            if (InputManager.Current.MostRecentInputDevice is MouseDevice) e.Handled = true;
        };
        Activated += (_, _) => Resources.Remove("GlassBrush");
        // BRAND.md: an unfocused window swaps its glass for surface-solid.
        Deactivated += (_, _) => Resources["GlassBrush"] = FindResource("SurfaceSolidBrush");
        Closed += (_, _) =>
        {
            _statusTimer.Stop();
            _undoTimer.Stop();
            _engine.Changed -= OnEngineChanged;
            _theme.Changed -= OnThemeChanged;
        };

        RefreshStatus();
        _statusTimer.Start();
    }

    private void OnEngineChanged(object? sender, EventArgs e) => Dispatcher.BeginInvoke(RefreshStatus, DispatcherPriority.Background);

    private void OnThemeChanged(object? sender, EventArgs e) => TintTitleBar();

    private void RefreshStatus() => _viewModel.UpdateStatus(_engine.Snapshot());

    // The title bar takes the canopy color and follows light/dark (Windows 11 22H2+).
    private void TintTitleBar()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == 0) return;
        NativeMethods.SetDwmInt(hwnd, NativeMethods.DWMWA_USE_IMMERSIVE_DARK_MODE, _theme.IsDark ? 1 : 0);
        NativeMethods.SetDwmInt(hwnd, NativeMethods.DWMWA_CAPTION_COLOR, ColorRef((Color)FindResource("CanopyColor")));
        NativeMethods.SetDwmInt(hwnd, NativeMethods.DWMWA_TEXT_COLOR, ColorRef((Color)FindResource("InkColor")));
    }

    private static int ColorRef(Color c) => c.R | (c.G << 8) | (c.B << 16);

    private void OnUndoOffered(object? sender, UndoOffer offer)
    {
        _undo = offer.Undo;
        UndoMessage.Text = offer.Message;
        UndoToast.IsHitTestVisible = true;
        var settle = MotionSpec.FullMotion ? MotionSpec.Hover : MotionSpec.ReducedFade;
        Anim.To(UndoToast, OpacityProperty, 1, settle, MotionSpec.Settle);
        if (MotionSpec.FullMotion) Anim.To(UndoRise, TranslateTransform.YProperty, 0, settle, MotionSpec.Settle, from: 8);
        _undoTimer.Stop();
        _undoTimer.Start();
    }

    private void OnUndoClick(object sender, RoutedEventArgs e)
    {
        _undo?.Invoke();
        HideUndo();
    }

    private void HideUndo()
    {
        _undoTimer.Stop();
        _undo = null;
        UndoToast.IsHitTestVisible = false;
        Anim.To(UndoToast, OpacityProperty, 0, MotionSpec.Exit, MotionSpec.Leave);
    }
}
