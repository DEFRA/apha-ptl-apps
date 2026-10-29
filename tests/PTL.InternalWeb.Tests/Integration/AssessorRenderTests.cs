using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PTL.ApiClient.Distribution;
using PTL.Contracts.Distribution;
using PTL.InternalWeb.Tests.TestSupport;

namespace PTL.InternalWeb.Tests.Integration;

// Renders Features/Assessor/Views/Index and AssessmentItemList - legacy Assessor/MenuAssessor.aspx and AssessmentItemList.aspx -
// through the real MVC pipeline, with the "Assessor" role signed in. The wording is legacy's.
public class AssessorRenderTests(WebApplicationFactory<Program> rawFactory) : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly Guid TestId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid SchemeId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid SampleTestId = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private static readonly Guid ParticipantId = Guid.Parse("66666666-6666-6666-6666-666666666666");

    private readonly FakeAssessorApiClient _apiClient = new();

    private WebApplicationFactory<Program> Factory(string roles) =>
        rawFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IAssessorApiClient>();
                services.AddSingleton<IAssessorApiClient>(_apiClient);
                services.Configure<TestAuthHandlerOptions>(TestAuthHandler.SchemeName, options => options.ResolvedRoles = roles);
            }));

    private static string MainContent(string html) => Regex.Match(html, "<main.*?</main>", RegexOptions.Singleline).Value;

    private async Task<string> GetAsync(string url, string roles = "Assessor")
    {
        var response = await Factory(roles).CreateClient().GetAsync(url);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return MainContent(await response.Content.ReadAsStringAsync());
    }

    private void Menu(bool submitted = false) =>
        _apiClient.Menu = new(
            AssessorApiStatus.Ok,
            new AssessorMenuResponse(
                [2020, 2021],
                2021,
                [
                    new AssessorMonthResponse(2021, 2,
                    [
                        new AssessorDistributionResponse(
                            SchemeId,
                            "14468/A/HI: PT0166 Cellular Pathology Technique (CPT)",
                            [new AssessorTestResponse(TestId, "Giemsa", submitted), new AssessorTestResponse(Guid.NewGuid(), "E-cadherin", false)]),
                    ]),
                ]));

    private void Items(bool submitted = false, bool withSamples = true, bool completed = true) =>
        _apiClient.ItemList = new(
            AssessorApiStatus.Ok,
            new AssessorItemListResponse(
                TestId,
                "February 2021",
                "14468/A/HI: PT0166 Cellular Pathology Technique (CPT)",
                "Alcian blue / PAS",
                submitted,
                "Mary Dexter",
                submitted ? new DateTime(2021, 3, 17, 13, 17, 39) : null,
                withSamples
                    ? [new AssessorSampleResponse(Guid.NewGuid(), "21/7140", [new AssessorItemResponse(SampleTestId, ParticipantId, "1126", true), new AssessorItemResponse(SampleTestId, Guid.NewGuid(), "1385", false)])]
                    : [],
                completed));

    // ---- Menu -----------------------------------------------------------------------------------

    [Fact]
    public async Task Menu_ShowsTheTitleAndTheLegacyIntroduction()
    {
        Menu();

        var html = await GetAsync("/Assessor");

        Assert.Contains("<h1 class=\"govuk-heading-l\">Assessor</h1>", html, StringComparison.Ordinal);
        Assert.Contains("The table below lists all of the Distributions in each month for which you are an assessor.", html, StringComparison.Ordinal);
        Assert.Contains("Clicking the link will allow you to see the samples associated with each test.", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Menu_OffersTheYearsWithTheSelectedOneChosen()
    {
        Menu();

        var html = await GetAsync("/Assessor");

        Assert.Contains("Select Year:", html, StringComparison.Ordinal);
        Assert.Contains("<option value=\"2020\">2020</option>", html, StringComparison.Ordinal);
        Assert.Contains("<option value=\"2021\" selected=\"selected\">2021</option>", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Menu_PassesTheRequestedYearOn()
    {
        Menu();

        await GetAsync("/Assessor?year=2020");

        Assert.Equal(2020, Assert.Single(_apiClient.RequestedMenus));
    }

    [Fact]
    public async Task Menu_EachMonthStartsCollapsedAndListsItsDistributionsAndTests()
    {
        Menu();

        var html = await GetAsync("/Assessor");

        Assert.Contains("<details class=\"govuk-details\">", html, StringComparison.Ordinal);
        Assert.DoesNotContain(" open", Regex.Match(html, "<details[^>]*>").Value, StringComparison.Ordinal);
        Assert.Contains("<span class=\"govuk-details__summary-text\">February 2021</span>", html, StringComparison.Ordinal);
        Assert.Contains("<h2 class=\"govuk-heading-s\">14468/A/HI: PT0166 Cellular Pathology Technique (CPT)</h2>", html, StringComparison.Ordinal);
        Assert.Contains("Giemsa", html, StringComparison.Ordinal);
        Assert.Contains("E-cadherin", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Menu_ASubmittedTestIsMarkedWithAGreenTag()
    {
        Menu(submitted: true);

        var html = await GetAsync("/Assessor");

        Assert.Matches("Giemsa\\s*<strong class=\"govuk-tag govuk-tag--green\">Assessment Submitted</strong>", html);
        Assert.Equal(1, Regex.Count(html, "Assessment Submitted</strong>"));
    }

    [Fact]
    public async Task Menu_ViewLinksToTheTestsAssessmentItemsKeepingTheYear()
    {
        Menu();

        var html = await GetAsync("/Assessor");

        Assert.Contains($"href=\"/Assessor/AssessmentItemList?testId={TestId}&amp;year=2021\">View<span class=\"govuk-visually-hidden\"> Giemsa for 14468/A/HI: PT0166 Cellular Pathology Technique (CPT)</span></a>", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Menu_NoDistributionsAtAll_SaysSoAndShowsNothingElse()
    {
        var html = await GetAsync("/Assessor");

        Assert.Contains("There are no Distributions requiring assessment.", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Select Year:", html, StringComparison.Ordinal);
        Assert.DoesNotContain("The table below lists", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Menu_AYearWithNoMonths_SaysSoInLegacyWords()
    {
        _apiClient.Menu = new(AssessorApiStatus.Ok, new AssessorMenuResponse([2021], 2021, []));

        var html = await GetAsync("/Assessor");

        Assert.Contains("There are no distributions that require assessment.", html, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Admin")]
    [InlineData("Test Consultant")]
    [InlineData("Results Sign-off")]
    public async Task Menu_WithoutTheAssessorRole_IsRefused(string roles)
    {
        Menu();
        var client = Factory(roles).CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync("/Assessor");

        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(_apiClient.RequestedMenus);
    }

    // ---- Assessment items -----------------------------------------------------------------------

    private static string ItemsUrl => $"/Assessor/AssessmentItemList?testId={TestId}&year=2021";

    [Fact]
    public async Task Items_ShowsTheMonthDistributionAndTestHeadings()
    {
        Items();

        var html = await GetAsync(ItemsUrl);

        Assert.Contains("<span class=\"govuk-caption-l\">February 2021</span>", html, StringComparison.Ordinal);
        Assert.Contains("<h1 class=\"govuk-heading-l\">Assessment Items</h1>", html, StringComparison.Ordinal);
        Assert.Contains("<h2 class=\"govuk-heading-m\">14468/A/HI: PT0166 Cellular Pathology Technique (CPT)</h2>", html, StringComparison.Ordinal);
        Assert.Contains("<h3 class=\"govuk-heading-s\">Alcian blue / PAS</h3>", html, StringComparison.Ordinal);
        Assert.Equal([TestId], _apiClient.RequestedItemLists);
    }

    [Fact]
    public async Task Items_ATablePerSampleWithTheLegacyColumnsAndNothingButLabCodeToIdentifyParticipants()
    {
        Items();

        var html = await GetAsync(ItemsUrl);

        Assert.Contains("<h4 class=\"govuk-heading-s\">Sample number: 21/7140</h4>", html, StringComparison.Ordinal);
        var headers = Regex.Matches(html, "<th scope=\"col\" class=\"govuk-table__header\">(?<text>[^<]*)</th>").Select(m => m.Groups["text"].Value);
        Assert.Equal(["Labcode", "Assessment Entered", "View/Edit"], headers);
        Assert.Contains("<th scope=\"row\" class=\"govuk-table__header govuk-!-font-weight-regular\">1126</th>", html, StringComparison.Ordinal);
        Assert.Contains("<th scope=\"row\" class=\"govuk-table__header govuk-!-font-weight-regular\">1385</th>", html, StringComparison.Ordinal);
        Assert.DoesNotContain(ParticipantId.ToString(), Regex.Replace(html, "href=\"[^\"]*\"", string.Empty), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Items_ShowsWhetherAnAssessmentHasBeenEntered()
    {
        Items();

        var html = await GetAsync(ItemsUrl);

        var cells = Regex.Matches(html, "<td class=\"govuk-table__cell\">(?<text>Yes|No)</td>").Select(m => m.Groups["text"].Value);
        Assert.Equal(["Yes", "No"], cells);
    }

    [Fact]
    public async Task Items_ViewEditLinksToTheAssessmentEntryOfThatParticipant()
    {
        Items();

        var html = await GetAsync(ItemsUrl);

        Assert.Contains($"href=\"/Assessor/AssessmentEntry?testId={TestId}&amp;sampleTestId={SampleTestId}&amp;participantId={ParticipantId}&amp;year=2021\">View/Edit<span class=\"govuk-visually-hidden\"> for Labcode 1126, sample 21/7140</span></a>", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Items_NoSamples_SaysSoInLegacyWords()
    {
        Items(withSamples: false);

        var html = await GetAsync(ItemsUrl);

        Assert.Contains("No samples have been submitted for assessment.", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<table", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Items_NotSubmitted_HasNoSubmissionMessage()
    {
        Items();

        Assert.DoesNotContain("Assessment was submitted", await GetAsync(ItemsUrl), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Items_Submitted_ShowsWhoSubmittedAndWhen()
    {
        Items(submitted: true);

        var html = await GetAsync(ItemsUrl);

        Assert.Contains("<div class=\"govuk-inset-text\">Assessment was submitted successfully for this test by Mary Dexter on 17/03/2021</div>", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Items_BackReturnsToTheMenuOfTheSameYear()
    {
        Items();

        var html = await GetAsync(ItemsUrl);

        Assert.Matches("<a class=\"govuk-button govuk-button--secondary\"[^>]*href=\"/Assessor\\?year=2021\">Back</a>", html);
    }

    [Fact]
    public async Task Items_UnknownTest_IsNotFound()
    {
        var response = await Factory("Assessor").CreateClient().GetAsync(ItemsUrl);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Items_BreadcrumbsRunHomeAssessorThenAssessmentItems()
    {
        Items();
        var response = await Factory("Assessor").CreateClient().GetAsync(ItemsUrl);
        var html = await response.Content.ReadAsStringAsync();

        var crumbs = Regex.Match(html, "<ol class=\"govuk-breadcrumbs__list\">(?<inner>.*?)</ol>", RegexOptions.Singleline).Groups["inner"].Value;
        Assert.Contains("href=\"/Assessor\">Assessor</a>", crumbs, StringComparison.Ordinal);
        Assert.Matches("Assessment Items\\s*</li>", crumbs);
    }

    [Theory]
    [InlineData("Admin")]
    [InlineData("Results Sign-off")]
    public async Task Items_WithoutTheAssessorRole_IsRefused(string roles)
    {
        Items();
        var client = Factory(roles).CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync(ItemsUrl);

        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(_apiClient.RequestedItemLists);
    }

    [Fact]
    public async Task Items_Submitted_HasOnlyBackAndNoSubmit()
    {
        Items(submitted: true);

        var html = await GetAsync(ItemsUrl);

        Assert.DoesNotContain(">Submit</button>", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<form", html, StringComparison.Ordinal);
        Assert.Matches("<a class=\"govuk-button govuk-button--secondary\"[^>]*href=\"/Assessor\\?year=2021\">Back</a>", html);
    }

    [Fact]
    public async Task Items_NotSubmitted_OffersSubmitThenBackInOneAlignedButtonGroup()
    {
        Items();

        var html = await GetAsync(ItemsUrl);

        var form = Regex.Match(html, "<form[^>]*method=\"post\"[^>]*action=\"/Assessor/Submit\"[^>]*>(?<inner>.*?)</form>", RegexOptions.Singleline).Groups["inner"].Value;
        Assert.NotEmpty(form);
        Assert.Contains($"<input type=\"hidden\" name=\"testId\" value=\"{TestId}\" />", form, StringComparison.Ordinal);
        Assert.Contains("<input type=\"hidden\" name=\"year\" value=\"2021\" />", form, StringComparison.Ordinal);
        var group = Regex.Match(form, "<div class=\"govuk-button-group\">(?<inner>.*?)</div>", RegexOptions.Singleline).Groups["inner"].Value;
        Assert.Contains("<button type=\"submit\" class=\"govuk-button\" data-module=\"govuk-button\">Submit</button>", group, StringComparison.Ordinal);
        Assert.True(group.IndexOf(">Submit</button>", StringComparison.Ordinal) < group.IndexOf(">Back</a>", StringComparison.Ordinal));
    }

    private static async Task<HttpResponseMessage> PostSubmitAsync(HttpClient client, params (string Name, string Value)[] extra)
    {
        var page = await (await client.GetAsync(ItemsUrl)).Content.ReadAsStringAsync();
        var token = Regex.Match(page, "__RequestVerificationToken[^>]*value=\"([^\"]+)\"").Groups[1].Value;

        List<KeyValuePair<string, string>> fields = [new("__RequestVerificationToken", token), new("testId", TestId.ToString()), new("year", "2021")];
        fields.AddRange(extra.Select(f => new KeyValuePair<string, string>(f.Name, f.Value)));
        return await client.PostAsync("/Assessor/Submit", new FormUrlEncodedContent(fields));
    }

    [Fact]
    public async Task Submit_AsksTheLegacyQuestionOnTheSharedConfirmationPageAndSubmitsNothing()
    {
        Items(completed: true);
        var client = Factory("Assessor").CreateClient();

        var response = await PostSubmitAsync(client);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Are you sure that you want to submit assessment for this test?", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Note: Assessment has not been entered", html, StringComparison.Ordinal);
        Assert.DoesNotContain("govuk-button--warning", html, StringComparison.Ordinal);
        Assert.Contains($"href=\"/Assessor/AssessmentItemList?testId={TestId}&amp;year=2021\"", html, StringComparison.Ordinal);
        Assert.Empty(_apiClient.Submitted);
    }

    [Fact]
    public async Task Submit_NotAllSamplesAssessed_ShowsTheLegacyNoteAsAWarning()
    {
        Items(completed: false);
        var client = Factory("Assessor").CreateClient();

        var html = await (await PostSubmitAsync(client)).Content.ReadAsStringAsync();

        Assert.Contains("Are you sure that you want to submit assessment for this test?", html, StringComparison.Ordinal);
        Assert.Matches("govuk-warning-text__text\">\\s*<span class=\"govuk-visually-hidden\">Warning</span>\\s*Note: Assessment has not been entered for all of the samples\\.", html);
        Assert.Empty(_apiClient.Submitted);
    }

    [Fact]
    public async Task Submit_Confirmed_SubmitsAndTheItemsPageThenShowsWhoSubmittedAndWhen()
    {
        Items();
        var client = Factory("Assessor").CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = true });
        _apiClient.SubmitStatus = AssessorSubmitStatus.Submitted;

        var response = await PostSubmitAsync(client, ("confirmation", "confirm"));
        // The fake does not change state, so the page it lands on is the unsubmitted list; what matters is the submit and the return.
        var html = MainContent(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal([TestId], _apiClient.Submitted);
        Assert.Contains("<h1 class=\"govuk-heading-l\">Assessment Items</h1>", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Submit_Confirmed_ThenTheItemsPageIsReadOnlyWithTheSubmissionMessage()
    {
        Items(submitted: true);
        var client = Factory("Assessor").CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = true });

        var html = MainContent(await (await client.GetAsync(ItemsUrl)).Content.ReadAsStringAsync());

        Assert.Contains("Assessment was submitted successfully for this test by Mary Dexter on 17/03/2021", html, StringComparison.Ordinal);
        Assert.DoesNotContain(">Submit</button>", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Submit_Confirmed_SubmittedMeanwhile_ShowsTheErrorOnTheItemsPage()
    {
        Items(completed: true);
        _apiClient.SubmitStatus = AssessorSubmitStatus.AlreadySubmitted;
        var client = Factory("Assessor").CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = true });
        var html = MainContent(await (await PostSubmitAsync(client, ("confirmation", "confirm"))).Content.ReadAsStringAsync());

        Assert.Contains("Assessment has already been submitted for this test, so it can no longer be changed.", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Submit_WithoutTheAssessorRole_IsRefusedAndSubmitsNothing()
    {
        var client = Factory("Admin").CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.PostAsync("/Assessor/Submit", new FormUrlEncodedContent([new("testId", TestId.ToString()), new("confirmation", "confirm")]));

        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(_apiClient.Submitted);
    }

    // ---- Assessment entry -----------------------------------------------------------------------

    private static readonly Guid Morphology = Guid.Parse("b0000000-0000-0000-0000-000000000001");
    private static readonly Guid Staining = Guid.Parse("b0000000-0000-0000-0000-000000000002");
    private static readonly Guid Shape = Guid.Parse("c0000000-0000-0000-0000-000000000001");
    private static readonly Guid Colour = Guid.Parse("c0000000-0000-0000-0000-000000000002");
    private static readonly Guid Intensity = Guid.Parse("c0000000-0000-0000-0000-000000000004");

    private static string EntryUrl => $"/Assessor/AssessmentEntry?testId={TestId}&sampleTestId={SampleTestId}&participantId={ParticipantId}&year=2021";

    private void Entry(bool submitted = false, bool withResults = true, int overall = 3, int stainingScore = 0) =>
        _apiClient.Entry = new(
            AssessorApiStatus.Ok,
            new AssessorEntryResponse(
                TestId,
                SampleTestId,
                ParticipantId,
                "February 2021",
                "14468/A/HI: PT0166 Cellular Pathology Technique (CPT)",
                "1126",
                "21/7140",
                "Alcian blue / PAS",
                submitted,
                "Good work",
                overall,
                withResults ? [new AssessorResultItemResponse("Isolate", "Positive")] : [],
                [
                    new AssessorCategoryResponse(Morphology, "Morphology", 2, [new(Shape, "Shape", true), new(Colour, "Colour", false)]),
                    new AssessorCategoryResponse(Staining, "Staining", stainingScore, [new(Intensity, "Intensity", false)]),
                ]));

    [Fact]
    public async Task Entry_ShowsTheMonthDistributionLabCodeSampleAndTestInLegacyWords()
    {
        Entry();

        var html = await GetAsync(EntryUrl);

        Assert.Contains("<span class=\"govuk-caption-l\">February 2021</span>", html, StringComparison.Ordinal);
        Assert.Contains("<h1 class=\"govuk-heading-l\">Assessment Entry</h1>", html, StringComparison.Ordinal);
        Assert.Contains("<h2 class=\"govuk-heading-m\">14468/A/HI: PT0166 Cellular Pathology Technique (CPT)</h2>", html, StringComparison.Ordinal);
        var rows = Regex.Matches(html, "<dt class=\"govuk-summary-list__key\">(?<key>[^<]*)</dt><dd class=\"govuk-summary-list__value\">(?<value>[^<]*)</dd>").Select(m => (m.Groups["key"].Value, m.Groups["value"].Value));
        Assert.Equal([("Labcode", "1126"), ("Sample number", "21/7140"), ("Test", "Alcian blue / PAS")], rows);
        Assert.Equal([(SampleTestId, ParticipantId)], _apiClient.RequestedEntries);
    }

    [Fact]
    public async Task Entry_ShowsTheParticipantsResultsWhenThereAreAny()
    {
        Entry();

        var html = await GetAsync(EntryUrl);

        var headers = Regex.Matches(html, "<th scope=\"col\" class=\"govuk-table__header\">(?<text>[^<]*)</th>").Select(m => m.Groups["text"].Value);
        Assert.Equal(["Test result item name", "Result"], headers);
        Assert.Contains(">Isolate</th>", html, StringComparison.Ordinal);
        Assert.Contains(">Positive</td>", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Entry_NoParticipantResults_LeavesOutTheTable()
    {
        Entry(withResults: false);

        Assert.DoesNotContain("Test result item name", await GetAsync(EntryUrl), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Entry_EachCategoryListsItsCriteriaAsCheckboxesWithTheSavedOnesTicked()
    {
        Entry();

        var html = await GetAsync(EntryUrl);

        Assert.Contains("<h3 class=\"govuk-fieldset__heading\">Morphology</h3>", html, StringComparison.Ordinal);
        Assert.Contains("<h3 class=\"govuk-fieldset__heading\">Staining</h3>", html, StringComparison.Ordinal);
        Assert.Matches($"<input[^>]*name=\"Categories\\[0\\]\\.CriterionItemIds\"[^>]*type=\"checkbox\"[^>]*value=\"{Shape}\"[^>]*checked=\"checked\"", html);
        Assert.DoesNotMatch($"<input[^>]*value=\"{Colour}\"[^>]*checked=\"checked\"", html);
        Assert.Contains(">Colour</label>", html, StringComparison.Ordinal);
        Assert.Matches($"<input[^>]*name=\"Categories\\[1\\]\\.CriterionItemIds\"[^>]*value=\"{Intensity}\"", html);
        Assert.Matches($"<input[^>]*name=\"Categories\\[0\\]\\.CategoryItemId\"[^>]*value=\"{Morphology}\"", html);
    }

    [Fact]
    public async Task Entry_EachCategoryHasAScoreDropDownStartingAtPleaseSelect()
    {
        Entry(stainingScore: 0);

        var html = await GetAsync(EntryUrl);

        var first = Regex.Match(html, "<select[^>]*id=\"Categories_0__Score\".*?</select>", RegexOptions.Singleline).Value;
        Assert.Contains("<option value=\"\">Please select...</option>", first, StringComparison.Ordinal);
        Assert.Contains("<option selected=\"selected\" value=\"2\">2</option>", first, StringComparison.Ordinal);
        var second = Regex.Match(html, "<select[^>]*id=\"Categories_1__Score\".*?</select>", RegexOptions.Singleline).Value;
        Assert.DoesNotContain("selected=\"selected\"", second.Replace("<option value=\"\">", string.Empty, StringComparison.Ordinal), StringComparison.Ordinal);
        Assert.Equal(2, Regex.Count(html, ">Score:</label>"));
    }

    [Fact]
    public async Task Entry_HasTheCommentBoxAndTheOverallScoreInLegacyWords()
    {
        Entry(overall: 4);

        var html = await GetAsync(EntryUrl);

        Assert.Contains(">Assessment comment:</label>", html, StringComparison.Ordinal);
        Assert.Matches("<textarea[^>]*id=\"Comment\"[^>]*>\\s*Good work</textarea>", html);
        Assert.Contains(">Overall Score:</label>", html, StringComparison.Ordinal);
        var overall = Regex.Match(html, "<select[^>]*id=\"OverallScore\".*?</select>", RegexOptions.Singleline).Value;
        Assert.Equal(["Please select...", "1 - Fail", "2 - Borderline Fail", "3 - Pass", "4 - Good", "5 - Excellent"], Regex.Matches(overall, "<option[^>]*>(?<text>[^<]*)</option>").Select(m => m.Groups["text"].Value));
        Assert.Contains("<option selected=\"selected\" value=\"4\">4 - Good</option>", overall, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Entry_OffersSaveApplyAndCancelInLegacyOrder()
    {
        Entry();

        var html = await GetAsync(EntryUrl);

        var buttons = Regex.Matches(html, "(?:<button[^>]*name=\"command\" value=\"(?<command>[a-z]+)\"[^>]*>(?<text>[^<]*)</button>|<a class=\"govuk-button[^>]*>(?<link>Cancel|Back)</a>)");
        Assert.Equal(["Save", "Apply", "Cancel"], buttons.Select(m => m.Groups["text"].Success && m.Groups["text"].Value.Length > 0 ? m.Groups["text"].Value : m.Groups["link"].Value));
        Assert.Contains("name=\"command\" value=\"save\"", html, StringComparison.Ordinal);
        Assert.Contains("name=\"command\" value=\"apply\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Entry_CancelReturnsToTheAssessmentItemsOfThatTestAndYear()
    {
        Entry();

        var html = await GetAsync(EntryUrl);

        Assert.Matches($"<a class=\"govuk-button govuk-button--secondary\"[^>]*href=\"/Assessor/AssessmentItemList\\?testId={TestId}&amp;year=2021\">Cancel</a>", html);
    }

    [Fact]
    public async Task Entry_Submitted_IsReadOnlyWithOnlyBack()
    {
        Entry(submitted: true);

        var html = await GetAsync(EntryUrl);

        Assert.DoesNotContain("name=\"command\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain(">Cancel</a>", html, StringComparison.Ordinal);
        Assert.Matches($"<a class=\"govuk-button govuk-button--secondary\"[^>]*href=\"/Assessor/AssessmentItemList\\?testId={TestId}&amp;year=2021\">Back</a>", html);
        Assert.Contains("Assessment has been submitted for this test, so it can no longer be changed.", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<form", MainContent(html), StringComparison.Ordinal);
        Assert.DoesNotContain("<input", MainContent(html), StringComparison.Ordinal);
        Assert.DoesNotContain("<select", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<textarea", html, StringComparison.Ordinal);
        Assert.DoesNotContain("disabled", html, StringComparison.Ordinal);
        Assert.Contains("Score: 2", html, StringComparison.Ordinal);
        Assert.Contains("Score: Not entered", html, StringComparison.Ordinal);
        Assert.Contains("<li>Shape</li>", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<li>Colour</li>", html, StringComparison.Ordinal);
        Assert.Contains("<span class=\"ptl-preserve-lines\">Good work</span>", html, StringComparison.Ordinal);
        Assert.Contains("3 - Pass", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Entry_NotSubmitted_ShowsTheCommentLimitAndLeavesEveryControlEnabled()
    {
        Entry();

        var html = await GetAsync(EntryUrl);

        Assert.DoesNotContain("disabled=\"disabled\"", html, StringComparison.Ordinal);
        Assert.Matches("<textarea[^>]*maxlength=\"2000\"", html);
    }

    [Fact]
    public async Task Entry_UnknownSubmission_IsNotFound()
    {
        var response = await Factory("Assessor").CreateClient().GetAsync(EntryUrl);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Entry_BreadcrumbsRunHomeAssessorAssessmentItemsThenAssessmentEntry()
    {
        Entry();
        var response = await Factory("Assessor").CreateClient().GetAsync(EntryUrl);
        var html = await response.Content.ReadAsStringAsync();

        var crumbs = Regex.Match(html, "<ol class=\"govuk-breadcrumbs__list\">(?<inner>.*?)</ol>", RegexOptions.Singleline).Groups["inner"].Value;
        Assert.Contains("href=\"/Assessor\">Assessor</a>", crumbs, StringComparison.Ordinal);
        Assert.Contains($"href=\"/Assessor/AssessmentItemList?testId={TestId}&amp;year=2021\">Assessment Items</a>", crumbs, StringComparison.Ordinal);
        Assert.Matches("Assessment Entry\\s*</li>", crumbs);
    }

    [Theory]
    [InlineData("Admin")]
    [InlineData("Test Consultant")]
    public async Task Entry_WithoutTheAssessorRole_IsRefused(string roles)
    {
        Entry();
        var client = Factory(roles).CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync(EntryUrl);

        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(_apiClient.RequestedEntries);
    }

    // ---- Save and Apply -------------------------------------------------------------------------

    private static async Task<HttpResponseMessage> PostEntryAsync(HttpClient client, string command, string? comment = "Fine work", string overall = "4", string morphologyScore = "3", string stainingScore = "5", Action? beforePost = null)
    {
        var page = await (await client.GetAsync(EntryUrl)).Content.ReadAsStringAsync();
        var token = Regex.Match(page, "__RequestVerificationToken[^>]*value=\"([^\"]+)\"").Groups[1].Value;
        beforePost?.Invoke();

        List<KeyValuePair<string, string>> fields =
        [
            new("__RequestVerificationToken", token),
            new("SampleTestId", SampleTestId.ToString()),
            new("ParticipantId", ParticipantId.ToString()),
            new("Year", "2021"),
            new("Comment", comment ?? string.Empty),
            new("OverallScore", overall),
            new("Categories[0].CategoryItemId", Morphology.ToString()),
            new("Categories[0].Score", morphologyScore),
            new("Categories[0].CriterionItemIds", Colour.ToString()),
            new("Categories[1].CategoryItemId", Staining.ToString()),
            new("Categories[1].Score", stainingScore),
            new("command", command),
        ];
        return await client.PostAsync("/Assessor/AssessmentEntry", new FormUrlEncodedContent(fields));
    }

    [Fact]
    public async Task Save_SavesTheEnteredValuesAndReturnsToTheItemsWithAConfirmation()
    {
        Entry();
        Items();
        var client = Factory("Assessor").CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = true });

        var response = await PostEntryAsync(client, "save");
        var html = MainContent(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var (sampleTestId, participantId, request) = Assert.Single(_apiClient.Saved);
        Assert.Equal((SampleTestId, ParticipantId, "Fine work", 4), (sampleTestId, participantId, request.Comment, request.OverallScore));
        Assert.Equal([3, 5], request.Categories.Select(c => c.Score));
        Assert.Equal([Colour], request.Categories[0].CriterionItemIds);
        Assert.Empty(request.Categories[1].CriterionItemIds);
        Assert.Contains("The assessment has been saved.", html, StringComparison.Ordinal);
        Assert.Contains("<h1 class=\"govuk-heading-l\">Assessment Items</h1>", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Apply_SavesAndStaysOnTheEntryWithAConfirmation()
    {
        Entry();
        var client = Factory("Assessor").CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = true });

        var response = await PostEntryAsync(client, "apply");
        var html = MainContent(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Single(_apiClient.Saved);
        Assert.Contains("The assessment has been saved.", html, StringComparison.Ordinal);
        Assert.Contains("<h1 class=\"govuk-heading-l\">Assessment Entry</h1>", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Save_WithoutAnOverallScore_ShowsTheLegacyMessageKeepsTheEnteredValuesAndSavesNothing()
    {
        Entry();
        var client = Factory("Assessor").CreateClient();

        var response = await PostEntryAsync(client, "save", comment: "Words I typed", overall: string.Empty);
        var html = MainContent(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(_apiClient.Saved);
        Assert.Contains("<h2 class=\"govuk-error-summary__title\">There is a problem</h2>", html, StringComparison.Ordinal);
        Assert.Contains("<a href=\"#OverallScore\">An overall score is required</a>", html, StringComparison.Ordinal);
        Assert.Contains("Words I typed", html, StringComparison.Ordinal);
        Assert.Matches("(?s)<select[^>]*id=\"Categories_0__Score\".*?<option selected=\"selected\" value=\"3\">3</option>", html);
    }

    [Fact]
    public async Task Save_WithoutACategoryScore_NamesTheCategoryField()
    {
        Entry();
        var client = Factory("Assessor").CreateClient();

        var html = MainContent(await (await PostEntryAsync(client, "save", stainingScore: string.Empty)).Content.ReadAsStringAsync());

        Assert.Contains("<a href=\"#Categories_1__Score\">A category score is required</a>", html, StringComparison.Ordinal);
        Assert.Empty(_apiClient.Saved);
    }

    [Fact]
    public async Task Save_ACommentOverTwoThousandCharacters_IsRefusedWithTheLegacyMessage()
    {
        Entry();
        var client = Factory("Assessor").CreateClient();

        var html = MainContent(await (await PostEntryAsync(client, "save", comment: new string('x', 2001))).Content.ReadAsStringAsync());

        Assert.Contains("<a href=\"#Comment\">The assessment comment cannot exceed 2000 characters</a>", html, StringComparison.Ordinal);
        Assert.Empty(_apiClient.Saved);
    }

    [Fact]
    public async Task Save_TheApiRefusesTheValues_ShowsItsMessage()
    {
        Entry();
        _apiClient.SaveResult = new(AssessorSaveStatus.Invalid, [new("Categories", "The assessment does not match the categories and criteria of this test")]);
        var client = Factory("Assessor").CreateClient();

        var html = MainContent(await (await PostEntryAsync(client, "save")).Content.ReadAsStringAsync());

        Assert.Contains("The assessment does not match the categories and criteria of this test", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Save_ATestSubmittedMeanwhile_ReturnsToTheItemsWithAnErrorAndSavesNothing()
    {
        Entry();
        Items(submitted: true);
        var client = Factory("Assessor").CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = true });

        var html = MainContent(await (await PostEntryAsync(client, "save", beforePost: () => Entry(submitted: true))).Content.ReadAsStringAsync());

        Assert.Contains("Assessment has already been submitted for this test, so it can no longer be changed.", html, StringComparison.Ordinal);
        Assert.Empty(_apiClient.Saved);
    }

    [Fact]
    public async Task Save_WithoutTheAssessorRole_IsRefusedAndSavesNothing()
    {
        var client = Factory("Admin").CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.PostAsync("/Assessor/AssessmentEntry", new FormUrlEncodedContent([new("SampleTestId", SampleTestId.ToString()), new("ParticipantId", ParticipantId.ToString())]));

        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(_apiClient.Saved);
    }
}
