using System.Runtime.InteropServices;
using Brady.Core.Activity;

namespace Brady.App.Platform;

/// <summary>
/// The last keyboard or mouse input, from GetLastInputInfo. Read only when the engine wakes up,
/// which is at most every few minutes while the user is active.
/// </summary>
public sealed class WindowsActivitySource(TimeProvider time) : IActivitySource
{
    public DateTimeOffset LastInputAt
    {
        get
        {
            var info = new NativeMethods.LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf<NativeMethods.LASTINPUTINFO>() };
            if (!NativeMethods.GetLastInputInfo(ref info)) return time.GetUtcNow();

            // Both are 32-bit millisecond tick counts; unsigned subtraction survives the 49.7-day wrap.
            var idleMs = unchecked((uint)Environment.TickCount - info.dwTime);
            return time.GetUtcNow() - TimeSpan.FromMilliseconds(idleMs);
        }
    }
}
