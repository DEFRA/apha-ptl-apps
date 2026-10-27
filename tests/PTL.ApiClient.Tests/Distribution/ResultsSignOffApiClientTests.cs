using System.Net;
using PTL.ApiClient.Distribution;
using PTL.Contracts.Distribution;

namespace PTL.ApiClient.Tests.Distribution;

// Results Sign-off queue call - /api/results-sign-off/queue.
public class ResultsSignOffApiClientTests
{
    private static readonly Guid SchemeId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid TabulationId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private static (ResultsSignOffApiClient Client, List<(HttpMethod Method, string PathAndQuery)> Requests) Create(HttpStatusCode statusCode, string? json)
    {
        var requests = new List<(HttpMethod, string)>();
        var handler = new StubHttpMessageHandler(statusCode, json) { OnRequest = r => requests.Add((r.Method, r.RequestUri!.PathAndQuery)) };
        return (new ResultsSignOffApiClient(new HttpClient(handler) { BaseAddress = new Uri("http://localhost") }), requests);
    }

    [Fact]
    public async Task GetQueueAsync_RequestsTheQueueAndDeserialises()
    {
        var json = $$"""
            {"months":[{"yearId":2026,"monthId":6,"schemes":[{"monthlyDistributionSchemeId":"{{SchemeId}}","distributionTitle":"17208/BA/PT0074 PM: Pasteurella",
            "targetDate":"2026-07-21T00:00:00","testConsultants":"Jane Smith","assessors":"Ann Assessor","tabulations":[{"tabulationId":"{{TabulationId}}",
            "name":"FINAL REPORT","publishCount":2,"isSingleParticipant":true,"availability":"All","status":"Awaiting Sign-off"}]}]}]}
            """;
        var (client, requests) = Create(HttpStatusCode.OK, json);

        var queue = await client.GetQueueAsync();

        Assert.Equal((HttpMethod.Get, "/api/results-sign-off/queue"), Assert.Single(requests));
        var scheme = Assert.Single(Assert.Single(queue.Months).Schemes);
        Assert.Equal((SchemeId, "Jane Smith", "Ann Assessor"), (scheme.MonthlyDistributionSchemeId, scheme.TestConsultants, scheme.Assessors));
        var tabulation = Assert.Single(scheme.Tabulations);
        Assert.Equal((TabulationId, "FINAL REPORT", 2, true, "All", "Awaiting Sign-off"),
            (tabulation.TabulationId, tabulation.Name, tabulation.PublishCount, tabulation.IsSingleParticipant, tabulation.Availability, tabulation.Status));
    }

    [Fact]
    public async Task GetQueueAsync_EmptyBody_ReturnsAnEmptyQueue()
    {
        var (client, _) = Create(HttpStatusCode.OK, "null");

        Assert.Empty((await client.GetQueueAsync()).Months);
    }

    [Fact]
    public async Task GetQueueAsync_ServerError_Throws()
    {
        var (client, _) = Create(HttpStatusCode.InternalServerError, null);

        await Assert.ThrowsAsync<HttpRequestException>(() => client.GetQueueAsync());
    }

    private static readonly Guid RejectedId = Guid.Parse("55555555-5555-5555-5555-555555555555");

    [Fact]
    public async Task GetReviewAsync_SendsTheFiltersAsEscapedQueryValuesAndDeserialises()
    {
        var participant = Guid.NewGuid();
        var viewer = Guid.NewGuid();
        var json = $$$"""
            {"report":{"tabulationId":"{{{TabulationId}}}","isPublished":false,"distributionTabulationId":"{{{TabulationId}}}","title":"T","tabulationName":"N",
            "distributionDateText":"","standardTabulationTextHtml":"","commentsHtml":"","testConsultantCommentsHtml":"","testConsultantCommentsTitle":"","showUkasLogo":false,
            "signatory":null,"participantOptions":[],"selectedParticipantId":"{{{Guid.Empty}}}","viewerOptions":[],"selectedViewerId":"{{{Guid.Empty}}}","tables":[],"ratings":null,
            "attachment":null,"print":{"participants":[],"tests":[],"ratingColumns":[]}},"hasMethodItems":true,"sampleNumbers":["26/1","26/2"]}
            """;
        var (client, requests) = Create(HttpStatusCode.OK, json);

        var review = await client.GetReviewAsync(TabulationId, participant, viewer, new TestConsultantTabulationFilter(["26/1", "26/2"], true));

        Assert.Equal(
            (HttpMethod.Get, $"/api/results-sign-off/tabulations/{TabulationId}?participantId={participant}&viewerId={viewer}&hiddenSamples=26%2F1&hiddenSamples=26%2F2&hideMethodItems=true"),
            Assert.Single(requests));
        Assert.Equal(["26/1", "26/2"], review!.SampleNumbers);
        Assert.True(review.HasMethodItems);
    }

    [Fact]
    public async Task GetReviewAsync_WithoutAFilter_SendsOnlyTheParticipantAndViewer()
    {
        var (client, requests) = Create(HttpStatusCode.NotFound, null);

        await client.GetReviewAsync(TabulationId, Guid.Empty, Guid.Empty, TestConsultantTabulationFilter.None);

        Assert.Equal($"/api/results-sign-off/tabulations/{TabulationId}?participantId={Guid.Empty}&viewerId={Guid.Empty}", Assert.Single(requests).PathAndQuery);
    }

    [Fact]
    public async Task GetReviewAsync_NotFound_ReturnsNull()
    {
        var (client, _) = Create(HttpStatusCode.NotFound, null);

        Assert.Null(await client.GetReviewAsync(TabulationId, Guid.Empty, Guid.Empty, TestConsultantTabulationFilter.None));
    }

    [Fact]
    public async Task GetReviewAsync_ServerError_Throws()
    {
        var (client, _) = Create(HttpStatusCode.InternalServerError, null);

        await Assert.ThrowsAsync<HttpRequestException>(() => client.GetReviewAsync(TabulationId, Guid.Empty, Guid.Empty, TestConsultantTabulationFilter.None));
    }

    [Fact]
    public async Task GetReviewAsync_NullFilter_Throws()
    {
        var (client, _) = Create(HttpStatusCode.OK, null);

        await Assert.ThrowsAsync<ArgumentNullException>(() => client.GetReviewAsync(TabulationId, Guid.Empty, Guid.Empty, null!));
    }

    [Theory]
    [InlineData(HttpStatusCode.NoContent, ResultsSignOffApiStatus.Done)]
    [InlineData(HttpStatusCode.NotFound, ResultsSignOffApiStatus.NotFound)]
    [InlineData(HttpStatusCode.Conflict, ResultsSignOffApiStatus.NotAwaiting)]
    public async Task RejectAsync_PostsToTheTabulationAndMapsTheStatus(HttpStatusCode statusCode, ResultsSignOffApiStatus expected)
    {
        var (client, requests) = Create(statusCode, null);

        var status = await client.RejectAsync(RejectedId);

        Assert.Equal(expected, status);
        Assert.Equal((HttpMethod.Post, $"/api/results-sign-off/tabulations/{RejectedId}/reject"), Assert.Single(requests));
    }

    [Fact]
    public async Task RejectAsync_ServerError_Throws()
    {
        var (client, _) = Create(HttpStatusCode.InternalServerError, null);

        await Assert.ThrowsAsync<HttpRequestException>(() => client.RejectAsync(RejectedId));
    }

    [Theory]
    [InlineData(HttpStatusCode.NoContent, ResultsSignOffApiStatus.Done)]
    [InlineData(HttpStatusCode.NotFound, ResultsSignOffApiStatus.NotFound)]
    [InlineData(HttpStatusCode.Conflict, ResultsSignOffApiStatus.NotAwaiting)]
    [InlineData(HttpStatusCode.Forbidden, ResultsSignOffApiStatus.NoSignatory)]
    public async Task AuthoriseAsync_PostsToTheTabulationAndMapsTheStatus(HttpStatusCode statusCode, ResultsSignOffApiStatus expected)
    {
        var (client, requests) = Create(statusCode, null);

        var status = await client.AuthoriseAsync(RejectedId);

        Assert.Equal(expected, status);
        Assert.Equal((HttpMethod.Post, $"/api/results-sign-off/tabulations/{RejectedId}/authorise"), Assert.Single(requests));
    }

    [Fact]
    public async Task AuthoriseAsync_ServerError_Throws()
    {
        var (client, _) = Create(HttpStatusCode.InternalServerError, null);

        await Assert.ThrowsAsync<HttpRequestException>(() => client.AuthoriseAsync(RejectedId));
    }
}
