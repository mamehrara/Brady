using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Brady.App.Controls;

/// <summary>
/// A minus / value / plus control for whole numbers: minutes, or a time of day in minutes since
/// midnight when <see cref="Wrap"/> is on. The value is drawn with fixed-width digits.
/// </summary>
public sealed class Stepper : UserControl
{
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value), typeof(int), typeof(Stepper),
        new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, _) => ((Stepper)d).Refresh()));

    public static readonly DependencyProperty MinimumProperty = DependencyProperty.Register(
        nameof(Minimum), typeof(int), typeof(Stepper), new PropertyMetadata(0, (d, _) => ((Stepper)d).Refresh()));

    public static readonly DependencyProperty MaximumProperty = DependencyProperty.Register(
        nameof(Maximum), typeof(int), typeof(Stepper), new PropertyMetadata(100, (d, _) => ((Stepper)d).Refresh()));

    public static readonly DependencyProperty StepProperty = DependencyProperty.Register(
        nameof(Step), typeof(int), typeof(Stepper), new PropertyMetadata(1));

    public static readonly DependencyProperty WrapProperty = DependencyProperty.Register(
        nameof(Wrap), typeof(bool), typeof(Stepper), new PropertyMetadata(false, (d, _) => ((Stepper)d).Refresh()));

    /// <summary>"min" after a number, or nothing for times.</summary>
    public static readonly DependencyProperty UnitProperty = DependencyProperty.Register(
        nameof(Unit), typeof(string), typeof(Stepper), new PropertyMetadata("", (d, _) => ((Stepper)d).Refresh()));

    /// <summary>True for the large <c>numeral</c> style; false for a compact headline-sized value.</summary>
    public static readonly DependencyProperty LargeProperty = DependencyProperty.Register(
        nameof(Large), typeof(bool), typeof(Stepper), new PropertyMetadata(false, (d, _) => ((Stepper)d).ApplyStyle()));

    private readonly BradyButton _minus = new() { Content = "−", Size = ButtonSize.Sm, Width = 28, Padding = new Thickness(0) };
    private readonly BradyButton _plus = new() { Content = "+", Size = ButtonSize.Sm, Width = 28, Padding = new Thickness(0) };
    private readonly TabularText _value = new() { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 4, 0) };
    private readonly TextBlock _unit = new() { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) };

    public Stepper()
    {
        _minus.Click += (_, _) => Nudge(-1);
        _plus.Click += (_, _) => Nudge(+1);
        SetAccessibleNames();
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        row.Children.Add(_minus);
        row.Children.Add(_value);
        row.Children.Add(_unit);
        row.Children.Add(_plus);
        Content = row;
        Focusable = false;
        PreviewKeyDown += OnKeyDown;
        ApplyStyle();
        Refresh();
    }

    public int Value { get => (int)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    public int Minimum { get => (int)GetValue(MinimumProperty); set => SetValue(MinimumProperty, value); }

    public int Maximum { get => (int)GetValue(MaximumProperty); set => SetValue(MaximumProperty, value); }

    public int Step { get => (int)GetValue(StepProperty); set => SetValue(StepProperty, value); }

    /// <summary>Time-of-day mode: the value is minutes since midnight, shown as HH:mm, and wraps.</summary>
    public bool Wrap { get => (bool)GetValue(WrapProperty); set => SetValue(WrapProperty, value); }

    public string Unit { get => (string)GetValue(UnitProperty); set => SetValue(UnitProperty, value); }

    public bool Large { get => (bool)GetValue(LargeProperty); set => SetValue(LargeProperty, value); }

    private void Nudge(int direction)
    {
        var next = Value + direction * Step;
        if (Wrap)
        {
            var span = Maximum - Minimum + 1;
            next = ((next - Minimum) % span + span) % span + Minimum;
        }
        Value = Math.Clamp(next, Minimum, Maximum);
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Up or Key.Right) Nudge(+1);
        else if (e.Key is Key.Down or Key.Left) Nudge(-1);
        else return;
        e.Handled = true;
    }

    private void Refresh()
    {
        _value.Text = Wrap ? $"{Value / 60:00}:{Value % 60:00}" : Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        _unit.Text = Unit;
        _unit.Visibility = string.IsNullOrEmpty(Unit) ? Visibility.Collapsed : Visibility.Visible;
        _value.Margin = new Thickness(10, 0, string.IsNullOrEmpty(Unit) ? 10 : 4, 0);
        _minus.IsEnabled = Wrap || Value > Minimum;
        _plus.IsEnabled = Wrap || Value < Maximum;
    }

    private void ApplyStyle()
    {
        var display = (FontFamily)FindResourceOrDefault("DisplayFontFamily");
        var sans = (FontFamily)FindResourceOrDefault("SansFontFamily");
        if (Large)
        {
            _value.FontFamily = display;
            _value.FontSize = (double)FindResourceOrDefault("NumeralFontSize");
            _value.FontWeight = (FontWeight)FindResourceOrDefault("NumeralFontWeight");
            _value.LineHeight = (double)FindResourceOrDefault("NumeralLineHeight");
        }
        else
        {
            _value.FontFamily = sans;
            _value.FontSize = (double)FindResourceOrDefault("HeadlineFontSize");
            _value.FontWeight = (FontWeight)FindResourceOrDefault("HeadlineFontWeight");
            _value.LineHeight = (double)FindResourceOrDefault("HeadlineLineHeight");
        }
        _value.SetResourceReference(TabularText.ForegroundProperty, "InkBrush");
        _unit.Style = (Style)FindResourceOrDefault("CaptionText");
    }

    private object FindResourceOrDefault(string key) => TryFindResource(key) ?? Application.Current.FindResource(key);

    private void SetAccessibleNames()
    {
        System.Windows.Automation.AutomationProperties.SetName(_minus, "Less");
        System.Windows.Automation.AutomationProperties.SetName(_plus, "More");
    }
}
