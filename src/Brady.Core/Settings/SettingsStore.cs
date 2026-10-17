using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Brady.Core.Settings;

/// <summary>
/// Loads and saves <see cref="BradySettings"/> as JSON. Loading migrates older schema versions and
/// clamps bad values; a file that can't be read at all is set aside as settings.json.bad and Brady
/// starts from defaults. Saves are atomic (write a temp file, then replace) and can be debounced.
/// </summary>
public sealed class SettingsStore : IDisposable
{
    public static readonly TimeSpan SaveDelay = TimeSpan.FromMilliseconds(500);

    // People hand-edit this file: forgive comments and trailing commas.
    private static readonly JsonDocumentOptions Lenient = new() { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };

    private readonly string _path;
    private readonly ITimer _saveTimer;
    private readonly Lock _gate = new();
    private BradySettings? _pending;

    public SettingsStore(string path, TimeProvider time)
    {
        _path = path;
        _saveTimer = time.CreateTimer(_ => Flush(), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    public static string DefaultPath { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Brady", "settings.json");

    /// <summary>Set by <see cref="Load"/> when the file was unreadable or needed repair.</summary>
    public string? LastProblem { get; private set; }

    public BradySettings Load()
    {
        LastProblem = null;
        if (!File.Exists(_path)) return BradySettings.CreateDefault();

        try
        {
            var root = JsonNode.Parse(File.ReadAllText(_path), documentOptions: Lenient) as JsonObject
                ?? throw new JsonException("settings.json is not a JSON object");
            var migrated = SettingsMigrator.Migrate(root);
            var settings = migrated.Deserialize(SettingsJsonContext.Default.BradySettings) ?? BradySettings.CreateDefault();
            return settings.Normalize();
        }
        catch (Exception e) when (e is JsonException or IOException or InvalidOperationException or NotSupportedException)
        {
            LastProblem = e.Message;
            TrySetAside();
            return BradySettings.CreateDefault();
        }
    }

    /// <summary>Saves after <see cref="SaveDelay"/>; a burst of changes becomes one write.</summary>
    public void ScheduleSave(BradySettings settings)
    {
        lock (_gate) _pending = settings;
        _saveTimer.Change(SaveDelay, Timeout.InfiniteTimeSpan);
    }

    /// <summary>Writes any pending change now (call on exit).</summary>
    public void Flush()
    {
        BradySettings? pending;
        lock (_gate)
        {
            pending = _pending;
            _pending = null;
        }
        if (pending is not null) Save(pending);
    }

    public void Save(BradySettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        var temp = _path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(settings, SettingsJsonContext.Default.BradySettings));
        File.Move(temp, _path, overwrite: true);
    }

    public void Dispose()
    {
        Flush();
        _saveTimer.Dispose();
    }

    private void TrySetAside()
    {
        try
        {
            File.Move(_path, _path + ".bad", overwrite: true);
        }
        catch (IOException)
        {
            // Leave it; defaults are used either way and the next save replaces it.
        }
    }
}

[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DictionaryKeyPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = true,
    ReadCommentHandling = JsonCommentHandling.Skip,
    AllowTrailingCommas = true,
    NumberHandling = JsonNumberHandling.AllowReadingFromString)]
[JsonSerializable(typeof(BradySettings))]
internal sealed partial class SettingsJsonContext : JsonSerializerContext;
