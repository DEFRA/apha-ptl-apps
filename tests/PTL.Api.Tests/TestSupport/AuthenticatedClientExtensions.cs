using Microsoft.AspNetCore.Mvc.Testing;
using PTL.Common.ApiAuthentication;

namespace PTL.Api.Tests.TestSupport;

internal static class AuthenticatedClientExtensions
{
    // Sends the headers a web front-end would, so endpoint tests get past the API key authentication.
    public static HttpClient CreateAuthenticatedClient(this WebApplicationFactory<Program> factory, string clientApp = ApiAuthenticationOptions.InternalWeb)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(ApiAuthenticationOptions.ApiKeyHeaderName, TestHostConfiguration.ApiKey);
        client.DefaultRequestHeaders.Add(ApiAuthenticationOptions.ClientAppHeaderName, clientApp);
        return client;
    }
}
