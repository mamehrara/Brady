using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Brady.App.Toasts.Motion;

namespace Brady.App.Controls;

/// <summary>
/// An on/off settings row, ported from <c>.br-switch</c>: label and description on the left, the
/// track on the right, and the whole row is the hit target. Visuals live in Theme/Controls.xaml.
/// The resting state is set directly; only a toggle the user makes animates (thumb 220ms on
/// cubic-bezier(.3,.7,.2,1), colours 200ms ease-out), so opening Settings never plays animations.
/// </summary>
[TemplatePart(Name = "ThumbShift", Type = typeof(TranslateTransform))]
[TemplatePart(Name = "OnFill", Type = typeof(UIElement))]
[TemplatePart(Name = "OnThumb", Type = typeof(UIElement))]
public sealed class SwitchRow : ToggleButton
{
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(
        nameof(Label), typeof(string), typeof(SwitchRow), new PropertyMetadata(""));

    public static readonly DependencyProperty DescriptionProperty = DependencyProperty.Register(
        nameof(Description), typeof(string), typeof(SwitchRow), new PropertyMetadata(null));

    private const double ThumbTravel = 18;
    private static readonly System.Windows.Media.Animation.IEasingFunction ThumbEase = Frozen(new CubicBezierEase(0.3, 0.7, 0.2, 1));
    private static readonly TimeSpan ThumbDuration = TimeSpan.FromMilliseconds(220);
    private static readonly TimeSpan FillDuration = TimeSpan.FromMilliseconds(200);

    private TranslateTransform? _thumb;
    private UIElement? _onFill;
    private UIElement? _onThumb;

    static SwitchRow()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(SwitchRow), new FrameworkPropertyMetadata(typeof(SwitchRow)));
    }

    public string Label
    {
        get => (string)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <summary>The schedule in plain words, such as "Every 30 minutes while you type".</summary>
    public string? Description
    {
        get => (string?)GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _thumb = GetTemplateChild("ThumbShift") as TranslateTransform;
        _onFill = GetTemplateChild("OnFill") as UIElement;
        _onThumb = GetTemplateChild("OnThumb") as UIElement;
        ShowState(animate: false);
    }

    protected override void OnChecked(RoutedEventArgs e)
    {
        base.OnChecked(e);
        ShowState(animate: IsLoaded);
    }

    protected override void OnUnchecked(RoutedEventArgs e)
    {
        base.OnUnchecked(e);
        ShowState(animate: IsLoaded);
    }

    private void ShowState(bool animate)
    {
        if (_thumb is null || _onFill is null || _onThumb is null) return;
        var on = IsChecked == true;
        double x = on ? ThumbTravel : 0, opacity = on ? 1 : 0;

        if (animate && MotionSpec.FullMotion)
        {
            Anim.To(_thumb, TranslateTransform.XProperty, x, ThumbDuration, ThumbEase);
            Anim.To(_onFill, OpacityProperty, opacity, FillDuration, MotionSpec.CssEaseOut);
            Anim.To(_onThumb, OpacityProperty, opacity, FillDuration, MotionSpec.CssEaseOut);
            return;
        }
        Anim.Set(_thumb, TranslateTransform.XProperty, x);
        Anim.Set(_onFill, OpacityProperty, opacity);
        Anim.Set(_onThumb, OpacityProperty, opacity);
    }

    private static System.Windows.Media.Animation.IEasingFunction Frozen(CubicBezierEase ease)
    {
        ease.Freeze();
        return ease;
    }
}
