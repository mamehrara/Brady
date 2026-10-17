using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Brady.App.Controls;

public enum ButtonVariant
{
    /// <summary>The one thing to do now (moss fill). One per surface; in a toast it is Done.</summary>
    Primary,

    /// <summary>The alternative (glass fill, hairline border).</summary>
    Glass,

    /// <summary>The way out (no fill, muted text).</summary>
    Quiet,
}

public enum ButtonSize
{
    /// <summary>32px tall.</summary>
    Md,

    /// <summary>28px tall, used in toasts.</summary>
    Sm,
}

/// <summary>
/// Brady's button, ported from <c>.br-btn</c> in the web reference. Visuals live in Theme/Controls.xaml.
/// </summary>
public sealed class BradyButton : Button
{
    public static readonly DependencyProperty VariantProperty = DependencyProperty.Register(
        nameof(Variant), typeof(ButtonVariant), typeof(BradyButton), new PropertyMetadata(ButtonVariant.Glass));

    public static readonly DependencyProperty SizeProperty = DependencyProperty.Register(
        nameof(Size), typeof(ButtonSize), typeof(BradyButton), new PropertyMetadata(ButtonSize.Md));

    public static readonly DependencyProperty IconProperty = DependencyProperty.Register(
        nameof(Icon), typeof(Geometry), typeof(BradyButton), new PropertyMetadata(null));

    public static readonly DependencyProperty HoverBackgroundProperty = DependencyProperty.Register(
        nameof(HoverBackground), typeof(Brush), typeof(BradyButton), new PropertyMetadata(null));

    static BradyButton()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(BradyButton), new FrameworkPropertyMetadata(typeof(BradyButton)));
    }

    public ButtonVariant Variant
    {
        get => (ButtonVariant)GetValue(VariantProperty);
        set => SetValue(VariantProperty, value);
    }

    public ButtonSize Size
    {
        get => (ButtonSize)GetValue(SizeProperty);
        set => SetValue(SizeProperty, value);
    }

    public Geometry? Icon
    {
        get => (Geometry?)GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    /// <summary>Fill cross-faded in on hover (an opacity tween, so theme brushes never get animated).</summary>
    public Brush? HoverBackground
    {
        get => (Brush?)GetValue(HoverBackgroundProperty);
        set => SetValue(HoverBackgroundProperty, value);
    }
}
