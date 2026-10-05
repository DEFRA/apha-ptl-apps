using System.Net;

namespace PTL.ApiClient.Tests;

// Minimal HttpMessageHandler stub so ApiClient can be tested without a real network call.
internal sealed class StubHttpMessageHandler(HttpStatusCode statusCode, string? jsonContent) : HttpMessageHandler
{
    private readonly Func<HttpResponseMessage>? _responseFactory;

    public StubHttpMessageHandler(Func<HttpResponseMessage> responseFactory) : this(HttpStatusCode.OK, null) =>
        _responseFactory = responseFactory;

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (_responseFactory is not null)
        {
            return Task.FromResult(_responseFactory());
        }

        var response = new HttpResponseMessage(statusCode);
        if (jsonContent is not null)
        {
            response.Content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");
        }

        return Task.FromResult(response);
    }
}
