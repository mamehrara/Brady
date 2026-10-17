using System.Windows;
using System.Windows.Media;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace Brady.App.Theme;

public enum ThemePreference
{
    System,
    Light,
    Dark,
}

/// <summary>
/// Owns the color slot in the application resources. Follows the Windows app theme and the
/// "Transparency effects" switch live, unless a Light/Dark preference overrides it.
/// All UI references colors through DynamicResource, so a swap is a single dictionary replace.
/// </summary>
public sealed partial class ThemeManager : IDisposable
{
    private const string PersonalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    private readonly ILogger<ThemeManager> _logger;
    private readonly ResourceDictionary _light = Load("Tokens.Light.xaml");
    private readonly ResourceDictionary _dark = Load("Tokens.Dark.xaml");
    private ResourceDictionary? _colorSlot;
    private ResourceDictionary? _solidGlass;
    private ThemePreference _preference = ThemePreference.System;
    private bool _started;

    public ThemeManager(ILogger<ThemeManager> logger) => _logger = logger;

    public event EventHandler? Changed;

    public bool IsDark { get; private set; }

    /// <summary>False when Windows "Transparency effects" is off: every glass fill becomes surface-solid.</summary>
    public bool TransparencyEnabled { get; private set; } = true;

    public ThemePreference Preference
    {
        get => _preference;
        set
        {
            if (_preference == value) return;
            _preference = value;
            Apply();
        }
    }

    /// <summary>Installs the color dictionary and starts listening for Windows theme changes.</summary>
    public void Start()
    {
        if (_started) return;
        _started = true;
        Apply();
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
    }

    public void Dispose()
    {
        if (_started) SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
    }

    private void OnUserPreferenceChanged(object? sender, UserPreferenceChangedEventArgs e)
    {
        // Theme and transparency flips arrive as General (registry broadcast); VisualStyle/Color cover the rest.
        if (e.Category is UserPreferenceCategory.General or UserPreferenceCategory.VisualStyle or UserPreferenceCategory.Color)
            Application.Current?.Dispatcher.BeginInvoke(Apply);
    }

    private void Apply()
    {
        var (systemLight, transparency) = ReadWindowsPersonalization();
        var dark = _preference switch
        {
            ThemePreference.Light => false,
            ThemePreference.Dark => true,
            _ => !systemLight,
        };

        var resources = Application.Current.Resources.MergedDictionaries;
        var tokens = dark ? _dark : _light;
        var changed = _colorSlot != tokens || TransparencyEnabled != transparency;
        if (!changed && _colorSlot is not null) return;

        ReplaceSlot(resources, ref _colorSlot, tokens);
        ReplaceSlot(resources, ref _solidGlass, transparency ? null : SolidGlassOverrides(tokens));

        IsDark = dark;
        TransparencyEnabled = transparency;
        LogThemeApplied(dark ? "dark" : "light", transparency);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    // The color slot sits first so later dictionaries (overrides) win on lookup.
    private static void ReplaceSlot(IList<ResourceDictionary> merged, ref ResourceDictionary? slot, ResourceDictionary? next)
    {
        if (slot is not null) merged.Remove(slot);
        slot = next;
        if (next is null) return;
        merged.Add(next);
    }

    private static ResourceDictionary SolidGlassOverrides(ResourceDictionary tokens)
    {
        var solid = (Color)tokens["SurfaceSolidColor"];
        var brush = new SolidColorBrush(solid);
        brush.Freeze();
        return new ResourceDictionary
        {
            ["GlassBrush"] = brush,
            ["GlassStrongBrush"] = brush,
            ["GlassColor"] = solid,
            ["GlassStrongColor"] = solid,
        };
    }

    private static (bool AppsUseLightTheme, bool Transparency) ReadWindowsPersonalization()
    {
        using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKey);
        var light = key?.GetValue("AppsUseLightTheme") is not int l || l != 0;
        var transparency = key?.GetValue("EnableTransparency") is not int t || t != 0;
        return (light, transparency);
    }

    private static ResourceDictionary Load(string name) =>
        new() { Source = new Uri($"pack://application:,,,/Brady;component/Theme/{name}", UriKind.Absolute) };

    [LoggerMessage(Level = LogLevel.Information, Message = "Theme applied: {Theme}, transparency {Transparency}")]
    private partial void LogThemeApplied(string theme, bool transparency);
}
