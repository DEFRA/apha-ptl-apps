using System.Net;
using PTL.ApiClient;
using PTL.ApiClient.Tests;
using PTL.Contracts.Contract;

namespace PTL.ApiClient.Tests.Contract;

public class ContractApiClientTests
{
    private static ContractApiClient CreateClient(HttpStatusCode statusCode, string? jsonContent) =>
        new(new HttpClient(new StubHttpMessageHandler(statusCode, jsonContent)) { BaseAddress = new Uri("http://localhost") });

    [Fact]
    public async Task GetContractAsync_NotFound_ReturnsNull()
    {
        var client = CreateClient(HttpStatusCode.NotFound, null);

        var result = await client.GetContractAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task GetContractsForCustomerAsync_ReturnsDeserializedSearchResponse()
    {
        const string json = """{"items":[],"totalCount":0,"page":1,"pageSize":20}""";
        var client = CreateClient(HttpStatusCode.OK, json);

        var result = await client.GetContractsForCustomerAsync(Guid.NewGuid(), new ContractSearchRequest());

        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
    }

    [Fact]
    public async Task CreateContractAsync_ValidationFailure_ReturnsFieldErrors()
    {
        const string json = """{"errors":{"UTNumber":["Enter either a UT number or an FT number, but not both."]}}""";
        var client = CreateClient(HttpStatusCode.BadRequest, json);

        var result = await client.CreateContractAsync(Guid.NewGuid(), MinimalCreateRequest());

        Assert.False(result.Success);
        Assert.True(result.FieldErrors.ContainsKey("UTNumber"));
    }

    [Fact]
    public async Task UpdateContractAsync_NotFound_ReturnsFailureResult()
    {
        var client = CreateClient(HttpStatusCode.NotFound, null);

        var result = await client.UpdateContractAsync(Guid.NewGuid(), MinimalUpdateRequest());

        Assert.False(result.Success);
        Assert.Null(result.Contract);
    }

    private static CreateContractRequest MinimalCreateRequest() => new(
        2027, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, 0, 0, 0, 0, 0, 0, 0, 0,
        DateTime.UtcNow, DateTime.UtcNow, DateTime.UtcNow, string.Empty, DateTime.UtcNow, true, string.Empty, string.Empty, false, false);

    private static UpdateContractRequest MinimalUpdateRequest() => new(
        2027, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, 0, 0, 0, 0, 0, 0, 0, 0,
        DateTime.UtcNow, DateTime.UtcNow, DateTime.UtcNow, string.Empty, DateTime.UtcNow, true, string.Empty, string.Empty, false, false);
}
