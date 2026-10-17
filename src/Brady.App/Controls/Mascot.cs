using System.Windows;
using System.Windows.Media;

namespace Brady.App.Controls;

public enum MascotPose
{
    /// <summary>Arms up, claws hooked over a top edge. Toasts only.</summary>
    Hang,

    /// <summary>The head mark: tray, app avatar, Settings header.</summary>
    Head,
}

public enum MascotMood
{
    Awake,
    Cheer,
    Sleepy,
}

/// <summary>Looks up Brady's frozen drawings from Theme/Mascot.xaml.</summary>
public static class MascotArt
{
    public static ImageSource Get(MascotPose pose, MascotMood mood) =>
        (ImageSource)Application.Current.FindResource($"Mascot{pose}{mood}");
}
