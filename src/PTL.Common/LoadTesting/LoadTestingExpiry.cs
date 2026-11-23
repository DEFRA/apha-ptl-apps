namespace PTL.Common.LoadTesting;

/// <summary>
/// Hard end date for the temporary load-testing sign-in, checked independently of the environment
/// name because that cannot be relied on. Shared by the web front-ends and PTL.Api.
/// </summary>
public static class LoadTestingExpiry
{
    public const string ConfigurationKey = "LoadTesting:ExpiresOn";

    /// <summary>
    /// True up to and including <paramref name="expiresOn"/> (UTC date). A missing date means the
    /// feature is never available, so a lost setting fails closed.
    /// </summary>
    public static bool IsOpen(DateOnly? expiresOn, TimeProvider timeProvider) =>
        expiresOn is { } lastDay && DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime) <= lastDay;
}
