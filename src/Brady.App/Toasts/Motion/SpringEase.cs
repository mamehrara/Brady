using System.Windows;
using System.Windows.Media.Animation;

namespace Brady.App.Toasts.Motion;

/// <summary>
/// An underdamped spring settling on 1. The default damping overshoots by about 14%,
/// which is Brady's soft ~2px bounce at the end of a 14px drop.
/// </summary>
public sealed class SpringEase : EasingFunctionBase
{
    public static readonly DependencyProperty DampingRatioProperty =
        DependencyProperty.Register(nameof(DampingRatio), typeof(double), typeof(SpringEase), new PropertyMetadata(0.53));

    public static readonly DependencyProperty StiffnessProperty =
        DependencyProperty.Register(nameof(Stiffness), typeof(double), typeof(SpringEase), new PropertyMetadata(11.0));

    public SpringEase() => EasingMode = EasingMode.EaseIn;

    /// <summary>Zeta: 1 is critically damped; lower bounces more.</summary>
    public double DampingRatio { get => (double)GetValue(DampingRatioProperty); set => SetValue(DampingRatioProperty, value); }

    /// <summary>Natural frequency in radians per animation duration; high enough to settle by t = 1.</summary>
    public double Stiffness { get => (double)GetValue(StiffnessProperty); set => SetValue(StiffnessProperty, value); }

    protected override double EaseInCore(double normalizedTime)
    {
        if (normalizedTime >= 1) return 1;
        double zeta = DampingRatio, omega = Stiffness;
        var damped = omega * Math.Sqrt(1 - zeta * zeta);
        var envelope = Math.Exp(-zeta * omega * normalizedTime);
        return 1 - envelope * (Math.Cos(damped * normalizedTime) + zeta * omega / damped * Math.Sin(damped * normalizedTime));
    }

    protected override Freezable CreateInstanceCore() => new SpringEase();
}
