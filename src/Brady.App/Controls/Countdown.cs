using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using Brady.App.Toasts.Motion;
using Brady.Core.Reminders;

namespace Brady.App.Controls;

/// <summary>
/// The guided-break ring, ported from <c>.br-ring</c>: a 96px ring with a 7px stroke in the kind's
/// tone over its -soft track, the time left in <c>numeral</c> digits, and a one-line label below.
/// The ring empties in one linear animation over the whole break (no per-second steps); only the
/// digits change once a second, drawn with fixed-width digits so they never jitter.
/// </summary>
public sealed class Countdown : UserControl
{
    private const double Size = 96;
    private const double Stroke = 7;
    private const double Radius = 42; // stroke centre, as in the web reference's SVG

    private readonly Ellipse _track = Ring();
    private readonly Ellipse _progress = Ring();
    private readonly TabularText _digits = new() { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _label = new() { HorizontalAlignment = HorizontalAlignment.Center, TextWrapping = TextWrapping.NoWrap };
    private readonly DispatcherTimer _tick = new(DispatcherPriority.Render);
    private DateTime _endsAt;
    private TaskCompletionSource? _finished;

    public Countdown()
    {
        // WPF dashes are measured in stroke widths; one dash the length of the ring, one gap the same.
        var circumference = 2 * Math.PI * Radius / Stroke;
        _progress.StrokeDashArray = [circumference, circumference];
        _progress.StrokeDashCap = PenLineCap.Round;
        // Start at 12 o'clock: rotate around the ellipse's own centre.
        _progress.RenderTransform = new RotateTransform(-90, Radius + Stroke / 2, Radius + Stroke / 2);

        var ring = new Grid { Width = Size, Height = Size };
        ring.Children.Add(_track);
        ring.Children.Add(_progress);
        ring.Children.Add(_digits);

        var stack = new StackPanel();
        stack.Children.Add(ring);
        _label.Margin = new Thickness(0, 7, 0, 1); // space-2 plus the caption baseline nudge
        stack.Children.Add(_label);
        Content = stack;

        _tick.Tick += (_, _) => ShowTimeLeft();
        Loaded += (_, _) => ApplyStyle();
        ApplyStyle();
    }

    /// <summary>Sets the tone (ring and track colors) and resets to a full ring showing <paramref name="length"/>.</summary>
    public void Prepare(ReminderKind kind, TimeSpan length, string label)
    {
        var tone = kind.ToneOf().ToString();
        _progress.SetResourceReference(Shape.StrokeProperty, $"{tone}Brush");
        _track.SetResourceReference(Shape.StrokeProperty, $"{tone}SoftBrush");
        _label.Text = label;
        Stop();
        Anim.Set(_progress, Shape.StrokeDashOffsetProperty, 0);
        _digits.Text = Format(length);
    }

    /// <summary>Shows a still frame with <paramref name="left"/> of <paramref name="length"/> remaining (for snapshots).</summary>
    public void ShowStill(TimeSpan left, TimeSpan length)
    {
        Stop();
        var empty = 2 * Math.PI * Radius / Stroke;
        Anim.Set(_progress, Shape.StrokeDashOffsetProperty, empty * (1 - left / length));
        _digits.Text = Format(left);
    }

    /// <summary>Runs the countdown; completes when the ring is empty (or immediately if stopped).</summary>
    public Task RunAsync(TimeSpan length)
    {
        Stop();
        _finished = new TaskCompletionSource();
        _endsAt = DateTime.UtcNow + length;
        ShowTimeLeft();

        var empty = 2 * Math.PI * Radius / Stroke;
        var ring = Anim.To(_progress, Shape.StrokeDashOffsetProperty, empty, length, ease: null, from: 0);
        _tick.Interval = TimeSpan.FromMilliseconds(250); // re-read the clock often, change the digits once a second
        _tick.Start();
        ring.ContinueWith(_ => Finish(), TaskScheduler.FromCurrentSynchronizationContext());
        return _finished.Task;
    }

    public void Stop()
    {
        _tick.Stop();
        _finished?.TrySetResult();
        _finished = null;
    }

    private void Finish()
    {
        _digits.Text = Format(TimeSpan.Zero);
        Stop();
    }

    private void ShowTimeLeft()
    {
        var left = _endsAt - DateTime.UtcNow;
        _digits.Text = Format(left < TimeSpan.Zero ? TimeSpan.Zero : left);
    }

    // Counts down and rounds up, so a 20 second break starts on 0:20 and reaches 0:00 at the end.
    private static string Format(TimeSpan left)
    {
        var seconds = (int)Math.Ceiling(left.TotalSeconds);
        return string.Create(CultureInfo.InvariantCulture, $"{seconds / 60}:{seconds % 60:00}");
    }

    private void ApplyStyle()
    {
        _digits.FontFamily = (FontFamily)Find("DisplayFontFamily");
        _digits.FontSize = (double)Find("NumeralFontSize");
        _digits.FontWeight = (FontWeight)Find("NumeralFontWeight");
        _digits.LineHeight = (double)Find("NumeralLineHeight");
        _digits.SetResourceReference(TabularText.ForegroundProperty, "InkBrush");
        _label.Style = (Style)Find("CaptionText");
    }

    private object Find(string key) => TryFindResource(key) ?? Application.Current.FindResource(key);

    private static Ellipse Ring() => new()
    {
        Width = 2 * Radius + Stroke,
        Height = 2 * Radius + Stroke,
        StrokeThickness = Stroke,
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
    };
}
