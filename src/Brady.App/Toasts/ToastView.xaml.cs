using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using Brady.App.Controls;
using Brady.App.Toasts.Motion;
using Brady.Core.Copy;

namespace Brady.App.Toasts;

/// <summary>
/// One toast card and its choreography. The view is reused: <see cref="Present"/> resets it to the
/// pre-enter state, and every Play* method animates only Opacity and RenderTransform.
/// </summary>
public sealed partial class ToastView : UserControl
{
    private const double ShadowBleed = 64;

    private readonly TranslateTransform _headShift = new();
    private readonly TranslateTransform _titleShift = new();
    private readonly TranslateTransform _bodyShift = new();
    private readonly TranslateTransform _actionsShift = new();
    private MascotMood _mood;
    private bool _fadeShadowBelow;

    public ToastView()
    {
        InitializeComponent();
        Head.RenderTransform = _headShift;
        PromptTitle.RenderTransform = _titleShift;
        PromptBody.RenderTransform = _bodyShift;
        Actions.RenderTransform = _actionsShift;

        CardRoot.SizeChanged += (_, _) => UpdateShadowGeometry();
        CardRoot.MouseEnter += OnCardMouseEnter;
        CardRoot.MouseLeave += OnCardMouseLeave;
    }

    /// <summary>Raised when a button is clicked (or <see cref="Invoke"/> simulates it).</summary>
    public event EventHandler<ToastAction>? ActionInvoked;

    /// <summary>Raised when the pointer enters or leaves the card; the stack pauses the timeout meanwhile.</summary>
    public event EventHandler<bool>? HoverChanged;

    public ToastContent? Shown { get; private set; }

    /// <summary>
    /// Fades the shadow out over the 16px between the card and the bottom of the host window,
    /// so the window can end at the work area and never covers the taskbar.
    /// </summary>
    public bool FadeShadowBelow
    {
        get => _fadeShadowBelow;
        set
        {
            _fadeShadowBelow = value;
            UpdateShadowGeometry();
        }
    }

    /// <summary>Fills the card and resets it to the invisible pre-enter state.</summary>
    /// <param name="showActions">False renders the static confirmation card (no action row at all).</param>
    /// <param name="mood">Brady's starting mood; awake for every new reminder.</param>
    public void Present(ToastContent content, bool showActions = true, MascotMood mood = MascotMood.Awake)
    {
        Shown = content;
        KindChip.Kind = content.Kind;
        Meta.Text = content.Meta ?? "";
        SetCopy(PromptTitle, PromptBody, content.Copy);
        SetCopy(DoneTitle, DoneBody, content.DoneCopy);
        SetCopy(SnoozeTitle, SnoozeBody, CopyDeck.AfterSnooze(content.Snooze));
        SnoozeButton.Content = $"Snooze {content.Snooze.TotalMinutes:0} min";
        SkipButton.Visibility = content.AllowSkip ? Visibility.Visible : Visibility.Collapsed;
        Actions.Visibility = showActions ? Visibility.Visible : Visibility.Collapsed;
        SetActionsEnabled(true);
        SetMood(mood);

        Anim.Set(CardRoot, OpacityProperty, 0);
        Anim.Set(CardScale, ScaleTransform.ScaleXProperty, 1);
        Anim.Set(CardScale, ScaleTransform.ScaleYProperty, 1);
        Anim.Set(CardShift, TranslateTransform.YProperty, 0);
        Anim.Set(StackShift, TranslateTransform.YProperty, 0);
        Anim.Set(HoverShadowHost, OpacityProperty, 0);
        Anim.Set(Prompt, OpacityProperty, 1);
        Anim.Set(DoneReply, OpacityProperty, 0);
        Anim.Set(SnoozeReply, OpacityProperty, 0);
        Anim.Set(Actions, OpacityProperty, 1);
        Anim.Set(Brady, OpacityProperty, 1);
        Anim.Set(BradyDrop, TranslateTransform.YProperty, 0);
        Anim.Set(BradySway, RotateTransform.AngleProperty, 0);
        foreach (var (element, shift) in ContentBlocks())
        {
            Anim.Set(element, OpacityProperty, 1);
            Anim.Set(shift, TranslateTransform.YProperty, 0);
        }
    }

    /// <summary>Shows the card in its settled state with no motion (snapshots and warm-up).</summary>
    public void ShowSettled() => Anim.Set(CardRoot, OpacityProperty, 1);

    /// <summary>Card rises and fades in; Brady drops in on a spring and sways once; content follows in a stagger.</summary>
    public Task PlayEnterAsync()
    {
        if (!MotionSpec.FullMotion)
            return Anim.To(CardRoot, OpacityProperty, 1, MotionSpec.ReducedFade, from: 0);

        var settle = MotionSpec.Settle;
        var card = Task.WhenAll(
            Anim.To(CardRoot, OpacityProperty, 1, MotionSpec.CardEnter, settle, from: 0),
            Anim.To(CardShift, TranslateTransform.YProperty, 0, MotionSpec.CardEnter, settle, from: MotionSpec.CardEnterRise),
            Anim.To(CardScale, ScaleTransform.ScaleXProperty, 1, MotionSpec.CardEnter, settle, from: MotionSpec.CardEnterScale),
            Anim.To(CardScale, ScaleTransform.ScaleYProperty, 1, MotionSpec.CardEnter, settle, from: MotionSpec.CardEnterScale));

        var brady = BradyEnterAsync();

        var blocks = new List<Task>();
        var delay = MotionSpec.ContentDelay;
        foreach (var (element, shift) in ContentBlocks())
        {
            blocks.Add(Anim.To(element, OpacityProperty, 1, MotionSpec.ContentEnter, settle, delay, from: 0));
            blocks.Add(Anim.To(shift, TranslateTransform.YProperty, 0, MotionSpec.ContentEnter, settle, delay, from: MotionSpec.ContentRise));
            delay += MotionSpec.ContentStagger;
        }

        return Task.WhenAll(card, brady, Task.WhenAll(blocks));
    }

    /// <summary>
    /// Done or Snooze: Brady changes mood (cheer or sleepy), the text cross-fades to his reply and the
    /// actions fade away. Both replies were laid out in <see cref="Present"/>, so the card keeps its
    /// size and only opacity changes. Returns once the reply has been held.
    /// </summary>
    public async Task PlayResolveAsync(ToastOutcome outcome)
    {
        var (reply, mood, hold) = outcome == ToastOutcome.Snoozed
            ? (SnoozeReply, MascotMood.Sleepy, MotionSpec.SnoozeHold)
            : (DoneReply, MascotMood.Cheer, MotionSpec.DoneHold);
        SetActionsEnabled(false);

        var fade = MotionSpec.FullMotion ? MotionSpec.TextCrossFade : MotionSpec.ReducedFade;
        var moodFade = MotionSpec.FullMotion ? MotionSpec.MoodCrossFade : MotionSpec.ReducedFade;
        await Task.WhenAll(
            CrossFadeMoodAsync(mood, moodFade),
            Anim.To(Prompt, OpacityProperty, 0, fade, MotionSpec.Settle),
            Anim.To(reply, OpacityProperty, 1, fade, MotionSpec.Settle),
            Anim.To(Actions, OpacityProperty, 0, fade, MotionSpec.Settle));

        await Task.Delay(hold);
    }

    /// <summary>Fades out while sinking 10px and shrinking slightly.</summary>
    public Task PlayExitAsync()
    {
        SetActionsEnabled(false);
        if (!MotionSpec.FullMotion)
            return Anim.To(CardRoot, OpacityProperty, 0, MotionSpec.ReducedFade);

        var leave = MotionSpec.Leave;
        return Task.WhenAll(
            Anim.To(CardRoot, OpacityProperty, 0, MotionSpec.Exit, leave),
            Anim.To(CardShift, TranslateTransform.YProperty, MotionSpec.ExitDrop, MotionSpec.Exit, leave),
            Anim.To(CardScale, ScaleTransform.ScaleXProperty, MotionSpec.ExitScale, MotionSpec.Exit, leave),
            Anim.To(CardScale, ScaleTransform.ScaleYProperty, MotionSpec.ExitScale, MotionSpec.Exit, leave));
    }

    /// <summary>
    /// Second half of a FLIP move: the stack has already placed the card at its new position,
    /// this slides it there from where it visually was (<paramref name="fromOffset"/> above or below).
    /// </summary>
    public Task PlayShiftAsync(double fromOffset)
    {
        if (!MotionSpec.FullMotion || Math.Abs(fromOffset) < 0.5)
        {
            Anim.Set(StackShift, TranslateTransform.YProperty, 0);
            return Task.CompletedTask;
        }

        var current = StackShift.Y; // a shift may still be running; continue from where it is
        return Anim.To(StackShift, TranslateTransform.YProperty, 0, MotionSpec.Reflow, MotionSpec.Settle, from: current + fromOffset);
    }

    /// <summary>Simulates a click, through the same path as a real one (used by --demo).</summary>
    public void Invoke(ToastAction action)
    {
        if (DoneButton.IsEnabled) ActionInvoked?.Invoke(this, action);
    }

    /// <summary>Updates the shadow colors and sizes from the current theme's shadow-glass token.</summary>
    public void ApplyShadowTokens()
    {
        ConfigureShadow(FarShadow, "ShadowGlass1", 1);
        ConfigureShadow(NearShadow, "ShadowGlass2", 1);
        // Hover: the far layer, a little stronger and lower.
        ConfigureShadow(HoverShadow, "ShadowGlass1", 1.35, extraDepth: 4);
    }

    private Task BradyEnterAsync()
    {
        var start = MotionSpec.BradyDelay;
        var drop = Task.WhenAll(
            Anim.To(Brady, OpacityProperty, 1, MotionSpec.BradyFadeIn, MotionSpec.Settle, start, from: 0),
            Anim.To(BradyDrop, TranslateTransform.YProperty, 0, MotionSpec.BradyDrop, MotionSpec.Spring, start, from: MotionSpec.BradyDropFrom));
        // The sway curve swings around 0 and returns to it; its nominal To is only the amplitude,
        // so the end is pinned at 0 and Brady stays perfectly still afterwards.
        var sway = Anim.To(BradySway, RotateTransform.AngleProperty, MotionSpec.BradySwayDegrees, MotionSpec.BradySway,
            MotionSpec.Sway, start + MotionSpec.BradyDrop, from: 0, settleAt: 0);
        return Task.WhenAll(drop, sway);
    }

    private Task CrossFadeMoodAsync(MascotMood mood, TimeSpan duration)
    {
        if (mood == _mood) return Task.CompletedTask;
        _mood = mood;
        MascotBack.Source = MascotArt.Get(MascotPose.Hang, mood);
        return Task.WhenAll(
            Anim.To(MascotBack, OpacityProperty, 1, duration, MotionSpec.Settle, from: 0),
            Anim.To(MascotFront, OpacityProperty, 0, duration, MotionSpec.Settle))
            .ContinueWith(_ =>
            {
                // Swap roles so the visible drawing is always the front one.
                MascotFront.Source = MascotBack.Source;
                Anim.Set(MascotFront, OpacityProperty, 1);
                Anim.Set(MascotBack, OpacityProperty, 0);
            }, TaskScheduler.FromCurrentSynchronizationContext());
    }

    private void SetMood(MascotMood mood)
    {
        _mood = mood;
        MascotFront.Source = MascotArt.Get(MascotPose.Hang, mood);
        Anim.Set(MascotFront, OpacityProperty, 1);
        Anim.Set(MascotBack, OpacityProperty, 0);
    }

    private static void SetCopy(TextBlock title, TextBlock body, ReminderCopy copy)
    {
        title.Text = copy.Title;
        body.Text = copy.Body;
        body.Visibility = string.IsNullOrEmpty(copy.Body) ? Visibility.Collapsed : Visibility.Visible;
    }

    private void SetActionsEnabled(bool enabled)
    {
        DoneButton.IsEnabled = SnoozeButton.IsEnabled = SkipButton.IsEnabled = enabled;
        CloseButton.IsEnabled = enabled;
        Actions.IsHitTestVisible = enabled;
    }

    private IEnumerable<(UIElement Element, TranslateTransform Shift)> ContentBlocks()
    {
        yield return (Head, _headShift);
        yield return (PromptTitle, _titleShift);
        yield return (PromptBody, _bodyShift);
        yield return (Actions, _actionsShift);
    }

    private void ConfigureShadow(DropShadowEffect effect, string token, double strength, double extraDepth = 0)
    {
        var color = (Color)FindResource($"{token}Color");
        effect.Color = Color.FromRgb(color.R, color.G, color.B);
        effect.Opacity = Math.Min(1, color.A / 255.0 * strength);
        effect.ShadowDepth = (double)FindResource($"{token}OffsetY") + extraDepth;
        // CSS blur B is a Gaussian with sigma B/2; WPF's BlurRadius is about 3 sigma.
        effect.BlurRadius = (double)FindResource($"{token}Blur") * 1.5;
    }

    // Clip the shadow out from under the card (CSS draws outer shadows only outside the border box)
    // and optionally fade it over the 16px below the card. Runs on layout only, never per frame.
    private void UpdateShadowGeometry()
    {
        var width = CardRoot.ActualWidth;
        var height = CardRoot.ActualHeight;
        if (width <= 0 || height <= 0) return;

        var radius = (double)FindResource("RadiusLg");
        var outer = new RectangleGeometry(new Rect(0, 0, width + 2 * ShadowBleed, height + 2 * ShadowBleed));
        var card = new RectangleGeometry(new Rect(ShadowBleed, ShadowBleed, width, height), radius, radius);
        var clip = new CombinedGeometry(GeometryCombineMode.Exclude, outer, card);
        clip.Freeze();

        Brush? mask = null;
        if (_fadeShadowBelow)
        {
            var bottom = ShadowBleed + height;
            var total = height + 2 * ShadowBleed;
            var gradient = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(0, 1),
                GradientStops =
                {
                    new GradientStop(Colors.Black, bottom / total),
                    new GradientStop(Colors.Transparent, (bottom + ToastLayout.EdgeInset) / total),
                },
            };
            gradient.Freeze();
            mask = gradient;
        }

        foreach (var host in new[] { ShadowHost, HoverShadowHost })
        {
            host.Clip = clip;
            host.OpacityMask = mask;
        }
    }

    private void OnCardMouseEnter(object sender, MouseEventArgs e)
    {
        Anim.To(HoverShadowHost, OpacityProperty, 1, MotionSpec.Hover, MotionSpec.CssEaseOut);
        HoverChanged?.Invoke(this, true);
    }

    private void OnCardMouseLeave(object sender, MouseEventArgs e)
    {
        Anim.To(HoverShadowHost, OpacityProperty, 0, MotionSpec.Hover, MotionSpec.CssEaseOut);
        HoverChanged?.Invoke(this, false);
    }

    private void OnDoneClick(object sender, RoutedEventArgs e) => ActionInvoked?.Invoke(this, ToastAction.Done);

    private void OnSnoozeClick(object sender, RoutedEventArgs e) => ActionInvoked?.Invoke(this, ToastAction.Snooze);

    private void OnSkipClick(object sender, RoutedEventArgs e) => ActionInvoked?.Invoke(this, ToastAction.Skip);

    private void OnCloseClick(object sender, RoutedEventArgs e) => ActionInvoked?.Invoke(this, ToastAction.Close);
}
