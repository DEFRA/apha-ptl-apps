using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PTL.ApiClient.Distribution;
using PTL.Contracts.Distribution;
using PTL.InternalWeb.Tests.TestSupport;

namespace PTL.InternalWeb.Tests.Integration;

// Distribution Participants (legacy Scheduling/DistributionParticipantList.aspx): the Change Deadline Date form
// opens natively (details element) and saves through the same server action, so it needs no script.
public partial class DistributionParticipantsRenderTests : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly Guid Scheme = Guid.Parse("aaaaaaaa-0000-0000-0000-0000000000a1");
    private static readonly Guid First = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000001");
    private static readonly Guid Second = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002");

    private static readonly string Url = $"/Distribution/Participants?year=2026&monthId=5&schemeId={Scheme}";

    [GeneratedRegex("__RequestVerificationToken[^>]*value=\"([^\"]+)\"", RegexOptions.None)]
    private static partial Regex AntiforgeryTokenPattern();

    private readonly WebApplicationFactory<Program> _factory;
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

    [Fact]
    public async Task Get_EachRowHasAnOpenableChangeFormWithItsInputsAndSaveButton()
    {
        var html = await _factory.CreateClient().GetStringAsync(Url);

        Assert.DoesNotContain("distribution-change-deadline-toggle", html, StringComparison.Ordinal);
        foreach (var (index, id) in new[] { (0, First), (1, Second) })
        {
            var details = Regex.Match(html, $"<details[^>]*id=\"deadline-{index}\"[^>]*>(.*?)</details>", RegexOptions.Singleline);
            Assert.True(details.Success, $"Row {index} has no change form");
            Assert.DoesNotContain("hidden", details.Value[..details.Value.IndexOf('>', StringComparison.Ordinal)], StringComparison.Ordinal);
            Assert.Contains("<summary class=\"govuk-details__summary\">", details.Value, StringComparison.Ordinal);
            Assert.Contains($"name=\"Participants[{index}].NewDeadlineDate\"", details.Value, StringComparison.Ordinal);
            Assert.Contains($"name=\"Participants[{index}].NewDeadlineReason\"", details.Value, StringComparison.Ordinal);
            Assert.Matches($"<button[^>]*formaction=\"[^\"]*ChangeDeadline[^\"]*participantId={id}", details.Value);
        }
    }

    // What a browser without scripting sends when the Save button inside the second row's form is pressed.
    [Fact]
    public async Task Post_SavingARowWithoutScript_ChangesThatParticipantsDeadline()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var page = await client.GetAsync(Url);
        var token = AntiforgeryTokenPattern().Match(await page.Content.ReadAsStringAsync()).Groups[1].Value;
        var cookie = string.Join("; ", page.Headers.TryGetValues("Set-Cookie", out var cookies) ? cookies.Select(c => c.Split(';')[0]) : []);

        var request = new HttpRequestMessage(HttpMethod.Post, $"/Distribution/ChangeDeadline?year=2026&monthId=5&schemeId={Scheme}&participantId={Second}")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["MonthlyDistributionSchemeId"] = Scheme.ToString(),
                ["YearId"] = "2026",
                ["MonthId"] = "5",
                ["IsRepeatDistribution"] = "false",
                ["Participants[0].DistributionParticipantId"] = First.ToString(),
                ["Participants[0].AdditionalParticipant"] = "false",
                ["Participants[0].DeadlineDate"] = "2026-06-01",
                ["Participants[0].DeadlineChangeReason"] = string.Empty,
                ["Participants[0].NewDeadlineDate"] = string.Empty,
                ["Participants[0].NewDeadlineReason"] = string.Empty,
                ["Participants[1].DistributionParticipantId"] = Second.ToString(),
                ["Participants[1].AdditionalParticipant"] = "false",
                ["Participants[1].DeadlineDate"] = "2026-06-02",
                ["Participants[1].DeadlineChangeReason"] = "Late post",
                ["Participants[1].NewDeadlineDate"] = "2026-06-10",
                ["Participants[1].NewDeadlineReason"] = "Courier delay",
            })
        };
        request.Headers.Add("Cookie", cookie);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var (participantId, saved) = Assert.Single(_apiClient.SavedDeadlines);
        Assert.Equal(Second, participantId);
        Assert.Equal(new DateTime(2026, 6, 10), saved.NewDeadlineDate);
        Assert.Equal("Courier delay", saved.Reason);
    }
}
