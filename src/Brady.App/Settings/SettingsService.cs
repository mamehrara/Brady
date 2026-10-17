using Brady.Core.Settings;
using Microsoft.Extensions.Logging;

namespace Brady.App.Settings;

/// <summary>
/// The app's one copy of the user's settings. Changes go through <see cref="Update"/>, which saves
/// (debounced) and tells listeners, so there is never a Save button.
/// </summary>
public sealed partial class SettingsService : IDisposable
{
    private readonly SettingsStore _store;
    private readonly ILogger<SettingsService> _logger;

    public SettingsService(TimeProvider time, ILogger<SettingsService> logger)
    {
        _logger = logger;
        _store = new SettingsStore(SettingsStore.DefaultPath, time);
        Current = _store.Load();
        if (_store.LastProblem is { } problem) LogUnreadable(problem);
    }

    public event EventHandler? Changed;

    public BradySettings Current { get; }

    public void Update(Action<BradySettings> change)
    {
        change(Current);
        Current.Normalize();
        _store.ScheduleSave(Current);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose() => _store.Dispose();

    [LoggerMessage(Level = LogLevel.Warning, Message = "settings.json was unreadable and was set aside; using defaults. {Problem}")]
    private partial void LogUnreadable(string problem);
}
