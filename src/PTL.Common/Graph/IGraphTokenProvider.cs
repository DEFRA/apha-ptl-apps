namespace PTL.Common.Graph;

/// <summary>Supplies an application (client-credentials) access token for Microsoft Graph.</summary>
public interface IGraphTokenProvider
{
    /// <summary>Returns a valid access token, reusing the cached one until it is close to expiry.</summary>
    /// <exception cref="GraphException">Graph is not configured or the token request failed.</exception>
    Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default);
}
