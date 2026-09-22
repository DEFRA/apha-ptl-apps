using System.Net;
using System.Net.Http.Json;
using PTL.ApiClient;
using PTL.ApiClient.Tests;
using PTL.Contracts.Participant;

namespace PTL.ApiClient.Tests.Participant;

public class ParticipantApiClientTests
{
    private static ParticipantApiClient CreateClient(HttpStatusCode statusCode, string? jsonContent) =>
        new(new HttpClient(new StubHttpMessageHandler(statusCode, jsonContent)) { BaseAddress = new Uri("http://localhost") });

    [Fact]
    public async Task GetParticipantsAsync_ReturnsDeserializedList()
    {
        const string json = """[{"participantId":"22222222-2222-2222-2222-222222222222","ssoId":"33333333-3333-3333-3333-333333333333","customerId":"11111111-1111-1111-1111-111111111111","labCode":"LAB001","labName":"Lab 1","labTypeId":"44444444-4444-4444-4444-444444444444","contactName":"John","organisation":"Org","address1":"St 1","address2":"Town","address3":"","address4":"","address5":"","countryId":"55555555-5555-5555-5555-555555555555","telephone":"01234567890","fax":"","email":"lab@example.com","email2":"","comments":"","isActive":true,"inactiveDate":null,"inactiveError":false,"inactiveErrorDate":null}]""";
        var client = CreateClient(HttpStatusCode.OK, json);

        var result = await client.GetParticipantsAsync(Guid.NewGuid());

        Assert.Single(result);
        Assert.Equal("Lab 1", result[0].LabName);
        Assert.True(result[0].IsActive);
    }

    [Fact]
    public async Task GetParticipantsAsync_WithIncludeInactive_ReturnsDeserializedList()
    {
        const string json = """[{"participantId":"22222222-2222-2222-2222-222222222222","ssoId":"33333333-3333-3333-3333-333333333333","customerId":"11111111-1111-1111-1111-111111111111","labCode":"LAB001","labName":"Lab 1","labTypeId":"44444444-4444-4444-4444-444444444444","contactName":"John","organisation":"Org","address1":"St 1","address2":"Town","address3":"","address4":"","address5":"","countryId":"55555555-5555-5555-5555-555555555555","telephone":"01234567890","fax":"","email":"lab@example.com","email2":"","comments":"","isActive":false,"inactiveDate":"2025-01-01T00:00:00Z","inactiveError":false,"inactiveErrorDate":null}]""";
        var client = CreateClient(HttpStatusCode.OK, json);

        var result = await client.GetParticipantsAsync(Guid.NewGuid(), includeInactive: true);

        Assert.Single(result);
        Assert.False(result[0].IsActive);
    }

    [Fact]
    public async Task GetParticipantsAsync_EmptyResponse_ReturnsEmptyList()
    {
        const string json = """[]""";
        var client = CreateClient(HttpStatusCode.OK, json);

        var result = await client.GetParticipantsAsync(Guid.NewGuid());

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetParticipantsAsync_NullResponse_ReturnsEmptyList()
    {
        var client = CreateClient(HttpStatusCode.OK, "null");

        var result = await client.GetParticipantsAsync(Guid.NewGuid());

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetParticipantAsync_Found_ReturnsDeserializedResponse()
    {
        const string json = """{"participantId":"22222222-2222-2222-2222-222222222222","ssoId":"33333333-3333-3333-3333-333333333333","customerId":"11111111-1111-1111-1111-111111111111","labCode":"LAB001","labName":"Lab 1","labTypeId":"44444444-4444-4444-4444-444444444444","contactName":"John","organisation":"Org","address1":"St 1","address2":"Town","address3":"","address4":"","address5":"","countryId":"55555555-5555-5555-5555-555555555555","telephone":"01234567890","fax":"","email":"lab@example.com","email2":"","comments":"","isActive":true,"inactiveDate":null,"inactiveError":false,"inactiveErrorDate":null}""";
        var client = CreateClient(HttpStatusCode.OK, json);

        var result = await client.GetParticipantAsync(Guid.NewGuid());

        Assert.NotNull(result);
        Assert.Equal("Lab 1", result.LabName);
    }

    [Fact]
    public async Task GetParticipantAsync_NotFound_ReturnsNull()
    {
        var client = CreateClient(HttpStatusCode.NotFound, null);

        var result = await client.GetParticipantAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task SearchParticipantsAsync_ReturnsDeserializedSearchResponse()
    {
        const string json = """{"items":[],"totalCount":0,"page":1,"pageSize":20}""";
        var client = CreateClient(HttpStatusCode.OK, json);

        var result = await client.SearchParticipantsAsync(new ParticipantSearchRequest(Guid.NewGuid(), "test", false, 1, 20));

        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
        Assert.Equal(1, result.Page);
    }

    [Fact]
    public async Task SearchParticipantsAsync_WithResults_ReturnsDeserializedSearchResponse()
    {
        const string json = """{"items":[{"participantId":"22222222-2222-2222-2222-222222222222","labCode":"LAB001","labName":"Lab 1","isActive":true}],"totalCount":1,"page":1,"pageSize":20}""";
        var client = CreateClient(HttpStatusCode.OK, json);

        var result = await client.SearchParticipantsAsync(new ParticipantSearchRequest(Guid.NewGuid(), "test", false, 1, 20));

        Assert.Single(result.Items);
        Assert.Equal(1, result.TotalCount);
    }

    [Fact]
    public async Task SearchParticipantsAsync_NullResponse_ReturnsDefaultWithQueryParams()
    {
        var client = CreateClient(HttpStatusCode.OK, "null");

        var result = await client.SearchParticipantsAsync(new ParticipantSearchRequest(Guid.NewGuid(), "test", false, 2, 50));

        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
        Assert.Equal(2, result.Page);
        Assert.Equal(50, result.PageSize);
    }

    [Fact]
    public async Task CreateParticipantAsync_Success_ReturnsDeserializedResponse()
    {
        const string json = """{"participantId":"22222222-2222-2222-2222-222222222222","ssoId":"33333333-3333-3333-3333-333333333333","customerId":"11111111-1111-1111-1111-111111111111","labCode":"LAB001","labName":"Lab 1","labTypeId":"44444444-4444-4444-4444-444444444444","contactName":"John","organisation":"Org","address1":"St 1","address2":"Town","address3":"","address4":"","address5":"","countryId":"55555555-5555-5555-5555-555555555555","telephone":"01234567890","fax":"","email":"lab@example.com","email2":"","comments":"","isActive":true,"inactiveDate":null,"inactiveError":false,"inactiveErrorDate":null}""";
        var client = CreateClient(HttpStatusCode.Created, json);

        var result = await client.CreateParticipantAsync(MinimalCreateRequest());

        Assert.Equal("22222222-2222-2222-2222-222222222222", result.ParticipantId.ToString());
        Assert.Equal("Lab 1", result.LabName);
    }

    [Fact]
    public async Task CreateParticipantAsync_EmptyResponse_Throws()
    {
        var client = CreateClient(HttpStatusCode.Created, "null");

        await Assert.ThrowsAsync<InvalidOperationException>(() => client.CreateParticipantAsync(MinimalCreateRequest()));
    }

    [Fact]
    public async Task CreateParticipantAsync_ServerError_Throws()
    {
        var client = CreateClient(HttpStatusCode.InternalServerError, null);

        await Assert.ThrowsAsync<HttpRequestException>(() => client.CreateParticipantAsync(MinimalCreateRequest()));
    }

    [Fact]
    public async Task UpdateParticipantAsync_Success_ReturnsDeserializedResponse()
    {
        const string json = """{"participantId":"22222222-2222-2222-2222-222222222222","ssoId":"33333333-3333-3333-3333-333333333333","customerId":"11111111-1111-1111-1111-111111111111","labCode":"LAB001","labName":"Lab 1","labTypeId":"44444444-4444-4444-4444-444444444444","contactName":"John","organisation":"Org","address1":"St 1","address2":"Town","address3":"","address4":"","address5":"","countryId":"55555555-5555-5555-5555-555555555555","telephone":"01234567890","fax":"","email":"lab@example.com","email2":"","comments":"","isActive":true,"inactiveDate":null,"inactiveError":false,"inactiveErrorDate":null}""";
        var client = CreateClient(HttpStatusCode.OK, json);

        var result = await client.UpdateParticipantAsync(Guid.NewGuid(), MinimalUpdateRequest());

        Assert.NotNull(result);
        Assert.Equal("Lab 1", result.LabName);
    }

    [Fact]
    public async Task UpdateParticipantAsync_NotFound_ReturnsNull()
    {
        var client = CreateClient(HttpStatusCode.NotFound, null);

        var result = await client.UpdateParticipantAsync(Guid.NewGuid(), MinimalUpdateRequest());

        Assert.Null(result);
    }

    [Fact]
    public async Task UpdateParticipantAsync_ServerError_Throws()
    {
        var client = CreateClient(HttpStatusCode.InternalServerError, null);

        await Assert.ThrowsAsync<HttpRequestException>(() => client.UpdateParticipantAsync(Guid.NewGuid(), MinimalUpdateRequest()));
    }

    private static ParticipantRequest MinimalCreateRequest() => new(
        Guid.NewGuid(), Guid.NewGuid(), "LAB001", "Lab 1", Guid.NewGuid(), "John", "Org",
        "Address 1", "Address 2", string.Empty, string.Empty, string.Empty, Guid.NewGuid(),
        "01234567890", string.Empty, "lab@example.com", string.Empty, string.Empty, true);

    private static ParticipantRequest MinimalUpdateRequest() => new(
        Guid.NewGuid(), Guid.NewGuid(), "LAB001", "Lab 1", Guid.NewGuid(), "John", "Org",
        "Address 1", "Address 2", string.Empty, string.Empty, string.Empty, Guid.NewGuid(),
        "01234567890", string.Empty, "lab@example.com", string.Empty, string.Empty, true);
}
