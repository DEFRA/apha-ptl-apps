using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PTL.ApiClient;
using PTL.ApiClient.Distribution;
using PTL.Contracts.Distribution;
using PTL.InternalWeb.Tests.TestSupport;
using static PTL.InternalWeb.Tests.TestSupport.TestConsultantTestData;

namespace PTL.InternalWeb.Tests.Integration;

// Renders Features/TestConsultant/Views (Index, Tabulation, AccessDenied) - legacy Test Consultant/MenuTestConsultants.aspx and
// Tabulation.aspx - through the real MVC pipeline, with the "Test Consultant" role signed in.
public class TestConsultantRenderTests : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly string TabulationUrl = $"/Test%20Consultant/Tabulation?monthlyDistributionSchemeId={SchemeId}";

    private readonly WebApplicationFactory<Program> _rawFactory;
    private readonly FakeTestConsultantTabulationApiClient _apiClient = new();

    public TestConsultantRenderTests(WebApplicationFactory<Program> factory)
    {
        _rawFactory = factory;
    }

    private WebApplicationFactory<Program> Factory(string roles = "Test Consultant") =>
        _rawFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ITestConsultantTabulationApiClient>();
                services.AddSingleton<ITestConsultantTabulationApiClient>(_apiClient);
                services.AddAuthentication(TestAuthHandler.SchemeName)
                    .AddScheme<TestAuthHandlerOptions, TestAuthHandler>(TestAuthHandler.SchemeName, options => options.ResolvedRoles = roles);
                services.PostConfigure<AuthenticationOptions>(options =>
                {
                    options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                    options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                    options.DefaultScheme = TestAuthHandler.SchemeName;
                });
            }));

    private static string MainContent(string html) => Regex.Match(html, "<main.*?</main>", RegexOptions.Singleline).Value;

    private static bool HasCommandButton(string html, string value, string label) =>
        Regex.IsMatch(html, $"<button type=\"submit\" name=\"command\" value=\"{value}\"[^>]*>{label}</button>");

    private async Task<string> GetAsync(string url, string roles = "Test Consultant", HttpStatusCode expected = HttpStatusCode.OK)
    {
        var response = await Factory(roles).CreateClient().GetAsync(url);

        Assert.Equal(expected, response.StatusCode);
        return await response.Content.ReadAsStringAsync();
    }

    private async Task<HttpResponseMessage> PostAsync(params (string Name, string Value)[] fields)
    {
        var client = Factory().CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var page = await (await client.GetAsync(TabulationUrl)).Content.ReadAsStringAsync();
        var token = Regex.Match(page, "__RequestVerificationToken[^>]*value=\"([^\"]+)\"").Groups[1].Value;

        var content = new List<KeyValuePair<string, string>> { new("__RequestVerificationToken", token) };
        content.AddRange(fields.Select(f => new KeyValuePair<string, string>(f.Name, f.Value)));
        return await client.PostAsync(TabulationUrl, new FormUrlEncodedContent(content));
    }

    // ---- Menu ---------------------------------------------------------------------------------

    [Fact]
    public async Task Menu_ListsTheMonthsAndDistributionsWithStatusAndLinks()
    {
        _apiClient.Distributions = new([
            Dist(2026, 6, "17208/BA: PT0074 Pasteurella", schemeId: SchemeId),
            Dist(2026, 1, "17050/BA: PT0001 Brucella", submitted: true),
            Dist(2025, 5, "Last year's distribution")]);

        var html = MainContent(await GetAsync("/Test%20Consultant"));

        Assert.Contains("June 2026", html, StringComparison.Ordinal);
        Assert.Contains("January 2026", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Last year", html, StringComparison.Ordinal);
        Assert.Contains("17208/BA: PT0074 Pasteurella", html, StringComparison.Ordinal);
        Assert.Contains("Awaiting comments", html, StringComparison.Ordinal);
        Assert.Contains("Comments submitted", html, StringComparison.Ordinal);
        Assert.Contains("21/06/2026", html, StringComparison.Ordinal);
        Assert.Contains($"href=\"/Test%20Consultant/Tabulation?monthlyDistributionSchemeId={SchemeId}\"", html, StringComparison.Ordinal);
        Assert.Contains("Comments Required", html, StringComparison.Ordinal);
        Assert.Contains("View Results / Comments", html, StringComparison.Ordinal);
        Assert.Contains("<option value=\"2026\" selected=\"selected\">2026</option>", html, StringComparison.Ordinal);
        Assert.Contains("<option value=\"2025\">2025</option>", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Menu_NoDistributions_SaysSo()
    {
        var html = MainContent(await GetAsync("/Test%20Consultant"));

        Assert.Contains("There are no distributions that require comments.", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Select Year", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Menu_SelectedYearWithoutMonths_SaysSo()
    {
        _apiClient.Distributions = new([Dist(2026, 6, "June")]);

        var html = MainContent(await GetAsync("/Test%20Consultant?year=1999"));

        Assert.Contains("June 2026", html, StringComparison.Ordinal);
    }

    // ---- Tabulation ---------------------------------------------------------------------------

    [Fact]
    public async Task Tabulation_ShowsTheTitleInstructionsFilterTabsAndTables()
    {
        _apiClient.Tabulation = new(TestConsultantApiStatus.Ok, Tabulation(withRatings: true));

        var html = MainContent(await GetAsync(TabulationUrl));

        Assert.Contains("Test Consultant Tabulation", html, StringComparison.Ordinal);
        Assert.Contains("FINAL REPORT for D1a/S1 BT: Brucella", html, StringComparison.Ordinal);
        Assert.Contains("Please use the box below the Tabulation to record your comments.", html, StringComparison.Ordinal);
        Assert.Contains("un-tick several of the check boxes", html, StringComparison.Ordinal);
        Assert.Contains(">Results Tabulation</a>", html, StringComparison.Ordinal);
        Assert.Contains(">Full Results Tabulation</a>", html, StringComparison.Ordinal);
        Assert.Contains(">Additional Sample Information</a>", html, StringComparison.Ordinal);
        Assert.Contains("PCR results", html, StringComparison.Ordinal);
        Assert.Contains("PCR full", html, StringComparison.Ordinal);
        Assert.Contains("QAU Ratings", html, StringComparison.Ordinal);
        Assert.Contains("Full Ratings", html, StringComparison.Ordinal);
        Assert.Contains("Participant information", html, StringComparison.Ordinal);
        Assert.Contains("Details of sample one", html, StringComparison.Ordinal);
        Assert.Contains("<p>Standard text</p>", html, StringComparison.Ordinal);
        Assert.Contains("<p>Scheme comments</p>", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Tabulation_OffersEveryColumnAsATickedCheckboxAndDrawTabulations()
    {
        _apiClient.Tabulation = new(TestConsultantApiStatus.Ok, Tabulation());

        var html = MainContent(await GetAsync(TabulationUrl));

        Assert.Contains("name=\"ShowMethodItems\" type=\"checkbox\" value=\"true\" checked=\"checked\"", html, StringComparison.Ordinal);
        Assert.Contains("name=\"ShownSamples\" type=\"checkbox\" value=\"26/1\" checked=\"checked\"", html, StringComparison.Ordinal);
        Assert.Contains("name=\"ShownSamples\" type=\"checkbox\" value=\"26/2\" checked=\"checked\"", html, StringComparison.Ordinal);
        Assert.Contains("<input type=\"hidden\" name=\"AllSamples\" value=\"26/2\" />", html, StringComparison.Ordinal);
        Assert.Contains("<input type=\"hidden\" name=\"IsFiltered\" value=\"true\" />", html, StringComparison.Ordinal);
        Assert.True(HasCommandButton(html, "draw", "Draw tabulations"));
        Assert.True(HasCommandButton(html, "export", "Export to Excel"));
    }

    [Fact]
    public async Task Tabulation_NoMethodItemsAndNoSamples_OffersNoFilter()
    {
        _apiClient.Tabulation = new(TestConsultantApiStatus.Ok, Tabulation(hasMethodItems: false, samples: [], withSampleInformation: false));

        var html = MainContent(await GetAsync(TabulationUrl));

        Assert.DoesNotContain("Columns to show", html, StringComparison.Ordinal);
        Assert.False(HasCommandButton(html, "draw", "Draw tabulations"));
        Assert.Contains("No additional sample information is available.", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Tabulation_NotSubmitted_ShowsTheEditorWithTheDraftAndSaveCancelAndSubmit()
    {
        _apiClient.Tabulation = new(TestConsultantApiStatus.Ok, Tabulation());

        var html = MainContent(await GetAsync(TabulationUrl));

        Assert.Contains("Add your comments here:", html, StringComparison.Ordinal);
        Assert.Contains("data-module=\"ptl-rich-text\"", html, StringComparison.Ordinal);
        Assert.Contains("&lt;p&gt;Draft&lt;/p&gt;", html, StringComparison.Ordinal);
        Assert.True(HasCommandButton(html, "save", "Save"));
        Assert.True(HasCommandButton(html, "cancel", "Cancel"));
        Assert.True(HasCommandButton(html, "submit", "Save and Submit"));
        Assert.Contains("Once your comments are submitted, you will not be able to edit them again.", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Tabulation_Submitted_ShowsTheCommentsReadOnlyWithoutSaveOrSubmit()
    {
        _apiClient.Tabulation = new(TestConsultantApiStatus.Ok, Tabulation(submitted: true, heading: "Comments provided by A Author:", commentsHtml: "<p>Final words</p>"));

        var html = MainContent(await GetAsync(TabulationUrl));

        Assert.Contains("Comments provided by A Author:", html, StringComparison.Ordinal);
        Assert.Contains("<p>Final words</p>", html, StringComparison.Ordinal);
        Assert.DoesNotContain("data-module=\"ptl-rich-text\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Please use the box below the Tabulation", html, StringComparison.Ordinal);
        Assert.False(HasCommandButton(html, "save", "Save"));
        Assert.False(HasCommandButton(html, "submit", "Save and Submit"));
        Assert.True(HasCommandButton(html, "cancel", "Back"));
        Assert.True(HasCommandButton(html, "export", "Export to Excel"));
    }

    [Fact]
    public async Task Tabulation_SubmittedWithoutComments_SaysNoneWereSubmitted()
    {
        _apiClient.Tabulation = new(TestConsultantApiStatus.Ok, Tabulation(submitted: true, heading: "No comments were submitted before the deadline", commentsHtml: string.Empty));

        var html = MainContent(await GetAsync(TabulationUrl));

        Assert.Contains("No comments were submitted before the deadline", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Tabulation_NotAllocatedToTheScheme_ShowsAccessDenied()
    {
        _apiClient.Tabulation = new(TestConsultantApiStatus.Forbidden);

        var html = MainContent(await GetAsync(TabulationUrl, expected: HttpStatusCode.Forbidden));

        Assert.Contains("You are not a Test Consultant for this distribution.", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Tabulation_UnknownScheme_IsNotFound() =>
        await GetAsync(TabulationUrl, expected: HttpStatusCode.NotFound);

    // ---- Posting ------------------------------------------------------------------------------

    [Fact]
    public async Task Post_Save_SendsTheCommentsAndRedirectsToTheMenu()
    {
        _apiClient.Tabulation = new(TestConsultantApiStatus.Ok, Tabulation());

        var response = await PostAsync(("Comments", "<p>New</p>"), ("command", "submit"));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Test%20Consultant", response.Headers.Location!.OriginalString);
        var (schemeId, request) = Assert.Single(_apiClient.Saved);
        Assert.Equal((SchemeId, "<p>New</p>", true), (schemeId, request.Comments, request.Submit));
    }

    [Fact]
    public async Task Post_SaveWithoutComments_RedisplaysWithTheRequiredError()
    {
        _apiClient.Tabulation = new(TestConsultantApiStatus.Ok, Tabulation());

        var response = await PostAsync(("Comments", string.Empty), ("command", "save"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Comments required.", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.Empty(_apiClient.Saved);
    }

    [Fact]
    public async Task Post_Draw_RedrawsWithTheFilterAndKeepsTheComments()
    {
        _apiClient.Tabulation = new(TestConsultantApiStatus.Ok, Tabulation());

        var response = await PostAsync(
            ("Comments", "<p>Typed so far</p>"), ("command", "draw"), ("IsFiltered", "true"), ("AllSamples", "26/1"), ("AllSamples", "26/2"), ("ShownSamples", "26/1"), ("ShowMethodItems", "false"));

        var html = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("&lt;p&gt;Typed so far&lt;/p&gt;", html, StringComparison.Ordinal);
        var filter = _apiClient.TabulationFilters[^1];
        Assert.Equal(["26/2"], filter.HiddenSamples);
        Assert.True(filter.HideMethodItems);
    }

    // ---- Access -------------------------------------------------------------------------------

    [Fact]
    public async Task Menu_UserWithoutTheTestConsultantRole_IsRefused()
    {
        var response = await Factory("Admin").CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }).GetAsync("/Test%20Consultant");

        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Menu_WithoutSignIn_RedirectsAwayFromThePage()
    {
        var client = _rawFactory.WithWebHostBuilder(builder => { }).CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync("/Test%20Consultant")).StatusCode);
    }

    [Fact]
    public async Task TheEmailLink_OpensTheTabulation()
    {
        _apiClient.Tabulation = new(TestConsultantApiStatus.Ok, Tabulation());

        // TabulationEmailOptions.InternalCommentsPath + ?MonthlyDistributionSchemeId=...
        var html = await GetAsync($"/Test%20Consultant/Tabulation?MonthlyDistributionSchemeId={SchemeId}");

        Assert.Contains("FINAL REPORT for D1a/S1 BT: Brucella", html, StringComparison.Ordinal);
    }
}
