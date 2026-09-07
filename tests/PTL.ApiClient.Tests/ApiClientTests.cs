using System.Net;

namespace PTL.ApiClient.Tests;

public class ApiClientTests
{
    [Fact]
    public async Task GetHealthAsync_ReturnsDeserializedResponse()
    {
        const string json = """{"status":"Healthy","uptimeSeconds":12.5,"timestampUtc":"2026-01-01T00:00:00Z"}""";
        var httpClient = new HttpClient(new StubHttpMessageHandler(HttpStatusCode.OK, json))
        {
            BaseAddress = new Uri("http://localhost")
        };
        var client = new ApiClient(httpClient);

        var result = await client.GetHealthAsync();

        Assert.NotNull(result);
        Assert.Equal("Healthy", result!.Status);
        Assert.Equal(12.5, result.UptimeSeconds);
        Assert.Equal(new DateTime(2026, 1, 1), result.TimestampUtc);
    }
}
