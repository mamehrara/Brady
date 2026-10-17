// Converts Brady's SVG art into frozen WPF XAML once, so the app needs no runtime SVG library.
//
//   dotnet run tools/svg2xaml.cs            writes src/Brady.App/Theme/Mascot.xaml and Icons.xaml
//   dotnet run tools/svg2xaml.cs -- --check exits 1 if the committed files are stale
//
// Handles exactly the SVG subset the design package uses: rect (rx), path, circle, ellipse,
// g/ellipse transforms (translate, scale, rotate), fill/stroke/opacity and round caps/joins.
// Mascot and logo art become DrawingImages; icons become bare Geometries that the Icon
// control strokes with its Foreground so they recolor.
//
// Sources: design/assets/**.svg. A few drawings ship only inside web-reference/bundle.js
// (check, close, head-cheer), so those are read from its ART table.

using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;

var check = args.Contains("--check");
var root = FindRepoRoot();
var design = Path.Combine(root, "design");
var outDir = Path.Combine(root, "src", "Brady.App", "Theme");
var art = LoadBundleArt(Path.Combine(design, "web-reference", "bundle.js"));

string Asset(string rel) => File.ReadAllText(Path.Combine(design, "assets", rel));
string BundleIcon(string name) =>
    "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 24 24\" fill=\"none\" stroke=\"#1c2620\" stroke-width=\"1.75\" " +
    "stroke-linecap=\"round\" stroke-linejoin=\"round\">" + art.GetProperty("icons").GetProperty(name).GetString() + "</svg>";

var drawings = new (string Key, string Svg)[]
{
    ("MascotHangAwake", Asset("Mascot/brady-hang-awake.svg")),
    ("MascotHangCheer", Asset("Mascot/brady-hang-cheer.svg")),
    ("MascotHangSleepy", Asset("Mascot/brady-hang-sleepy.svg")),
    ("MascotHeadAwake", Asset("Logos/brady-mark.svg")),
    ("MascotHeadCheer", art.GetProperty("head").GetProperty("cheer").GetString()!),
    ("MascotHeadSleepy", Asset("Logos/brady-mark-sleepy.svg")),
    ("BradyAppIcon", Asset("Logos/brady-app-icon.svg")),
};

var icons = new (string Key, string Svg)[]
{
    ("IconPosture", Asset("Icons/posture.svg")),
    ("IconMove", Asset("Icons/move.svg")),
    ("IconEyes", Asset("Icons/eyes.svg")),
    ("IconWater", Asset("Icons/water.svg")),
    ("IconStretch", Asset("Icons/stretch.svg")),
    ("IconSnooze", Asset("Icons/snooze.svg")),
    ("IconCheck", BundleIcon("check")),
    ("IconClose", BundleIcon("close")),
};

var outputs = new Dictionary<string, string>
{
    ["Mascot.xaml"] = Wrap("Brady and the app mark as frozen DrawingImages (pose x mood)", drawings.Select(d => DrawingImage(d.Key, d.Svg))),
    ["Icons.xaml"] = Wrap("24px icon geometries; the Icon control strokes them at 1.75px with its Foreground", icons.Select(i => IconGeometry(i.Key, i.Svg))),
};

var stale = new List<string>();
foreach (var (name, content) in outputs)
{
    var path = Path.Combine(outDir, name);
    var existing = File.Exists(path) ? File.ReadAllText(path).Replace("\r\n", "\n", StringComparison.Ordinal) : null;
    if (existing == content.Replace("\r\n", "\n", StringComparison.Ordinal)) continue;
    if (check) { stale.Add(name); continue; }
    File.WriteAllText(path, content, new UTF8Encoding(false));
    Console.WriteLine($"wrote {Path.GetRelativePath(root, path)}");
}
if (stale.Count > 0)
{
    Console.Error.WriteLine($"Stale art files (run `dotnet run tools/svg2xaml.cs`): {string.Join(", ", stale)}");
    return 1;
}
Console.WriteLine(check ? "art up to date" : "done");
return 0;

// ---------------------------------------------------------------------------------------------

static string DrawingImage(string key, string svgText)
{
    var svg = XElement.Parse(svgText);
    var vb = Nums(svg.Attribute("viewBox")!.Value);
    var sb = new StringBuilder();
    sb.AppendLine($"    <DrawingImage x:Key=\"{key}\" po:Freeze=\"True\">");
    sb.AppendLine("        <DrawingImage.Drawing>");
    sb.AppendLine($"            <DrawingGroup ClipGeometry=\"M{F(vb[0])},{F(vb[1])} H{F(vb[0] + vb[2])} V{F(vb[1] + vb[3])} H{F(vb[0])} Z\">");
    // Transparent frame so the image's bounds are the viewBox, not the ink.
    sb.AppendLine($"                <GeometryDrawing Brush=\"#00000000\" Geometry=\"M{F(vb[0])},{F(vb[1])} H{F(vb[0] + vb[2])} V{F(vb[1] + vb[3])} H{F(vb[0])} Z\" />");
    foreach (var child in svg.Elements()) Emit(child, Style.From(svg, Style.Initial), sb, 4);
    sb.AppendLine("            </DrawingGroup>");
    sb.AppendLine("        </DrawingImage.Drawing>");
    sb.AppendLine("    </DrawingImage>");
    return sb.ToString();
}

static void Emit(XElement el, Style inherited, StringBuilder sb, int depth)
{
    var pad = new string(' ', depth * 4);
    var style = Style.From(el, inherited);
    var name = el.Name.LocalName;

    if (name == "g")
    {
        var transform = el.Attribute("transform")?.Value;
        sb.AppendLine(transform is null ? $"{pad}<DrawingGroup>" : $"{pad}<DrawingGroup Transform=\"{Matrix(transform)}\">");
        foreach (var child in el.Elements()) Emit(child, style, sb, depth + 1);
        sb.AppendLine($"{pad}</DrawingGroup>");
        return;
    }

    var geometry = GeometryElement(el);
    var brush = style.Fill is null ? "" : $" Brush=\"{Color(style.Fill, style.Opacity)}\"";
    sb.AppendLine($"{pad}<GeometryDrawing{brush}>");
    if (style.Stroke is not null)
    {
        sb.AppendLine($"{pad}    <GeometryDrawing.Pen>");
        sb.AppendLine($"{pad}        <Pen Brush=\"{Color(style.Stroke, style.Opacity)}\" Thickness=\"{F(style.StrokeWidth)}\" StartLineCap=\"{style.LineCap}\" EndLineCap=\"{style.LineCap}\" LineJoin=\"{style.LineJoin}\" />");
        sb.AppendLine($"{pad}    </GeometryDrawing.Pen>");
    }
    sb.AppendLine($"{pad}    <GeometryDrawing.Geometry>");
    sb.AppendLine($"{pad}        {geometry}");
    sb.AppendLine($"{pad}    </GeometryDrawing.Geometry>");
    sb.AppendLine($"{pad}</GeometryDrawing>");
}

static string IconGeometry(string key, string svgText)
{
    var svg = XElement.Parse(svgText);
    var sb = new StringBuilder();
    sb.AppendLine($"    <GeometryGroup x:Key=\"{key}\" FillRule=\"Nonzero\" po:Freeze=\"True\">");
    foreach (var el in svg.Elements()) sb.AppendLine($"        {GeometryElement(el)}");
    sb.AppendLine("    </GeometryGroup>");
    return sb.ToString();
}

static string GeometryElement(XElement el)
{
    double A(string n) => double.Parse(el.Attribute(n)?.Value ?? "0", CultureInfo.InvariantCulture);
    var transform = el.Attribute("transform")?.Value;
    var t = transform is null ? "" : $" Transform=\"{Matrix(transform)}\"";
    return el.Name.LocalName switch
    {
        "path" => $"<PathGeometry FillRule=\"Nonzero\" Figures=\"{el.Attribute("d")!.Value}\"{t} />",
        "rect" => $"<RectangleGeometry Rect=\"{F(A("x"))},{F(A("y"))},{F(A("width"))},{F(A("height"))}\" RadiusX=\"{F(A(el.Attribute("rx") is null ? "ry" : "rx"))}\" RadiusY=\"{F(A(el.Attribute("ry") is null ? "rx" : "ry"))}\"{t} />",
        "circle" => $"<EllipseGeometry Center=\"{F(A("cx"))},{F(A("cy"))}\" RadiusX=\"{F(A("r"))}\" RadiusY=\"{F(A("r"))}\"{t} />",
        "ellipse" => $"<EllipseGeometry Center=\"{F(A("cx"))},{F(A("cy"))}\" RadiusX=\"{F(A("rx"))}\" RadiusY=\"{F(A("ry"))}\"{t} />",
        var other => throw new NotSupportedException($"SVG element <{other}> is not in the supported subset"),
    };
}

// SVG transform list -> WPF MatrixTransform string "m11,m12,m21,m22,offsetX,offsetY".
static string Matrix(string transform)
{
    double m11 = 1, m12 = 0, m21 = 0, m22 = 1, dx = 0, dy = 0;
    foreach (Match op in Regex.Matches(transform, @"(\w+)\(([^)]*)\)"))
    {
        var n = Nums(op.Groups[2].Value);
        (double a, double b, double c, double d, double e, double f) next = op.Groups[1].Value switch
        {
            "translate" => (1, 0, 0, 1, n[0], n.Length > 1 ? n[1] : 0),
            "scale" => (n[0], 0, 0, n.Length > 1 ? n[1] : n[0], 0, 0),
            "rotate" => Rotate(n),
            var other => throw new NotSupportedException($"transform {other}"),
        };
        // SVG applies the list left to right onto the coordinate system: M = M * next (column vectors).
        // WPF matrices are row-vector, so the equivalent is next * M.
        (m11, m12, m21, m22, dx, dy) = (
            next.a * m11 + next.b * m21,
            next.a * m12 + next.b * m22,
            next.c * m11 + next.d * m21,
            next.c * m12 + next.d * m22,
            next.e * m11 + next.f * m21 + dx,
            next.e * m12 + next.f * m22 + dy);
    }
    return $"{F(m11)},{F(m12)},{F(m21)},{F(m22)},{F(dx)},{F(dy)}";

    static (double, double, double, double, double, double) Rotate(double[] n)
    {
        var rad = n[0] * Math.PI / 180;
        double cos = Math.Cos(rad), sin = Math.Sin(rad);
        double cx = n.Length > 2 ? n[1] : 0, cy = n.Length > 2 ? n[2] : 0;
        // rotate(a, cx, cy) = translate(cx, cy) rotate(a) translate(-cx, -cy), as a row-vector matrix.
        return (cos, sin, -sin, cos, cx - cx * cos + cy * sin, cy - cx * sin - cy * cos);
    }
}

static string Color(string css, double opacity)
{
    if (css == "currentColor") throw new NotSupportedException("currentColor belongs in icons, which are geometry-only");
    var hex = css.TrimStart('#');
    if (hex.Length == 3) hex = string.Concat(hex.Select(c => $"{c}{c}"));
    var alpha = (int)Math.Round(opacity * 255, MidpointRounding.AwayFromZero);
    return $"#{alpha:X2}{hex.ToUpperInvariant()}";
}

static double[] Nums(string s) =>
    Regex.Matches(s, @"-?(?:\d+\.?\d*|\.\d+)").Select(m => double.Parse(m.Value, CultureInfo.InvariantCulture)).ToArray();

static string F(double v) => Math.Round(v, 4).ToString("0.####", CultureInfo.InvariantCulture);

static string Wrap(string title, IEnumerable<string> items)
{
    var sb = new StringBuilder();
    sb.AppendLine("<!-- Generated by tools/svg2xaml.cs from design/assets. Do not edit by hand. -->");
    sb.AppendLine($"<!-- {title} -->");
    sb.AppendLine("<ResourceDictionary xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\"");
    sb.AppendLine("                    xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\"");
    sb.AppendLine("                    xmlns:mc=\"http://schemas.openxmlformats.org/markup-compatibility/2006\"");
    sb.AppendLine("                    xmlns:po=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation/options\"");
    sb.AppendLine("                    mc:Ignorable=\"po\">");
    foreach (var item in items) sb.Append(item);
    sb.AppendLine("</ResourceDictionary>");
    return sb.ToString();
}

static JsonElement LoadBundleArt(string bundlePath)
{
    var js = File.ReadAllText(bundlePath);
    var m = Regex.Match(js, @"var ART=(\{.*?\});\s*\n", RegexOptions.Singleline);
    if (!m.Success) throw new InvalidOperationException("ART table not found in bundle.js");
    return JsonDocument.Parse(m.Groups[1].Value).RootElement.Clone();
}

static string FindRepoRoot()
{
    for (var dir = new DirectoryInfo(Directory.GetCurrentDirectory()); dir is not null; dir = dir.Parent)
        if (File.Exists(Path.Combine(dir.FullName, "Brady.slnx"))) return dir.FullName;
    throw new InvalidOperationException("Run from inside the brady-app repository.");
}

sealed record Style(string? Fill, string? Stroke, double StrokeWidth, string LineCap, string LineJoin, double Opacity)
{
    public static readonly Style Initial = new("#000000", null, 1, "Flat", "Miter", 1);

    // Presentation attributes cascade from svg/g to children; opacity applies to the element only.
    public static Style From(XElement el, Style parent)
    {
        string? Attr(string n) => el.Attribute(n)?.Value;
        var fill = Attr("fill") is { } f ? (f == "none" ? null : f) : parent.Fill;
        var stroke = Attr("stroke") is { } s ? (s == "none" ? null : s) : parent.Stroke;
        var width = Attr("stroke-width") is { } w ? double.Parse(w, CultureInfo.InvariantCulture) : parent.StrokeWidth;
        var cap = Attr("stroke-linecap") switch { "round" => "Round", "square" => "Square", "butt" => "Flat", _ => parent.LineCap };
        var join = Attr("stroke-linejoin") switch { "round" => "Round", "bevel" => "Bevel", "miter" => "Miter", _ => parent.LineJoin };
        var opacity = Attr("opacity") is { } o ? double.Parse(o, CultureInfo.InvariantCulture) : 1;
        return new Style(fill, stroke, width, cap, join, opacity);
    }
}
