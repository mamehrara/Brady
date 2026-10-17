// Generates the WPF token dictionaries from design/tokens.json so they never drift by hand.
//
//   dotnet run tools/gen-tokens.cs            writes src/Brady.App/Theme/Tokens.*.xaml
//   dotnet run tools/gen-tokens.cs -- --check exits 1 if the committed files are stale
//
// Colors become a Color + frozen SolidColorBrush pair per theme (MossColor / MossBrush).
// rgba() becomes #AARRGGBB, "{alias}" values resolve to the aliased token.
// Spacing, radius, blur and type metrics are theme-independent and go to Tokens.Metrics.xaml.

using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

var check = args.Contains("--check");
var root = FindRepoRoot();
var tokensPath = Path.Combine(root, "design", "tokens.json");
var outDir = Path.Combine(root, "src", "Brady.App", "Theme");

using var doc = JsonDocument.Parse(File.ReadAllText(tokensPath));
var json = doc.RootElement;

var outputs = new Dictionary<string, string>
{
    ["Tokens.Light.xaml"] = BuildTheme(json, "light"),
    ["Tokens.Dark.xaml"] = BuildTheme(json, "dark"),
    ["Tokens.Metrics.xaml"] = BuildMetrics(json),
};

var stale = new List<string>();
foreach (var (name, content) in outputs)
{
    var path = Path.Combine(outDir, name);
    var existing = File.Exists(path) ? Normalize(File.ReadAllText(path)) : null;
    if (existing == Normalize(content)) continue;

    if (check) { stale.Add(name); continue; }
    Directory.CreateDirectory(outDir);
    File.WriteAllText(path, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    Console.WriteLine($"wrote {Path.GetRelativePath(root, path)}");
}

if (check && stale.Count > 0)
{
    Console.Error.WriteLine($"Stale token files (run `dotnet run tools/gen-tokens.cs`): {string.Join(", ", stale)}");
    return 1;
}
Console.WriteLine(check ? "tokens up to date" : "done");
return 0;

static string BuildTheme(JsonElement json, string theme)
{
    var tokens = json.GetProperty("color").GetProperty("tokens").EnumerateArray()
        .ToDictionary(t => t.GetProperty("name").GetString()!, t => t.GetProperty("value"));

    var sb = Header(theme == "light" ? "Light theme colors" : "Dark theme colors");
    foreach (var (name, value) in tokens)
    {
        var argb = ResolveColor(tokens, value, theme);
        var key = Pascal(name);
        sb.AppendLine($"    <Color x:Key=\"{key}Color\">{argb}</Color>");
        sb.AppendLine($"    <SolidColorBrush x:Key=\"{key}Brush\" Color=\"{argb}\" po:Freeze=\"True\" />");
    }

    // shadow-glass: "0 12px 32px rgba(...), 0 2px 6px rgba(...)" -> ShadowGlass1*, ShadowGlass2*
    foreach (var shadow in json.GetProperty("shadow").GetProperty("tokens").EnumerateArray())
    {
        var key = Pascal(shadow.GetProperty("name").GetString()!);
        var layers = SplitTopLevel(shadow.GetProperty("value").GetProperty(theme).GetString()!);
        sb.AppendLine();
        for (var i = 0; i < layers.Count; i++)
        {
            var m = Regex.Match(layers[i], @"^\s*(-?[\d.]+)(?:px)?\s+(-?[\d.]+)(?:px)?\s+([\d.]+)(?:px)?\s+(rgba?\(.+\))\s*$");
            if (!m.Success) throw new FormatException($"Unsupported shadow layer '{layers[i]}'");
            var n = i + 1;
            sb.AppendLine($"    <Color x:Key=\"{key}{n}Color\">{ParseCss(m.Groups[4].Value)}</Color>");
            sb.AppendLine($"    <sys:Double x:Key=\"{key}{n}OffsetX\">{m.Groups[1].Value}</sys:Double>");
            sb.AppendLine($"    <sys:Double x:Key=\"{key}{n}OffsetY\">{m.Groups[2].Value}</sys:Double>");
            sb.AppendLine($"    <sys:Double x:Key=\"{key}{n}Blur\">{m.Groups[3].Value}</sys:Double>");
        }
    }
    return Footer(sb);
}

static string BuildMetrics(JsonElement json)
{
    var sb = Header("Theme-independent metrics: spacing, radius, blur and type");

    foreach (var t in json.GetProperty("spacing").GetProperty("tokens").EnumerateArray())
        sb.AppendLine($"    <sys:Double x:Key=\"{Pascal(t.GetProperty("name").GetString()!)}\">{Px(t.GetProperty("value").GetString()!)}</sys:Double>");

    sb.AppendLine();
    foreach (var t in json.GetProperty("radius").GetProperty("tokens").EnumerateArray())
    {
        var key = Pascal(t.GetProperty("name").GetString()!);
        var px = Px(t.GetProperty("value").GetString()!);
        sb.AppendLine($"    <sys:Double x:Key=\"{key}\">{px}</sys:Double>");
        sb.AppendLine($"    <CornerRadius x:Key=\"{key}Corner\">{px}</CornerRadius>");
    }

    sb.AppendLine();
    foreach (var t in json.GetProperty("blur").GetProperty("tokens").EnumerateArray())
        sb.AppendLine($"    <sys:Double x:Key=\"{Pascal(t.GetProperty("name").GetString()!)}\">{Px(t.GetProperty("value").GetString()!)}</sys:Double>");

    foreach (var group in json.GetProperty("type").GetProperty("groups").EnumerateArray())
    {
        sb.AppendLine();
        foreach (var style in group.GetProperty("styles").EnumerateArray())
        {
            var key = Pascal(style.GetProperty("name").GetString()!);
            sb.AppendLine($"    <sys:Double x:Key=\"{key}FontSize\">{Px(style.GetProperty("fontSize").GetString()!)}</sys:Double>");
            sb.AppendLine($"    <sys:Double x:Key=\"{key}LineHeight\">{Px(style.GetProperty("lineHeight").GetString()!)}</sys:Double>");
            sb.AppendLine($"    <FontWeight x:Key=\"{key}FontWeight\">{WeightName(style.GetProperty("fontWeight").GetInt32())}</FontWeight>");
        }
    }
    return Footer(sb);
}

static string ResolveColor(Dictionary<string, JsonElement> tokens, JsonElement value, string theme)
{
    var raw = value.ValueKind == JsonValueKind.Object ? value.GetProperty(theme).GetString()! : value.GetString()!;
    var alias = Regex.Match(raw, @"^\{(.+)\}$");
    return alias.Success ? ResolveColor(tokens, tokens[alias.Groups[1].Value], theme) : ParseCss(raw);
}

static string ParseCss(string css)
{
    css = css.Trim();
    if (css.StartsWith('#'))
    {
        var hex = css[1..];
        if (hex.Length == 3) hex = string.Concat(hex.Select(c => $"{c}{c}"));
        return "#FF" + hex.ToUpperInvariant();
    }
    var m = Regex.Match(css, @"^rgba?\(\s*(\d+)\s*,\s*(\d+)\s*,\s*(\d+)\s*(?:,\s*([\d.]+)\s*)?\)$");
    if (!m.Success) throw new FormatException($"Unsupported color '{css}'");
    var a = m.Groups[4].Success ? double.Parse(m.Groups[4].Value, CultureInfo.InvariantCulture) : 1.0;
    var alpha = (int)Math.Round(a * 255, MidpointRounding.AwayFromZero);
    return $"#{alpha:X2}{int.Parse(m.Groups[1].Value):X2}{int.Parse(m.Groups[2].Value):X2}{int.Parse(m.Groups[3].Value):X2}";
}

static List<string> SplitTopLevel(string value)
{
    var parts = new List<string>();
    int depth = 0, start = 0;
    for (var i = 0; i < value.Length; i++)
    {
        if (value[i] == '(') depth++;
        else if (value[i] == ')') depth--;
        else if (value[i] == ',' && depth == 0) { parts.Add(value[start..i]); start = i + 1; }
    }
    parts.Add(value[start..]);
    return parts;
}

static string Px(string v) => v.Replace("px", "", StringComparison.Ordinal).Trim();

static string Pascal(string kebab) =>
    string.Concat(kebab.Split('-').Select(p => char.ToUpperInvariant(p[0]) + p[1..]));

static string WeightName(int w) => w switch
{
    300 => "Light", 400 => "Normal", 500 => "Medium", 600 => "SemiBold", 700 => "Bold",
    _ => throw new FormatException($"Unsupported weight {w}"),
};

static StringBuilder Header(string title)
{
    var sb = new StringBuilder();
    sb.AppendLine("<!-- Generated by tools/gen-tokens.cs from design/tokens.json. Do not edit by hand. -->");
    sb.AppendLine($"<!-- {title} -->");
    sb.AppendLine("<ResourceDictionary xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\"");
    sb.AppendLine("                    xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\"");
    sb.AppendLine("                    xmlns:sys=\"clr-namespace:System;assembly=System.Runtime\"");
    sb.AppendLine("                    xmlns:mc=\"http://schemas.openxmlformats.org/markup-compatibility/2006\"");
    sb.AppendLine("                    xmlns:po=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation/options\"");
    sb.AppendLine("                    mc:Ignorable=\"po\">");
    return sb;
}

static string Footer(StringBuilder sb)
{
    sb.AppendLine("</ResourceDictionary>");
    return sb.ToString();
}

static string Normalize(string s) => s.Replace("\r\n", "\n", StringComparison.Ordinal);

static string FindRepoRoot()
{
    for (var dir = new DirectoryInfo(Directory.GetCurrentDirectory()); dir is not null; dir = dir.Parent)
        if (File.Exists(Path.Combine(dir.FullName, "Brady.slnx"))) return dir.FullName;
    throw new InvalidOperationException("Run from inside the brady-app repository.");
}
