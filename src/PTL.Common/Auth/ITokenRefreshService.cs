namespace PTL.Common.Auth;

/// <summary>Outcome of an OAuth2 refresh_token exchange.</summary>
public sealed record TokenRefreshResult(bool Succeeded, string? AccessToken, string? IdToken, string? RefreshToken, DateTimeOffset? ExpiresAt) : ITokenRefreshResult
{
    public static TokenRefreshResult Failed { get; } = new(false, null, null, null, null);
}

/// <summary>Exchanges a refresh_token for a new token set.</summary>
public interface ITokenRefreshService
{
    /// <param name="refreshToken">The refresh_token from the current session.</param>
    /// <param name="redirectUri">
    /// Must exactly match the redirect_uri used on the original /authorize request - the caller builds this
    /// from the current request, since this service has no HTTP context of its own.
    /// </param>
    /// <param name="cancellationToken">Cancellation token for the outbound HTTP request.</param>
    Task<TokenRefreshResult> RefreshAsync(string refreshToken, string redirectUri, CancellationToken cancellationToken = default);
}
