using System.Windows;
using System.Windows.Media.Animation;

namespace Brady.App.Toasts.Motion;

/// <summary>
/// A thin awaitable wrapper over <see cref="IAnimatable.BeginAnimation(DependencyProperty, AnimationTimeline)"/>.
/// When an animation completes, its end value becomes the property's base value and the clock is
/// released, so finished animations hold nothing and cost nothing.
/// </summary>
internal static class Anim
{
    private static readonly Dictionary<(DependencyObject, DependencyProperty), TaskCompletionSource> Pending = [];

    /// <summary>Animates <paramref name="property"/> to <paramref name="to"/>; completes when done or replaced.</summary>
    /// <param name="from">Start value, pinned immediately so a delayed animation shows it while waiting.</param>
    /// <param name="settleAt">Resting value when the curve doesn't end on <paramref name="to"/> (oscillating eases).</param>
    public static Task To(
        DependencyObject target,
        DependencyProperty property,
        double to,
        TimeSpan duration,
        IEasingFunction? ease = null,
        TimeSpan delay = default,
        double? from = null,
        double? settleAt = null)
    {
        var key = (target, property);
        // A new animation on the same property replaces the old one, and the old awaiter completes now.
        if (Pending.Remove(key, out var replaced)) replaced.TrySetResult();

        var animatable = (IAnimatable)target;
        if (from is { } start)
        {
            // Pin the start value so a delayed animation shows it during its BeginTime.
            animatable.BeginAnimation(property, null);
            target.SetValue(property, start);
        }

        var done = new TaskCompletionSource();
        var animation = new DoubleAnimation
        {
            To = to,
            Duration = duration,
            BeginTime = delay,
            EasingFunction = ease,
            FillBehavior = FillBehavior.HoldEnd,
        };
        animation.Completed += (_, _) =>
        {
            if (!Pending.TryGetValue(key, out var current) || current != done) return;
            Pending.Remove(key);
            target.SetValue(property, settleAt ?? to);
            animatable.BeginAnimation(property, null);
            done.TrySetResult();
        };

        Pending[key] = done;
        animatable.BeginAnimation(property, animation, HandoffBehavior.SnapshotAndReplace);
        return done.Task;
    }

    /// <summary>Stops any animation on the property and pins it to <paramref name="value"/>.</summary>
    public static void Set(DependencyObject target, DependencyProperty property, double value)
    {
        if (Pending.Remove((target, property), out var replaced)) replaced.TrySetResult();
        ((IAnimatable)target).BeginAnimation(property, null);
        target.SetValue(property, value);
    }
}
