using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using PTL.ApiClient;
using PTL.Contracts.Distribution;

namespace PTL.ApiClient.Tests.TestConsultant;

// Test Consultant menu, tabulation, export and comments calls - /api/test-consultant/...
public class TestConsultantTabulationApiClientTests
{
    private static readonly Guid SchemeId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private sealed record Sent(HttpMethod Method, string PathAndQuery, string? Body);

    private static (TestConsultantTabulationApiClient Client, List<Sent> Requests) Create(HttpStatusCode statusCode, string? json) =>
        Create(new StubHttpMessageHandler(statusCode, json));

    private static (TestConsultantTabulationApiClient Client, List<Sent> Requests) Create(StubHttpMessageHandler handler)
    {
        var requests = new List<Sent>();
        handler.OnRequest = r => requests.Add(new(r.Method, r.RequestUri!.PathAndQuery, r.Content?.ReadAsStringAsync().GetAwaiter().GetResult()));
        return (new TestConsultantTabulationApiClient(new HttpClient(handler) { BaseAddress = new Uri("http://localhost") }), requests);
    }

    private const string TabulationJson = """
        {"monthlyDistributionSchemeId":"11111111-1111-1111-1111-111111111111","title":"FINAL REPORT","distributionDateText":"","standardTabulationTextHtml":"",
         "commentsHtml":"","hasMethodItems":true,"sampleNumbers":["26/1"],"resultsTables":[],"fullTables":[],"sampleInformation":[],
         "comments":{"isSubmitted":false,"heading":"Add your comments here:","commentsHtml":"","hasComments":false}}
        """;

    [Fact]
    public async Task GetDistributionsAsync_RequestsTheListAndDeserialises()
    {
        var json = $$"""{"distributions":[{"monthlyDistributionSchemeId":"{{SchemeId}}","yearId":2026,"monthId":6,"distributionTitle":"D1","targetDate":"2026-07-21T00:00:00","isCommentSubmitted":false,"commentStatus":"Awaiting comments"}]}""";
        var (client, requests) = Create(HttpStatusCode.OK, json);

        var response = await client.GetDistributionsAsync();

        var request = Assert.Single(requests);
        Assert.Equal((HttpMethod.Get, "/api/test-consultant/distributions"), (request.Method, request.PathAndQuery));
        Assert.Equal((SchemeId, 2026, 6, "Awaiting comments"),
            (response.Distributions[0].MonthlyDistributionSchemeId, response.Distributions[0].YearId, response.Distributions[0].MonthId, response.Distributions[0].CommentStatus));
    }

    [Fact]
    public async Task GetDistributionsAsync_EmptyBody_ReturnsAnEmptyList()
    {
        var (client, _) = Create(HttpStatusCode.OK, "null");

        Assert.Empty((await client.GetDistributionsAsync()).Distributions);
    }

    [Fact]
    public async Task GetDistributionsAsync_ServerError_Throws()
    {
        var (client, _) = Create(HttpStatusCode.InternalServerError, null);

        await Assert.ThrowsAsync<HttpRequestException>(() => client.GetDistributionsAsync());
    }

    [Fact]
    public async Task GetTabulationAsync_WithoutAFilter_RequestsTheBareUrl()
    {
        var (client, requests) = Create(HttpStatusCode.OK, TabulationJson);

        var result = await client.GetTabulationAsync(SchemeId, TestConsultantTabulationFilter.None);

        Assert.Equal($"/api/test-consultant/schemes/{SchemeId}/tabulation", Assert.Single(requests).PathAndQuery);
        Assert.True(result.IsOk);
        Assert.Equal(("FINAL REPORT", true), (result.Value!.Title, result.Value.HasMethodItems));
        Assert.Equal(["26/1"], result.Value.SampleNumbers);
        Assert.False(result.Value.Comments.IsSubmitted);
    }

    [Fact]
    public async Task GetTabulationAsync_SendsTheFilterAsEscapedQueryValues()
    {
        var (client, requests) = Create(HttpStatusCode.OK, TabulationJson);

        await client.GetTabulationAsync(SchemeId, new TestConsultantTabulationFilter(["26/1", "26/2"], true));

        Assert.Equal(
            $"/api/test-consultant/schemes/{SchemeId}/tabulation?hiddenSamples=26%2F1&hiddenSamples=26%2F2&hideMethodItems=true",
            Assert.Single(requests).PathAndQuery);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound, TestConsultantApiStatus.NotFound)]
    [InlineData(HttpStatusCode.Forbidden, TestConsultantApiStatus.Forbidden)]
    public async Task GetTabulationAsync_RefusedOrMissing_MapsTheStatusWithoutAValue(HttpStatusCode statusCode, TestConsultantApiStatus expected)
    {
        var (client, _) = Create(statusCode, null);

        var result = await client.GetTabulationAsync(SchemeId, TestConsultantTabulationFilter.None);

        Assert.Equal((expected, null, false), (result.Status, result.Value, result.IsOk));
    }

    [Fact]
    public async Task GetTabulationAsync_ServerError_Throws()
    {
        var (client, _) = Create(HttpStatusCode.BadGateway, null);

        await Assert.ThrowsAsync<HttpRequestException>(() => client.GetTabulationAsync(SchemeId, TestConsultantTabulationFilter.None));
    }

    [Fact]
    public async Task ExportTabulationAsync_ReturnsTheFileWithItsHeaders()
    {
        var handler = new StubHttpMessageHandler(() =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([1, 2, 3]) };
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
            response.Content.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment") { FileName = "D1_BT.xlsx" };
            return response;
        });
        var (client, requests) = Create(handler);

        var result = await client.ExportTabulationAsync(SchemeId, new TestConsultantTabulationFilter(["26/1"], false));

        Assert.Equal($"/api/test-consultant/schemes/{SchemeId}/tabulation/export?hiddenSamples=26%2F1", Assert.Single(requests).PathAndQuery);
        Assert.Equal([1, 2, 3], result.Value!.Content);
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", result.Value.ContentType);
        Assert.Contains("D1_BT.xlsx", result.Value.ContentDisposition, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExportTabulationAsync_WithoutHeaders_FallsBackToADownload()
    {
        var handler = new StubHttpMessageHandler(() => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([9]) });
        var (client, _) = Create(handler);

        var result = await client.ExportTabulationAsync(SchemeId, TestConsultantTabulationFilter.None);

        Assert.Equal(("application/octet-stream", "attachment"), (result.Value!.ContentType, result.Value.ContentDisposition));
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound, TestConsultantApiStatus.NotFound)]
    [InlineData(HttpStatusCode.Forbidden, TestConsultantApiStatus.Forbidden)]
    public async Task ExportTabulationAsync_RefusedOrMissing_MapsTheStatus(HttpStatusCode statusCode, TestConsultantApiStatus expected)
    {
        var (client, _) = Create(statusCode, null);

        var result = await client.ExportTabulationAsync(SchemeId, TestConsultantTabulationFilter.None);

        Assert.Equal((expected, null), (result.Status, result.Value));
    }

    [Fact]
    public async Task SaveCommentsAsync_PutsTheRequestAndReadsTheResult()
    {
        var (client, requests) = Create(HttpStatusCode.OK, """{"success":false,"isLocked":false,"errors":{"Comments":"Comments required."}}""");

        var result = await client.SaveCommentsAsync(SchemeId, new TestConsultantTabulationSaveRequest("<p>Hi</p>", true));

        var request = Assert.Single(requests);
        Assert.Equal((HttpMethod.Put, $"/api/test-consultant/schemes/{SchemeId}/comments"), (request.Method, request.PathAndQuery));
        var body = JsonDocument.Parse(request.Body!).RootElement;
        Assert.Equal(("<p>Hi</p>", true), (body.GetProperty("comments").GetString(), body.GetProperty("submit").GetBoolean()));
        Assert.Equal((false, "Comments required."), (result.Value!.Success, result.Value.Errors["Comments"]));
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound, TestConsultantApiStatus.NotFound)]
    [InlineData(HttpStatusCode.Forbidden, TestConsultantApiStatus.Forbidden)]
    public async Task SaveCommentsAsync_RefusedOrMissing_MapsTheStatus(HttpStatusCode statusCode, TestConsultantApiStatus expected)
    {
        var (client, _) = Create(statusCode, null);

        var result = await client.SaveCommentsAsync(SchemeId, new TestConsultantTabulationSaveRequest("<p>Hi</p>", false));

        Assert.Equal((expected, null), (result.Status, result.Value));
    }
}
