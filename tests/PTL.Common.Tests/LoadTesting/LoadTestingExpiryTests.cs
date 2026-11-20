using PTL.Common.LoadTesting;

namespace PTL.Common.Tests.LoadTesting;

public class LoadTestingExpiryTests
{
    private static readonly DateOnly LastDay = new(2027, 1, 31);

    [Theory]
    [InlineData("2026-11-20T12:00:00Z", true)]
    [InlineData("2027-01-31T00:00:00Z", true)]
    [InlineData("2027-01-31T23:59:59Z", true)]
    [InlineData("2027-02-01T00:00:00Z", false)]
    [InlineData("2030-06-01T00:00:00Z", false)]
    public void IsOpen_UpToAndIncludingTheLastDayUtc(string utcNow, bool expected)
    {
        Assert.Equal(expected, LoadTestingExpiry.IsOpen(LastDay, new FixedTimeProvider(DateTimeOffset.Parse(utcNow, System.Globalization.CultureInfo.InvariantCulture))));
    }

    [Fact]
    public void IsOpen_UsesUtcNotTheCallersOffset()
    {
        // 1 Feb 00:30 in UTC+1 is still 31 Jan 23:30 UTC.
        var now = new DateTimeOffset(2027, 2, 1, 0, 30, 0, TimeSpan.FromHours(1));

        Assert.True(LoadTestingExpiry.IsOpen(LastDay, new FixedTimeProvider(now)));
    }

    [Fact]
    public void IsOpen_WithoutAnExpiryDate_IsClosed()
    {
        Assert.False(LoadTestingExpiry.IsOpen(null, new FixedTimeProvider(new DateTimeOffset(2026, 11, 20, 0, 0, 0, TimeSpan.Zero))));
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
