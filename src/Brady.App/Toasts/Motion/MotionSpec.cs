using System.Windows;
using System.Windows.Media.Animation;

namespace Brady.App.Toasts.Motion;

/// <summary>
/// Every duration, distance and curve the toast uses, in one place.
/// BRAND.md wins where it is specific (enter: 12px over 320ms); the motion spec fills in the rest.
/// </summary>
public static class MotionSpec
{
    /// <summary>cubic-bezier(0.22, 1, 0.36, 1): Brady's ease-out for everything that arrives or settles.</summary>
    public static IEasingFunction Settle { get; } = Frozen(new CubicBezierEase(0.22, 1, 0.36, 1));

    /// <summary>cubic-bezier(0.4, 0, 1, 1): a gentle accelerate for leaving.</summary>
    public static IEasingFunction Leave { get; } = Frozen(new CubicBezierEase(0.4, 0, 1, 1));

    /// <summary>CSS <c>ease-out</c>, used by the web reference's hover and press transitions.</summary>
    public static IEasingFunction CssEaseOut { get; } = Frozen(new CubicBezierEase(0, 0, 0.58, 1));

    public static IEasingFunction Spring { get; } = Frozen(new SpringEase());
    public static IEasingFunction Sway { get; } = Frozen(new DampedSineEase());

    // Card enter
    public static readonly TimeSpan CardEnter = Ms(320);
    public const double CardEnterRise = 12;
    public const double CardEnterScale = 0.96;

    // Brady enter: drop on a spring, then exactly one decaying sway around the claws, then still.
    public static readonly TimeSpan BradyDelay = Ms(120);
    public static readonly TimeSpan BradyDrop = Ms(420);
    public const double BradyDropFrom = -14;
    public static readonly TimeSpan BradyFadeIn = Ms(200);
    public static readonly TimeSpan BradySway = Ms(900);
    public const double BradySwayDegrees = 4;

    // Content enter: chip row, title, body and actions rise 6px in turn.
    public static readonly TimeSpan ContentDelay = Ms(80);
    public static readonly TimeSpan ContentStagger = Ms(40);
    public static readonly TimeSpan ContentEnter = Ms(240);
    public const double ContentRise = 6;

    // Exit (dismiss, timeout, after a confirmation)
    public static readonly TimeSpan Exit = Ms(220);
    public const double ExitDrop = 10;
    public const double ExitScale = 0.98;

    // Done / Snooze morph
    public static readonly TimeSpan MoodCrossFade = Ms(180);
    public static readonly TimeSpan TextCrossFade = Ms(160);
    public static readonly TimeSpan DoneHold = Ms(1600);
    public static readonly TimeSpan SnoozeHold = Ms(1200);

    // Stack and small interactions
    public static readonly TimeSpan Reflow = Ms(280);
    public static readonly TimeSpan Hover = Ms(160);
    public static readonly TimeSpan Press = Ms(120);

    /// <summary>When Windows "Animation effects" is off, every transition is this plain fade.</summary>
    public static readonly TimeSpan ReducedFade = Ms(150);

    /// <summary>
    /// False when Windows "Animation effects" is off (SPI_GETCLIENTAREAANIMATION).
    /// Read live, so flipping the setting applies from the next transition.
    /// </summary>
    public static bool FullMotion => SystemParameters.ClientAreaAnimation && !ForceReducedMotion;

    /// <summary>Set by <c>--reduced-motion</c> to preview the reduced path without changing Windows settings.</summary>
    public static bool ForceReducedMotion { get; set; }

    private static TimeSpan Ms(double ms) => TimeSpan.FromMilliseconds(ms);

    private static IEasingFunction Frozen(EasingFunctionBase ease)
    {
        ease.Freeze();
        return ease;
    }
}
