using System.Net;
using System.Text.Json;
using PTL.ApiClient.Distribution;
using PTL.Contracts.Distribution;

namespace PTL.ApiClient.Tests.Distribution;

// Participant Nil Return client calls - GET/PUT /api/distributions/schemes/{id}/participants/{id}/nil-return.
public class DistributionApiClientNilReturnTests
{
    private static readonly Guid SchemeId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Participant = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private static readonly string Url = $"/api/distributions/schemes/{SchemeId}/participants/{Participant}/nil-return";

    private sealed record Sent(HttpMethod Method, string PathAndQuery, string? Body);

    private static (DistributionApiClient Client, List<Sent> Requests) Create(HttpStatusCode statusCode, string? json)
    {
        var requests = new List<Sent>();
        var handler = new StubHttpMessageHandler(statusCode, json)
        {
            OnRequest = r => requests.Add(new(r.Method, r.RequestUri!.PathAndQuery, r.Content?.ReadAsStringAsync().GetAwaiter().GetResult())),
        };
        return (new DistributionApiClient(new HttpClient(handler) { BaseAddress = new Uri("http://localhost") }), requests);
    }

    [Fact]
    public async Task GetParticipantNilReturnAsync_RequestsTheParticipantAndDeserialises()
    {
        var (client, requests) = Create(HttpStatusCode.OK, """{"hasNilReturn":true,"nilReturnReason":"No samples received"}""");

        var result = await client.GetParticipantNilReturnAsync(SchemeId, Participant);

        var request = Assert.Single(requests);
        Assert.Equal((HttpMethod.Get, Url), (request.Method, request.PathAndQuery));
        Assert.Equal(new ParticipantNilReturnResponse(true, "No samples received"), result);
    }

    [Fact]
    public async Task GetParticipantNilReturnAsync_NotFound_ReturnsNull()
    {
        var (client, _) = Create(HttpStatusCode.NotFound, null);

        Assert.Null(await client.GetParticipantNilReturnAsync(SchemeId, Participant));
    }

    [Fact]
    public async Task GetParticipantNilReturnAsync_ServerError_Throws()
    {
        var (client, _) = Create(HttpStatusCode.InternalServerError, null);

        await Assert.ThrowsAsync<HttpRequestException>(() => client.GetParticipantNilReturnAsync(SchemeId, Participant));
    }

    [Fact]
    public async Task SaveParticipantNilReturnAsync_PutsTheReasonAndDeserialisesTheOutcome()
    {
        var (client, requests) = Create(HttpStatusCode.OK, """{"success":false,"deadlinePassed":true,"reasonRequired":true,"hasResults":true}""");

        var result = await client.SaveParticipantNilReturnAsync(SchemeId, Participant, new ParticipantNilReturnSaveRequest("Lost in post"));

        var request = Assert.Single(requests);
        Assert.Equal((HttpMethod.Put, Url), (request.Method, request.PathAndQuery));
        using var body = JsonDocument.Parse(request.Body!);
        Assert.Equal("Lost in post", body.RootElement.GetProperty("nilReturnReason").GetString());
        Assert.Equal(new ParticipantNilReturnSaveResult(false, true, true, true), result);
    }

    [Fact]
    public async Task SaveParticipantNilReturnAsync_AFlagsOmittedFromTheResponse_DefaultToFalse()
    {
        var (client, _) = Create(HttpStatusCode.OK, """{"success":true}""");

        var result = await client.SaveParticipantNilReturnAsync(SchemeId, Participant, new ParticipantNilReturnSaveRequest(null));

        Assert.Equal(new ParticipantNilReturnSaveResult(true), result);
        Assert.False(result!.DeadlinePassed);
        Assert.False(result.ReasonRequired);
        Assert.False(result.HasResults);
    }

    [Fact]
    public async Task SaveParticipantNilReturnAsync_NotFound_ReturnsNull()
    {
        var (client, _) = Create(HttpStatusCode.NotFound, null);

        Assert.Null(await client.SaveParticipantNilReturnAsync(SchemeId, Participant, new ParticipantNilReturnSaveRequest("x")));
    }

    [Fact]
    public async Task SaveParticipantNilReturnAsync_ServerError_Throws()
    {
        var (client, _) = Create(HttpStatusCode.InternalServerError, null);

        await Assert.ThrowsAsync<HttpRequestException>(
            () => client.SaveParticipantNilReturnAsync(SchemeId, Participant, new ParticipantNilReturnSaveRequest("x")));
    }
}
