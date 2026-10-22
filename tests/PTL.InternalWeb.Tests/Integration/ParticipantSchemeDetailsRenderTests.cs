using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PTL.ApiClient;
using PTL.Contracts.Contract;
using PTL.Contracts.Participant;
using PTL.InternalWeb.Tests.TestSupport;

namespace PTL.InternalWeb.Tests.Integration;

// Renders Features/ParticipantScheme/Views/Details.cshtml: a read-only page, so values must be text
// (GOV.UK summary lists and a table), never disabled form controls.
public class ParticipantSchemeDetailsRenderTests : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly Guid ParticipantSchemeId = Guid.Parse("ffffffff-0000-0000-0000-0000000000a1");
    private static readonly Guid ContractId = Guid.Parse("ffffffff-0000-0000-0000-0000000000a2");
    private static readonly Guid CustomerId = Guid.Parse("ffffffff-0000-0000-0000-0000000000a3");

    private readonly WebApplicationFactory<Program> _factory;

    public ParticipantSchemeDetailsRenderTests(WebApplicationFactory<Program> factory)
    {
        var participantSchemes = new FakeParticipantSchemeApiClient
        {
            ParticipantSchemeResponse = new ParticipantSchemeResponse(
                ParticipantSchemeId, ContractId, Guid.NewGuid(), Guid.NewGuid(),
                true, false, false, false, false, false, false, false, false, false, false, false,
                true, true, true, true, true, true, true, true, true, true, true, true,
                2, "REF-1", null, false, true, false, false, null, false, false,
                false, false, false, false, false, false, false, false, false, false, false, false,
                42.5m, "Lab One: LAB1", "S1: Salmonella"),
        };
        var contracts = new FakeContractApiClient
        {
            ContractResponse = new ContractResponse(
                ContractId, CustomerId, "Sample Laboratories Ltd", "QAL/00001", DateTime.UtcNow.Year, "UT12345",
                string.Empty, "Alice Example", string.Empty, string.Empty, 0, 0, 0, 0, 0, 0, 0, 0,
                DateTime.UtcNow, DateTime.UtcNow, DateTime.UtcNow, string.Empty, DateTime.UtcNow, true, false, "A",
                DateTime.UtcNow, string.Empty, false, false, false, null, null),
        };

        _factory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IParticipantSchemeApiClient>();
                services.AddSingleton<IParticipantSchemeApiClient>(participantSchemes);
                services.RemoveAll<IContractApiClient>();
                services.AddSingleton<IContractApiClient>(contracts);
                services.RemoveAll<IParticipantApiClient>();
                services.AddSingleton<IParticipantApiClient>(new FakeParticipantApiClient());
                services.RemoveAll<ISchemeApiClient>();
                services.AddSingleton<ISchemeApiClient>(new FakeSchemeApiClient());
                services.RemoveAll<ILookupApiClient>();
                services.AddSingleton<ILookupApiClient>(new FakeLookupApiClient());
            }));
    }

    private async Task<string> MainContentAsync()
    {
        var response = await _factory.CreateClient().GetAsync($"/ParticipantScheme/Details/{ParticipantSchemeId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        return Regex.Match(html, "<main.*?</main>", RegexOptions.Singleline).Value;
    }

    [Fact]
    public async Task Get_ShowsValuesAsTextWithNoDisabledControlsOrFormFields()
    {
        var html = await MainContentAsync();

        Assert.DoesNotContain("<input", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<select", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<script", html, StringComparison.Ordinal);
        Assert.DoesNotContain("disabled", html, StringComparison.Ordinal);
        Assert.Contains("<dt class=\"govuk-summary-list__key\">Participant</dt>", html, StringComparison.Ordinal);
        Assert.Contains("<dd class=\"govuk-summary-list__value\">Lab One: LAB1</dd>", html, StringComparison.Ordinal);
        Assert.Contains("<dd class=\"govuk-summary-list__value\">REF-1</dd>", html, StringComparison.Ordinal);
        // Legacy ParticipantScheme.aspx has no price - it only appears on the Contract Items grid.
        Assert.DoesNotContain("42.50", html, StringComparison.Ordinal);
        Assert.DoesNotContain(">Price<", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Get_HasOneH1_NoBackLink_AndTheEditButtonOnTheRight()
    {
        var html = await MainContentAsync();

        Assert.Equal(1, Regex.Count(html, "<h1[ >]"));
        Assert.DoesNotContain("govuk-back-link", html, StringComparison.Ordinal);
        Assert.Contains($"<div class=\"govuk-grid-column-one-third govuk-!-text-align-right\">", html, StringComparison.Ordinal);
        Assert.Contains($"href=\"/ParticipantScheme/Edit/{ParticipantSchemeId}\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Get_DistributionMonthsTable_HasACaptionRowHeadersAndYesNoText()
    {
        var html = await MainContentAsync();

        Assert.Contains("<caption class=\"govuk-table__caption govuk-table__caption--m\">Months</caption>", html, StringComparison.Ordinal);
        Assert.Matches("<th scope=\"row\" class=\"govuk-table__header\">January</th>\\s*<td class=\"govuk-table__cell\">Yes</td>\\s*<td class=\"govuk-table__cell\">No</td>", html);
        Assert.Matches("<th scope=\"row\" class=\"govuk-table__header\">February</th>\\s*<td class=\"govuk-table__cell\">No</td>", html);
    }

    [Fact]
    public async Task Get_EmptyOptionalFields_ShowNotProvided()
    {
        var html = await MainContentAsync();

        Assert.True(Regex.Count(html, "Not provided") >= 2);
    }
}
