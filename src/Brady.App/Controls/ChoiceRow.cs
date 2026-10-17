using System.Collections;
using System.Windows;
using System.Windows.Controls;

namespace Brady.App.Controls;

/// <summary>One option of a <see cref="ChoiceRow"/>.</summary>
public sealed record Choice(object Value, string Label);

/// <summary>
/// A segmented row of small glass buttons; the selected one is primary. Chips are never clickable
/// in Brady's design system, so choices are always buttons.
/// </summary>
public sealed class ChoiceRow : UserControl
{
    public static readonly DependencyProperty ChoicesProperty = DependencyProperty.Register(
        nameof(Choices), typeof(IEnumerable), typeof(ChoiceRow),
        new PropertyMetadata(null, (d, _) => ((ChoiceRow)d).Rebuild()));

    public static readonly DependencyProperty SelectedValueProperty = DependencyProperty.Register(
        nameof(SelectedValue), typeof(object), typeof(ChoiceRow),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, _) => ((ChoiceRow)d).UpdateSelection()));

    private readonly WrapPanel _panel = new();

    public ChoiceRow()
    {
        Content = _panel;
        Focusable = false;
    }

    public IEnumerable? Choices
    {
        get => (IEnumerable?)GetValue(ChoicesProperty);
        set => SetValue(ChoicesProperty, value);
    }

    public object? SelectedValue
    {
        get => GetValue(SelectedValueProperty);
        set => SetValue(SelectedValueProperty, value);
    }

    private void Rebuild()
    {
        _panel.Children.Clear();
        foreach (var choice in Choices?.OfType<Choice>() ?? [])
        {
            var button = new BradyButton
            {
                Content = choice.Label,
                Tag = choice.Value,
                Size = ButtonSize.Sm,
                Margin = new Thickness(0, 0, 8, 8),
            };
            button.Click += (_, _) => SelectedValue = choice.Value;
            _panel.Children.Add(button);
        }
        UpdateSelection();
    }

    private void UpdateSelection()
    {
        foreach (var button in _panel.Children.OfType<BradyButton>())
            button.Variant = Equals(button.Tag, SelectedValue) ? ButtonVariant.Primary : ButtonVariant.Glass;
    }
}
