namespace Brady.App.Hosting;

/// <summary>
/// One Brady per user session. The first instance owns a named mutex and waits (without polling)
/// on two named events. A second launch sets one of them and exits: "activate" (the tray flyout,
/// later Settings) or, with <c>--quit</c>, "quit" (used by the installer before an upgrade).
/// </summary>
public sealed class SingleInstance : IDisposable
{
    /// <summary>The mutex name; the installer's AppMutex uses the same one.</summary>
    public const string MutexName = "Brady-7c1d2a54-5f0e-4f4b-9a63-2e8f1b6d0c41";

    private readonly Mutex _mutex;
    private readonly EventWaitHandle _activate;
    private readonly EventWaitHandle _quit;
    private readonly List<RegisteredWaitHandle> _registrations = [];

    private SingleInstance(Mutex mutex, EventWaitHandle activate, EventWaitHandle quit)
    {
        _mutex = mutex;
        _activate = activate;
        _quit = quit;
    }

    /// <summary>
    /// Returns the instance guard, or null after signalling the instance that is already running
    /// (to activate, or to quit when <paramref name="quit"/> is set).
    /// </summary>
    public static SingleInstance? TryAcquire(bool quit = false)
    {
        var mutex = new Mutex(initiallyOwned: true, $@"Local\{MutexName}", out var createdNew);
        var activate = new EventWaitHandle(false, EventResetMode.AutoReset, $@"Local\{MutexName}-activate");
        var quitEvent = new EventWaitHandle(false, EventResetMode.AutoReset, $@"Local\{MutexName}-quit");
        if (createdNew && !quit) return new SingleInstance(mutex, activate, quitEvent);

        if (!createdNew) (quit ? quitEvent : activate).Set();
        activate.Dispose();
        quitEvent.Dispose();
        if (createdNew) mutex.ReleaseMutex();
        mutex.Dispose();
        return null;
    }

    /// <summary>Calls back (on a pool thread) whenever another launch asks to activate or to quit.</summary>
    public void Listen(Action onActivate, Action onQuit)
    {
        _registrations.Add(ThreadPool.RegisterWaitForSingleObject(_activate, (_, _) => onActivate(), null, Timeout.Infinite, executeOnlyOnce: false));
        _registrations.Add(ThreadPool.RegisterWaitForSingleObject(_quit, (_, _) => onQuit(), null, Timeout.Infinite, executeOnlyOnce: true));
    }

    public void Dispose()
    {
        foreach (var registration in _registrations) registration.Unregister(null);
        _activate.Dispose();
        _quit.Dispose();
        _mutex.ReleaseMutex();
        _mutex.Dispose();
    }
}
