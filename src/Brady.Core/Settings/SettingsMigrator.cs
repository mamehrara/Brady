using System.Text.Json.Nodes;

namespace Brady.Core.Settings;

/// <summary>
/// Brings an older settings.json up to <see cref="BradySettings.CurrentVersion"/>, one version at a
/// time. Each step edits the raw JSON, so old files never need old classes. A file without a
/// version field is treated as version 1.
/// </summary>
public static class SettingsMigrator
{
    // Steps[n] migrates version n+1 to n+2. Add a step whenever CurrentVersion is bumped.
    private static readonly Func<JsonObject, JsonObject>[] Steps = [V1ToV2];

    public static JsonObject Migrate(JsonObject root)
    {
        var version = root["version"]?.GetValue<int>() ?? 1;
        if (version > BradySettings.CurrentVersion)
            throw new NotSupportedException($"settings.json is version {version}, newer than this Brady ({BradySettings.CurrentVersion}).");

        for (var v = Math.Max(version, 1); v < BradySettings.CurrentVersion; v++)
        {
            root = Steps[v - 1](root);
            root["version"] = v + 1;
        }
        return root;
    }

    // v2 adds per-kind snooze and guided-break lengths, the idle threshold and the minimum gap,
    // all with the values v1 used implicitly.
    private static JsonObject V1ToV2(JsonObject root)
    {
        if (root["kinds"] is JsonObject kinds)
        {
            foreach (var (name, node) in kinds)
            {
                if (node is not JsonObject kind) continue;
                kind["snoozeMinutes"] ??= SettingsChoices.DefaultSnoozeMinutes;
                var guided = name switch { "eyes" => 20, "stretch" => 15, _ => (int?)null };
                if (guided is { } seconds) kind["guidedSeconds"] ??= seconds;
            }
        }
        root["idleThresholdMinutes"] ??= SettingsChoices.DefaultIdleThresholdMinutes;
        root["minGapMinutes"] ??= SettingsChoices.DefaultGapMinutes;
        return root;
    }
}
