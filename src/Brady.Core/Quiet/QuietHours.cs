namespace Brady.Core.Quiet;

/// <summary>
/// A daily window, in local time, when Brady stays silent. The window may cross midnight
/// (22:00 to 08:00). Start is inclusive, end exclusive; equal start and end means no window.
/// </summary>
public sealed record QuietHours(bool Enabled, TimeOnly Start, TimeOnly End)
{
    public static QuietHours Default { get; } = new(true, new TimeOnly(22, 0), new TimeOnly(8, 0));

    public bool Contains(TimeOnly time)
    {
        if (!Enabled || Start == End) return false;
        return Start < End
            ? time >= Start && time < End
            : time >= Start || time < End;
    }

    /// <summary>The next time the window ends after <paramref name="localNow"/>.</summary>
    public DateTime NextEnd(DateTime localNow)
    {
        var end = localNow.Date + End.ToTimeSpan();
        return end > localNow ? end : end.AddDays(1);
    }
}
