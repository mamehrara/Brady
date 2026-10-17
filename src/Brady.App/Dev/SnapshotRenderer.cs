using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Brady.App.Controls;
using Brady.App.Theme;
using Brady.App.Toasts;
using Brady.Core.Copy;
using Brady.Core.Reminders;
using Microsoft.Extensions.Logging;

namespace Brady.App.Dev;

/// <summary>
/// <c>--snapshot &lt;dir&gt;</c>: lays out the same four toasts as design/reference-screens/Toast-*.png,
/// on the same stand-in desktop (canopy plus a moss and a bloom circle), and renders them in light
/// and dark at 1x so they can be compared pixel for pixel with the references.
/// </summary>
public sealed partial class SnapshotRenderer(ThemeManager theme, ILogger<SnapshotRenderer> logger)
{
    private const int Width = 900;
    private const int Height = 428;

    public async Task RenderAsync(string directory)
    {
        Directory.CreateDirectory(directory);
        foreach (var preference in new[] { ThemePreference.Light, ThemePreference.Dark })
        {
            theme.Preference = preference;
            var stage = BuildStage();
            stage.Measure(new Size(Width, Height));
            stage.Arrange(new Rect(0, 0, Width, Height));
            stage.UpdateLayout();
            await Task.Yield();

            var bitmap = new RenderTargetBitmap(Width, Height, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(stage);
            var path = System.IO.Path.Combine(directory, $"Toast-{preference.ToString().ToLowerInvariant()}.png");
            await using (var file = File.Create(path))
            {
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmap));
                encoder.Save(file);
            }
            LogRendered(path);
        }
    }

    private Grid BuildStage()
    {
        var canopy = (Brush)Application.Current.FindResource("CanopyBrush");
        var stage = new Grid { Width = Width, Height = Height, Background = canopy, ClipToBounds = true };
        var canvas = new Canvas();
        stage.Children.Add(canvas);

        // .br-stage::before / ::after
        AddCircle(canvas, "MossBrush", 200, left: Width - Width * 0.08 - 200, top: -60, opacity: 0.45);
        AddCircle(canvas, "BloomBrush", 160, left: Width * 0.18, top: Height + 70 - 160, opacity: 0.35);

        var posture = new ToastContent(ReminderKind.Posture, CopyDeck.LinesFor(ReminderKind.Posture)[0], "Every 30 min", true, CopyDeck.AfterDone(ReminderKind.Posture, TimeSpan.FromMinutes(30)));
        var eyes = new ToastContent(ReminderKind.Eyes, CopyDeck.LinesFor(ReminderKind.Eyes)[0], "20-20-20", false, CopyDeck.AfterDone(ReminderKind.Eyes, TimeSpan.FromMinutes(20)));
        var move = new ToastContent(ReminderKind.Move, CopyDeck.LinesFor(ReminderKind.Move)[0], "52 min still", false, CopyDeck.AfterDone(ReminderKind.Move, TimeSpan.FromMinutes(50)));
        var waterDone = CopyDeck.AfterDone(ReminderKind.Water, TimeSpan.FromMinutes(45));
        var water = new ToastContent(ReminderKind.Water, waterDone, null, false, waterDone);

        AddToast(canvas, posture, 24, 32);
        AddToast(canvas, eyes, 416, 32);
        AddToast(canvas, move, 24, 232);
        AddToast(canvas, water, 416, 232, showActions: false, MascotMood.Cheer);
        return stage;
    }

    private static void AddCircle(Canvas canvas, string brushKey, double size, double left, double top, double opacity)
    {
        var circle = new Ellipse { Width = size, Height = size, Fill = (Brush)Application.Current.FindResource(brushKey), Opacity = opacity };
        Canvas.SetLeft(circle, left);
        Canvas.SetTop(circle, top);
        canvas.Children.Add(circle);
    }

    private static void AddToast(Canvas canvas, ToastContent content, double left, double top, bool showActions = true, MascotMood mood = MascotMood.Awake)
    {
        var view = new ToastView();
        view.ApplyShadowTokens();
        view.Present(content, showActions, mood);
        view.ShowSettled();
        Canvas.SetLeft(view, left);
        Canvas.SetTop(view, top);
        canvas.Children.Add(view);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Snapshot written: {Path}")]
    private partial void LogRendered(string path);
}
