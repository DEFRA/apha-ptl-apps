using PTL.Common.Auth;

namespace PTL.Auth.Entra.TokenRefresh;

public sealed record EntraTokenRefreshResult(bool Succeeded, string? AccessToken, string? IdToken, string? RefreshToken, DateTimeOffset? ExpiresAt) : ITokenRefreshResult
{
    public static EntraTokenRefreshResult Failed { get; } = new(false, null, null, null, null);
}

public interface IEntraTokenRefreshService
{
    /// <param name="redirectUri">
    /// Must exactly match the redirect_uri used on the original /authorize request - the caller builds this
    /// from the current request, since this service has no HTTP context of its own.
    /// </param>
    Task<EntraTokenRefreshResult> RefreshAsync(string refreshToken, string redirectUri, CancellationToken cancellationToken = default);
}
