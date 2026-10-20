using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PTL.ApiClient.Distribution;
using PTL.Contracts.Distribution;
using PTL.InternalWeb.Tests.TestSupport;

namespace PTL.InternalWeb.Tests.Integration;

// Renders Features/Distribution/Views/Tabulations.cshtml (and the Dashboard's Tabulations link) through
// the real view engine - legacy Scheduling/Tabulations.aspx parity.
public class TabulationsRenderTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string Url = "/Distribution/Tabulations?year=2026&monthId=5";
    private const string WaitingHint = "This Distribution has been made available for Results Sign-Off and is waiting to be published";

    private readonly WebApplicationFactory<Program> _factory;
    private readonly FakeDistributionApiClient _apiClient = new();

    public TabulationsRenderTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IDistributionApiClient>();
                services.AddSingleton<IDistributionApiClient>(_apiClient);
            }));
    }

    private static TabulationActionResponse Action(
        string text, bool enabled, string hint, string command, bool overdue = false, bool label = false) =>
        new(text, enabled, hint, command, overdue, label);

    private static TabulationActionsResponse DefaultActions() =>
        new(
            Action("Amend Results", true, "Click to Amend the Results for this Distribution.", "None"),
            Action("Send To Test Consultant", true, "Send these results to the Test Consultant for comments", "SendToTestConsultant"),
            Action("Send For Assessment", true, "Send this distribution for assessment", "SendForAssessment"),
            Action("Send For Results Sign-Off", true, "Send this Distribution's Tabulation(s) for Results Sign-Off", "SendToResultsSignOff"));

    private static TabulationsSchemeResponse Plain(
        TabulationActionsResponse? actions = null, IReadOnlyList<TabulationRowResponse>? tabulations = null) =>
        new(
            Guid.NewGuid(), "BT: Brucella", "D1a/S1", new DateTime(2026, 5, 20), new DateTime(2026, 6, 30), false,
            ["Dr A", "", "Dr C"],
            tabulations ?? [new TabulationRowResponse(Guid.NewGuid(), "Summary", true, 2), new TabulationRowResponse(Guid.NewGuid(), "Detail", false, 0)],
            actions ?? DefaultActions());

    private static TabulationsSchemeResponse Assessed(TabulationActionsResponse? actions = null) =>
        new(
            Guid.NewGuid(), "TB: Tuberculosis", "D2/S2", new DateTime(2026, 5, 21), new DateTime(2026, 7, 1), true,
            ["As 1", "As 2", "", "As 4"],
            [],
            actions ?? DefaultActions());

    private void Seed(params TabulationsSchemeResponse[] schemes)
    {
        _apiClient.Tabulations = new TabulationsResponse(2026, 5, schemes);
        _apiClient.TabulationsPreview = _apiClient.Tabulations;
    }

    private async Task<string> GetHtmlAsync()
    {
        var response = await _factory.CreateClient().GetAsync(Url);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync();
    }

    private static string Text(string fragment) =>
        WebUtility.HtmlDecode(Regex.Replace(Regex.Replace(fragment, "<[^>]+>", " "), @"\s+", " ").Trim());

    private static string Section(string html, int schemeIndex) =>
        html.Split("<h2 class=\"govuk-heading-m govuk-!-margin-bottom-2\">")[schemeIndex + 1];

    private static string Panel(string section, string heading, string endMarker)
    {
        var match = Regex.Match(section, $"<h3 class=\"govuk-heading-s\">{heading}</h3>(.*?){Regex.Escape(endMarker)}", RegexOptions.Singleline);
        Assert.True(match.Success, $"Panel '{heading}' not found");
        return match.Groups[1].Value;
    }

    private static string ActionsHtml(string section)
    {
        var match = Regex.Match(section, "<h3 class=\"govuk-heading-s\">Actions</h3>(.*?)<hr class=\"govuk-section-break govuk-section-break--l", RegexOptions.Singleline);
        Assert.True(match.Success, "Actions panel not found");
        return match.Groups[1].Value;
    }

    private static List<string> ActionItems(string section) =>
        [.. Regex.Matches(ActionsHtml(section), "<li>.*?</li>", RegexOptions.Singleline).Select(m => m.Value)];

    // Send actions are stage buttons inside the single Tabulations form: value "{schemeId}:{command}".
    private static string SendCommandOf(string item, Guid? schemeId = null)
    {
        Assert.DoesNotContain("<a ", item, StringComparison.Ordinal);
        Assert.DoesNotContain("<form", item, StringComparison.Ordinal);
        var button = Regex.Match(item, "<button type=\"submit\" name=\"stage\" value=\"([^\"]+)\" class=\"govuk-link[^\"]* ptl-link-button\">");
        Assert.True(button.Success, "Stage button not found");
        var parts = WebUtility.HtmlDecode(button.Groups[1].Value).Split(':', 2);
        Assert.True(Guid.TryParse(parts[0], out var posted));
        if (schemeId is { } expected)
        {
            Assert.Equal(expected, posted);
        }

        return parts[1];
    }

    private static Match TabulationsForm(string html) =>
        Regex.Match(html, "<form method=\"post\"[^>]* action=\"(/Distribution/Tabulations[^\"]*)\"[^>]*>(.*?)</form>", RegexOptions.Singleline);

    private static string Stage(Guid schemeId, string command) => $"{schemeId}:{command}";

    private async Task<string> PostHtmlAsync(params (string Name, string Value)[] fields)
    {
        var client = _factory.CreateClient();
        var page = await (await client.GetAsync(Url)).Content.ReadAsStringAsync();
        var token = Regex.Match(page, "__RequestVerificationToken[^>]*value=\"([^\"]+)\"").Groups[1].Value;

        var content = new List<KeyValuePair<string, string>> { new("__RequestVerificationToken", token) };
        content.AddRange(fields.Select(f => new KeyValuePair<string, string>(f.Name, f.Value)));

        var response = await client.PostAsync(Url, new FormUrlEncodedContent(content));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync();
    }

    private static TabulationSendResponse Send(Guid schemeId, string question, string command = "SendToTestConsultant") =>
        new(schemeId, "BT: Brucella", "D1a/S1", command, "Send To Test Consultant", question);

    [Fact]
    public async Task Get_RendersLegacyTitleHeadingAndIntro_WithoutABackLink()
    {
        Seed(Plain());

        var html = await GetHtmlAsync();

        Assert.Contains("Monthly Distributions - May 2026", Regex.Match(html, "<title>(.*?)</title>", RegexOptions.Singleline).Groups[1].Value, StringComparison.Ordinal);
        Assert.Equal("Monthly Distributions - May 2026", Text(Regex.Match(html, "<h1[^>]*>(.*?)</h1>", RegexOptions.Singleline).Groups[1].Value));
        Assert.Contains("The table below lists the Tabulations that are available for each of this month's Distributions.", WebUtility.HtmlDecode(html), StringComparison.Ordinal);
        Assert.DoesNotContain("govuk-back-link", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Get_NoSchemes_ShowsLegacyEmptyStateMessage()
    {
        Seed();

        var html = await GetHtmlAsync();

        Assert.Contains("There are no schemes set to be distributed this month.", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<h2 class=\"govuk-heading-m govuk-!-margin-bottom-2\">", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Get_WithSchemes_DoesNotShowTheEmptyStateMessage()
    {
        Seed(Plain());

        Assert.DoesNotContain("There are no schemes set to be distributed this month.", await GetHtmlAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Get_ApiReturnsNull_Returns404()
    {
        _apiClient.Tabulations = null;

        var response = await _factory.CreateClient().GetAsync(Url);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal([(2026, 5)], _apiClient.TabulationRequests);
    }

    [Fact]
    public async Task Get_EachScheme_ShowsNameReferenceAndDatesInLegacyFormat()
    {
        Seed(Plain(), Assessed());

        var html = await GetHtmlAsync();

        Assert.Equal(2, Regex.Count(html, "<h2 class=\"govuk-heading-m govuk-!-margin-bottom-2\">"));
        var first = Text(Section(html, 0));
        Assert.StartsWith("BT: Brucella", first, StringComparison.Ordinal);
        Assert.Contains("D1a/S1", first, StringComparison.Ordinal);
        Assert.Contains("Deadline Date: 20/05/2026", first, StringComparison.Ordinal);
        Assert.Contains("Results Date: 30/06/2026", first, StringComparison.Ordinal);

        var second = Text(Section(html, 1));
        Assert.StartsWith("TB: Tuberculosis", second, StringComparison.Ordinal);
        Assert.Contains("D2/S2", second, StringComparison.Ordinal);
        Assert.Contains("Deadline Date: 21/05/2026", second, StringComparison.Ordinal);
        Assert.Contains("Results Date: 01/07/2026", second, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Get_NonAssessmentScheme_ListsNumberedTestConsultantSlotsIncludingBlanks()
    {
        Seed(Plain());

        var section = Section(await GetHtmlAsync(), 0);

        var panel = Panel(section, "Test Consultants", "</ol>");
        Assert.Equal(["1. Dr A", "2.", "3. Dr C"], Regex.Matches(panel, "<li>(.*?)</li>", RegexOptions.Singleline).Select(m => Text(m.Groups[1].Value)));
        Assert.DoesNotContain("Assessors</h3>", section, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Get_AssessmentScheme_ListsNumberedAssessorSlotsIncludingBlanks()
    {
        Seed(Assessed());

        var section = Section(await GetHtmlAsync(), 0);

        var panel = Panel(section, "Assessors", "</ol>");
        Assert.Equal(["1. As 1", "2. As 2", "3.", "4. As 4"], Regex.Matches(panel, "<li>(.*?)</li>", RegexOptions.Singleline).Select(m => Text(m.Groups[1].Value)));
        Assert.DoesNotContain("Test Consultants</h3>", section, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Get_TabulationRows_ShowTcMarkerNameLinkAndIssuedLinkOnlyWhenIssued()
    {
        Seed(Plain());

        var section = Section(await GetHtmlAsync(), 0);

        var panel = Panel(section, "Tabulations", "<h3 class=\"govuk-heading-s\">Actions</h3>");
        var rows = Regex.Matches(panel, "<tr class=\"govuk-table__row\">.*?</tr>", RegexOptions.Singleline).Select(m => m.Value).Skip(1).ToList();
        Assert.Equal(2, rows.Count);

        Assert.Contains("govuk-tag--grey\">TC</strong>", rows[0], StringComparison.Ordinal);
        Assert.Contains("Summary", Text(rows[0]), StringComparison.Ordinal);
        Assert.Contains("/Distribution/Tabulation?year=2026&amp;monthId=5&amp;tabulationId=", rows[0], StringComparison.Ordinal);
        Assert.Contains("Issued (2)", Text(rows[0]), StringComparison.Ordinal);
        Assert.Contains("/Distribution/PublishedTabulations?year=2026&amp;monthId=5&amp;tabulationId=", rows[0], StringComparison.Ordinal);

        Assert.DoesNotContain(">TC<", rows[1], StringComparison.Ordinal);
        Assert.Contains("Detail", Text(rows[1]), StringComparison.Ordinal);
        Assert.Contains("/Distribution/Tabulation?year=2026&amp;monthId=5&amp;tabulationId=", rows[1], StringComparison.Ordinal);
        Assert.DoesNotContain("Issued", rows[1], StringComparison.Ordinal);
        Assert.Contains("This is the Tabulation that will be commented upon by the Test Consultant", panel, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Get_NoTestConsultantTabulation_ShowsNoTcMarkerOrExplanatoryHint()
    {
        Seed(Plain(tabulations: [new TabulationRowResponse(Guid.NewGuid(), "Only", false, 0)]));

        var section = Section(await GetHtmlAsync(), 0);

        Assert.DoesNotContain(">TC<", section, StringComparison.Ordinal);
        Assert.DoesNotContain("commented upon by the Test Consultant", section, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Get_SchemeWithoutTabulations_ShowsSampleNumbersNotDefinedAndNoTable()
    {
        Seed(Assessed());

        var section = Section(await GetHtmlAsync(), 0);

        var panel = Panel(section, "Tabulations", "<h3 class=\"govuk-heading-s\">Actions</h3>");
        Assert.Contains("The Sample Numbers have not yet been defined", panel, StringComparison.Ordinal);
        Assert.DoesNotContain("<table", panel, StringComparison.Ordinal);
    }

    // ---- Actions ------------------------------------------------------------------------------

    [Fact]
    public async Task Get_NonAssessmentScheme_ShowsAmendTestConsultantAndSignOffActionsOnly()
    {
        Seed(Plain());

        var items = ActionItems(Section(await GetHtmlAsync(), 0));

        Assert.Equal(3, items.Count);
        Assert.StartsWith("Amend Results", Text(items[0]), StringComparison.Ordinal);
        Assert.StartsWith("Send To Test Consultant", Text(items[1]), StringComparison.Ordinal);
        Assert.StartsWith("Send For Results Sign-Off", Text(items[2]), StringComparison.Ordinal);
        Assert.DoesNotContain("Send For Assessment", string.Concat(items), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Get_AssessmentScheme_ShowsAmendAssessmentAndSignOffActionsOnly()
    {
        Seed(Assessed());

        var items = ActionItems(Section(await GetHtmlAsync(), 0));

        Assert.Equal(3, items.Count);
        Assert.StartsWith("Amend Results", Text(items[0]), StringComparison.Ordinal);
        Assert.StartsWith("Send For Assessment", Text(items[1]), StringComparison.Ordinal);
        Assert.StartsWith("Send For Results Sign-Off", Text(items[2]), StringComparison.Ordinal);
        Assert.DoesNotContain("Send To Test Consultant", string.Concat(items), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Get_EnabledSendActions_StageTheSendCommandInTheTabulationsForm_WithAHiddenSchemeContext()
    {
        var scheme = Plain();
        Seed(scheme);

        var html = await GetHtmlAsync();
        var items = ActionItems(Section(html, 0));

        Assert.StartsWith("/Distribution/AmendResults?year=2026&monthId=5&schemeId=", WebUtility.HtmlDecode(Regex.Match(items[0], "<a [^>]*href=\"([^\"]+)\"").Groups[1].Value), StringComparison.Ordinal);
        Assert.Equal("SendToTestConsultant", SendCommandOf(items[1], scheme.MonthlyDistributionSchemeId));
        Assert.Equal("SendToResultsSignOff", SendCommandOf(items[2], scheme.MonthlyDistributionSchemeId));
        Assert.Contains("name=\"stage\"", TabulationsForm(html).Groups[2].Value, StringComparison.Ordinal);
        Assert.DoesNotContain("SendTabulation", html, StringComparison.Ordinal);
        Assert.All(items, i => Assert.Contains("<span class=\"govuk-visually-hidden\"> for BT: Brucella</span>", i, StringComparison.Ordinal));
        Assert.DoesNotContain("ViewStage", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Get_EnabledAmendResultsAction_LinksToAmendResultsForThatScheme()
    {
        var scheme = Plain();
        Seed(scheme);

        var item = ActionItems(Section(await GetHtmlAsync(), 0))[0];

        var link = Regex.Match(item, "<a [^>]*href=\"([^\"]+)\"[^>]*>(.*?)</a>", RegexOptions.Singleline);
        Assert.True(link.Success);
        Assert.Equal(
            $"/Distribution/AmendResults?year=2026&monthId=5&schemeId={scheme.MonthlyDistributionSchemeId}",
            WebUtility.HtmlDecode(link.Groups[1].Value));
        Assert.Equal("Amend Results for BT: Brucella", Text(link.Groups[2].Value));
    }

    [Theory]
    [InlineData("SendToTestConsultant")]
    [InlineData("SendForAssessment")]
    [InlineData("SendToResultsSignOff")]
    public async Task Get_EnabledAction_PostedCommandIsChosenFromTheCommandNotTheText(string command)
    {
        var actions = DefaultActions() with { SendToTestConsultant = Action("Whatever the label says", true, string.Empty, command) };
        Seed(Plain(actions));

        var item = ActionItems(Section(await GetHtmlAsync(), 0))[1];

        Assert.StartsWith("Whatever the label says", Text(item), StringComparison.Ordinal);
        Assert.Equal(command, SendCommandOf(item));
    }

    [Fact]
    public async Task Get_DisabledActions_RenderAsPlainTextWithVisibleHints_AndNoDisabledAttributes()
    {
        var actions = new TabulationActionsResponse(
            Action("Amend Results", false, "Sample Numbers not defined", "None"),
            Action("Send To Test Consultant", false, "Deadline Date not Passed (20/05/2026)", "SendToTestConsultant"),
            Action("Send For Assessment", false, "Deadline Date not Passed (20/05/2026)", "SendForAssessment"),
            Action("Send For Results Sign-Off", false, "Deadline Date not Passed (20/05/2026)", "SendToResultsSignOff"));
        Seed(Plain(actions));

        var section = Section(await GetHtmlAsync(), 0);
        var actionsHtml = ActionsHtml(section);
        var items = ActionItems(section);

        Assert.All(items, i => Assert.DoesNotContain("<a ", i, StringComparison.Ordinal));
        Assert.All(items, i => Assert.DoesNotContain("<form", i, StringComparison.Ordinal));
        Assert.All(items, i => Assert.DoesNotContain("govuk-visually-hidden", i, StringComparison.Ordinal));
        Assert.Contains("Sample Numbers not defined", Text(items[0]), StringComparison.Ordinal);
        Assert.Contains("Deadline Date not Passed (20/05/2026)", Text(items[1]), StringComparison.Ordinal);
        Assert.Contains("class=\"govuk-hint", items[0], StringComparison.Ordinal);
        Assert.Contains("class=\"govuk-hint", items[1], StringComparison.Ordinal);
        // The same hint is not repeated under the next action.
        Assert.DoesNotContain("govuk-hint", items[2], StringComparison.Ordinal);
        Assert.DoesNotContain("title=", actionsHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("aria-disabled", actionsHtml, StringComparison.Ordinal);
        Assert.DoesNotMatch(@"\sdisabled[\s=>]", actionsHtml);
        Assert.DoesNotContain("govuk-tag--red", actionsHtml, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Get_EnabledActionsWithNavigationOnlyHints_SuppressTheHint_ButKeepInformativeHints()
    {
        var actions = DefaultActions() with
        {
            SendToTestConsultant = Action("View Comments", true, "Click to view the Test Consultant's Comments", "ViewComments"),
            SendForAssessment = Action("View/Enter Comments", true, "Click to view the Tabulation Comments", "ViewComments"),
        };
        Seed(Plain(actions), Assessed(actions));

        var html = await GetHtmlAsync();

        var plainItems = ActionItems(Section(html, 0));
        Assert.DoesNotContain("govuk-hint", plainItems[0], StringComparison.Ordinal);
        Assert.DoesNotContain("Click to Amend", plainItems[0], StringComparison.Ordinal);
        Assert.DoesNotContain("govuk-hint", plainItems[1], StringComparison.Ordinal);
        Assert.DoesNotContain("Click to view", plainItems[1], StringComparison.Ordinal);
        Assert.DoesNotContain("Send this Distribution's Tabulation(s) for Results Sign-Off", Text(plainItems[2]), StringComparison.Ordinal);
        Assert.DoesNotContain("govuk-hint", plainItems[2], StringComparison.Ordinal);

        var assessedItems = ActionItems(Section(html, 1));
        Assert.DoesNotContain("govuk-hint", assessedItems[1], StringComparison.Ordinal);
        Assert.DoesNotContain("Click to view the Tabulation Comments", assessedItems[1], StringComparison.Ordinal);
    }

    [Fact]
    public async Task Get_DisabledActionWithANavigationHint_StillShowsTheHint()
    {
        var actions = DefaultActions() with { AmendResults = Action("Amend Results", false, "Click to Amend the Results for this Distribution.", "None") };
        Seed(Plain(actions));

        var item = ActionItems(Section(await GetHtmlAsync(), 0))[0];

        Assert.DoesNotContain("<a ", item, StringComparison.Ordinal);
        Assert.Contains("Click to Amend the Results for this Distribution.", Text(item), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Get_EnabledOverdueAction_IsARedLinkNotATag()
    {
        var actions = DefaultActions() with
        {
            SendToTestConsultant = Action("OVERDUE", true, "Test Consultant Comments are now Overdue", "ViewComments", overdue: true),
        };
        Seed(Plain(actions));

        var item = ActionItems(Section(await GetHtmlAsync(), 0))[1];

        Assert.Matches("<a class=\"govuk-link ptl-link--overdue\" [^>]*>OVERDUE<span class=\"govuk-visually-hidden\"> for BT: Brucella</span></a>", item);
        Assert.DoesNotContain("govuk-tag", item, StringComparison.Ordinal);
        var href = WebUtility.HtmlDecode(Regex.Match(item, "<a [^>]*href=\"([^\"]+)\"").Groups[1].Value);
        Assert.StartsWith("/Distribution/ViewComments?year=2026&monthId=5&schemeId=", href, StringComparison.Ordinal);
        Assert.Contains("Test Consultant Comments are now Overdue", Text(item), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Get_EnabledViewCommentsAction_LinksToViewCommentsForThatScheme()
    {
        var scheme = Plain(DefaultActions() with
        {
            SendToTestConsultant = Action("View Comments", true, "Click to view the Test Consultant's Comments", "ViewComments"),
        });
        Seed(scheme);

        var item = ActionItems(Section(await GetHtmlAsync(), 0))[1];

        var link = Regex.Match(item, "<a [^>]*href=\"([^\"]+)\"[^>]*>(.*?)</a>", RegexOptions.Singleline);
        Assert.True(link.Success);
        Assert.Equal(
            $"/Distribution/ViewComments?year=2026&monthId=5&schemeId={scheme.MonthlyDistributionSchemeId}",
            WebUtility.HtmlDecode(link.Groups[1].Value));
        Assert.Equal("View Comments for BT: Brucella", Text(link.Groups[2].Value));
        Assert.DoesNotContain("govuk-tag", item, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Get_OverdueSignOffLabel_IsARedTagWithoutALink()
    {
        var actions = DefaultActions() with
        {
            SendToResultsSignOff = Action("OVERDUE", false, WaitingHint, "SendToResultsSignOff", overdue: true, label: true),
        };
        Seed(Plain(actions));

        var item = ActionItems(Section(await GetHtmlAsync(), 0))[2];

        Assert.DoesNotContain("<a ", item, StringComparison.Ordinal);
        Assert.DoesNotContain("<form", item, StringComparison.Ordinal);
        Assert.Contains("<strong class=\"govuk-tag govuk-tag--red\">OVERDUE</strong>", item, StringComparison.Ordinal);
        Assert.DoesNotContain("govuk-visually-hidden", item, StringComparison.Ordinal);
        Assert.Contains(WaitingHint, Text(item), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(true, false, false)]
    [InlineData(true, true, true)]
    [InlineData(true, true, false)]
    public async Task Get_OverdueAction_IsALinkOnlyWhenEnabledAndNotALabel(bool overdue, bool enabled, bool label)
    {
        var actions = DefaultActions() with
        {
            SendToResultsSignOff = Action("Send For Results Sign-Off", enabled, "The Results Issue Target date for this Distribution has passed.", "SendToResultsSignOff", overdue, label),
        };
        Seed(Plain(actions));

        var item = ActionItems(Section(await GetHtmlAsync(), 0))[2];

        var actionable = enabled && !label;
        Assert.DoesNotContain("<a ", item, StringComparison.Ordinal);
        Assert.Equal(actionable, item.Contains("<button ", StringComparison.Ordinal));
        Assert.Equal(actionable, item.Contains("ptl-link--overdue", StringComparison.Ordinal));
        Assert.Equal(!actionable, item.Contains("govuk-tag--red", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Get_EnabledOverdueFlaggedSignOff_StaysALinkAndShowsTheOverdueHint()
    {
        var actions = DefaultActions() with
        {
            SendToResultsSignOff = Action("Send For Results Sign-Off", true, "The Results Issue Target date for this Distribution has passed.", "SendToResultsSignOff", overdue: true),
        };
        Seed(Plain(actions));

        var item = ActionItems(Section(await GetHtmlAsync(), 0))[2];

        Assert.Equal("SendToResultsSignOff", SendCommandOf(item));
        Assert.Contains("ptl-link--overdue", item, StringComparison.Ordinal);
        Assert.Contains("The Results Issue Target date for this Distribution has passed.", Text(item), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Get_PlainDisabledAction_WithoutHint_ShowsNoHintElement()
    {
        var actions = DefaultActions() with { SendToTestConsultant = Action("Chaser Email Sent", false, string.Empty, "SendToTestConsultant") };
        Seed(Plain(actions));

        var item = ActionItems(Section(await GetHtmlAsync(), 0))[1];

        Assert.StartsWith("Chaser Email Sent", Text(item), StringComparison.Ordinal);
        Assert.DoesNotContain("govuk-hint", item, StringComparison.Ordinal);
        Assert.DoesNotContain("<a ", item, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Waiting to send Email 14/05/2026", false)]
    [InlineData("First Email Sent", false)]
    [InlineData("Comments not submitted", true)]
    [InlineData("View Comments", true)]
    public async Task Get_WorkflowStateChange_ShowsTheCorrectLabelAndLinkState(string text, bool enabled)
    {
        var actions = DefaultActions() with { SendToTestConsultant = Action(text, enabled, "state hint", "ViewComments") };
        Seed(Plain(actions));

        var item = ActionItems(Section(await GetHtmlAsync(), 0))[1];

        Assert.StartsWith(text, Text(item), StringComparison.Ordinal);
        Assert.Equal(enabled, item.Contains("<a ", StringComparison.Ordinal));
    }

    // ---- Staged sends, Save and Cancel (A1) ---------------------------------------------------

    [Fact]
    public async Task Get_NothingStaged_RendersOneFormWithSaveAndCancelButNoInsetTextOrStagedInputs()
    {
        Seed(Plain());

        var html = await GetHtmlAsync();

        var form = TabulationsForm(html);
        Assert.True(form.Success);
        Assert.Equal("/Distribution/Tabulations?year=2026&monthId=5", WebUtility.HtmlDecode(form.Groups[1].Value));
        Assert.Matches("<button type=\"submit\" name=\"command\" value=\"save\"[^>]*>Save</button>", form.Groups[2].Value);
        Assert.Matches("<button type=\"submit\" name=\"command\" value=\"cancel\"[^>]*>Cancel</button>", form.Groups[2].Value);
        Assert.DoesNotContain("value=\"apply\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("name=\"staged\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("govuk-inset-text", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Post_StageWithoutAQuestion_RendersStagedInputsAndTheUnsavedChangesInsetText()
    {
        var scheme = Plain();
        Seed(scheme);
        var earlier = Guid.NewGuid();
        _apiClient.TabulationStage = Send(scheme.MonthlyDistributionSchemeId, string.Empty);

        var html = await PostHtmlAsync(
            ("stage", Stage(scheme.MonthlyDistributionSchemeId, "SendToTestConsultant")),
            ("staged", Stage(earlier, "SendToResultsSignOff")));

        var form = TabulationsForm(html);
        Assert.Equal("/Distribution/Tabulations?year=2026&monthId=5", WebUtility.HtmlDecode(form.Groups[1].Value));
        var staged = Regex.Matches(form.Groups[2].Value, "<input type=\"hidden\" name=\"staged\" value=\"([^\"]+)\"").Select(m => m.Groups[1].Value);
        Assert.Equal([Stage(earlier, "SendToResultsSignOff"), Stage(scheme.MonthlyDistributionSchemeId, "SendToTestConsultant")], staged);
        Assert.Contains(
            "<div class=\"govuk-inset-text\">You have changes that have not been saved. Select Save to keep them.</div>",
            form.Groups[2].Value, StringComparison.Ordinal);
        Assert.Matches("name=\"command\" value=\"save\"[^>]*>Save</button>", form.Groups[2].Value);
        Assert.Matches("name=\"command\" value=\"cancel\"[^>]*>Cancel</button>", form.Groups[2].Value);
        Assert.DoesNotContain(">Apply</button>", html, StringComparison.Ordinal);
        Assert.Empty(_apiClient.SavedTabulations);
    }

    [Fact]
    public async Task Post_StageWithAQuestion_RendersTheSendTabulationConfirmation()
    {
        var scheme = Plain();
        Seed(scheme);
        var earlier = Guid.NewGuid();
        _apiClient.TabulationStage = Send(scheme.MonthlyDistributionSchemeId, "Are you sure you want to send these results to the Test Consultant?");

        var html = await PostHtmlAsync(
            ("stage", Stage(scheme.MonthlyDistributionSchemeId, "SendToTestConsultant")),
            ("staged", Stage(earlier, "SendToResultsSignOff")));

        Assert.Equal(
            "Are you sure you want to send these results to the Test Consultant?",
            Text(Regex.Match(html, "<h1[^>]*>(.*?)</h1>", RegexOptions.Singleline).Groups[1].Value));
        Assert.DoesNotContain("govuk-warning-text", html, StringComparison.Ordinal);
        Assert.DoesNotContain("govuk-back-link", html, StringComparison.Ordinal);

        var forms = Regex.Matches(html, "<form ([^>]*)>(.*?)</form>", RegexOptions.Singleline);
        var ok = forms.Single(f => !f.Groups[1].Value.Contains("confirmation-cancel", StringComparison.Ordinal) && f.Groups[2].Value.Contains(">OK</button>", StringComparison.Ordinal));
        var cancel = forms.Single(f => f.Groups[1].Value.Contains("id=\"confirmation-cancel\"", StringComparison.Ordinal));

        Assert.Contains("action=\"/Distribution/Tabulations?year=2026&amp;monthId=5\"", ok.Groups[1].Value, StringComparison.Ordinal);
        Assert.Contains($"<input type=\"hidden\" name=\"stage\" value=\"{Stage(scheme.MonthlyDistributionSchemeId, "SendToTestConsultant")}\"", ok.Groups[2].Value, StringComparison.Ordinal);
        Assert.Contains("<input type=\"hidden\" name=\"confirmation\" value=\"confirm\"", ok.Groups[2].Value, StringComparison.Ordinal);
        Assert.Contains($"<input type=\"hidden\" name=\"staged\" value=\"{Stage(earlier, "SendToResultsSignOff")}\"", ok.Groups[2].Value, StringComparison.Ordinal);
        Assert.Matches("<button type=\"submit\" form=\"confirmation-cancel\"[^>]*>Cancel</button>", ok.Groups[2].Value);

        // Cancel posts the same form back declined, so the earlier staged sends are kept.
        Assert.Contains("action=\"/Distribution/Tabulations?year=2026&amp;monthId=5\"", cancel.Groups[1].Value, StringComparison.Ordinal);
        Assert.Contains($"<input type=\"hidden\" name=\"staged\" value=\"{Stage(earlier, "SendToResultsSignOff")}\"", cancel.Groups[2].Value, StringComparison.Ordinal);
        Assert.Contains("<input type=\"hidden\" name=\"confirmation\" value=\"cancel\"", cancel.Groups[2].Value, StringComparison.Ordinal);
        Assert.DoesNotContain("value=\"confirm\"", cancel.Groups[2].Value, StringComparison.Ordinal);
        Assert.Empty(_apiClient.PreviewedTabulations);
    }

    [Fact]
    public async Task Post_StageWithADiscardQuestionAndNothingStaged_RendersAWarning()
    {
        var scheme = Plain();
        Seed(scheme);
        _apiClient.TabulationStage = Send(scheme.MonthlyDistributionSchemeId, "Discard the Test Consultant's comments and send again?");

        var html = await PostHtmlAsync(("stage", Stage(scheme.MonthlyDistributionSchemeId, "SendToTestConsultant")));

        var warning = Regex.Match(html, "<div class=\"govuk-warning-text\">(.*?)</div>", RegexOptions.Singleline);
        Assert.True(warning.Success);
        Assert.Contains("<h1 class=\"govuk-heading-l govuk-warning-text__text", warning.Groups[1].Value, StringComparison.Ordinal);
        Assert.Equal("! Warning Discard the Test Consultant's comments and send again?", Text(warning.Groups[1].Value));
        Assert.DoesNotContain("govuk-back-link", html, StringComparison.Ordinal);
        Assert.DoesNotContain("name=\"staged\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Dashboard_TabulationsViewLink_PointsToTheTabulationsScreen()
    {
        _apiClient.Years = [new DistributionYearOptionResponse(2026, "2026/27")];
        _apiClient.Months = [new DistributionDashboardMonthResponse(2026, 5, "May", Guid.NewGuid(), false, 0, 0, 0, 0, 0)];

        var response = await _factory.CreateClient().GetAsync("/Distribution/Index?year=2026");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var link = Regex.Match(html, "<a [^>]*href=\"([^\"]+)\"[^>]*>View<span[^>]*> Tabulations for May</span></a>");
        Assert.True(link.Success);
        var href = WebUtility.HtmlDecode(link.Groups[1].Value);
        Assert.Contains("/Distribution/Tabulations", href, StringComparison.Ordinal);
        Assert.Contains("year=2026", href, StringComparison.Ordinal);
        Assert.Contains("monthId=5", href, StringComparison.Ordinal);
        Assert.DoesNotContain("ViewStage", href, StringComparison.Ordinal);
    }
}
