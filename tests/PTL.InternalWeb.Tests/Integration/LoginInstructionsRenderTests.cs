using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PTL.ApiClient;
using PTL.Contracts.Participant;
using PTL.Contracts.TestConsultant;
using PTL.InternalWeb.Tests.TestSupport;

namespace PTL.InternalWeb.Tests.Integration;

// The Login Instructions column at the end of Manage Participants, Manage Viewers and Manage External Test Consultants:
// users that are already Sent show a green "Sent" tag, the rest show a Send Instructions button styled as a link.
public class LoginInstructionsRenderTests : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly Guid CustomerId = Guid.NewGuid();
    private static readonly Guid SentParticipantId = Guid.NewGuid();
    private static readonly Guid NewParticipantId = Guid.NewGuid();
    private static readonly Guid SentViewerId = Guid.NewGuid();
    private static readonly Guid NewViewerId = Guid.NewGuid();
    private static readonly Guid SentConsultantId = Guid.NewGuid();
    private static readonly Guid NewConsultantId = Guid.NewGuid();
    private static readonly Guid InactiveSentParticipantId = Guid.NewGuid();
    private static readonly Guid InactiveNewParticipantId = Guid.NewGuid();
    private static readonly Guid InactiveSentConsultantId = Guid.NewGuid();
    private static readonly Guid InactiveNewConsultantId = Guid.NewGuid();

    private const string SentTag = "<strong class=\"govuk-tag govuk-tag--green\">Sent</strong>";
    private const string NotSentTag = "<strong class=\"govuk-tag govuk-tag--grey\">Not Sent</strong>";

    private readonly WebApplicationFactory<Program> _factory;

    public LoginInstructionsRenderTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IParticipantApiClient>();
                services.AddSingleton<IParticipantApiClient>(new FakeParticipantApiClient
                {
                    SearchResponse = new ParticipantSearchResponse(
                        [
                            new ParticipantSummaryResponse(SentParticipantId, CustomerId, "0101", "Sent Lab", "Sam Sent", true, true),
                            new ParticipantSummaryResponse(NewParticipantId, CustomerId, "0202", "New Lab", "Nina New", true, false),
                            new ParticipantSummaryResponse(InactiveSentParticipantId, CustomerId, "0303", "Dormant Lab", "Dot Dormant", false, true),
                            new ParticipantSummaryResponse(InactiveNewParticipantId, CustomerId, "0404", "Retired Lab", "Rae Retired", false, false),
                        ],
                        4, 1, 20)
                });
                services.RemoveAll<ICustomerApiClient>();
                services.AddSingleton<ICustomerApiClient>(new FakeCustomerApiClient());
                services.RemoveAll<ILookupApiClient>();
                services.AddSingleton<ILookupApiClient>(new FakeLookupApiClient());
                services.RemoveAll<IViewerApiClient>();
                services.AddSingleton<IViewerApiClient>(new FakeViewerApiClient
                {
                    Viewers =
                    [
                        new PTL.Contracts.Viewer.ViewerResponse(SentViewerId, "Sam Sent", "sam@example.com", [], [], true),
                        new PTL.Contracts.Viewer.ViewerResponse(NewViewerId, "Nina New", "nina@example.com", [], [], false),
                    ]
                });
                services.RemoveAll<IExternalTestConsultantApiClient>();
                services.AddSingleton<IExternalTestConsultantApiClient>(new FakeExternalTestConsultantApiClient
                {
                    TestConsultants =
                    [
                        new ExternalTestConsultantResponse(SentConsultantId, "Sam Sent", "Science", "sam@example.com", false, null, 0, null, true),
                        new ExternalTestConsultantResponse(NewConsultantId, "Nina New", "Science", "nina@example.com", false, null, 0, null, false),
                        new ExternalTestConsultantResponse(InactiveSentConsultantId, "Zed Dormant", "Science", "zed@example.com", true, new DateTime(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc), 0, null, true),
                        new ExternalTestConsultantResponse(InactiveNewConsultantId, "Zoe Retired", "Science", "zoe@example.com", true, new DateTime(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc), 0, null, false),
                    ]
                });
            }));
    }

    private static string Row(string html, string rowText)
    {
        var rows = Regex.Matches(html, "<tr class=\"govuk-table__row\">.*?</tr>", RegexOptions.Singleline);
        return rows.Select(m => m.Value).Single(r => r.Contains(rowText, StringComparison.Ordinal));
    }

    [Fact]
    public async Task ParticipantList_LoginInstructionsIsTheLastColumn()
    {
        var html = await _factory.CreateClient().GetStringAsync($"/Participant/Index?customerId={CustomerId}");

        var headers = Regex.Matches(html, "<th scope=\"col\" class=\"govuk-table__header\">([^<]*)</th>").Select(m => m.Groups[1].Value).ToList();
        Assert.Equal("Login Instructions", headers[^1]);
    }

    [Fact]
    public async Task ParticipantList_ShowsSentForExistingUsersAndALinkForNewOnes()
    {
        var html = await _factory.CreateClient().GetStringAsync($"/Participant/Index?customerId={CustomerId}");

        var sentRow = Row(html, "Sent Lab");
        Assert.Contains(SentTag, sentRow, StringComparison.Ordinal);
        Assert.DoesNotContain("Send Instructions", sentRow, StringComparison.Ordinal);

        var newRow = Row(html, "New Lab");
        Assert.DoesNotContain(SentTag, newRow, StringComparison.Ordinal);
        Assert.Contains("Send Instructions", newRow, StringComparison.Ordinal);
        Assert.Contains($"action=\"/Participant/SendLoginInstructions?participantId={NewParticipantId}&amp;customerId={CustomerId}", newRow, StringComparison.Ordinal);
        Assert.Contains("method=\"post\"", newRow, StringComparison.Ordinal);
        Assert.Contains("__RequestVerificationToken", newRow, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ParticipantList_InactiveParticipants_ShowSentOrNotSentAndNeverALink()
    {
        var html = await _factory.CreateClient().GetStringAsync($"/Participant/Index?customerId={CustomerId}");

        var inactiveSentRow = Row(html, "Dormant Lab");
        Assert.Contains(SentTag, inactiveSentRow, StringComparison.Ordinal);
        Assert.DoesNotContain(NotSentTag, inactiveSentRow, StringComparison.Ordinal);
        Assert.DoesNotContain("Send Instructions", inactiveSentRow, StringComparison.Ordinal);
        Assert.DoesNotContain("SendLoginInstructions", inactiveSentRow, StringComparison.Ordinal);

        var inactiveNewRow = Row(html, "Retired Lab");
        Assert.Contains(NotSentTag, inactiveNewRow, StringComparison.Ordinal);
        Assert.DoesNotContain(SentTag, inactiveNewRow, StringComparison.Ordinal);
        Assert.DoesNotContain("Send Instructions", inactiveNewRow, StringComparison.Ordinal);
        Assert.DoesNotContain("SendLoginInstructions", inactiveNewRow, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ParticipantList_ActiveParticipants_NeverShowNotSent()
    {
        var html = await _factory.CreateClient().GetStringAsync($"/Participant/Index?customerId={CustomerId}");

        Assert.DoesNotContain(NotSentTag, Row(html, "Sent Lab"), StringComparison.Ordinal);
        Assert.DoesNotContain(NotSentTag, Row(html, "New Lab"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ViewerList_ShowsSentForExistingUsersAndALinkForNewOnes()
    {
        var html = await _factory.CreateClient().GetStringAsync("/SystemAdministration/ViewerManagement");

        Assert.Contains(">Login Instructions</th>", html, StringComparison.Ordinal);
        var sentRow = Row(html, "sam@example.com");
        Assert.Contains(SentTag, sentRow, StringComparison.Ordinal);
        Assert.DoesNotContain("Send Instructions", sentRow, StringComparison.Ordinal);

        var newRow = Row(html, "nina@example.com");
        Assert.DoesNotContain(SentTag, newRow, StringComparison.Ordinal);
        Assert.Contains("command=sendInstructions&amp;rowIndex=", newRow, StringComparison.Ordinal);
        Assert.Contains("Send Instructions", newRow, StringComparison.Ordinal);
    }

    // Rows are listed by name, so Nina New (not sent) comes before Sam Sent.
    [Fact]
    public async Task ViewerList_RoundTripsTheSentFlagOfEveryRow()
    {
        var html = await _factory.CreateClient().GetStringAsync("/SystemAdministration/ViewerManagement");

        Assert.Contains("name=\"Rows[0].LoginInstructionsSent\" value=\"false\"", html, StringComparison.Ordinal);
        Assert.Contains("name=\"Rows[1].LoginInstructionsSent\" value=\"true\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExternalTestConsultantList_ShowsSentForExistingUsersAndALinkForNewOnes()
    {
        var html = await _factory.CreateClient().GetStringAsync("/SystemAdministration/ExternalTestConsultantManagement");

        var headers = Regex.Matches(html, "<th scope=\"col\" class=\"govuk-table__header\">([^<]*)</th>").Select(m => m.Groups[1].Value).ToList();
        Assert.Equal("Login Instructions", headers[^1]);
        var sentRow = Row(html, "sam@example.com");
        Assert.Contains(SentTag, sentRow, StringComparison.Ordinal);
        Assert.DoesNotContain("Send Instructions", sentRow, StringComparison.Ordinal);

        var newRow = Row(html, "nina@example.com");
        Assert.DoesNotContain(SentTag, newRow, StringComparison.Ordinal);
        Assert.Contains("command=sendInstructions&amp;rowIndex=", newRow, StringComparison.Ordinal);
        Assert.Contains("Send Instructions", newRow, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExternalTestConsultantList_InactiveConsultants_ShowSentOrNotSentAndNeverALink()
    {
        var html = await _factory.CreateClient().GetStringAsync("/SystemAdministration/ExternalTestConsultantManagement");

        var inactiveSentRow = Row(html, "zed@example.com");
        Assert.Contains(SentTag, inactiveSentRow, StringComparison.Ordinal);
        Assert.DoesNotContain(NotSentTag, inactiveSentRow, StringComparison.Ordinal);
        Assert.DoesNotContain("Send Instructions", inactiveSentRow, StringComparison.Ordinal);
        Assert.DoesNotContain("command=sendInstructions", inactiveSentRow, StringComparison.Ordinal);

        var inactiveNewRow = Row(html, "zoe@example.com");
        Assert.Contains(NotSentTag, inactiveNewRow, StringComparison.Ordinal);
        Assert.DoesNotContain(SentTag, inactiveNewRow, StringComparison.Ordinal);
        Assert.DoesNotContain("Send Instructions", inactiveNewRow, StringComparison.Ordinal);
        Assert.DoesNotContain("command=sendInstructions", inactiveNewRow, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExternalTestConsultantList_ActiveConsultants_NeverShowNotSent()
    {
        var html = await _factory.CreateClient().GetStringAsync("/SystemAdministration/ExternalTestConsultantManagement");

        Assert.DoesNotContain(NotSentTag, Row(html, "sam@example.com"), StringComparison.Ordinal);
        Assert.DoesNotContain(NotSentTag, Row(html, "nina@example.com"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ViewerList_HasNoNotSentTag_BecauseViewersHaveNoActiveState()
    {
        var html = await _factory.CreateClient().GetStringAsync("/SystemAdministration/ViewerManagement");

        Assert.DoesNotContain(NotSentTag, html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExternalTestConsultantList_RoundTripsTheSentFlagOfEveryRow()
    {
        var html = await _factory.CreateClient().GetStringAsync("/SystemAdministration/ExternalTestConsultantManagement");

        Assert.Contains("name=\"Rows[0].LoginInstructionsSent\" value=\"false\"", html, StringComparison.Ordinal);
        Assert.Contains("name=\"Rows[1].LoginInstructionsSent\" value=\"true\"", html, StringComparison.Ordinal);
    }
}
