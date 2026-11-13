using System.Net;
using PTL.ApiClient.Tests;

namespace PTL.ApiClient.Tests.Labels;

public class LabelPrinterApiClientTests
{
    private static LabelPrinterApiClient CreateClient(HttpStatusCode statusCode, string? jsonContent) =>
        new(new HttpClient(new StubHttpMessageHandler(statusCode, jsonContent)) { BaseAddress = new Uri("http://localhost") });

    [Fact]
    public async Task PrintAsync_SuccessResponseWithPrintedTrue_ReturnsTrue()
    {
        var client = CreateClient(HttpStatusCode.OK, """{"printed":true}""");

        var result = await client.PrintAsync(["Alice Example", "1 Sample Street"]);

        Assert.True(result);
    }

    [Fact]
    public async Task PrintAsync_SuccessResponseWithPrintedFalse_ReturnsFalse()
    {
        var client = CreateClient(HttpStatusCode.OK, """{"printed":false}""");

        var result = await client.PrintAsync(["Alice Example"]);

        Assert.False(result);
    }

    [Fact]
    public async Task PrintAsync_NonSuccessStatusCode_ReturnsFalse()
    {
        var client = CreateClient(HttpStatusCode.InternalServerError, null);

        var result = await client.PrintAsync(["Alice Example"]);

        Assert.False(result);
    }

    [Fact]
    public async Task PrintAsync_NullResponseBody_ReturnsFalse()
    {
        var client = CreateClient(HttpStatusCode.OK, "null");

        var result = await client.PrintAsync(["Alice Example"]);

        Assert.False(result);
    }
}
