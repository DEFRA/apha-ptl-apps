using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PTL.ApiClient.Distribution;
using PTL.Contracts.Distribution;
using PTL.InternalWeb.Tests.TestSupport;

namespace PTL.InternalWeb.Tests.Integration;

// Renders Features/ResultsSignOff/Views/Index - legacy Results Sign-off/SignOff.aspx - through the real MVC pipeline, with the
// "Results Sign-off" role signed in. The wording is legacy's.
public class ResultsSignOffRenderTests(WebApplicationFactory<Program> rawFactory) : IClassFixture<WebApplicationFactory<Program>>
{
    private const string QueueUrl = "/Results%20Sign-off";

    private static readonly Guid TabulationId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private readonly FakeResultsSignOffApiClient _apiClient = new();
    private readonly FakeDistributionApiClient _distribution = new();

    private WebApplicationFactory<Program> Factory(string roles) =>
        rawFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IResultsSignOffApiClient>();
                services.AddSingleton<IResultsSignOffApiClient>(_apiClient);
                services.RemoveAll<IDistributionApiClient>();
                services.AddSingleton<IDistributionApiClient>(_distribution);
                services.Configure<TestAuthHandlerOptions>(TestAuthHandler.SchemeName, options => options.ResolvedRoles = roles);
            }));

    private static string MainContent(string html) => Regex.Match(html, "<main.*?</main>", RegexOptions.Singleline).Value;

    private async Task<string> GetAsync(string roles = "Results Sign-off")
    {
        var response = await Factory(roles).CreateClient().GetAsync(QueueUrl);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync();
    }

    private void Queue(int publishCount = 0, bool single = false, string availability = "All") =>
        _apiClient.Queue = new ResultsSignOffQueueResponse(
        [
            new ResultsSignOffMonthResponse(2026, 6,
            [
                new ResultsSignOffSchemeResponse(
                    Guid.NewGuid(),
                    "17208/BA/PT0074 PM: Pasteurella",
                    new DateTime(2026, 7, 21),
                    "Jane Smith, John Doe",
                    "Ann Assessor",
                    [new ResultsSignOffTabulationResponse(TabulationId, "FINAL REPORT", publishCount, single, availability, "Awaiting Sign-off")]),
            ]),
        ]);

    [Fact]
    public async Task Queue_NothingAwaiting_SaysSoInLegacyWords()
    {
        var html = MainContent(await GetAsync());

        Assert.Contains("<h1 class=\"govuk-heading-l\">Results Sign-off</h1>", html, StringComparison.Ordinal);
        Assert.Contains("There are no Tabulations awaiting Sign-off.", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<table", html, StringComparison.Ordinal);
        Assert.DoesNotContain("The table below shows", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Queue_ListsEachMonthWithItsSchemesCollapsedAndTheDueDate()
    {
        Queue();

        var html = MainContent(await GetAsync());

        Assert.Contains("The table below shows each Tabulation on each Distribution that is ready to be signed-off.", html, StringComparison.Ordinal);
        Assert.Contains("<h2 class=\"govuk-heading-m\">June 2026</h2>", html, StringComparison.Ordinal);
        Assert.Contains("17208/BA/PT0074 PM: Pasteurella", html, StringComparison.Ordinal);
        Assert.Contains("(Due Date: 21/07/2026)", html, StringComparison.Ordinal);
        Assert.Matches("<details class=\"govuk-details\">", html);
        Assert.DoesNotContain("<details class=\"govuk-details\" open", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Queue_ShowsLegacyColumnsPlusTheStoryColumns()
    {
        Queue(single: true, availability: "Participants Only");

        var html = MainContent(await GetAsync());

        var headers = Regex.Matches(html, "<th scope=\"col\" class=\"govuk-table__header\">([^<]*)</th>").Select(m => m.Groups[1].Value);
        Assert.Equal(["Tabulation Name", "Test Consultant", "Assessor", "Single Participant", "Availability", "Status", "View"], headers);
        Assert.Contains("<td class=\"govuk-table__cell\">Jane Smith, John Doe</td>", html, StringComparison.Ordinal);
        Assert.Contains("<td class=\"govuk-table__cell\">Ann Assessor</td>", html, StringComparison.Ordinal);
        Assert.Contains("<td class=\"govuk-table__cell\">Yes</td>", html, StringComparison.Ordinal);
        Assert.Contains("<td class=\"govuk-table__cell\">Participants Only</td>", html, StringComparison.Ordinal);
        Assert.Contains("Awaiting Sign-off", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Queue_NeverPublished_ShowsNoPublishCount()
    {
        Queue(publishCount: 0);

        var html = MainContent(await GetAsync());

        Assert.DoesNotContain("has already been published", html, StringComparison.Ordinal);
        Assert.Contains("<td class=\"govuk-table__cell\">No</td>", html, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(1, "time")]
    [InlineData(3, "times")]
    public async Task Queue_PreviouslyPublished_ShowsTheCountInBracketsWithAReadableExplanation(int count, string times)
    {
        Queue(publishCount: count);

        var html = MainContent(await GetAsync());

        Assert.Contains($"(" + count + ")", html, StringComparison.Ordinal);
        Assert.Contains($"This Tabulation has already been published {count} {times}", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Queue_ViewLinksToTheReviewPageForThatTabulation()
    {
        Queue();

        var html = MainContent(await GetAsync());

        Assert.Contains($"href=\"/Results%20Sign-off/Tabulation?tabulationId={TabulationId}\"", html, StringComparison.Ordinal);
        Assert.Matches("View<span class=\"govuk-visually-hidden\"> FINAL REPORT</span>", html);
    }

    [Fact]
    public async Task Queue_TablesScrollInsideALabelledRegion()
    {
        Queue();

        var html = MainContent(await GetAsync());

        Assert.Contains("<section class=\"ptl-table-scroll\" aria-label=\"Tabulations for 17208/BA/PT0074 PM: Pasteurella\">", html, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Admin")]
    [InlineData("Test Consultant")]
    [InlineData("Scheme Admin,Contracts Admin")]
    public async Task Queue_WithoutTheResultsSignOffRole_IsRefused(string roles)
    {
        var client = Factory(roles).CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync(QueueUrl);

        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
    }

    // The side navigation shows only the current node's title (legacy behaviour), so on this page it names the node; the
    // node's link target is the route the controller is mapped to.
    [Fact]
    public async Task Queue_NavigationNamesTheNodeAndItsRouteIsTheQueueUrl()
    {
        var factory = Factory("Results Sign-off");
        var response = await factory.CreateClient().GetAsync(QueueUrl);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("<div class=\"govuk-body app-side-navigation__title\">Results Sign-off</div>", html, StringComparison.Ordinal);
        using var scope = factory.Services.CreateScope();
        Assert.Equal(QueueUrl, scope.ServiceProvider.GetRequiredService<LinkGenerator>().GetPathByAction("Index", "ResultsSignOff"));
    }

    // ---- Review page ----------------------------------------------------------------------------

    private static readonly Guid TestId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid AttachmentId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid ParticipantId = Guid.Parse("55555555-5555-5555-5555-555555555555");

    private static string ReviewUrl => $"/Results%20Sign-off/Tabulation?tabulationId={TabulationId}";

    private static TabulationTableResponse Table(Guid? assessmentTestId) =>
        new("Brucella", [new TabulationGridRowResponse([new TabulationCellResponse("Lab 001", 1, 1, "Cell", false)], false)], assessmentTestId);

    private void Report(bool withOptions = false, bool withAttachment = false, string standard = "", string comments = "", string consultantComments = "", bool hasMethodItems = false, params string[] samples)
    {
        var report = new TabulationReportResponse(
            TabulationId, false, TabulationId, "FINAL REPORT for D1a/S1 BT: Brucella", "Summary", "Date Of Distribution: 03/05/2026",
            standard, comments, consultantComments, "Comments provided by Dr A, QAU:", false, null,
            withOptions ? [new TabulationFilterOptionResponse(Guid.Empty, "All Participants"), new TabulationFilterOptionResponse(ParticipantId, "L001")] : [],
            Guid.Empty,
            withOptions ? [new TabulationFilterOptionResponse(Guid.Empty, "Normal View")] : [],
            Guid.Empty,
            [Table(TestId)], null,
            withAttachment ? new TabulationAttachmentResponse(AttachmentId, "support.pdf") : null,
            new TabulationPrintResponse([], [], []),
            "D1a/S1 BT: Brucella");
        _apiClient.Review = new ResultsSignOffReviewResponse(report, hasMethodItems, samples);
    }

    private async Task<string> GetReviewAsync(string roles = "Results Sign-off")
    {
        var response = await Factory(roles).CreateClient().GetAsync(ReviewUrl);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return MainContent(await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Review_ShowsTheDistributionTheTabulationAndItsTables()
    {
        Report();

        var html = await GetReviewAsync();

        Assert.Contains("<h1 class=\"govuk-heading-l\">Results Sign-Off Tabulation</h1>", html, StringComparison.Ordinal);
        Assert.Contains("<h2 class=\"govuk-heading-m\">D1a/S1 BT: Brucella</h2>", html, StringComparison.Ordinal);
        Assert.Contains("<h3 class=\"govuk-heading-s\">Summary</h3>", html, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"Brucella\"", html, StringComparison.Ordinal);
        Assert.Contains("Lab 001", html, StringComparison.Ordinal);
        Assert.Equal([(TabulationId, Guid.Empty, Guid.Empty, TestConsultantTabulationFilter.None)], _apiClient.RequestedReviews);
    }

    // ---- Draw tabulation (legacy check boxes and "Draw tabulation" button) ---------------------

    [Fact]
    public async Task Review_WithSamplesAndMethodItems_OffersTheCheckBoxesTickedAndTheDrawButtonInLegacyWords()
    {
        Report(hasMethodItems: true, samples: ["26/1", "26/2"]);

        var html = await GetReviewAsync();

        Assert.Contains(">Method Items</label>", html, StringComparison.Ordinal);
        Assert.Matches("<input[^>]*name=\"ShownSamples\"[^>]*value=\"26/1\"[^>]*checked=\"checked\"", html);
        Assert.Matches("<input[^>]*name=\"ShownSamples\"[^>]*value=\"26/2\"[^>]*checked=\"checked\"", html);
        Assert.Matches("<input[^>]*name=\"ShowMethodItems\"[^>]*type=\"checkbox\"[^>]*checked=\"checked\"", html);
        Assert.Contains("name=\"AllSamples\" value=\"26/1\"", html, StringComparison.Ordinal);
        Assert.Contains("data-module=\"govuk-button\">Draw tabulation</button>", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Review_NoSamplesOrMethodItems_OffersNoCheckBoxes()
    {
        Report(withOptions: true);

        var html = await GetReviewAsync();

        Assert.DoesNotContain("Columns to show", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Draw tabulation", html, StringComparison.Ordinal);
        Assert.Contains("data-module=\"govuk-button\">View</button>", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Review_DrawnWithAColumnUnticked_AsksForTheTabulationWithoutIt()
    {
        Report(hasMethodItems: true, samples: ["26/1", "26/2"]);
        var client = Factory("Results Sign-off").CreateClient();

        var response = await client.GetAsync($"{ReviewUrl}&IsFiltered=true&AllSamples=26/1&AllSamples=26/2&ShownSamples=26/1&ShowMethodItems=false");
        var html = MainContent(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var filter = Assert.Single(_apiClient.RequestedReviews).Filter;
        Assert.Equal(["26/2"], filter.HiddenSamples);
        Assert.True(filter.HideMethodItems);
        Assert.Matches("<input[^>]*name=\"ShownSamples\"[^>]*value=\"26/1\"[^>]*checked=\"checked\"", html);
        Assert.DoesNotMatch("<input[^>]*name=\"ShownSamples\"[^>]*value=\"26/2\"[^>]*checked=\"checked\"", html);
        Assert.DoesNotMatch("<input[^>]*name=\"ShowMethodItems\"[^>]*type=\"checkbox\"[^>]*checked=\"checked\"", html);
    }

    [Fact]
    public async Task Review_AssessmentLinkGoesToTheSignOffChartPage()
    {
        Report();

        var html = await GetReviewAsync();

        Assert.Contains($"href=\"/Results%20Sign-off/AssessmentResults?tabulationId={TabulationId}&amp;testId={TestId}\"", html, StringComparison.Ordinal);
        Assert.Contains("View assessment results", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Review_OffersTheParticipantAndViewerSelectorsInLegacyWords()
    {
        Report(withOptions: true);

        var html = await GetReviewAsync();

        Assert.Contains("Select Participant:", html, StringComparison.Ordinal);
        Assert.Contains("Select Viewer:", html, StringComparison.Ordinal);
        Assert.Contains("All Participants", html, StringComparison.Ordinal);
        Assert.Contains("Normal View", html, StringComparison.Ordinal);
        Assert.Contains($"<input type=\"hidden\" name=\"tabulationId\" value=\"{TabulationId}\" />", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Review_NoSelectors_WhenThereAreNoOptions()
    {
        Report();

        Assert.DoesNotContain("Select Participant:", await GetReviewAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Review_ShowsTheCommentsAndSupportingDocument()
    {
        Report(withAttachment: true, standard: "<p>Standard text</p>", comments: "<p>Scheme comments</p>", consultantComments: "<p>Consultant words</p>");

        var html = await GetReviewAsync();

        var comments = Regex.Match(html, "<div id=\"commentsDiv\">(?<inner>.*?)</div>\\s*</div>\\s*<p class=\"govuk-body\">", RegexOptions.Singleline).Groups["inner"].Value;
        Assert.Contains("<p>Standard text</p>", comments, StringComparison.Ordinal);
        Assert.Contains("<p>Scheme comments</p>", comments, StringComparison.Ordinal);
        Assert.Contains("<h2 class=\"govuk-heading-m commentsTitle\">Comments provided by Dr A, QAU:</h2>", comments, StringComparison.Ordinal);
        Assert.Contains("<p>Consultant words</p>", comments, StringComparison.Ordinal);
        Assert.Contains($"href=\"/Results%20Sign-off/Attachment?tabulationId={TabulationId}&amp;attachmentId={AttachmentId}\">View Supporting Document</a>", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Review_WithoutCommentsOrAttachment_ShowsNeither()
    {
        Report();

        var html = await GetReviewAsync();

        Assert.DoesNotContain("commentsTitle", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Supporting Document", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Review_OffersAuthoriseRejectAndCancelInLegacyOrderInOneAlignedButtonGroup()
    {
        Report();

        var html = await GetReviewAsync();

        // One form holds the group, so the three controls are direct children of govuk-button-group and share a baseline.
        var form = Regex.Match(html, "<form[^>]*method=\"post\"[^>]*action=\"/Results%20Sign-off/Authorise\"[^>]*>(?<inner>.*?)</form>", RegexOptions.Singleline).Groups["inner"].Value;
        Assert.NotEmpty(form);
        var group = Regex.Match(form, "<div class=\"govuk-button-group\">(?<inner>.*?)</div>", RegexOptions.Singleline).Groups["inner"].Value;
        Assert.DoesNotContain("<form", group, StringComparison.Ordinal);
        Assert.Matches("<button[^>]*class=\"govuk-button\"[^>]*formaction=\"/Results%20Sign-off/Authorise\"[^>]*>Authorise</button>", group);
        Assert.Matches("<button[^>]*class=\"govuk-button govuk-button--warning\"[^>]*formaction=\"/Results%20Sign-off/Reject\"[^>]*>Reject</button>", group);
        Assert.Matches("<a class=\"govuk-button govuk-button--secondary\"[^>]*href=\"/Results%20Sign-off\">Cancel</a>", group);
        Assert.True(group.IndexOf(">Authorise</button>", StringComparison.Ordinal) < group.IndexOf(">Reject</button>", StringComparison.Ordinal));
        Assert.True(group.IndexOf(">Reject</button>", StringComparison.Ordinal) < group.IndexOf(">Cancel</a>", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Review_UnknownTabulation_IsNotFound()
    {
        var response = await Factory("Results Sign-off").CreateClient().GetAsync(ReviewUrl);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Review_BreadcrumbsRunHomeResultsSignOffThenTheTabulation()
    {
        Report();
        var response = await Factory("Results Sign-off").CreateClient().GetAsync(ReviewUrl);
        var html = await response.Content.ReadAsStringAsync();

        var crumbs = Regex.Match(html, "<ol class=\"govuk-breadcrumbs__list\">(?<inner>.*?)</ol>", RegexOptions.Singleline).Groups["inner"].Value;
        Assert.Contains("href=\"/Results%20Sign-off\">Results Sign-off</a>", crumbs, StringComparison.Ordinal);
        Assert.Matches("Results Sign-Off Tabulation\\s*</li>", crumbs);
    }

    [Theory]
    [InlineData("Admin")]
    [InlineData("Scheme Admin,Internal User,Distributions,Scheduling")]
    public async Task Review_WithoutTheResultsSignOffRole_IsRefused(string roles)
    {
        Report();
        var client = Factory(roles).CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync(ReviewUrl);

        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
    }

    // ---- Assessment results ---------------------------------------------------------------------

    private static string AssessmentUrl => $"/Results%20Sign-off/AssessmentResults?tabulationId={TabulationId}&testId={TestId}";

    [Fact]
    public async Task AssessmentResults_ShowsTheChartWithItsDataTableAndALinkBackToTheTabulation()
    {
        _distribution.AssessmentResults = new AssessmentResultsResponse(
            true, ["Bar chart shows the frequency of assessment scores"], "Assessment Score", "Frequency",
            [new AssessmentScoreFrequencyResponse(1, "Pass", 3), new AssessmentScoreFrequencyResponse(2, "Fail", 1)],
            "D1a/S1 BT: Brucella");

        var response = await Factory("Results Sign-off").CreateClient().GetAsync(AssessmentUrl);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var main = MainContent(html);
        Assert.Contains("<h1 class=\"govuk-heading-l\">Assessment Results</h1>", main, StringComparison.Ordinal);
        Assert.Contains("<h2 class=\"govuk-heading-m\">D1a/S1 BT: Brucella</h2>", main, StringComparison.Ordinal);
        Assert.Contains("<svg class=\"ptl-assessment-chart\"", main, StringComparison.Ordinal);
        Assert.Contains("Bar chart shows the frequency of assessment scores (data table)", main, StringComparison.Ordinal);
        Assert.Contains($"href=\"/Results%20Sign-off/Tabulation?tabulationId={TabulationId}\">Back</a>", main, StringComparison.Ordinal);
        Assert.Equal([(TabulationId, TestId, false)], _distribution.RequestedAssessmentResults);
    }

    [Fact]
    public async Task AssessmentResults_BreadcrumbLinksBackToTheTabulation()
    {
        _distribution.AssessmentResults = new AssessmentResultsResponse(false, [], "x", "y", []);

        var response = await Factory("Results Sign-off").CreateClient().GetAsync(AssessmentUrl);
        var html = await response.Content.ReadAsStringAsync();

        var crumbs = Regex.Match(html, "<ol class=\"govuk-breadcrumbs__list\">(?<inner>.*?)</ol>", RegexOptions.Singleline).Groups["inner"].Value;
        Assert.Contains($"href=\"/Results%20Sign-off/Tabulation?tabulationId={TabulationId}\">Results Sign-Off Tabulation</a>", crumbs, StringComparison.Ordinal);
        Assert.Matches("Assessment Results\\s*</li>", crumbs);
    }

    [Fact]
    public async Task AssessmentResults_UnknownTest_IsNotFound()
    {
        var response = await Factory("Results Sign-off").CreateClient().GetAsync(AssessmentUrl);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ---- Attachment -----------------------------------------------------------------------------

    [Fact]
    public async Task Attachment_StreamsTheFile()
    {
        _distribution.TabulationAttachment = new TabulationAttachmentFile([1, 2, 3], "application/pdf", "inline; filename=support.pdf");

        var response = await Factory("Results Sign-off").CreateClient().GetAsync($"/Results%20Sign-off/Attachment?tabulationId={TabulationId}&attachmentId={AttachmentId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType!.MediaType);
        Assert.Equal([1, 2, 3], await response.Content.ReadAsByteArrayAsync());
    }

    // ---- Reject ---------------------------------------------------------------------------------

    private static async Task<HttpResponseMessage> PostRejectAsync(HttpClient client, params (string Name, string Value)[] extra) =>
        await PostDecisionAsync(client, "Reject", extra);

    private static async Task<HttpResponseMessage> PostDecisionAsync(HttpClient client, string action, params (string Name, string Value)[] extra)
    {
        var page = await (await client.GetAsync(ReviewUrl)).Content.ReadAsStringAsync();
        var token = Regex.Match(page, "__RequestVerificationToken[^>]*value=\"([^\"]+)\"").Groups[1].Value;

        var fields = new List<KeyValuePair<string, string>> { new("__RequestVerificationToken", token), new("tabulationId", TabulationId.ToString()) };
        fields.AddRange(extra.Select(f => new KeyValuePair<string, string>(f.Name, f.Value)));
        return await client.PostAsync($"/Results%20Sign-off/{action}", new FormUrlEncodedContent(fields));
    }

    [Fact]
    public async Task Reject_AsksOnTheSharedConfirmationPageFirstAndRejectsNothing()
    {
        Report();
        var client = Factory("Results Sign-off").CreateClient();

        var response = await PostRejectAsync(client);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Are you sure you wish to Reject this Tabulation?", html, StringComparison.Ordinal);
        Assert.Contains("Rejected Tabulations are returned for amendments and restart the tabulation cycle.", html, StringComparison.Ordinal);
        Assert.Contains("govuk-button--warning", html, StringComparison.Ordinal);
        Assert.Contains($"href=\"/Results%20Sign-off/Tabulation?tabulationId={TabulationId}\"", html, StringComparison.Ordinal);
        Assert.Empty(_apiClient.Rejected);
    }

    [Fact]
    public async Task Reject_Confirmed_RejectsAndReturnsToTheQueueWithASuccessMessage()
    {
        Report();
        var client = Factory("Results Sign-off").CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = true });

        var response = await PostRejectAsync(client, ("confirmation", "confirm"));
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal([TabulationId], _apiClient.Rejected);
        Assert.Contains("The Tabulation has been rejected.", html, StringComparison.Ordinal);
        Assert.Contains("<h1 class=\"govuk-heading-l\">Results Sign-off</h1>", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Reject_NoLongerAwaiting_ReturnsToTheQueueWithAnErrorMessage()
    {
        Report();
        _apiClient.RejectStatus = ResultsSignOffApiStatus.NotAwaiting;
        var client = Factory("Results Sign-off").CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = true });

        var response = await PostRejectAsync(client, ("confirmation", "confirm"));
        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("This Tabulation is no longer awaiting Sign-off.", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Reject_WithoutTheResultsSignOffRole_IsRefusedAndRejectsNothing()
    {
        var client = Factory("Admin").CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.PostAsync("/Results%20Sign-off/Reject", new FormUrlEncodedContent([new("tabulationId", TabulationId.ToString()), new("confirmation", "confirm")]));

        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(_apiClient.Rejected);
    }

    // ---- Authorise ------------------------------------------------------------------------------

    [Fact]
    public async Task Authorise_AsksOnTheSharedConfirmationPageFirstAndAuthorisesNothing()
    {
        Report();
        var client = Factory("Results Sign-off").CreateClient();

        var response = await PostDecisionAsync(client, "Authorise");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Are you sure you wish to Authorise this Tabulation?", html, StringComparison.Ordinal);
        Assert.Contains("Authorising publishes the Tabulation and queues its Tabulation Published notification emails.", html, StringComparison.Ordinal);
        Assert.DoesNotContain("govuk-button--warning", html, StringComparison.Ordinal);
        Assert.Contains($"href=\"/Results%20Sign-off/Tabulation?tabulationId={TabulationId}\"", html, StringComparison.Ordinal);
        Assert.Empty(_apiClient.Authorised);
    }

    [Fact]
    public async Task Authorise_Confirmed_AuthorisesAndReturnsToTheQueueWithASuccessMessage()
    {
        Report();
        var client = Factory("Results Sign-off").CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = true });

        var response = await PostDecisionAsync(client, "Authorise", ("confirmation", "confirm"));
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal([TabulationId], _apiClient.Authorised);
        Assert.Contains("The Tabulation has been authorised and published.", html, StringComparison.Ordinal);
        Assert.Contains("<h1 class=\"govuk-heading-l\">Results Sign-off</h1>", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Authorise_NoLongerAwaiting_ReturnsToTheQueueWithAnErrorMessage()
    {
        Report();
        _apiClient.AuthoriseStatus = ResultsSignOffApiStatus.NotAwaiting;
        var client = Factory("Results Sign-off").CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = true });

        var response = await PostDecisionAsync(client, "Authorise", ("confirmation", "confirm"));
        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("This Tabulation is no longer awaiting Sign-off.", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Authorise_WithoutTheResultsSignOffRole_IsRefusedAndAuthorisesNothing()
    {
        var client = Factory("Admin").CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.PostAsync("/Results%20Sign-off/Authorise", new FormUrlEncodedContent([new("tabulationId", TabulationId.ToString()), new("confirmation", "confirm")]));

        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(_apiClient.Authorised);
    }
}
