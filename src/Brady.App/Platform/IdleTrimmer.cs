using System.Windows.Threading;
using Microsoft.Extensions.Logging;

namespace Brady.App.Platform;

/// <summary>
/// Gives memory back once Brady goes quiet. A few seconds after the last toast leaves, it runs one
/// compacting GC and asks Windows to trim the working set. Brady then touches almost nothing until
/// the next reminder, so the pages stay out and the idle footprint stays small.
/// </summary>
public sealed partial class IdleTrimmer
{
    private static readonly TimeSpan Delay = TimeSpan.FromSeconds(5);

    private readonly DispatcherTimer _timer = new(DispatcherPriority.ApplicationIdle) { Interval = Delay };
    private readonly ILogger<IdleTrimmer> _logger;

    public IdleTrimmer(ILogger<IdleTrimmer> logger)
    {
        _logger = logger;
        _timer.Tick += (_, _) =>
        {
            _timer.Stop();
            Trim();
        };
    }

    /// <summary>(Re)starts the countdown; repeated calls coalesce into one trim.</summary>
    public void Schedule()
    {
        _timer.Stop();
        _timer.Start();
    }

    /// <summary>Called when Brady gets busy again: never collect while a toast is on screen.</summary>
    public void Cancel() => _timer.Stop();

    private void Trim()
    {
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
        GC.WaitForPendingFinalizers();
        NativeMethods.SetProcessWorkingSetSize(NativeMethods.GetCurrentProcess(), -1, -1);
        LogTrimmed(GC.GetTotalMemory(forceFullCollection: false) / 1024);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Idle trim done; managed heap {HeapKb} KB")]
    private partial void LogTrimmed(long heapKb);
}
