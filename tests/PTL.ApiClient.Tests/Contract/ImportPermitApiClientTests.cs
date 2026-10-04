using System.Net;
using System.Net.Http.Json;
using PTL.ApiClient.Tests;
using PTL.Contracts.Contract;

namespace PTL.ApiClient.Tests.Contract;

public class ImportPermitApiClientTests
{
    private static ImportPermitApiClient CreateClient(HttpStatusCode statusCode, string? jsonContent) =>
        new(new HttpClient(new StubHttpMessageHandler(statusCode, jsonContent)) { BaseAddress = new Uri("http://localhost") });

    [Fact]
    public async Task GetImportPermitsAsync_ReturnsDeserializedList()
    {
        const string json = """[{"participantSchemeId":"11111111-1111-1111-1111-111111111111","schemeNumber":"PT0001","schemeName":"AHS","labId":"1476","importPermitRequired":true,"importPermitReceived":false,"importPermitExpiry":null}]""";
        var client = CreateClient(HttpStatusCode.OK, json);

        var result = await client.GetImportPermitsAsync(Guid.NewGuid());

        Assert.Single(result);
        Assert.Equal("PT0001", result[0].SchemeNumber);
    }

    [Fact]
    public async Task GetImportPermitsAsync_NullResponse_ReturnsEmptyList()
    {
        var client = CreateClient(HttpStatusCode.OK, "null");

        var result = await client.GetImportPermitsAsync(Guid.NewGuid());

        Assert.Empty(result);
    }

    [Fact]
    public async Task UpdateImportPermitAsync_Success_ReturnsTrue()
    {
        var client = CreateClient(HttpStatusCode.NoContent, null);

        var result = await client.UpdateImportPermitAsync(Guid.NewGuid(), new UpdateImportPermitRequest(true, DateTime.UtcNow));

        Assert.True(result);
    }

    [Fact]
    public async Task UpdateImportPermitAsync_Failure_ReturnsFalse()
    {
        var client = CreateClient(HttpStatusCode.BadRequest, """{"errors":{"ImportPermitExpiry":["Import permit expiry date is required when permit is received."]}}""");

        var result = await client.UpdateImportPermitAsync(Guid.NewGuid(), new UpdateImportPermitRequest(true, null));

        Assert.False(result);
    }
}
