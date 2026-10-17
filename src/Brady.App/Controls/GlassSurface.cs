using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace Brady.App.Controls;

/// <summary>
/// A floating glass panel: glass-strong fill, 1px glass-edge, radius-lg and the shadow-glass
/// shadow. Like the toast, the shadow is rendered once into a bitmap cache and clipped out from
/// under the translucent fill, the way CSS box-shadow is. Used by the tray flyout.
/// </summary>
[ContentProperty(nameof(Child))]
public sealed class GlassSurface : UserControl
{
    public static readonly DependencyProperty ChildProperty = DependencyProperty.Register(
        nameof(Child), typeof(UIElement), typeof(GlassSurface),
        new PropertyMetadata(null, (d, e) => ((GlassSurface)d)._presenter.Content = e.NewValue));

    private const double Bleed = 64;

    private readonly Grid _shadowHost = new() { Margin = new Thickness(-Bleed), IsHitTestVisible = false };
    private readonly DropShadowEffect _far = new() { Direction = 270, RenderingBias = RenderingBias.Quality };
    private readonly DropShadowEffect _near = new() { Direction = 270, RenderingBias = RenderingBias.Quality };
    private readonly Border _fill = new();
    private readonly Border _edge = new() { BorderThickness = new Thickness(1), IsHitTestVisible = false };
    private readonly ContentPresenter _presenter = new();

    public GlassSurface()
    {
        // A bare template: UserControl's default one applies Padding to the whole surface, and here
        // Padding belongs inside the glass only (see the presenter's binding below).
        Template = new ControlTemplate(typeof(GlassSurface)) { VisualTree = new FrameworkElementFactory(typeof(ContentPresenter)) };

        _shadowHost.CacheMode = new BitmapCache { SnapsToDevicePixels = true };
        _shadowHost.Children.Add(new Border { Margin = new Thickness(Bleed), Background = Brushes.Black, Effect = _far });
        _shadowHost.Children.Add(new Border { Margin = new Thickness(Bleed), Background = Brushes.Black, Effect = _near });
        _fill.SetResourceReference(Border.BackgroundProperty, "GlassStrongBrush");
        _edge.SetResourceReference(Border.BorderBrushProperty, "GlassEdgeBrush");
        foreach (var border in _shadowHost.Children.OfType<Border>().Append(_fill).Append(_edge))
            border.SetResourceReference(Border.CornerRadiusProperty, "RadiusLgCorner");
        _presenter.SetBinding(MarginProperty, new System.Windows.Data.Binding(nameof(Padding)) { Source = this });

        var root = new Grid();
        root.Children.Add(_shadowHost);
        root.Children.Add(_fill);
        root.Children.Add(_presenter);
        root.Children.Add(_edge);
        Content = root;

        ApplyTheme();
        Loaded += (_, _) => ApplyTheme();
        SizeChanged += (_, _) => UpdateClip();
    }

    public UIElement? Child
    {
        get => (UIElement?)GetValue(ChildProperty);
        set => SetValue(ChildProperty, value);
    }

    /// <summary>Re-reads the shadow tokens (call after a theme change).</summary>
    public void ApplyTheme()
    {
        Configure(_far, "ShadowGlass1");
        Configure(_near, "ShadowGlass2");
        UpdateClip();
    }

    private void Configure(DropShadowEffect effect, string token)
    {
        var color = (Color)FindResource($"{token}Color");
        effect.Color = Color.FromRgb(color.R, color.G, color.B);
        effect.Opacity = color.A / 255.0;
        effect.ShadowDepth = (double)FindResource($"{token}OffsetY");
        // CSS blur B is a Gaussian with sigma B/2; WPF's BlurRadius is about 3 sigma.
        effect.BlurRadius = (double)FindResource($"{token}Blur") * 1.5;
    }

    private void UpdateClip()
    {
        if (ActualWidth <= 0 || ActualHeight <= 0) return;
        var radius = _fill.CornerRadius.TopLeft;
        var outer = new RectangleGeometry(new Rect(0, 0, ActualWidth + 2 * Bleed, ActualHeight + 2 * Bleed));
        var inner = new RectangleGeometry(new Rect(Bleed, Bleed, ActualWidth, ActualHeight), radius, radius);
        var clip = new CombinedGeometry(GeometryCombineMode.Exclude, outer, inner);
        clip.Freeze();
        _shadowHost.Clip = clip;
    }
}
