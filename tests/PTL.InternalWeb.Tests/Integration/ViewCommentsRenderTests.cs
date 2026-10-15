using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PTL.ApiClient.Distribution;
using PTL.Contracts.Distribution;
using PTL.InternalWeb.Tests.TestSupport;

namespace PTL.InternalWeb.Tests.Integration;

// Renders Features/Distribution/Views/ViewComments.cshtml - legacy Scheduling/ViewComments.aspx with
// attachment changes staged until Save.
public class ViewCommentsRenderTests : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly Guid Scheme = Guid.Parse("cccccccc-0000-0000-0000-0000000000d1");
    private static readonly Guid SavedId = Guid.Parse("cccccccc-0000-0000-0000-0000000000d2");
    private static readonly Guid Token = Guid.Parse("cccccccc-0000-0000-0000-0000000000d3");

    private static readonly string Url = $"/Distribution/ViewComments?year=2026&monthId=5&schemeId={Scheme}";

    private const string SubmitWarning = "Submitting the comments will prevent the Test Consultants from making any further modifications.";

    private readonly WebApplicationFactory<Program> _factory;
    private readonly FakeDistributionApiClient _apiClient = new();

    public ViewCommentsRenderTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IDistributionApiClient>();
                services.AddSingleton<IDistributionApiClient>(_apiClient);
            }));
    }

    private void Seed(bool canSubmit = true, bool withSaved = false) =>
        _apiClient.TestConsultantComments = new TestConsultantCommentsResponse(
            Scheme, "D1a/S1 BT: Brucella", "Comments by Dr A", "<p>Stored</p>", canSubmit,
            withSaved ? [new CommentsAttachmentResponse(SavedId, "saved.pdf")] : [],
            [".pdf"], "PDF", "10MB");

    private async Task<string> GetHtmlAsync()
    {
        var response = await _factory.CreateClient().GetAsync(Url);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync();
    }

    private async Task<string> PostHtmlAsync(params (string Name, string Value)[] fields)
    {
        var client = _factory.CreateClient();
        var page = await (await client.GetAsync(Url)).Content.ReadAsStringAsync();
        var token = Regex.Match(page, "__RequestVerificationToken[^>]*value=\"([^\"]+)\"").Groups[1].Value;

        var content = new List<KeyValuePair<string, string>> { new("__RequestVerificationToken", token), new("Comments", "<p>New</p>") };
        content.AddRange(fields.Select(f => new KeyValuePair<string, string>(f.Name, f.Value)));

        var response = await client.PostAsync(Url, new FormUrlEncodedContent(content));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync();
    }

    private static string Main(string html) => Regex.Match(html, "<main.*?</main>", RegexOptions.Singleline).Value;

    private static bool HasCommandButton(string html, string value, string label) =>
        Regex.IsMatch(html, $"<button type=\"submit\" name=\"command\" value=\"{value}\"[^>]*>{label}</button>");

    private static bool HasUploadInput(string html) => html.Contains("name=\"file\" type=\"file\"", StringComparison.Ordinal);

    [Fact]
    public async Task Get_NoAttachments_ShowsTheUploadInputSaveCancelAndSubmit()
    {
        Seed();

        var html = Main(await GetHtmlAsync());

        Assert.True(HasUploadInput(html));
        Assert.Contains(">Upload</button>", html, StringComparison.Ordinal);
        Assert.True(HasCommandButton(html, "save", "Save"));
        Assert.True(HasCommandButton(html, "cancel", "Cancel"));
        Assert.True(HasCommandButton(html, "submit", "Submit"));
        Assert.DoesNotContain("name=\"StagedAttachments\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("name=\"RemovedAttachmentIds\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("AttachmentAuditWhy", html, StringComparison.Ordinal);
        Assert.DoesNotContain("name=\"AuditWhy\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Get_CannotSubmit_HidesTheSubmitButton()
    {
        Seed(canSubmit: false);

        var html = Main(await GetHtmlAsync());

        Assert.False(HasCommandButton(html, "submit", "Submit"));
        Assert.True(HasCommandButton(html, "save", "Save"));
    }

    [Fact]
    public async Task Get_SavedAttachment_LinksToItAndHidesTheUploadInput()
    {
        Seed(withSaved: true);

        var html = Main(await GetHtmlAsync());

        Assert.Contains(
            $"href=\"/Distribution/ViewCommentsAttachment?schemeId={Scheme}&amp;attachmentId={SavedId}\">saved.pdf</a>",
            html, StringComparison.Ordinal);
        Assert.DoesNotContain("Not saved", html, StringComparison.Ordinal);
        Assert.False(HasUploadInput(html));
        Assert.Contains("formaction=\"/Distribution/ViewCommentsRemoveAttachment?", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Post_StagedAndRemovedAttachments_ShowTheStagedUploadOnlyAndKeepBothHidden()
    {
        Seed(withSaved: true);

        var html = Main(await PostHtmlAsync(
            ("command", "cancelAudit"),
            ("StagedAttachments", $"{Token}|new.pdf"),
            ("RemovedAttachmentIds", SavedId.ToString())));

        Assert.Contains(
            $"href=\"/Distribution/ViewCommentsStagedAttachment?schemeId={Scheme}&amp;token={Token}\">new.pdf</a>",
            html, StringComparison.Ordinal);
        Assert.Contains("<strong class=\"govuk-tag govuk-tag--yellow govuk-!-margin-left-2\">Not saved</strong>", html, StringComparison.Ordinal);
        Assert.DoesNotContain("saved.pdf", html, StringComparison.Ordinal);
        Assert.DoesNotContain("ViewCommentsAttachment?", html, StringComparison.Ordinal);
        Assert.Contains($"<input type=\"hidden\" name=\"StagedAttachments\" value=\"{Token}|new.pdf\" />", html, StringComparison.Ordinal);
        Assert.Contains($"<input type=\"hidden\" name=\"RemovedAttachmentIds\" value=\"{SavedId}\" />", html, StringComparison.Ordinal);
        Assert.False(HasUploadInput(html));
        Assert.DoesNotContain("AttachmentAuditWhy", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Post_SavedAttachmentRemoved_ShowsTheUploadInputAgain()
    {
        Seed(withSaved: true);

        var html = Main(await PostHtmlAsync(("command", "cancelAudit"), ("RemovedAttachmentIds", SavedId.ToString())));

        Assert.DoesNotContain("saved.pdf", html, StringComparison.Ordinal);
        Assert.True(HasUploadInput(html));
    }

    [Theory]
    [InlineData("submit", true)]
    [InlineData("save", false)]
    public async Task Post_AuditReasonRequired_ShowsTheAuditStepWithTheSubmitWarningOnlyForSubmit(string command, bool warning)
    {
        Seed();
        _apiClient.CommentsSaveResult = new TestConsultantCommentsSaveResult(false, true, "Comments for D1a/S1", new Dictionary<string, string>());

        var html = Main(await PostHtmlAsync(("command", command), ("StagedAttachments", $"{Token}|new.pdf")));

        Assert.Contains("<h1 class=\"govuk-heading-l\">Audit Trail</h1>", html, StringComparison.Ordinal);
        Assert.Equal(warning, html.Contains(SubmitWarning, StringComparison.Ordinal));
        Assert.Equal(warning, html.Contains("govuk-warning-text", StringComparison.Ordinal));
        Assert.Matches("<textarea[^>]*name=\"AuditWhy\"", html);
        Assert.Contains($"<input type=\"hidden\" name=\"StagedAttachments\" value=\"{Token}|new.pdf\" />", html, StringComparison.Ordinal);
        Assert.True(HasCommandButton(html, command, "Save"));
        Assert.True(HasCommandButton(html, "cancelAudit", "Cancel"));
        Assert.DoesNotContain("AttachmentAuditWhy", html, StringComparison.Ordinal);
    }
}
