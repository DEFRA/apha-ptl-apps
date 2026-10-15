using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using PTL.ApiClient.Distribution;
using PTL.Contracts.Distribution;

namespace PTL.ApiClient.Tests.Distribution;

// View Comments client calls - /api/distributions/schemes/{id}/comments[/attachments...].
public class DistributionApiClientCommentsTests
{
    private static readonly Guid SchemeId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Token = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid AttachmentId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private static readonly string Url = $"/api/distributions/schemes/{SchemeId}/comments";

    private sealed record Sent(HttpMethod Method, string PathAndQuery, string? Body);

    private static (DistributionApiClient Client, List<Sent> Requests) Create(HttpStatusCode statusCode, string? json) =>
        Create(new StubHttpMessageHandler(statusCode, json));

    private static (DistributionApiClient Client, List<Sent> Requests) Create(Func<HttpResponseMessage> responseFactory) =>
        Create(new StubHttpMessageHandler(responseFactory));

    private static (DistributionApiClient Client, List<Sent> Requests) Create(StubHttpMessageHandler handler)
    {
        var requests = new List<Sent>();
        handler.OnRequest = r => requests.Add(new(r.Method, r.RequestUri!.PathAndQuery, r.Content?.ReadAsStringAsync().GetAwaiter().GetResult()));
        return (new DistributionApiClient(new HttpClient(handler) { BaseAddress = new Uri("http://localhost") }), requests);
    }

    private static CommentsAttachmentStageRequest StageRequest() => new("notes.pdf", "application/pdf", [1, 2], [Token], [AttachmentId]);

    [Fact]
    public async Task GetTestConsultantCommentsAsync_RequestsTheSchemeAndDeserialises()
    {
        var json = $$"""{"monthlyDistributionSchemeId":"{{SchemeId}}","distributionTitle":"D1a/S1 BT: Brucella","authorText":"A Author","commentsHtml":"Hi","canSubmit":true}""";
        var (client, requests) = Create(HttpStatusCode.OK, json);

        var page = await client.GetTestConsultantCommentsAsync(SchemeId);

        var request = Assert.Single(requests);
        Assert.Equal((HttpMethod.Get, Url), (request.Method, request.PathAndQuery));
        Assert.Equal((SchemeId, "D1a/S1 BT: Brucella", "A Author", "Hi", true),
            (page!.MonthlyDistributionSchemeId, page.DistributionTitle, page.AuthorText, page.CommentsHtml, page.CanSubmit));
    }

    [Fact]
    public async Task GetTestConsultantCommentsAsync_NotFound_ReturnsNull()
    {
        var (client, _) = Create(HttpStatusCode.NotFound, null);

        Assert.Null(await client.GetTestConsultantCommentsAsync(SchemeId));
    }

    [Fact]
    public async Task SaveTestConsultantCommentsAsync_PutsTheRequestAndReadsTheResult()
    {
        var json = """{"success":false,"auditReasonRequired":true,"auditObjectName":"Comments","errors":{"Comments":"Required"}}""";
        var (client, requests) = Create(HttpStatusCode.OK, json);

        var result = await client.SaveTestConsultantCommentsAsync(SchemeId, new TestConsultantCommentsSaveRequest("Hi", true, "Typo", [Token], [AttachmentId]));

        var request = Assert.Single(requests);
        Assert.Equal((HttpMethod.Put, Url), (request.Method, request.PathAndQuery));
        var body = JsonDocument.Parse(request.Body!).RootElement;
        Assert.Equal(("Hi", true, "Typo"), (body.GetProperty("comments").GetString(), body.GetProperty("submit").GetBoolean(), body.GetProperty("auditWhy").GetString()));
        Assert.Equal(Token, body.GetProperty("stagedAttachmentTokens")[0].GetGuid());
        Assert.Equal(AttachmentId, body.GetProperty("removedAttachmentIds")[0].GetGuid());
        Assert.Equal((false, true, "Comments", "Required"), (result!.Success, result.AuditReasonRequired, result.AuditObjectName, result.Errors["Comments"]));
    }

    [Fact]
    public async Task SaveTestConsultantCommentsAsync_NotFound_ReturnsNull()
    {
        var (client, _) = Create(HttpStatusCode.NotFound, null);

        Assert.Null(await client.SaveTestConsultantCommentsAsync(SchemeId, new TestConsultantCommentsSaveRequest("Hi", false)));
    }

    [Fact]
    public async Task SaveTestConsultantCommentsAsync_ServerError_Throws()
    {
        var (client, _) = Create(HttpStatusCode.InternalServerError, null);

        await Assert.ThrowsAsync<HttpRequestException>(() => client.SaveTestConsultantCommentsAsync(SchemeId, new TestConsultantCommentsSaveRequest("Hi", false)));
    }

    [Fact]
    public async Task StageTestConsultantCommentsAttachmentAsync_PostsTheFileAndReadsTheResult()
    {
        var json = $$$"""{"success":true,"token":"{{{Token}}}","fileName":"notes.pdf","errors":{}}""";
        var (client, requests) = Create(HttpStatusCode.OK, json);

        var result = await client.StageTestConsultantCommentsAttachmentAsync(SchemeId, StageRequest());

        var request = Assert.Single(requests);
        Assert.Equal((HttpMethod.Post, $"{Url}/attachments/staged"), (request.Method, request.PathAndQuery));
        var body = JsonDocument.Parse(request.Body!).RootElement;
        Assert.Equal(("notes.pdf", "application/pdf"), (body.GetProperty("fileName").GetString(), body.GetProperty("contentType").GetString()));
        Assert.Equal([1, 2], body.GetProperty("fileData").GetBytesFromBase64());
        Assert.Equal((true, Token, "notes.pdf"), (result!.Success, result.Token, result.FileName));
        Assert.Empty(result.Errors);
    }

    [Fact]
    public async Task StageTestConsultantCommentsAttachmentAsync_NotFound_ReturnsNull()
    {
        var (client, _) = Create(HttpStatusCode.NotFound, null);

        Assert.Null(await client.StageTestConsultantCommentsAttachmentAsync(SchemeId, StageRequest()));
    }

    [Fact]
    public async Task DiscardStagedTestConsultantCommentsAttachmentsAsync_PostsTheTokens()
    {
        var (client, requests) = Create(HttpStatusCode.NoContent, null);

        await client.DiscardStagedTestConsultantCommentsAttachmentsAsync(SchemeId, [Token]);

        var request = Assert.Single(requests);
        Assert.Equal((HttpMethod.Post, $"{Url}/attachments/staged/discard"), (request.Method, request.PathAndQuery));
        var tokens = JsonDocument.Parse(request.Body!).RootElement.GetProperty("tokens");
        Assert.Equal(Token, Assert.Single(tokens.EnumerateArray()).GetGuid());
    }

    [Fact]
    public async Task DiscardStagedTestConsultantCommentsAttachmentsAsync_Error_Throws()
    {
        var (client, _) = Create(HttpStatusCode.InternalServerError, null);

        await Assert.ThrowsAsync<HttpRequestException>(() => client.DiscardStagedTestConsultantCommentsAttachmentsAsync(SchemeId, [Token]));
    }

    public static TheoryData<bool> Staged => new() { false, true };

    private static Task<TabulationAttachmentFile?> GetFile(DistributionApiClient client, bool staged) =>
        staged
            ? client.GetStagedTestConsultantCommentsAttachmentAsync(SchemeId, Token)
            : client.GetTestConsultantCommentsAttachmentAsync(SchemeId, AttachmentId);

    [Theory]
    [MemberData(nameof(Staged))]
    public async Task GetAttachment_PassesTheContentAndHeadersThrough(bool staged)
    {
        var (client, requests) = Create(() =>
        {
            var content = new ByteArrayContent([1, 2, 3]);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
            content.Headers.ContentDisposition = new ContentDispositionHeaderValue("inline") { FileName = "notes.pdf" };
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
        });

        var file = await GetFile(client, staged);

        var request = Assert.Single(requests);
        var expected = staged ? $"{Url}/attachments/staged/{Token}" : $"{Url}/attachments/{AttachmentId}";
        Assert.Equal((HttpMethod.Get, expected), (request.Method, request.PathAndQuery));
        Assert.Equal([1, 2, 3], file!.Content);
        Assert.Equal(("application/pdf", "inline; filename=notes.pdf"), (file.ContentType, file.ContentDisposition));
    }

    [Theory]
    [MemberData(nameof(Staged))]
    public async Task GetAttachment_NotFound_ReturnsNull(bool staged)
    {
        var (client, _) = Create(HttpStatusCode.NotFound, null);

        Assert.Null(await GetFile(client, staged));
    }
}
