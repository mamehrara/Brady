using System.Globalization;
using Brady.Core.Reminders;

namespace Brady.Core.Copy;

/// <summary>The words in the tray tooltip and flyout, in Brady's voice.</summary>
public static class StatusText
{
    /// <summary>"Brady · next: Eyes in 7 min", or why nothing is coming.</summary>
    public static string Tooltip(EngineSnapshot snapshot, TimeZoneInfo zone) => "Brady · " + Headline(snapshot, zone);

    /// <summary>The status line under Brady's name in the flyout (the tooltip without the name).</summary>
    public static string Headline(EngineSnapshot snapshot, TimeZoneInfo zone)
    {
        if (snapshot.PausedUntil is { } until) return "paused until " + When(until, snapshot.Now, zone);
        if (!snapshot.IsPresent) return "see you when you're back";
        var next = snapshot.Upcoming.FirstOrDefault(u => u.DueAt is not null);
        if (next is null) return snapshot.Upcoming.Count == 0 ? "all reminders are off" : "waiting";
        return $"next: {next.Kind.Label()} in {Duration(next.DueAt!.Value - snapshot.Now)}";
    }

    /// <summary>"in 7 min" style countdown, rounded up so it never says 0.</summary>
    public static string Duration(TimeSpan span)
    {
        var minutes = Math.Max(1, (int)Math.Ceiling(span.TotalMinutes));
        if (minutes < 60) return $"{minutes} min";
        var hours = minutes / 60;
        var rest = minutes % 60;
        return rest == 0 ? $"{hours} h" : $"{hours} h {rest} min";
    }

    /// <summary>
    /// How long until <see cref="Tooltip"/> would read differently, so the tray can sleep until then
    /// instead of ticking: the countdown changes when the time left crosses a whole minute.
    /// </summary>
    public static TimeSpan NextChange(EngineSnapshot snapshot)
    {
        var next = snapshot.Upcoming.FirstOrDefault(u => u.DueAt is not null);
        if (snapshot.PausedUntil is not null || next?.DueAt is not { } due) return TimeSpan.FromMinutes(1);
        var left = due - snapshot.Now;
        if (left <= TimeSpan.Zero) return TimeSpan.FromMinutes(1);
        var intoMinute = TimeSpan.FromTicks(left.Ticks % TimeSpan.TicksPerMinute);
        return (intoMinute == TimeSpan.Zero ? TimeSpan.FromMinutes(1) : intoMinute) + TimeSpan.FromMilliseconds(50);
    }

    private static string When(DateTimeOffset at, DateTimeOffset now, TimeZoneInfo zone)
    {
        var local = TimeZoneInfo.ConvertTime(at, zone);
        var today = TimeZoneInfo.ConvertTime(now, zone).Date;
        var time = local.ToString("HH:mm", CultureInfo.InvariantCulture);
        return local.Date == today ? time : local.Date == today.AddDays(1) ? "tomorrow, " + time : local.ToString("ddd HH:mm", CultureInfo.InvariantCulture);
    }
}
