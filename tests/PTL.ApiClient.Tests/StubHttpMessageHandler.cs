using System.Net;

namespace PTL.ApiClient.Tests;

// Minimal HttpMessageHandler stub so ApiClient can be tested without a real network call.
internal sealed class StubHttpMessageHandler(HttpStatusCode statusCode, string? jsonContent) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = new HttpResponseMessage(statusCode);
        if (jsonContent is not null)
        {
            response.Content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");
        }

        return Task.FromResult(response);
    }
}
