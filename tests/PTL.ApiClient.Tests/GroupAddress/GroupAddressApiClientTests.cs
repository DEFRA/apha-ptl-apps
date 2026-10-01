using System.Net;
using PTL.ApiClient.Tests;
using PTL.Contracts.GroupAddress;

namespace PTL.ApiClient.Tests.GroupAddress;

public class GroupAddressApiClientTests
{
    private static GroupAddressApiClient CreateClient(HttpStatusCode statusCode, string? jsonContent) =>
        new(new HttpClient(new StubHttpMessageHandler(statusCode, jsonContent)) { BaseAddress = new Uri("http://localhost") });

    private const string SampleJson = """
        {"groupAddressId":"11111111-1111-1111-1111-111111111111","identifier":"PTL-001","address1":"1 Sample Street","address2":"Second Line","address3":"Third Line","address4":"Fourth Line","address5":"Fifth Line","countryId":"22222222-2222-2222-2222-222222222222","telephone":"020 1234 5678","packingInstructions":"Fragile"}
        """;

    [Fact]
    public async Task GetGroupAddressesAsync_ReturnsDeserializedList()
    {
        var client = CreateClient(HttpStatusCode.OK, $"[{SampleJson}]");

        var result = await client.GetGroupAddressesAsync();

        Assert.Single(result);
        Assert.Equal("PTL-001", result[0].Identifier);
        Assert.Equal("Fragile", result[0].PackingInstructions);
    }

    [Fact]
    public async Task GetGroupAddressesAsync_NullResponse_ReturnsEmptyList()
    {
        var client = CreateClient(HttpStatusCode.OK, "null");

        var result = await client.GetGroupAddressesAsync();

        Assert.Empty(result);
    }

    [Fact]
    public async Task SearchGroupAddressesAsync_ReturnsDeserializedResponse()
    {
        var json = $$"""{"items":[{{SampleJson}}],"totalCount":1,"page":1,"pageSize":20}""";
        var client = CreateClient(HttpStatusCode.OK, json);

        var result = await client.SearchGroupAddressesAsync(new GroupAddressSearchRequest(1, 20));

        Assert.Equal(1, result.TotalCount);
        Assert.Single(result.Items);
    }

    [Fact]
    public async Task SearchGroupAddressesAsync_NullResponse_ReturnsEmptyResult()
    {
        var client = CreateClient(HttpStatusCode.OK, "null");

        var result = await client.SearchGroupAddressesAsync(new GroupAddressSearchRequest(2, 10));

        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
        Assert.Equal(2, result.Page);
        Assert.Equal(10, result.PageSize);
    }

    [Fact]
    public async Task GetGroupAddressAsync_NotFound_ReturnsNull()
    {
        var client = CreateClient(HttpStatusCode.NotFound, null);

        var result = await client.GetGroupAddressAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task GetGroupAddressAsync_Found_ReturnsDeserialized()
    {
        var client = CreateClient(HttpStatusCode.OK, SampleJson);

        var result = await client.GetGroupAddressAsync(Guid.NewGuid());

        Assert.NotNull(result);
        Assert.Equal("PTL-001", result!.Identifier);
    }

    [Fact]
    public async Task CreateGroupAddressAsync_Success_ReturnsSaveResult()
    {
        var client = CreateClient(HttpStatusCode.Created, SampleJson);

        var result = await client.CreateGroupAddressAsync(new GroupAddressSaveRequest(
            "PTL-001", "1 Sample Street", "Second Line", "Third Line", "Fourth Line", "Fifth Line",
            Guid.NewGuid(), "020 1234 5678", "Fragile"));

        Assert.True(result.Success);
        Assert.NotNull(result.GroupAddress);
    }

    [Fact]
    public async Task CreateGroupAddressAsync_ValidationFailure_ReturnsFieldErrors()
    {
        const string problemJson = """{"errors":{"Identifier":["Identifier is required"]}}""";
        var client = CreateClient(HttpStatusCode.BadRequest, problemJson);

        var result = await client.CreateGroupAddressAsync(new GroupAddressSaveRequest(
            string.Empty, "1 Sample Street", "Second Line", "Third Line", "Fourth Line", "Fifth Line",
            Guid.NewGuid(), "020 1234 5678", "Fragile"));

        Assert.False(result.Success);
        Assert.Contains("Identifier", result.FieldErrors.Keys);
    }

    [Fact]
    public async Task UpdateGroupAddressAsync_NotFound_ReturnsFailureResult()
    {
        var client = CreateClient(HttpStatusCode.NotFound, null);

        var result = await client.UpdateGroupAddressAsync(Guid.NewGuid(), new GroupAddressSaveRequest(
            "PTL-001", "1 Sample Street", "Second Line", "Third Line", "Fourth Line", "Fifth Line",
            Guid.NewGuid(), "020 1234 5678", "Fragile"));

        Assert.False(result.Success);
    }

    [Fact]
    public async Task UpdateGroupAddressAsync_Success_ReturnsSaveResult()
    {
        var client = CreateClient(HttpStatusCode.OK, SampleJson);

        var result = await client.UpdateGroupAddressAsync(Guid.NewGuid(), new GroupAddressSaveRequest(
            "PTL-001", "1 Sample Street", "Second Line", "Third Line", "Fourth Line", "Fifth Line",
            Guid.NewGuid(), "020 1234 5678", "Fragile"));

        Assert.True(result.Success);
    }
}
