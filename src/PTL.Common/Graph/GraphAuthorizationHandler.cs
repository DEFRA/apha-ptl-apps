using System.Net.Http.Headers;

namespace PTL.Common.Graph;

/// <summary>Adds the Graph bearer token to every request made through the named Graph client.</summary>
public sealed class GraphAuthorizationHandler(IGraphTokenProvider tokenProvider) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var token = await tokenProvider.GetAccessTokenAsync(cancellationToken);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await base.SendAsync(request, cancellationToken);
    }
}
