using System.Globalization;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace Brady.App.Controls;

/// <summary>
/// Text with fixed-width digits, for numbers that change in place (steppers, the countdown).
/// Fredoka has no tabular figures (its "1" is two-thirds the width of its "2"), so each digit is
/// drawn centred in a cell as wide as the widest digit and the number never jitters.
/// </summary>
public sealed class TabularText : FrameworkElement
{
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text), typeof(string), typeof(TabularText),
        new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty FontFamilyProperty = TextElement.FontFamilyProperty.AddOwner(typeof(TabularText),
        new FrameworkPropertyMetadata(SystemFonts.MessageFontFamily, FrameworkPropertyMetadataOptions.Inherits | FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty FontSizeProperty = TextElement.FontSizeProperty.AddOwner(typeof(TabularText),
        new FrameworkPropertyMetadata(14.0, FrameworkPropertyMetadataOptions.Inherits | FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty FontWeightProperty = TextElement.FontWeightProperty.AddOwner(typeof(TabularText),
        new FrameworkPropertyMetadata(FontWeights.Normal, FrameworkPropertyMetadataOptions.Inherits | FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ForegroundProperty = TextElement.ForegroundProperty.AddOwner(typeof(TabularText),
        new FrameworkPropertyMetadata(Brushes.Black, FrameworkPropertyMetadataOptions.Inherits | FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>The line box height, as in CSS line-height; glyphs are centred in it.</summary>
    public static readonly DependencyProperty LineHeightProperty = DependencyProperty.Register(
        nameof(LineHeight), typeof(double), typeof(TabularText),
        new FrameworkPropertyMetadata(double.NaN, FrameworkPropertyMetadataOptions.AffectsMeasure));

    public string Text { get => (string)GetValue(TextProperty); set => SetValue(TextProperty, value); }

    public FontFamily FontFamily { get => (FontFamily)GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }

    public double FontSize { get => (double)GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }

    public FontWeight FontWeight { get => (FontWeight)GetValue(FontWeightProperty); set => SetValue(FontWeightProperty, value); }

    public Brush Foreground { get => (Brush)GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }

    public double LineHeight { get => (double)GetValue(LineHeightProperty); set => SetValue(LineHeightProperty, value); }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = Text.Sum(CellWidth);
        var height = double.IsNaN(LineHeight) ? FontSize * 1.25 : LineHeight;
        return new Size(width, height);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        var pixelsPerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        var x = 0.0;
        foreach (var ch in Text)
        {
            var glyph = Format(ch.ToString(), pixelsPerDip);
            var cell = CellWidth(ch);
            var y = (RenderSize.Height - glyph.Height) / 2;
            drawingContext.DrawText(glyph, new Point(x + (cell - glyph.WidthIncludingTrailingWhitespace) / 2, y));
            x += cell;
        }
    }

    // Digits share the widest digit's width; everything else keeps its own.
    private double CellWidth(char ch)
    {
        var pixelsPerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        return char.IsAsciiDigit(ch)
            ? "0123456789".Max(d => Format(d.ToString(), pixelsPerDip).WidthIncludingTrailingWhitespace)
            : Format(ch.ToString(), pixelsPerDip).WidthIncludingTrailingWhitespace;
    }

    private FormattedText Format(string text, double pixelsPerDip) =>
        new(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            new Typeface(FontFamily, FontStyles.Normal, FontWeight, FontStretches.Normal), FontSize, Foreground, pixelsPerDip);
}
