using System.Net;
using PTL.ApiClient;
using PTL.Contracts.Participant;

namespace PTL.ApiClient.Tests.Participant;

public class PendingParticipantUpdateApiClientTests
{
    private static ParticipantApiClient CreateClient(HttpStatusCode statusCode, string? jsonContent) =>
        new(new HttpClient(new StubHttpMessageHandler(statusCode, jsonContent)) { BaseAddress = new Uri("http://localhost") });

    [Fact]
    public async Task GetPendingParticipantUpdatesAsync_ReturnsDeserializedList()
    {
        const string json = """[{"participantId":"11111111-1111-1111-1111-111111111111","labCode":"001","labName":"Alpha Lab"}]""";
        var client = CreateClient(HttpStatusCode.OK, json);

        var result = await client.GetPendingParticipantUpdatesAsync();

        Assert.Single(result);
        Assert.Equal("Alpha Lab", result[0].LabName);
    }

    [Fact]
    public async Task GetPendingParticipantUpdatesAsync_NullResponse_ReturnsEmptyList()
    {
        var client = CreateClient(HttpStatusCode.OK, "null");

        var result = await client.GetPendingParticipantUpdatesAsync();

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetPendingParticipantUpdateAsync_NotFound_ReturnsNull()
    {
        var client = CreateClient(HttpStatusCode.NotFound, null);

        var result = await client.GetPendingParticipantUpdateAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task GetPendingParticipantUpdateAsync_Found_ReturnsComparison()
    {
        const string json = """{"current":{"participantId":"11111111-1111-1111-1111-111111111111","labCode":"001","labName":"Alpha Lab","contactName":"Alice Example"},"pending":{"participantId":"11111111-1111-1111-1111-111111111111","customerId":"22222222-2222-2222-2222-222222222222","labCode":"001","contactName":"New Contact","organisation":"","address1":"","address2":"","address3":"","address4":"","address5":"","countryId":"00000000-0000-0000-0000-000000000000","telephone":"","fax":"","email":"","email2":""}}""";
        var client = CreateClient(HttpStatusCode.OK, json);

        var result = await client.GetPendingParticipantUpdateAsync(Guid.NewGuid());

        Assert.NotNull(result);
        Assert.Equal("Alpha Lab", result.Current.LabName);
        Assert.Equal("New Contact", result.Pending.ContactName);
    }

    [Fact]
    public async Task ApprovePendingParticipantUpdateAsync_NotFound_ReturnsNotFoundResult()
    {
        var client = CreateClient(HttpStatusCode.NotFound, null);

        var result = await client.ApprovePendingParticipantUpdateAsync(Guid.NewGuid());

        Assert.False(result.Success);
        Assert.True(result.NotFound);
    }

    [Fact]
    public async Task ApprovePendingParticipantUpdateAsync_Success_ReturnsSuccessResult()
    {
        var client = CreateClient(HttpStatusCode.NoContent, null);

        var result = await client.ApprovePendingParticipantUpdateAsync(Guid.NewGuid());

        Assert.True(result.Success);
        Assert.Empty(result.FieldErrors);
    }

    [Fact]
    public async Task ApprovePendingParticipantUpdateAsync_ValidationFailure_ReturnsFieldErrors()
    {
        const string json = """{"errors":{"ContactName":["Contact name is required."]}}""";
        var client = CreateClient(HttpStatusCode.BadRequest, json);

        var result = await client.ApprovePendingParticipantUpdateAsync(Guid.NewGuid());

        Assert.False(result.Success);
        Assert.True(result.FieldErrors.ContainsKey("ContactName"));
    }

    [Fact]
    public async Task ApprovePendingParticipantUpdateAsync_BadRequestWithNoErrors_ReturnsDefaultError()
    {
        const string json = """{"errors":{}}""";
        var client = CreateClient(HttpStatusCode.BadRequest, json);

        var result = await client.ApprovePendingParticipantUpdateAsync(Guid.NewGuid(), AmendedRequest());

        Assert.False(result.Success);
        Assert.Equal("The request was invalid.", result.FieldErrors[string.Empty][0]);
    }

    [Fact]
    public async Task DeclinePendingParticipantUpdateAsync_NotFound_ReturnsFalse()
    {
        var client = CreateClient(HttpStatusCode.NotFound, null);

        var result = await client.DeclinePendingParticipantUpdateAsync(Guid.NewGuid());

        Assert.False(result);
    }

    [Fact]
    public async Task DeclinePendingParticipantUpdateAsync_Success_ReturnsTrue()
    {
        var client = CreateClient(HttpStatusCode.NoContent, null);

        var result = await client.DeclinePendingParticipantUpdateAsync(Guid.NewGuid());

        Assert.True(result);
    }

    private static PendingParticipantUpdateSaveRequest AmendedRequest() => new(
        "New Contact", "New Organisation", "Address 1", "Address 2", string.Empty, string.Empty, string.Empty,
        Guid.NewGuid(), "01234 567890", string.Empty, "new@example.com", string.Empty);
}
