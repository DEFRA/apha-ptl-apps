using System.Net;
using System.Text.Json;
using PTL.ApiClient.Distribution;
using PTL.Contracts.Distribution;

namespace PTL.ApiClient.Tests.Distribution;

// Send-tabulation client calls - /api/distributions/months/{y}/{m}/tabulations[/preview|/stage].
public class DistributionApiClientTabulationSendTests
{
    private static readonly Guid SchemeId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private const string Base = "/api/distributions/months/2026/5/tabulations";

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

    private static readonly IReadOnlyList<TabulationStagedSendRequest> Staged = [new(SchemeId, "SendToTC")];

    private static void AssertStagedBody(string? body)
    {
        var staged = JsonDocument.Parse(body!).RootElement.GetProperty("staged");
        Assert.Equal(1, staged.GetArrayLength());
        Assert.Equal(SchemeId, staged[0].GetProperty("monthlyDistributionSchemeId").GetGuid());
        Assert.Equal("SendToTC", staged[0].GetProperty("command").GetString());
    }

    [Fact]
    public async Task PreviewTabulationsAsync_PostsTheStagedSendsAndDeserialises()
    {
        var (client, requests) = Create(HttpStatusCode.OK, """{"yearId":2026,"monthId":5,"schemes":[]}""");

        var page = await client.PreviewTabulationsAsync(2026, 5, Staged);

        var request = Assert.Single(requests);
        Assert.Equal((HttpMethod.Post, $"{Base}/preview"), (request.Method, request.PathAndQuery));
        AssertStagedBody(request.Body);
        Assert.Equal((2026, 5), (page!.YearId, page.MonthId));
        Assert.Empty(page.Schemes);
    }

    [Fact]
    public async Task PreviewTabulationsAsync_NotFound_ReturnsNull()
    {
        var (client, _) = Create(HttpStatusCode.NotFound, null);

        Assert.Null(await client.PreviewTabulationsAsync(2026, 5, Staged));
    }

    [Fact]
    public async Task StageTabulationSendAsync_PostsTheRequestAndDeserialises()
    {
        var json = $$"""
            {"monthlyDistributionSchemeId":"{{SchemeId}}","schemeNameFull":"BT: Brucella","distributionReferenceFull":"D1a/S1",
             "command":"SendToTC","actionText":"Send to TC","confirmationQuestion":"Are you sure?"}
            """;
        var (client, requests) = Create(HttpStatusCode.OK, json);

        var result = await client.StageTabulationSendAsync(2026, 5, new TabulationStageRequest(Staged, SchemeId, "SendToAssessor"));

        var request = Assert.Single(requests);
        Assert.Equal((HttpMethod.Post, $"{Base}/stage"), (request.Method, request.PathAndQuery));
        var body = JsonDocument.Parse(request.Body!).RootElement;
        Assert.Equal("SendToAssessor", body.GetProperty("command").GetString());
        AssertStagedBody(request.Body);
        Assert.Equal(new TabulationSendResponse(SchemeId, "BT: Brucella", "D1a/S1", "SendToTC", "Send to TC", "Are you sure?"), result);
    }

    [Fact]
    public async Task StageTabulationSendAsync_NotFound_ReturnsNull()
    {
        var (client, _) = Create(HttpStatusCode.NotFound, null);

        Assert.Null(await client.StageTabulationSendAsync(2026, 5, new TabulationStageRequest([], SchemeId, "SendToTC")));
    }

    [Fact]
    public async Task SaveTabulationsAsync_NoContent_PutsTheStagedSendsAndReturnsTrue()
    {
        var (client, requests) = Create(HttpStatusCode.NoContent, null);

        Assert.True(await client.SaveTabulationsAsync(2026, 5, Staged));

        var request = Assert.Single(requests);
        Assert.Equal((HttpMethod.Put, Base), (request.Method, request.PathAndQuery));
        AssertStagedBody(request.Body);
    }

    [Fact]
    public async Task SaveTabulationsAsync_Conflict_ReturnsFalse()
    {
        var (client, _) = Create(HttpStatusCode.Conflict, null);

        Assert.False(await client.SaveTabulationsAsync(2026, 5, Staged));
    }

    [Fact]
    public async Task SaveTabulationsAsync_ServerError_Throws()
    {
        var (client, _) = Create(HttpStatusCode.InternalServerError, null);

        await Assert.ThrowsAsync<HttpRequestException>(() => client.SaveTabulationsAsync(2026, 5, Staged));
    }
}
