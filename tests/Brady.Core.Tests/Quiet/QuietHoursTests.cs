using Brady.Core.Quiet;

namespace Brady.Core.Tests.Quiet;

public sealed class QuietHoursTests
{
    private static readonly QuietHours Night = new(true, new TimeOnly(22, 0), new TimeOnly(8, 0));

    [Theory]
    [InlineData("22:00", true)]
    [InlineData("23:59", true)]
    [InlineData("00:00", true)]
    [InlineData("07:59", true)]
    [InlineData("08:00", false)]
    [InlineData("12:00", false)]
    [InlineData("21:59", false)]
    public void A_window_across_midnight(string time, bool quiet) =>
        Assert.Equal(quiet, Night.Contains(TimeOnly.Parse(time, System.Globalization.CultureInfo.InvariantCulture)));

    [Theory]
    [InlineData("12:59", false)]
    [InlineData("13:00", true)]
    [InlineData("13:59", true)]
    [InlineData("14:00", false)]
    public void A_window_within_one_day(string time, bool quiet)
    {
        var lunch = new QuietHours(true, new TimeOnly(13, 0), new TimeOnly(14, 0));
        Assert.Equal(quiet, lunch.Contains(TimeOnly.Parse(time, System.Globalization.CultureInfo.InvariantCulture)));
    }

    [Fact]
    public void Disabled_or_empty_windows_are_never_quiet()
    {
        Assert.False((Night with { Enabled = false }).Contains(new TimeOnly(23, 0)));
        Assert.False(new QuietHours(true, new TimeOnly(9, 0), new TimeOnly(9, 0)).Contains(new TimeOnly(9, 0)));
    }

    [Fact]
    public void Next_end_is_this_morning_after_midnight_and_tomorrow_before()
    {
        Assert.Equal(new DateTime(2026, 10, 20, 8, 0, 0), Night.NextEnd(new DateTime(2026, 10, 20, 1, 0, 0)));
        Assert.Equal(new DateTime(2026, 10, 21, 8, 0, 0), Night.NextEnd(new DateTime(2026, 10, 20, 15, 0, 0)));
    }
}
