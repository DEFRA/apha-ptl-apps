using System.Net;
using System.Net.Http.Headers;
using PTL.ApiClient.Distribution;

namespace PTL.ApiClient.Tests.Distribution;

public class DistributionApiClientTabulationTests
{
    private static readonly Guid TabulationId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid PublishedId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid ParticipantId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid ViewerId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid TestId = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private static readonly Guid AttachmentId = Guid.Parse("66666666-6666-6666-6666-666666666666");

    private const string ReportJson = """
        {"tabulationId":"11111111-1111-1111-1111-111111111111","isPublished":false,"distributionTabulationId":"11111111-1111-1111-1111-111111111111",
         "title":"FINAL REPORT for D1a/S1 BT: Brucella","tabulationName":"Summary","distributionDateText":"Date Of Distribution: 03/05/2026",
         "standardTabulationTextHtml":"","commentsHtml":"","testConsultantCommentsHtml":"","testConsultantCommentsTitle":"Comments :","showUkasLogo":false,
         "signatory":null,"participantOptions":[],"selectedParticipantId":"00000000-0000-0000-0000-000000000000","viewerOptions":[],
         "selectedViewerId":"00000000-0000-0000-0000-000000000000",
         "tables":[{"caption":"Culture","rows":[{"cells":[{"text":"Culture","colSpan":1,"rowSpan":2,"kind":"Header1","isNilReturn":false}],"isHeader":true}],"assessmentTestId":null}],
         "ratings":null,"attachment":{"attachmentId":"66666666-6666-6666-6666-666666666666","fileName":"notes.pdf"},
         "print":{"participants":[],"tests":[],"ratingColumns":[]}}
        """;

    private static (DistributionApiClient Client, List<Uri> Requested) Create(HttpStatusCode statusCode, string? json)
    {
        var requested = new List<Uri>();
        var handler = new StubHttpMessageHandler(statusCode, json) { OnRequest = r => requested.Add(r.RequestUri!) };
        return (new DistributionApiClient(new HttpClient(handler) { BaseAddress = new Uri("http://localhost") }), requested);
    }

    private static (DistributionApiClient Client, List<Uri> Requested) Create(Func<HttpResponseMessage> response)
    {
        var requested = new List<Uri>();
        var handler = new StubHttpMessageHandler(response) { OnRequest = r => requested.Add(r.RequestUri!) };
        return (new DistributionApiClient(new HttpClient(handler) { BaseAddress = new Uri("http://localhost") }), requested);
    }

    [Fact]
    public async Task GetTabulationAsync_RequestsTheTabulationWithBothFiltersAndDeserialises()
    {
        var (client, requested) = Create(HttpStatusCode.OK, ReportJson);

        var report = await client.GetTabulationAsync(TabulationId, ParticipantId, ViewerId);

        Assert.Equal(
            $"/api/distributions/tabulations/{TabulationId}?participantId={ParticipantId}&viewerId={ViewerId}",
            Assert.Single(requested).PathAndQuery);
        Assert.Equal("FINAL REPORT for D1a/S1 BT: Brucella", report!.Title);
        Assert.Equal(("Culture", 2, "Header1"), (report.Tables[0].Rows[0].Cells[0].Text, report.Tables[0].Rows[0].Cells[0].RowSpan, report.Tables[0].Rows[0].Cells[0].Kind));
        Assert.Equal("notes.pdf", report.Attachment!.FileName);
    }

    [Fact]
    public async Task GetTabulationAsync_NotFound_ReturnsNull()
    {
        var (client, _) = Create(HttpStatusCode.NotFound, null);

        Assert.Null(await client.GetTabulationAsync(TabulationId, Guid.Empty, Guid.Empty));
    }

    [Fact]
    public async Task GetTabulationAsync_ServerError_Throws()
    {
        var (client, _) = Create(HttpStatusCode.InternalServerError, null);

        await Assert.ThrowsAsync<HttpRequestException>(() => client.GetTabulationAsync(TabulationId, Guid.Empty, Guid.Empty));
    }

    [Fact]
    public async Task GetPublishedTabulationAsync_RequestsThePublishedTabulation()
    {
        var (client, requested) = Create(HttpStatusCode.OK, ReportJson);

        var report = await client.GetPublishedTabulationAsync(PublishedId, Guid.Empty, ViewerId);

        Assert.Equal(
            $"/api/distributions/published-tabulations/{PublishedId}?participantId={Guid.Empty}&viewerId={ViewerId}",
            Assert.Single(requested).PathAndQuery);
        Assert.NotNull(report);
    }

    [Fact]
    public async Task GetPublishedTabulationAsync_NotFound_ReturnsNull()
    {
        var (client, _) = Create(HttpStatusCode.NotFound, null);

        Assert.Null(await client.GetPublishedTabulationAsync(PublishedId, Guid.Empty, Guid.Empty));
    }

    [Fact]
    public async Task GetPublishedTabulationsAsync_RequestsTheIssuedListAndDeserialises()
    {
        const string json = """
            {"distributionTabulationId":"11111111-1111-1111-1111-111111111111","title":"D1a/S1 BT: Brucella","tabulationName":"Summary",
             "tabulations":[{"publishedTabulationId":"22222222-2222-2222-2222-222222222222","creationDate":"2026-06-10T14:30:00","signatory":"Dr S"}]}
            """;
        var (client, requested) = Create(HttpStatusCode.OK, json);

        var page = await client.GetPublishedTabulationsAsync(TabulationId);

        Assert.Equal($"/api/distributions/tabulations/{TabulationId}/published", Assert.Single(requested).PathAndQuery);
        var row = Assert.Single(page!.Tabulations);
        Assert.Equal((PublishedId, new DateTime(2026, 6, 10, 14, 30, 0), "Dr S"), (row.PublishedTabulationId, row.CreationDate, row.Signatory));
    }

    [Fact]
    public async Task GetPublishedTabulationsAsync_NotFound_ReturnsNull()
    {
        var (client, _) = Create(HttpStatusCode.NotFound, null);

        Assert.Null(await client.GetPublishedTabulationsAsync(TabulationId));
    }

    [Theory]
    [InlineData(false, "tabulations")]
    [InlineData(true, "published-tabulations")]
    public async Task GetAssessmentResultsAsync_UsesTheDraftOrPublishedSegment(bool isPublished, string segment)
    {
        const string json = """
            {"isAssessmentSubmitted":true,"titles":["Bar chart shows the frequency of assessment scores"],"xAxisTitle":"Assessment Score","yAxisTitle":"Frequency",
             "scores":[{"score":1,"label":"1 Fail","frequency":3}]}
            """;
        var (client, requested) = Create(HttpStatusCode.OK, json);

        var results = await client.GetAssessmentResultsAsync(TabulationId, TestId, isPublished);

        Assert.Equal($"/api/distributions/{segment}/{TabulationId}/tests/{TestId}/assessment-results", Assert.Single(requested).PathAndQuery);
        Assert.True(results!.IsAssessmentSubmitted);
        Assert.Equal(("1 Fail", 3), (results.Scores[0].Label, results.Scores[0].Frequency));
    }

    [Fact]
    public async Task GetAssessmentResultsAsync_NotFound_ReturnsNull()
    {
        var (client, _) = Create(HttpStatusCode.NotFound, null);

        Assert.Null(await client.GetAssessmentResultsAsync(TabulationId, TestId, false));
    }

    [Theory]
    [InlineData(false, "tabulations")]
    [InlineData(true, "published-tabulations")]
    public async Task GetTabulationAttachmentAsync_PassesContentTypeAndDispositionThrough(bool isPublished, string segment)
    {
        var (client, requested) = Create(() =>
        {
            var content = new ByteArrayContent([1, 2, 3]);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
            content.Headers.ContentDisposition = new ContentDispositionHeaderValue("inline") { FileName = "notes.pdf" };
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
        });

        var file = await client.GetTabulationAttachmentAsync(TabulationId, isPublished, AttachmentId);

        Assert.Equal($"/api/distributions/{segment}/{TabulationId}/attachments/{AttachmentId}", Assert.Single(requested).PathAndQuery);
        Assert.Equal([1, 2, 3], file!.Content);
        Assert.Equal("application/pdf", file.ContentType);
        Assert.Equal("inline; filename=notes.pdf", file.ContentDisposition);
    }

    [Fact]
    public async Task GetTabulationAttachmentAsync_NoContentHeaders_DefaultsToADownload()
    {
        var (client, _) = Create(() => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([7]) });

        var file = await client.GetTabulationAttachmentAsync(TabulationId, false, AttachmentId);

        Assert.Equal(("application/octet-stream", "attachment"), (file!.ContentType, file.ContentDisposition));
        Assert.Equal([7], file.Content);
    }

    [Fact]
    public async Task GetTabulationAttachmentAsync_NotFound_ReturnsNull()
    {
        var (client, _) = Create(HttpStatusCode.NotFound, null);

        Assert.Null(await client.GetTabulationAttachmentAsync(TabulationId, true, AttachmentId));
    }

    [Fact]
    public async Task GetTabulationAttachmentAsync_ServerError_Throws()
    {
        var (client, _) = Create(HttpStatusCode.InternalServerError, null);

        await Assert.ThrowsAsync<HttpRequestException>(() => client.GetTabulationAttachmentAsync(TabulationId, false, AttachmentId));
    }
}
