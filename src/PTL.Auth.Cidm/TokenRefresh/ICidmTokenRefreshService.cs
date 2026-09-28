namespace PTL.Auth.Cidm.TokenRefresh;

public sealed record CidmTokenRefreshResult(bool Succeeded, string? AccessToken, string? IdToken, string? RefreshToken, DateTimeOffset? ExpiresAt)
{
    public static CidmTokenRefreshResult Failed { get; } = new(false, null, null, null, null);
}

public interface ICidmTokenRefreshService
{
    /// <param name="redirectUri">
    /// Must exactly match the redirect_uri used on the original /authorize request - the caller builds this
    /// from the current request, since this service has no HTTP context of its own.
    /// </param>
    Task<CidmTokenRefreshResult> RefreshAsync(string refreshToken, string redirectUri, CancellationToken cancellationToken = default);
}
