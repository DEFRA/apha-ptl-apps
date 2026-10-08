using System.Net;
using PTL.ApiClient.Tests;

namespace PTL.ApiClient.Tests.SystemMessage;

public class SystemMessageApiClientTests
{
    private static SystemMessageApiClient CreateClient(HttpStatusCode statusCode, string? jsonContent) =>
        new(new HttpClient(new StubHttpMessageHandler(statusCode, jsonContent)) { BaseAddress = new Uri("http://localhost") });

    [Fact]
    public async Task GetImportantMessageAsync_ReturnsDeserializedResponse()
    {
        const string json = """{"importantMessage":"<p>Planned maintenance</p>"}""";
        var client = CreateClient(HttpStatusCode.OK, json);

        var result = await client.GetImportantMessageAsync();

        Assert.Equal("<p>Planned maintenance</p>", result.ImportantMessage);
    }

    [Fact]
    public async Task GetImportantMessageAsync_NullResponse_ReturnsFallbackWithNullMessage()
    {
        var client = CreateClient(HttpStatusCode.OK, "null");

        var result = await client.GetImportantMessageAsync();

        Assert.Null(result.ImportantMessage);
    }

    [Fact]
    public async Task GetImportantMessageAsync_ErrorStatusCode_Throws()
    {
        var client = CreateClient(HttpStatusCode.InternalServerError, null);

        await Assert.ThrowsAsync<HttpRequestException>(() => client.GetImportantMessageAsync());
    }
}
