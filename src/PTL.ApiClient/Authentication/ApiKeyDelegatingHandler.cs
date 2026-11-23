using Microsoft.Extensions.Options;
using PTL.Common.ApiAuthentication;

namespace PTL.ApiClient.Authentication;

/// <summary>
/// Adds this front-end's <c>X-Api-Key</c> and <c>X-Client-App</c> headers to every request sent to
/// PTL.Api. Registered as an <see cref="System.Net.Http.IHttpClientFactory"/> default so no typed
/// client can be missed, which is why it only stamps requests whose scheme, host and port match
/// <c>Api:BaseUrl</c> - the key must never reach Graph, CIDM, Entra or any other host.
/// </summary>
public sealed class ApiKeyDelegatingHandler(Uri apiBaseUri, IOptions<ApiAuthenticationOptions> options) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.RequestUri is { IsAbsoluteUri: true } requestUri
            && Uri.Compare(requestUri, apiBaseUri, UriComponents.SchemeAndServer, UriFormat.Unescaped, StringComparison.OrdinalIgnoreCase) == 0)
        {
            var settings = options.Value;
            request.Headers.Remove(ApiAuthenticationOptions.ApiKeyHeaderName);
            request.Headers.TryAddWithoutValidation(ApiAuthenticationOptions.ApiKeyHeaderName, settings.ApiKey);
            request.Headers.Remove(ApiAuthenticationOptions.ClientAppHeaderName);
            request.Headers.TryAddWithoutValidation(ApiAuthenticationOptions.ClientAppHeaderName, settings.ClientApp);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
