namespace PTL.Common.Auth;

/// <summary>
/// Common shape of a token-refresh outcome, shared by every PTL auth provider's token refresh
/// service (CIDM/Entra ID) so their near-identical cookie-refresh logic can live in one place.
/// </summary>
public interface ITokenRefreshResult
{
    /// <summary>Gets a value indicating whether the refresh succeeded.</summary>
    bool Succeeded { get; }

    /// <summary>Gets the refreshed access token, when <see cref="Succeeded"/> is <see langword="true"/>.</summary>
    string? AccessToken { get; }

    /// <summary>Gets the refreshed id_token, if the provider returned one.</summary>
    string? IdToken { get; }

    /// <summary>Gets the refreshed refresh_token, if the provider rotated it.</summary>
    string? RefreshToken { get; }

    /// <summary>Gets the new access token expiry, when <see cref="Succeeded"/> is <see langword="true"/>.</summary>
    DateTimeOffset? ExpiresAt { get; }
}
