using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PTL.ApiClient.Distribution;
using PTL.Contracts.Distribution;
using PTL.InternalWeb.Tests.TestSupport;

namespace PTL.InternalWeb.Tests.Integration;

// Distribution Participants (legacy Scheduling/DistributionParticipantList.aspx): the Change Deadline Date
// button opens that row's form through a server round trip, so the whole flow works without scripting.
public partial class DistributionParticipantsRenderTests : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly Guid Scheme = Guid.Parse("aaaaaaaa-0000-0000-0000-0000000000a1");
    private static readonly Guid First = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000001");
    private static readonly Guid Second = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002");

    private static readonly string Url = $"/Distribution/Participants?year=2026&monthId=5&schemeId={Scheme}";
    private static readonly string Query = $"year=2026&monthId=5&schemeId={Scheme}";

    [GeneratedRegex("__RequestVerificationToken[^>]*value=\"([^\"]+)\"", RegexOptions.None)]
    private static partial Regex AntiforgeryTokenPattern();

    private readonly WebApplicationFactory<Program> _factory;
    private bool _tickFirst = true;
    private readonly FakeDistributionApiClient _apiClient = new()
    {
        Participants = new DistributionParticipantsResponse(Scheme, 2026, 5, "S1", "Brucella", "D1a/S1", false,
        [
            new DistributionParticipantResponse(First, "L001: Lab One", new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc), string.Empty, false),
            new DistributionParticipantResponse(Second, "L002: Lab Two", new DateTime(2026, 6, 2, 0, 0, 0, DateTimeKind.Utc), "Late post", false)
        ],
        []),
        ChangeDeadlineResult = new ChangeDeadlineResult(true, null, null)
    };

    public DistributionParticipantsRenderTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IDistributionApiClient>();
                services.AddSingleton<IDistributionApiClient>(_apiClient);
            }));
    }

    // What a browser sends when any button on the page is pressed: the hidden fields, ticks and the edited row's inputs.
    private async Task<(HttpResponseMessage Response, string Html)> PostAsync(string action, string extraQuery, params KeyValuePair<string, string>[] extraFields)
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var page = await client.GetAsync(Url);
        var token = AntiforgeryTokenPattern().Match(await page.Content.ReadAsStringAsync()).Groups[1].Value;
        var cookie = string.Join("; ", page.Headers.TryGetValues("Set-Cookie", out var cookies) ? cookies.Select(c => c.Split(';')[0]) : []);

        var fields = new List<KeyValuePair<string, string>>
        {
            new("__RequestVerificationToken", token),
            new("MonthlyDistributionSchemeId", Scheme.ToString()),
            new("YearId", "2026"),
            new("MonthId", "5"),
            new("IsRepeatDistribution", "false"),
            new("Participants[0].DistributionParticipantId", First.ToString()),
            new("Participants[0].AdditionalParticipant", "false"),
            new("Participants[0].DeadlineDate", "2026-06-01"),
            new("Participants[0].DeadlineChangeReason", string.Empty),
            new("Participants[1].DistributionParticipantId", Second.ToString()),
            new("Participants[1].AdditionalParticipant", "false"),
            new("Participants[1].DeadlineDate", "2026-06-02"),
            new("Participants[1].DeadlineChangeReason", "Late post"),
        };
        if (_tickFirst)
        {
            fields.Add(new("Participants[0].Selected", "true"));
        }

        fields.AddRange(extraFields);
        var request = new HttpRequestMessage(HttpMethod.Post, $"/Distribution/{action}?{Query}&{extraQuery}")
        {
            Content = new FormUrlEncodedContent(fields)
        };
        request.Headers.Add("Cookie", cookie);

        var response = await client.SendAsync(request);
        return (response, await response.Content.ReadAsStringAsync());
    }

    private static List<string> Buttons(string html) =>
        [.. Regex.Matches(html, "<button[^>]*>.*?</button>", RegexOptions.Singleline).Select(m => m.Value)];

    [Fact]
    public async Task Get_TheRepeatSelectionMessageIsPresentButHiddenUntilNeeded()
    {
        var html = await _factory.CreateClient().GetStringAsync(Url);

        Assert.Matches("<p class=\"govuk-error-message\" id=\"repeatDistributionSelectionError\" role=\"alert\" hidden=\"hidden\">", html);
        Assert.DoesNotContain("govuk-error-summary", html, StringComparison.Ordinal);
    }

    // What a browser without scripting sends when Repeat this Distribution is pressed with nothing ticked.
    [Fact]
    public async Task Post_RepeatWithNothingTicked_ShowsTheSelectionMessageInTheErrorSummaryAndOnThePage()
    {
        _tickFirst = false;

        var (response, html) = await PostAsync("RepeatDistribution", string.Empty);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        const string message = "Select at least one participant to be included in the repeat of this distribution.";
        Assert.Matches($"<a href=\"#repeatDistributionSelectionError\">{message}</a>", html);
        Assert.Matches($"<p class=\"govuk-error-message\" id=\"repeatDistributionSelectionError\" role=\"alert\">\\s*<span class=\"govuk-visually-hidden\">Error:</span>\\s*{message}", html);
        Assert.Empty(_apiClient.SavedRepeatRequests);
    }

    [Fact]
    public async Task Get_EachRowHasAChangeDeadlineDateSubmitButtonAndNoInputsUntilItIsOpened()
    {
        var html = await _factory.CreateClient().GetStringAsync(Url);

        Assert.DoesNotContain("<details", html, StringComparison.Ordinal);
        Assert.DoesNotContain("NewDeadlineDate", html, StringComparison.Ordinal);
        foreach (var id in new[] { First, Second })
        {
            var button = Assert.Single(Buttons(html), b => b.Contains($"participantId={id}", StringComparison.Ordinal));
            Assert.Contains("type=\"submit\"", button, StringComparison.Ordinal);
            Assert.Matches("formaction=\"[^\"]*/Distribution/EditDeadline\\?[^\"]*command=edit", button);
            Assert.Contains("Change Deadline Date", button, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task Post_ChangeDeadlineDate_OpensOnlyThatRowsFormWithSaveAndCancelAndKeepsTheTicks()
    {
        var (response, html) = await PostAsync("EditDeadline", $"participantId={Second}&command=edit");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("name=\"Participants[1].NewDeadlineDate\"", html, StringComparison.Ordinal);
        Assert.Contains("name=\"Participants[1].NewDeadlineReason\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Participants[0].NewDeadlineDate", html, StringComparison.Ordinal);
        Assert.Matches("<label[^>]*class=\"govuk-label\"[^>]*>\\s*New Date\\s*</label>", html);
        Assert.Matches("<label[^>]*class=\"govuk-label\"[^>]*>\\s*Reason\\s*</label>", html);
        var buttons = Buttons(html);
        Assert.Contains(buttons, b => b.Contains("Save", StringComparison.Ordinal) && Regex.IsMatch(b, $"formaction=\"[^\"]*ChangeDeadline[^\"]*participantId={Second}"));
        Assert.Contains(buttons, b => b.Contains("Cancel", StringComparison.Ordinal) && b.Contains("command=cancel", StringComparison.Ordinal));
        // The row that is not being edited still offers the button.
        Assert.Contains(buttons, b => b.Contains($"participantId={First}", StringComparison.Ordinal) && b.Contains("command=edit", StringComparison.Ordinal));
        // Enter in an edit field must save that row, so its Save is the form's first submit button.
        var firstSubmit = buttons.First(b => b.Contains("type=\"submit\"", StringComparison.Ordinal));
        Assert.Contains("govuk-visually-hidden", firstSubmit, StringComparison.Ordinal);
        Assert.Matches($"formaction=\"[^\"]*ChangeDeadline[^\"]*participantId={Second}", firstSubmit);
        Assert.Matches("<input[^>]*name=\"Participants\\[0\\]\\.Selected\"[^>]*checked", html);
        Assert.Empty(_apiClient.SavedDeadlines);
    }

    [Fact]
    public async Task Post_Cancel_ClosesTheChangeForm()
    {
        var (response, html) = await PostAsync("EditDeadline", $"participantId={Second}&command=cancel");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain("NewDeadlineDate", html, StringComparison.Ordinal);
        Assert.Equal(2, Buttons(html).Count(b => b.Contains("command=edit", StringComparison.Ordinal)));
        Assert.Empty(_apiClient.SavedDeadlines);
    }

    // What a browser without scripting sends when the Save button in the second row's open form is pressed.
    [Fact]
    public async Task Post_Save_ChangesThatParticipantsDeadline()
    {
        var (response, _) = await PostAsync("ChangeDeadline", $"participantId={Second}",
            new("Participants[1].NewDeadlineDate", "2026-06-10"),
            new("Participants[1].NewDeadlineReason", "Courier delay"));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var (participantId, saved) = Assert.Single(_apiClient.SavedDeadlines);
        Assert.Equal(Second, participantId);
        Assert.Equal(new DateTime(2026, 6, 10), saved.NewDeadlineDate);
        Assert.Equal("Courier delay", saved.Reason);
    }

    [Fact]
    public async Task Post_SaveWithMissingValues_RedisplaysThatRowsFormWithTheErrors()
    {
        _apiClient.ChangeDeadlineResult = new ChangeDeadlineResult(false, "Deadline Date required.", "A reason for changing the deadline date is required.");

        var (response, html) = await PostAsync("ChangeDeadline", $"participantId={Second}",
            new("Participants[1].NewDeadlineDate", string.Empty),
            new("Participants[1].NewDeadlineReason", string.Empty));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("name=\"Participants[1].NewDeadlineDate\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Participants[0].NewDeadlineDate", html, StringComparison.Ordinal);
        Assert.Contains("Deadline Date required.", html, StringComparison.Ordinal);
        Assert.Contains("A reason for changing the deadline date is required.", html, StringComparison.Ordinal);
    }
}
