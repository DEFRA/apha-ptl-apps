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
        Assert.Contains("<th scope=\"col\" class=\"govuk-table__header\">Distribution reference</th>", body, StringComparison.Ordinal);
        Assert.Contains("<th scope=\"col\" class=\"govuk-table__header\">Scheme code</th>", body, StringComparison.Ordinal);
        Assert.Contains("<th scope=\"col\" class=\"govuk-table__header\">Scheme name</th>", body, StringComparison.Ordinal);
        Assert.Contains("<td class=\"govuk-table__cell\">D1a</td>", body, StringComparison.Ordinal);
        Assert.Contains("<td class=\"govuk-table__cell\">S1 BT</td>", body, StringComparison.Ordinal);
        Assert.Contains("<td class=\"govuk-table__cell\">Brucella</td>", body, StringComparison.Ordinal);
        Assert.DoesNotContain("Salmonella", body, StringComparison.Ordinal);
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
        Assert.Contains("Results Report", body, StringComparison.Ordinal);
        Assert.Contains("Full Report", body, StringComparison.Ordinal);
        Assert.Contains("Additional Sample Information", body, StringComparison.Ordinal);
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

        var form = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["MonthlyDistributionSchemeId"] = apiClient.SchemeId.ToString(),
            ["Comments"] = "<p>Submit</p>",
            ["Acknowledged"] = "true",
            ["command"] = "submit"
        };

        var response = await CookieForwardingHttpClient.SendWithCookiesAsync(
            client, HttpMethod.Post, $"/Comments/Entry?schemeId={apiClient.SchemeId}", cookies, new FormUrlEncodedContent(form));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.EndsWith("/Comments/Index", response.Headers.Location!.ToString(), StringComparison.Ordinal);
        var request = Assert.Single(apiClient.SavedRequests);
        Assert.True(request.Submit);
        Assert.Equal("<p>Submit</p>", request.Comments);
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

    private static async Task<(string Token, string Body)> GetEntryTokenAsync(HttpClient client, Dictionary<string, string> cookies, Guid schemeId)
    {
        var response = await CookieForwardingHttpClient.SendWithCookiesAsync(client, HttpMethod.Get, $"/Comments/Entry?schemeId={schemeId}", cookies);
        var body = await response.Content.ReadAsStringAsync();
        var token = Regex.Match(body, "__RequestVerificationToken[^>]*value=\"([^\"]+)\"").Groups[1].Value;
        return (token, body);
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
            SampleNumbers: [],
            ResultsTables:
            [
                new TabulationTableResponse(
                    "Culture",
                    [new TabulationGridRowResponse([new TabulationCellResponse("S001", 1, 1, "Cell", false)], IsHeader: false)],
                    AssessmentTestId: null)
            ],
            ResultsRatings: null,
            FullTables: [],
            FullRatings: null,
            ParticipantInformation: null,
            SampleInformation: [],
            Comments: new TestConsultantCommentsStateResponse(false, "Add your comments here:", "<p>Draft</p>", true));

    private static TestConsultantTabulationResponse SubmittedTabulation() =>
        DraftTabulation() with
        {
            Comments = new TestConsultantCommentsStateResponse(true, "Comments provided by Dr Who", "<p>Submitted</p>", true)
        };

    private sealed class FakeTestConsultantTabulationApiClient : ITestConsultantTabulationApiClient
    {
        public Guid SchemeId { get; } = Guid.NewGuid();
        public TestConsultantDistributionsResponse Distributions { get; set; } = new([]);
        public TestConsultantTabulationResponse Tabulation { get; set; } = DraftTabulation();
        public TestConsultantTabulationSaveResult SaveResult { get; set; } = new(true, false, new Dictionary<string, string>());
        public List<TestConsultantTabulationSaveRequest> SavedRequests { get; } = [];

        public Task<TestConsultantDistributionsResponse> GetDistributionsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(Distributions);

        public Task<TestConsultantApiResult<TestConsultantTabulationResponse>> GetTabulationAsync(
            Guid monthlyDistributionSchemeId, TestConsultantTabulationFilter filter, CancellationToken cancellationToken = default)
        {
            if (monthlyDistributionSchemeId != SchemeId)
            {
                return Task.FromResult(new TestConsultantApiResult<TestConsultantTabulationResponse>(TestConsultantApiStatus.NotFound));
            }

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
