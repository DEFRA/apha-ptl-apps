namespace PTL.ApiClient.Tests.LoadTesting;

internal sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
{
    public static readonly DateOnly LastDay = new(2027, 1, 31);

    public static FixedTimeProvider BeforeExpiry { get; } = new(new DateTimeOffset(2026, 11, 20, 12, 0, 0, TimeSpan.Zero));

    public static FixedTimeProvider LastMomentOfLastDay { get; } = new(new DateTimeOffset(2027, 1, 31, 23, 59, 59, TimeSpan.Zero));

    public static FixedTimeProvider DayAfterExpiry { get; } = new(new DateTimeOffset(2027, 2, 1, 0, 0, 0, TimeSpan.Zero));

    public override DateTimeOffset GetUtcNow() => utcNow;
}
