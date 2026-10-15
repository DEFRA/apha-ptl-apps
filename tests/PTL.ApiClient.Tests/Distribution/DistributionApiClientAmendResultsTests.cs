using System.Net;
using System.Text.Json;
using PTL.ApiClient.Distribution;
using PTL.Contracts.Distribution;

namespace PTL.ApiClient.Tests.Distribution;

// Amend Results client calls - GET/PUT /api/distributions/schemes/{id}/amend-results.
public class DistributionApiClientAmendResultsTests
{
    private static readonly Guid SchemeId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Participant = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid Sample = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid Rating = Guid.Parse("44444444-4444-4444-4444-444444444444");

    private static readonly string Url = $"/api/distributions/schemes/{SchemeId}/amend-results";

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

    private static AmendResultsSaveRequest Request(string? auditWhy) =>
        new([], [], [], [], [], [], [], [new AmendNilReturnRequest(Participant, "Lost")], [new AmendRatingRequest(Participant, Sample, Rating)], auditWhy);

    [Fact]
    public async Task GetAmendResultsAsync_RequestsTheSchemeAndDeserialises()
    {
        var json = $$"""
            {"monthlyDistributionSchemeId":"{{SchemeId}}","distributionTitle":"D1a/S1 BT: Brucella","requiresAssessment":true,"storeRatings":true,
             "tests":[],"participants":[{"distributionParticipantId":"{{Participant}}","labCode":"L001","nilReturnReason":"Lost","hasResults":false,
               "hasRatingResults":false,"methodResults":[],"resultItemResults":[],"categoryResults":[],"assessments":[],"ratings":[]}],
             "ratingOptions":[{"ratingId":"{{Rating}}","rating":"2"}],"ratingSamples":[{"sampleId":"{{Sample}}","sampleNumber":"S01"}],
             "statistics":[{"sampleId":"{{Sample}}","meanScore":"2.0","labsReporting":"1","participantCount":"1","percentageCorrect":"100.0%"}]}
            """;
        var (client, requests) = Create(HttpStatusCode.OK, json);

        var page = await client.GetAmendResultsAsync(SchemeId);

        var request = Assert.Single(requests);
        Assert.Equal((HttpMethod.Get, Url), (request.Method, request.PathAndQuery));
        Assert.NotNull(page);
        Assert.Equal(("D1a/S1 BT: Brucella", true, true), (page.DistributionTitle, page.RequiresAssessment, page.StoreRatings));
        Assert.Equal(("L001", "Lost"), (page.Participants[0].LabCode, page.Participants[0].NilReturnReason));
        Assert.Equal(new AmendRatingOptionResponse(Rating, "2"), Assert.Single(page.RatingOptions));
        Assert.Equal(new AmendSampleStatisticsResponse(Sample, "2.0", "1", "1", "100.0%"), Assert.Single(page.Statistics));
    }

    [Fact]
    public async Task GetAmendResultsAsync_NotFound_ReturnsNull()
    {
        var (client, _) = Create(HttpStatusCode.NotFound, null);

        Assert.Null(await client.GetAmendResultsAsync(SchemeId));
    }

    [Fact]
    public async Task SaveAmendResultsAsync_PutsTheGridAndReadsTheResult()
    {
        var key = AmendResultsFieldKeys.NilReturn(Participant);
        var json = $$$"""{"success":false,"auditReasonRequired":false,"auditObjectName":"","fieldErrors":{"{{{key}}}":"Input must be of format: Less than 100 characters"}}""";
        var (client, requests) = Create(HttpStatusCode.OK, json);

        var result = await client.SaveAmendResultsAsync(SchemeId, Request("Typo"));

        var request = Assert.Single(requests);
        Assert.Equal((HttpMethod.Put, Url), (request.Method, request.PathAndQuery));
        var body = JsonDocument.Parse(request.Body!).RootElement;
        Assert.Equal("Typo", body.GetProperty("auditWhy").GetString());
        Assert.Equal("Lost", body.GetProperty("nilReturns")[0].GetProperty("nilReturnReason").GetString());
        Assert.Equal(Rating, body.GetProperty("ratings")[0].GetProperty("ratingId").GetGuid());
        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.Equal("Input must be of format: Less than 100 characters", result.FieldErrors[key]);
    }

    [Fact]
    public async Task SaveAmendResultsAsync_AuditReasonRequired_IsDeserialised()
    {
        var (client, _) = Create(HttpStatusCode.OK, """{"success":false,"auditReasonRequired":true,"auditObjectName":"Amended Results for D1a/S1 BT: Brucella","fieldErrors":{}}""");

        var result = await client.SaveAmendResultsAsync(SchemeId, Request(null));

        Assert.True(result!.AuditReasonRequired);
        Assert.Equal("Amended Results for D1a/S1 BT: Brucella", result.AuditObjectName);
    }

    [Fact]
    public async Task SaveAmendResultsAsync_NotFound_ReturnsNull()
    {
        var (client, _) = Create(HttpStatusCode.NotFound, null);

        Assert.Null(await client.SaveAmendResultsAsync(SchemeId, Request(null)));
    }

    [Fact]
    public async Task SaveAmendResultsAsync_ServerError_Throws()
    {
        var (client, _) = Create(HttpStatusCode.InternalServerError, null);

        await Assert.ThrowsAsync<HttpRequestException>(() => client.SaveAmendResultsAsync(SchemeId, Request(null)));
    }
}
