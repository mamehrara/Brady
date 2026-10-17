using System.Windows;
using System.Windows.Media.Animation;

namespace Brady.App.Toasts.Motion;

/// <summary>
/// An exact CSS <c>cubic-bezier(x1, y1, x2, y2)</c> timing function, so the motion spec's curves
/// carry over 1:1 from the web reference.
/// </summary>
public sealed class CubicBezierEase : EasingFunctionBase
{
    public static readonly DependencyProperty X1Property = Register(nameof(X1));
    public static readonly DependencyProperty Y1Property = Register(nameof(Y1));
    public static readonly DependencyProperty X2Property = Register(nameof(X2));
    public static readonly DependencyProperty Y2Property = Register(nameof(Y2));

    public CubicBezierEase()
    {
        // EaseIn mode makes Ease(t) return EaseInCore(t) untouched.
        EasingMode = EasingMode.EaseIn;
    }

    public CubicBezierEase(double x1, double y1, double x2, double y2) : this()
    {
        X1 = x1;
        Y1 = y1;
        X2 = x2;
        Y2 = y2;
    }

    public double X1 { get => (double)GetValue(X1Property); set => SetValue(X1Property, value); }
    public double Y1 { get => (double)GetValue(Y1Property); set => SetValue(Y1Property, value); }
    public double X2 { get => (double)GetValue(X2Property); set => SetValue(X2Property, value); }
    public double Y2 { get => (double)GetValue(Y2Property); set => SetValue(Y2Property, value); }

    protected override double EaseInCore(double normalizedTime)
    {
        if (normalizedTime <= 0) return 0;
        if (normalizedTime >= 1) return 1;
        double x1 = X1, y1 = Y1, x2 = X2, y2 = Y2;
        var s = SolveForX(normalizedTime, x1, x2);
        return Bezier(s, y1, y2);
    }

    protected override Freezable CreateInstanceCore() => new CubicBezierEase();

    private static double Bezier(double s, double p1, double p2)
    {
        var inv = 1 - s;
        return 3 * inv * inv * s * p1 + 3 * inv * s * s * p2 + s * s * s;
    }

    private static double BezierSlope(double s, double p1, double p2)
    {
        var inv = 1 - s;
        return 3 * inv * inv * p1 + 6 * inv * s * (p2 - p1) + 3 * s * s * (1 - p2);
    }

    // Newton-Raphson first (converges in a few steps for UI curves), bisection as the fallback.
    private static double SolveForX(double x, double x1, double x2)
    {
        var s = x;
        for (var i = 0; i < 8; i++)
        {
            var error = Bezier(s, x1, x2) - x;
            if (Math.Abs(error) < 1e-6) return s;
            var slope = BezierSlope(s, x1, x2);
            if (Math.Abs(slope) < 1e-6) break;
            s -= error / slope;
        }

        double lo = 0, hi = 1;
        s = x;
        for (var i = 0; i < 32; i++)
        {
            var value = Bezier(s, x1, x2);
            if (Math.Abs(value - x) < 1e-6) break;
            if (value < x) lo = s; else hi = s;
            s = (lo + hi) / 2;
        }
        return s;
    }

    private static DependencyProperty Register(string name) =>
        DependencyProperty.Register(name, typeof(double), typeof(CubicBezierEase), new PropertyMetadata(0.0));
}
