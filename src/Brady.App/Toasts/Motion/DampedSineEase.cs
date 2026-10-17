using System.Windows;
using System.Windows.Media.Animation;

namespace Brady.App.Toasts.Motion;

/// <summary>
/// A decaying swing that starts and ends at rest: <c>A·sin(2π·cycles·t)·e^(−decay·t)</c>, scaled so
/// the first peak reaches exactly 1. Animate From 0 To the amplitude and it comes back to 0.
/// With 1.5 cycles, sin is zero again at t = 1, so the animation ends without a jump.
/// </summary>
public sealed class DampedSineEase : EasingFunctionBase
{
    public static readonly DependencyProperty CyclesProperty =
        DependencyProperty.Register(nameof(Cycles), typeof(double), typeof(DampedSineEase), new PropertyMetadata(1.5));

    public static readonly DependencyProperty DecayProperty =
        DependencyProperty.Register(nameof(Decay), typeof(double), typeof(DampedSineEase), new PropertyMetadata(3.0));

    public DampedSineEase() => EasingMode = EasingMode.EaseIn;

    public double Cycles { get => (double)GetValue(CyclesProperty); set => SetValue(CyclesProperty, value); }

    public double Decay { get => (double)GetValue(DecayProperty); set => SetValue(DecayProperty, value); }

    protected override double EaseInCore(double normalizedTime)
    {
        if (normalizedTime <= 0 || normalizedTime >= 1) return 0;
        double cycles = Cycles, decay = Decay;
        var firstPeak = 1 / (4 * cycles);
        var normalize = Math.Exp(decay * firstPeak);
        return normalize * Math.Sin(2 * Math.PI * cycles * normalizedTime) * Math.Exp(-decay * normalizedTime);
    }

    protected override Freezable CreateInstanceCore() => new DampedSineEase();
}
