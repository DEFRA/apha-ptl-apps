using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PTL.ApiClient;
using PTL.ApiClient.Distribution;
using PTL.Contracts.Distribution;
using PTL.ExternalWeb.Tests.TestSupport;

namespace PTL.ExternalWeb.Tests.Integration;

public class CommentsRouteSmokeTests : IClassFixture<PtlExternalWebTestFactory>
{
    private readonly PtlExternalWebTestFactory _factory;

    public CommentsRouteSmokeTests(PtlExternalWebTestFactory factory) => _factory = factory;

    private HttpClient CreateClient(FakeTestConsultantTabulationApiClient apiClient, bool allowAutoRedirect = true) =>
        _factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ITestConsultantTabulationApiClient>();
                services.AddSingleton<ITestConsultantTabulationApiClient>(apiClient);
            })).CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = allowAutoRedirect });

    private static async Task<Dictionary<string, string>> SignInAsync(HttpClient client, Guid testConsultantId)
    {
        var cookies = new Dictionary<string, string>();
        var response = await client.GetAsync($"{PtlExternalWebTestFactory.TestSignInPath}?roles={Uri.EscapeDataString("Test Consultant")}&testConsultantId={testConsultantId}");
        CookieForwardingHttpClient.CaptureCookies(response, cookies);
        return cookies;
    }

    [Fact]
    public async Task Index_Search_FiltersDistributions()
    {
        var apiClient = new FakeTestConsultantTabulationApiClient
        {
            Distributions = new TestConsultantDistributionsResponse(
            [
                Distribution("D1a/S1 BT: Brucella"),
                Distribution("D2a/S2 PTX: Salmonella")
            ])
        };
        var client = CreateClient(apiClient);
        var cookies = await SignInAsync(client, Guid.NewGuid());

        var response = await CookieForwardingHttpClient.SendWithCookiesAsync(client, HttpMethod.Get, "/Comments/Index?schemeName=Brucella", cookies);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Distribution reference", body, StringComparison.Ordinal);
        Assert.Contains("Scheme code", body, StringComparison.Ordinal);
        Assert.Contains("Scheme name", body, StringComparison.Ordinal);
        Assert.Contains("<td class=\"govuk-table__cell\">D1a</td>", body, StringComparison.Ordinal);
        Assert.Contains("<td class=\"govuk-table__cell\">S1 BT</td>", body, StringComparison.Ordinal);
        Assert.Contains("<td class=\"govuk-table__cell\">Brucella</td>", body, StringComparison.Ordinal);
        Assert.DoesNotContain("Salmonella", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Index_WhenFilteredAndNoRows_ShowsNoMatchesMessage()
    {
        var apiClient = new FakeTestConsultantTabulationApiClient
        {
            Distributions = new TestConsultantDistributionsResponse(
            [
                Distribution("D1a/S1 BT: Brucella")
            ])
        };
        var client = CreateClient(apiClient);
        var cookies = await SignInAsync(client, Guid.NewGuid());

        var response = await CookieForwardingHttpClient.SendWithCookiesAsync(client, HttpMethod.Get, "/Comments/Index?schemeName=Salmonella", cookies);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("No distributions found for the selected search criteria.", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Index_WhenNotFilteredAndNoRows_ShowsEmptyMessage()
    {
        var apiClient = new FakeTestConsultantTabulationApiClient
        {
            Distributions = new TestConsultantDistributionsResponse([])
        };
        var client = CreateClient(apiClient);
        var cookies = await SignInAsync(client, Guid.NewGuid());

        var response = await CookieForwardingHttpClient.SendWithCookiesAsync(client, HttpMethod.Get, "/Comments/Index", cookies);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("There are no distributions to display.", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Entry_Get_Draft_ShowsEditorAndSaveSubmitControls()
    {
        var apiClient = new FakeTestConsultantTabulationApiClient { Tabulation = DraftTabulation() };
        var client = CreateClient(apiClient);
        var cookies = await SignInAsync(client, Guid.NewGuid());

        var response = await CookieForwardingHttpClient.SendWithCookiesAsync(client, HttpMethod.Get, $"/Comments/Entry?schemeId={apiClient.SchemeId}", cookies);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Comments entry", body, StringComparison.Ordinal);
        Assert.Contains("D1a/S1 BT: Brucella", body, StringComparison.Ordinal);
        Assert.DoesNotContain("FINAL REPORT for D1a/S1 BT: Brucella", body, StringComparison.Ordinal);
        Assert.Contains("Results Report", body, StringComparison.Ordinal);
        Assert.Contains("Full Report", body, StringComparison.Ordinal);
        Assert.Contains("Additional Sample Information", body, StringComparison.Ordinal);
        Assert.Contains("Draw reports", body, StringComparison.Ordinal);
        Assert.Contains("Export to Excel", body, StringComparison.Ordinal);
        Assert.Contains("name=\"command\" value=\"save\"", body, StringComparison.Ordinal);
        Assert.Contains("name=\"command\" value=\"submit\"", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Entry_Post_SubmitWithoutAcknowledgement_ShowsError_AndDoesNotSave()
    {
        var apiClient = new FakeTestConsultantTabulationApiClient { Tabulation = DraftTabulation() };
        var client = CreateClient(apiClient, allowAutoRedirect: false);
        var cookies = await SignInAsync(client, Guid.NewGuid());
        var (token, _) = await GetEntryTokenAsync(client, cookies, apiClient.SchemeId);

        var form = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["MonthlyDistributionSchemeId"] = apiClient.SchemeId.ToString(),
            ["Comments"] = "<p>Draft</p>",
            ["command"] = "submit"
        };

        var response = await CookieForwardingHttpClient.SendWithCookiesAsync(
            client, HttpMethod.Post, $"/Comments/Entry?schemeId={apiClient.SchemeId}", cookies, new FormUrlEncodedContent(form));
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Select to confirm you understand submitted comments cannot be edited.", body, StringComparison.Ordinal);
        Assert.Empty(apiClient.SavedRequests);
    }

    [Fact]
    public async Task Entry_Post_SubmitWithAcknowledgement_SavesAndRedirects()
    {
        var apiClient = new FakeTestConsultantTabulationApiClient
        {
            Tabulation = DraftTabulation(),
            SaveResult = new TestConsultantTabulationSaveResult(true, false, new Dictionary<string, string>())
        };
        var client = CreateClient(apiClient, allowAutoRedirect: false);
        var cookies = await SignInAsync(client, Guid.NewGuid());
        var (token, _) = await GetEntryTokenAsync(client, cookies, apiClient.SchemeId);

        var returnUrl = "/Comments/Index?schemeName=Brucella&sortBy=schemeName&sortDir=asc";
        var form = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["MonthlyDistributionSchemeId"] = apiClient.SchemeId.ToString(),
            ["Comments"] = "<p>Submit</p>",
            ["Acknowledged"] = "true",
            ["ReturnUrl"] = returnUrl,
            ["command"] = "submit"
        };

        var response = await CookieForwardingHttpClient.SendWithCookiesAsync(
            client, HttpMethod.Post, $"/Comments/Entry?schemeId={apiClient.SchemeId}", cookies, new FormUrlEncodedContent(form));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(returnUrl, response.Headers.Location!.ToString());
        var request = Assert.Single(apiClient.SavedRequests);
        Assert.True(request.Submit);
        Assert.Equal("<p>Submit</p>", request.Comments);
    }

    [Fact]
    public async Task Entry_Post_Draw_RedrawsUsingFilter_AndKeepsTypedComments()
    {
        var apiClient = new FakeTestConsultantTabulationApiClient { Tabulation = DraftTabulation() };
        var client = CreateClient(apiClient, allowAutoRedirect: false);
        var cookies = await SignInAsync(client, Guid.NewGuid());
        var (token, _) = await GetEntryTokenAsync(client, cookies, apiClient.SchemeId);

        var form = new List<KeyValuePair<string, string>>
        {
            new("__RequestVerificationToken", token),
            new("MonthlyDistributionSchemeId", apiClient.SchemeId.ToString()),
            new("Comments", "<p>Keep me</p>"),
            new("IsFiltered", "true"),
            new("AllSamples", "S001"),
            new("AllSamples", "S002"),
            new("ShownSamples", "S001"),
            new("ShowMethodItems", "false"),
            new("command", "draw")
        };

        var response = await CookieForwardingHttpClient.SendWithCookiesAsync(
            client, HttpMethod.Post, $"/Comments/Entry?schemeId={apiClient.SchemeId}", cookies, new FormUrlEncodedContent(form));
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Keep me", body, StringComparison.Ordinal);
        Assert.Empty(apiClient.SavedRequests);
        Assert.Contains(apiClient.TabulationRequests, f => f.HiddenSamples.Contains("S002") && f.HideMethodItems);
        Assert.Contains("Sample S001</span>", body, StringComparison.Ordinal);
        Assert.DoesNotContain("Sample S002</span>", body, StringComparison.Ordinal);
        Assert.Contains("full-tabulation-test-1", body, StringComparison.Ordinal);
        Assert.True(body.IndexOf("Show all sections", StringComparison.Ordinal) < body.IndexOf("Participant Information", StringComparison.Ordinal));
        Assert.Contains("Participant Information</span>", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Entry_Get_Submitted_ShowsReadOnlyAndNoEditControls()
    {
        var apiClient = new FakeTestConsultantTabulationApiClient { Tabulation = SubmittedTabulation() };
        var client = CreateClient(apiClient);
        var cookies = await SignInAsync(client, Guid.NewGuid());

        var response = await CookieForwardingHttpClient.SendWithCookiesAsync(client, HttpMethod.Get, $"/Comments/Entry?schemeId={apiClient.SchemeId}", cookies);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Comments provided by Dr Who", body, StringComparison.Ordinal);
        Assert.DoesNotContain("name=\"command\" value=\"save\"", body, StringComparison.Ordinal);
        Assert.DoesNotContain("name=\"command\" value=\"submit\"", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Entry_Get_DraftWithoutSamples_RendersDirectTablesRatingsAndSampleInformation()
    {
        var apiClient = new FakeTestConsultantTabulationApiClient { Tabulation = NoSampleDraftTabulation() };
        var client = CreateClient(apiClient);
        var cookies = await SignInAsync(client, Guid.NewGuid());

        var response = await CookieForwardingHttpClient.SendWithCookiesAsync(client, HttpMethod.Get, $"/Comments/Entry?schemeId={apiClient.SchemeId}", cookies);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain("Draw reports", body, StringComparison.Ordinal);
        Assert.Contains("Result direct cell", body, StringComparison.Ordinal);
        Assert.Contains("Results rating cell", body, StringComparison.Ordinal);
        Assert.Contains("Full direct cell", body, StringComparison.Ordinal);
        Assert.Contains("Participant direct cell", body, StringComparison.Ordinal);
        Assert.Contains("Full rating cell", body, StringComparison.Ordinal);
        Assert.Contains("S777 Positive", body, StringComparison.Ordinal);
        Assert.Contains("Detailed narrative", body, StringComparison.Ordinal);
        Assert.DoesNotContain("No additional sample information is available.", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Entry_Post_DrawWithoutSamples_UsesNonAccordionRenderingPaths()
    {
        var apiClient = new FakeTestConsultantTabulationApiClient { Tabulation = NoSampleDraftTabulation() };
        var client = CreateClient(apiClient, allowAutoRedirect: false);
        var cookies = await SignInAsync(client, Guid.NewGuid());
        var (token, _) = await GetEntryTokenAsync(client, cookies, apiClient.SchemeId);

        var form = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["MonthlyDistributionSchemeId"] = apiClient.SchemeId.ToString(),
            ["Comments"] = "<p>Keep me</p>",
            ["IsFiltered"] = "true",
            ["ShowMethodItems"] = "false",
            ["command"] = "draw"
        };

        var response = await CookieForwardingHttpClient.SendWithCookiesAsync(
            client, HttpMethod.Post, $"/Comments/Entry?schemeId={apiClient.SchemeId}", cookies, new FormUrlEncodedContent(form));
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Keep me", body, StringComparison.Ordinal);
        Assert.Contains("Result direct cell", body, StringComparison.Ordinal);
        Assert.Contains("Full direct cell", body, StringComparison.Ordinal);
        Assert.Contains("Participant direct cell", body, StringComparison.Ordinal);
        Assert.Contains("Results rating cell", body, StringComparison.Ordinal);
        Assert.Contains("Full rating cell", body, StringComparison.Ordinal);
        Assert.DoesNotContain("full-tabulation-test-1", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Entry_Get_SubmittedWithoutCommentBody_ShowsHeadingOnly()
    {
        var apiClient = new FakeTestConsultantTabulationApiClient
        {
            Tabulation = DraftTabulation() with
            {
                Comments = new TestConsultantCommentsStateResponse(true, "Comments provided by Dr Who", "<p>Submitted hidden</p>", false)
            }
        };
        var client = CreateClient(apiClient);
        var cookies = await SignInAsync(client, Guid.NewGuid());

        var response = await CookieForwardingHttpClient.SendWithCookiesAsync(client, HttpMethod.Get, $"/Comments/Entry?schemeId={apiClient.SchemeId}", cookies);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Comments provided by Dr Who", body, StringComparison.Ordinal);
        Assert.DoesNotContain("Submitted hidden", body, StringComparison.Ordinal);
    }

    private static async Task<(string Token, string Body)> GetEntryTokenAsync(HttpClient client, Dictionary<string, string> cookies, Guid schemeId)
    {
        var response = await CookieForwardingHttpClient.SendWithCookiesAsync(client, HttpMethod.Get, $"/Comments/Entry?schemeId={schemeId}", cookies);
        var body = await response.Content.ReadAsStringAsync();
        var token = Regex.Match(body, "__RequestVerificationToken[^>]*value=\"([^\"]+)\"").Groups[1].Value;
        return (token, body);
    }

    [Fact]
    public async Task Index_ViewLink_IncludesCurrentSearchAsReturnUrl()
    {
        var apiClient = new FakeTestConsultantTabulationApiClient
        {
            Distributions = new TestConsultantDistributionsResponse(
            [
                Distribution("D1a/S1 BT: Brucella")
            ])
        };
        var client = CreateClient(apiClient);
        var cookies = await SignInAsync(client, Guid.NewGuid());

        var response = await CookieForwardingHttpClient.SendWithCookiesAsync(
            client,
            HttpMethod.Get,
            "/Comments/Index?schemeName=Brucella&sortBy=schemeName&sortDir=asc",
            cookies);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains($"returnUrl={Uri.EscapeDataString("/Comments/Index?schemeName=Brucella&sortBy=schemeName&sortDir=asc")}", body, StringComparison.Ordinal);
    }

    private static TestConsultantDistributionResponse Distribution(string title) =>
        new(Guid.NewGuid(), 2026, 6, title, new DateTime(2026, 6, 30), false, "Awaiting comments");

    private static TestConsultantTabulationResponse DraftTabulation() =>
        new(
            Guid.NewGuid(),
            "D1a/S1 BT: Brucella",
            "Date Of Distribution: 01/05/2026",
            string.Empty,
            string.Empty,
            HasMethodItems: false,
            SampleNumbers: ["S001", "S002"],
            ResultsTables:
            [
                new TabulationTableResponse(
                    "Culture",
                    [new TabulationGridRowResponse([new TabulationCellResponse("S001", 1, 1, "Cell", false)], IsHeader: false)],
                    AssessmentTestId: null)
            ],
            ResultsRatings: null,
            FullTables:
            [
                new TabulationTableResponse(
                    "Various",
                    [new TabulationGridRowResponse([new TabulationCellResponse("S001", 1, 1, "Cell", false)], IsHeader: false)],
                    AssessmentTestId: null)
            ],
            FullRatings: null,
            ParticipantInformation: new TabulationTableResponse(
                "Participant information",
                [new TabulationGridRowResponse([new TabulationCellResponse("1188", 1, 1, "Cell", false), new TabulationCellResponse("No", 1, 1, "Cell", false), new TabulationCellResponse("", 1, 1, "Cell", false)], IsHeader: false)],
                AssessmentTestId: null),
            SampleInformation: [],
            Comments: new TestConsultantCommentsStateResponse(false, "Add your comments here:", "<p>Draft</p>", true));

    private static TestConsultantTabulationResponse SubmittedTabulation() =>
        DraftTabulation() with
        {
            Comments = new TestConsultantCommentsStateResponse(true, "Comments provided by Dr Who", "<p>Submitted</p>", true)
        };

    private static TestConsultantTabulationResponse NoSampleDraftTabulation() =>
        new(
            Guid.NewGuid(),
            "D1a/S1 BT: Brucella",
            "Date Of Distribution: 01/05/2026",
            "<p>Standard note</p>",
            "<p>Existing notes</p>",
            HasMethodItems: false,
            SampleNumbers: [],
            ResultsTables:
            [
                new TabulationTableResponse(
                    "Results table",
                    [new TabulationGridRowResponse([new TabulationCellResponse("Result direct cell", 1, 1, "Cell", false)], IsHeader: false)],
                    AssessmentTestId: null)
            ],
            ResultsRatings: new TabulationTableResponse(
                "Results ratings",
                [new TabulationGridRowResponse([new TabulationCellResponse("Results rating cell", 1, 1, "Cell", false)], IsHeader: false)],
                AssessmentTestId: null),
            FullTables:
            [
                new TabulationTableResponse(
                    "Full table",
                    [new TabulationGridRowResponse([new TabulationCellResponse("Full direct cell", 1, 1, "Cell", false)], IsHeader: false)],
                    AssessmentTestId: null)
            ],
            FullRatings: new TabulationTableResponse(
                "Full ratings",
                [new TabulationGridRowResponse([new TabulationCellResponse("Full rating cell", 1, 1, "Cell", false)], IsHeader: false)],
                AssessmentTestId: null),
            ParticipantInformation: new TabulationTableResponse(
                "Participant information",
                [new TabulationGridRowResponse([new TabulationCellResponse("Participant direct cell", 1, 1, "Cell", false)], IsHeader: false)],
                AssessmentTestId: null),
            SampleInformation:
            [
                new TestConsultantSampleInformationResponse("S777", "Positive", "Detailed narrative")
            ],
            Comments: new TestConsultantCommentsStateResponse(false, "Add your comments here:", "<p>Draft</p>", true));

    private sealed class FakeTestConsultantTabulationApiClient : ITestConsultantTabulationApiClient
    {
        public Guid SchemeId { get; } = Guid.NewGuid();
        public TestConsultantDistributionsResponse Distributions { get; set; } = new([]);
        public TestConsultantTabulationResponse Tabulation { get; set; } = DraftTabulation();
        public TestConsultantTabulationSaveResult SaveResult { get; set; } = new(true, false, new Dictionary<string, string>());
        public List<TestConsultantTabulationSaveRequest> SavedRequests { get; } = [];
        public List<TestConsultantTabulationFilter> TabulationRequests { get; } = [];

        public Task<TestConsultantDistributionsResponse> GetDistributionsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(Distributions);

        public Task<TestConsultantApiResult<TestConsultantTabulationResponse>> GetTabulationAsync(
            Guid monthlyDistributionSchemeId, TestConsultantTabulationFilter filter, CancellationToken cancellationToken = default)
        {
            if (monthlyDistributionSchemeId != SchemeId)
            {
                return Task.FromResult(new TestConsultantApiResult<TestConsultantTabulationResponse>(TestConsultantApiStatus.NotFound));
            }

            TabulationRequests.Add(filter);
            return Task.FromResult(new TestConsultantApiResult<TestConsultantTabulationResponse>(TestConsultantApiStatus.Ok, Tabulation));
        }

        public Task<TestConsultantApiResult<PTL.ApiClient.Distribution.TabulationAttachmentFile>> ExportTabulationAsync(
            Guid monthlyDistributionSchemeId, TestConsultantTabulationFilter filter, CancellationToken cancellationToken = default) =>
            Task.FromResult(new TestConsultantApiResult<PTL.ApiClient.Distribution.TabulationAttachmentFile>(TestConsultantApiStatus.NotFound));

        public Task<TestConsultantApiResult<TestConsultantTabulationSaveResult>> SaveCommentsAsync(
            Guid monthlyDistributionSchemeId, TestConsultantTabulationSaveRequest request, CancellationToken cancellationToken = default)
        {
            SavedRequests.Add(request);
            return Task.FromResult(new TestConsultantApiResult<TestConsultantTabulationSaveResult>(TestConsultantApiStatus.Ok, SaveResult));
        }
    }
}
