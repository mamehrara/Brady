#:package System.Drawing.Common@10.0.13
#pragma warning disable CA1416 // Windows-only by design: the installer is Windows-only.

// Generates the installer wizard's branding from design/ so it never drifts by hand:
//
//   dotnet run tools/installer-art.cs     writes build/installer/wizard-*.bmp
//
// The side panel (Welcome and Finish pages) is canopy with the stage's moss and bloom circles,
// the app icon and "Brady" set in Fredoka 600. The small header image is the app icon on white.
// Each comes in 100%, 125%, 150% and 200% sizes; Inno Setup picks the best one for the screen.

using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;

var root = FindRepoRoot();
var design = Path.Combine(root, "design");
var outDir = Path.Combine(root, "build", "installer");
Directory.CreateDirectory(outDir);

using var icon = Image.FromFile(Path.Combine(design, "icons", "brady-app-256.png"));
using var fonts = new PrivateFontCollection();
fonts.AddFontFile(Path.Combine(design, "fonts", "Fredoka-SemiBold.ttf"));

var canopy = ColorTranslator.FromHtml("#E8EEE4");
var ink = ColorTranslator.FromHtml("#1C2620");
var moss = ColorTranslator.FromHtml("#2F6E45");
var bloom = ColorTranslator.FromHtml("#A33D28");

foreach (var scale in new[] { 1.0, 1.25, 1.5, 2.0 })
{
    Write(Large((int)Math.Round(164 * scale), (int)Math.Round(314 * scale)), "large");
    var small = (int)Math.Round(55 * scale);
    Write(Small(small), "small");
}
Console.WriteLine("done");

Bitmap Large(int width, int height)
{
    var bitmap = new Bitmap(width, height, PixelFormat.Format24bppRgb);
    using var g = Graphics.FromImage(bitmap);
    Smooth(g);
    g.Clear(canopy);

    // The preview stage's circles: moss top-right, bloom bottom-left.
    using (var mossBrush = new SolidBrush(Color.FromArgb(115, moss)))
        g.FillEllipse(mossBrush, width * 0.45f, -height * 0.12f, width * 0.95f, width * 0.95f);
    using (var bloomBrush = new SolidBrush(Color.FromArgb(89, bloom)))
        g.FillEllipse(bloomBrush, -width * 0.35f, height * 0.78f, width * 0.8f, width * 0.8f);

    var iconSize = width * 0.56f;
    g.DrawImage(icon, (width - iconSize) / 2, height * 0.30f, iconSize, iconSize);

    using var font = new Font(fonts.Families[0], width * 0.16f, FontStyle.Regular, GraphicsUnit.Pixel);
    using var inkBrush = new SolidBrush(ink);
    var text = "Brady";
    var size = g.MeasureString(text, font);
    g.DrawString(text, font, inkBrush, (width - size.Width) / 2, height * 0.30f + iconSize + height * 0.03f);
    return bitmap;
}

Bitmap Small(int edge)
{
    var bitmap = new Bitmap(edge, edge, PixelFormat.Format24bppRgb);
    using var g = Graphics.FromImage(bitmap);
    Smooth(g);
    g.Clear(Color.White);
    g.DrawImage(icon, 0, 0, edge, edge);
    return bitmap;
}

void Write(Bitmap bitmap, string kind)
{
    using (bitmap)
    {
        var path = Path.Combine(outDir, $"wizard-{kind}-{bitmap.Width}x{bitmap.Height}.bmp");
        bitmap.Save(path, ImageFormat.Bmp);
        Console.WriteLine($"wrote {Path.GetRelativePath(root, path)}");
    }
}

static void Smooth(Graphics g)
{
    g.SmoothingMode = SmoothingMode.AntiAlias;
    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
    g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
}

static string FindRepoRoot()
{
    for (var dir = new DirectoryInfo(Directory.GetCurrentDirectory()); dir is not null; dir = dir.Parent)
        if (File.Exists(Path.Combine(dir.FullName, "Brady.slnx"))) return dir.FullName;
    throw new InvalidOperationException("Run from inside the brady-app repository.");
}
