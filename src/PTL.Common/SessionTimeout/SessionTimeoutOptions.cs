namespace PTL.Common.SessionTimeout;

/// <summary>
/// Configuration for the client-side inactivity warning shown by both web front-ends (see each
/// app's <c>_SessionTimeoutWarning.cshtml</c>/<c>session-timeout.js</c>). Independent of the
/// underlying sliding auth cookie and identity-provider token refresh - this is a UX/security layer
/// on top of them, not a replacement.
/// </summary>
public sealed class SessionTimeoutOptions
{
    public const string SectionName = "SessionTimeout";

    /// <summary>How long the user may be inactive before the "Extend session?" warning appears.</summary>
    public TimeSpan InactivityWarningAfter { get; set; } = TimeSpan.FromMinutes(15);

    /// <summary>
    /// How long the warning remains visible (counting down) before the session is actively expired
    /// if the user takes no action.
    /// </summary>
    public TimeSpan ExpireAfterWarning { get; set; } = TimeSpan.FromMinutes(2);
}
