namespace Brady.App.Hosting;

/// <summary>
/// One Brady per user session. The first instance owns a named mutex and waits (without polling)
/// on a named event; a second launch sets the event and exits, and the first opens Settings.
/// </summary>
public sealed class SingleInstance : IDisposable
{
    private const string Id = "Brady-7c1d2a54-5f0e-4f4b-9a63-2e8f1b6d0c41";

    private readonly Mutex _mutex;
    private readonly EventWaitHandle _activate;
    private RegisteredWaitHandle? _registration;

    private SingleInstance(Mutex mutex, EventWaitHandle activate)
    {
        _mutex = mutex;
        _activate = activate;
    }

    /// <summary>Returns the instance guard, or null after signalling the instance that is already running.</summary>
    public static SingleInstance? TryAcquire()
    {
        var mutex = new Mutex(initiallyOwned: true, $@"Local\{Id}", out var createdNew);
        var activate = new EventWaitHandle(false, EventResetMode.AutoReset, $@"Local\{Id}-activate");
        if (createdNew) return new SingleInstance(mutex, activate);

        activate.Set();
        activate.Dispose();
        mutex.Dispose();
        return null;
    }

    /// <summary>Calls <paramref name="onActivated"/> (on a pool thread) whenever another launch is attempted.</summary>
    public void Listen(Action onActivated) =>
        _registration = ThreadPool.RegisterWaitForSingleObject(
            _activate, (_, _) => onActivated(), null, Timeout.Infinite, executeOnlyOnce: false);

    public void Dispose()
    {
        _registration?.Unregister(null);
        _activate.Dispose();
        _mutex.ReleaseMutex();
        _mutex.Dispose();
    }
}
