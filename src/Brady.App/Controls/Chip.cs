using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Brady.Core.Reminders;

namespace Brady.App.Controls;

/// <summary>
/// The small pill naming a reminder kind: icon plus word, in the kind's tone on its -soft tint.
/// The template lives in Theme/Controls.xaml; tones switch there by <see cref="Kind"/>.
/// </summary>
public sealed class Chip : Control
{
    public static readonly DependencyProperty KindProperty = DependencyProperty.Register(
        nameof(Kind), typeof(ReminderKind), typeof(Chip),
        new PropertyMetadata(ReminderKind.Posture, (d, _) => ((Chip)d).UpdateKind()));

    private static readonly DependencyPropertyKey GlyphPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(Glyph), typeof(Geometry), typeof(Chip), new PropertyMetadata(null));

    public static readonly DependencyProperty GlyphProperty = GlyphPropertyKey.DependencyProperty;

    private static readonly DependencyPropertyKey LabelPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(Label), typeof(string), typeof(Chip), new PropertyMetadata(""));

    public static readonly DependencyProperty LabelProperty = LabelPropertyKey.DependencyProperty;

    static Chip()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(Chip), new FrameworkPropertyMetadata(typeof(Chip)));
        FocusableProperty.OverrideMetadata(typeof(Chip), new FrameworkPropertyMetadata(false));
    }

    public Chip() => UpdateKind();

    public ReminderKind Kind
    {
        get => (ReminderKind)GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    public Geometry? Glyph => (Geometry?)GetValue(GlyphProperty);

    public string Label => (string)GetValue(LabelProperty);

    private void UpdateKind()
    {
        SetValue(GlyphPropertyKey, Application.Current?.TryFindResource($"Icon{Kind}") as Geometry);
        SetValue(LabelPropertyKey, Kind.Label());
    }
}
