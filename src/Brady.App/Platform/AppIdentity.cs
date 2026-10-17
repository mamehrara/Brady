using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace Brady.App.Platform;

/// <summary>Who Brady is to Windows, shared with the installer (build/Brady.iss).</summary>
public static partial class AppIdentity
{
    /// <summary>Shown by Windows for Brady's taskbar entries and Start menu shortcut.</summary>
    public const string AppUserModelId = "mamehrara.Brady";

    /// <summary>The installer's AppId; Inno writes its per-user uninstall entry under it.</summary>
    public const string InstallerAppId = "{AEAB95B4-8658-4D0B-AF0A-5BEC8EDFFD65}";

    /// <summary>True when Brady was installed by its installer (rather than run from a build folder).</summary>
    public static bool IsInstalled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey($@"Software\Microsoft\Windows\CurrentVersion\Uninstall\{InstallerAppId}_is1");
            return key is not null;
        }
    }

    public static void Apply() => SetCurrentProcessExplicitAppUserModelID(AppUserModelId);

    [LibraryImport("shell32.dll", StringMarshalling = StringMarshalling.Utf16)]
    private static partial int SetCurrentProcessExplicitAppUserModelID(string appId);
}
