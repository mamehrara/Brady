using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace Brady.App.Controls;

/// <summary>
/// One of Brady's outlined icons. It strokes a 24-unit geometry at 1.75 units with the inherited
/// Foreground, scaled to the element's size (14 in chips, 16 in buttons, 24 elsewhere), so icons
/// recolor with their text just like <c>currentColor</c> on the web.
/// </summary>
public sealed class Icon : FrameworkElement
{
    private const double GridSize = 24;
    private const double StrokeWidth = 1.75;

    public static readonly DependencyProperty GlyphProperty = DependencyProperty.Register(
        nameof(Glyph), typeof(Geometry), typeof(Icon),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ForegroundProperty = TextElement.ForegroundProperty.AddOwner(
        typeof(Icon),
        new FrameworkPropertyMetadata(
            Brushes.Black,
            FrameworkPropertyMetadataOptions.Inherits | FrameworkPropertyMetadataOptions.AffectsRender,
            (d, _) => ((Icon)d)._pen = null));

    private Pen? _pen;

    public Geometry? Glyph
    {
        get => (Geometry?)GetValue(GlyphProperty);
        set => SetValue(GlyphProperty, value);
    }

    public Brush Foreground
    {
        get => (Brush)GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    // 24 unless Width/Height say otherwise (the framework clamps to them). Not set in a constructor,
    // because a local value would override sizes set by templates.
    protected override Size MeasureOverride(Size availableSize) =>
        new(Math.Min(GridSize, availableSize.Width), Math.Min(GridSize, availableSize.Height));

    protected override void OnRender(DrawingContext drawingContext)
    {
        if (Glyph is null || ActualWidth <= 0) return;

        _pen ??= CreatePen(Foreground);
        var scale = ActualWidth / GridSize;
        drawingContext.PushTransform(new ScaleTransform(scale, scale));
        drawingContext.DrawGeometry(null, _pen, Glyph);
        drawingContext.Pop();
    }

    private static Pen CreatePen(Brush brush)
    {
        var pen = new Pen(brush, StrokeWidth)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
            LineJoin = PenLineJoin.Round,
        };
        if (pen.CanFreeze) pen.Freeze();
        return pen;
    }
}
