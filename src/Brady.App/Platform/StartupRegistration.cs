using Microsoft.Win32;

namespace Brady.App.Platform;

/// <summary>Whether Brady starts when Windows does.</summary>
public interface IStartupRegistration
{
    bool IsEnabled { get; }

    void Enable();

    void Disable();
}

/// <summary>
/// The HKCU Run entry, the same mechanism the installer uses: value <c>Brady</c> =
/// <c>"…\Brady.exe" --background</c>. Settings always reads the real registry state, so the
/// installer and Settings can never disagree.
/// </summary>
public sealed class StartupRegistration : IStartupRegistration
{
    public const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    public const string ValueName = "Brady";

    public bool IsEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue(ValueName) is string { Length: > 0 };
        }
    }

    public void Enable()
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        key.SetValue(ValueName, $"\"{Environment.ProcessPath}\" --background");
    }

    public void Disable()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
        key?.DeleteValue(ValueName, throwOnMissingValue: false);
    }
}
