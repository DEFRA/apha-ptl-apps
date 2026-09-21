using System.Net;
using PTL.ApiClient;
using PTL.ApiClient.Tests;
using PTL.Contracts.Scheme;

namespace PTL.ApiClient.Tests.Scheme;

public class SchemeApiClientTests
{
    private static SchemeApiClient CreateClient(HttpStatusCode statusCode, string? jsonContent) =>
        new(new HttpClient(new StubHttpMessageHandler(statusCode, jsonContent)) { BaseAddress = new Uri("http://localhost") });

    [Fact]
    public async Task GetSchemeAsync_NotFound_ReturnsNull()
    {
        var client = CreateClient(HttpStatusCode.NotFound, null);

        var result = await client.GetSchemeAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task GetSchemesForYearAsync_ReturnsDeserializedSearchResponse()
    {
        const string json = "{\"items\":[],\"totalCount\":0,\"page\":1,\"pageSize\":20}";
        var client = CreateClient(HttpStatusCode.OK, json);

        var result = await client.GetSchemesForYearAsync(new SchemeSearchRequest(2027));

        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
    }

    [Fact]
    public async Task GetSchemeHistoryAsync_ReturnsDeserializedList()
    {
        const string json = "[]";
        var client = CreateClient(HttpStatusCode.OK, json);

        var result = await client.GetSchemeHistoryAsync(Guid.NewGuid());

        Assert.Empty(result);
    }

    [Fact]
    public async Task CreateSchemeAsync_ValidationFailure_ReturnsFieldErrors()
    {
        const string json = "{\"errors\":{\"Identifier\":[\"Identifier must match the format PT followed by 4 digits (e.g. PT1234).\"]}}";
        var client = CreateClient(HttpStatusCode.BadRequest, json);

        var result = await client.CreateSchemeAsync(MinimalCreateRequest());

        Assert.False(result.Success);
        Assert.True(result.FieldErrors.ContainsKey("Identifier"));
    }

    [Fact]
    public async Task UpdateSchemeAsync_NotFound_ReturnsFailureResult()
    {
        var client = CreateClient(HttpStatusCode.NotFound, null);

        var result = await client.UpdateSchemeAsync(Guid.NewGuid(), MinimalUpdateRequest());

        Assert.False(result.Success);
        Assert.Null(result.Scheme);
    }

    private static SchemeRequest MinimalCreateRequest() => new(
        2027, "PT1234", "Test Scheme", Guid.NewGuid(), Guid.NewGuid(), null,
        true, false, false, false, false, false, false, false, false, false, false, false, false,
        0, Guid.NewGuid(), 5, "UK", 10, string.Empty, false, null, null, string.Empty,
        false, false, false, false, false, false, false,
        "Biological samples", false, null, "Instructions", false, false, false,
        null, null, null, null, false, false, null, null, null, null, null);

    private static SchemeRequest MinimalUpdateRequest() => MinimalCreateRequest();
}
