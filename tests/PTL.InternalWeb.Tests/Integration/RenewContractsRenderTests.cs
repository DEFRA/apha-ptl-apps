using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PTL.ApiClient;
using PTL.Contracts.Contract;
using PTL.InternalWeb.Tests.TestSupport;

namespace PTL.InternalWeb.Tests.Integration;

// Renew Contracts (legacy MergeContracts.aspx) must work without scripting: every control that decides the
// renewal posts under its own name, so no script has to mirror ticks into hidden fields.
public partial class RenewContractsRenderTests : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly Guid CustomerId = Guid.Parse("aaaaaaaa-0000-0000-0000-0000000000c1");
    private static readonly Guid ContractA = Guid.Parse("aaaaaaaa-0000-0000-0000-0000000000a1");
    private static readonly Guid ContractB = Guid.Parse("aaaaaaaa-0000-0000-0000-0000000000b1");
    private static readonly Guid ItemA1 = Guid.Parse("bbbbbbbb-0000-0000-0000-0000000000a1");
    private static readonly Guid ItemA2 = Guid.Parse("bbbbbbbb-0000-0000-0000-0000000000a2");
    private static readonly Guid ItemB1 = Guid.Parse("bbbbbbbb-0000-0000-0000-0000000000b1");

    private static readonly string Url = $"/Contract/RenewContracts?customerId={CustomerId}";

    [GeneratedRegex("__RequestVerificationToken[^>]*value=\"([^\"]+)\"", RegexOptions.None)]
    private static partial Regex AntiforgeryTokenPattern();

    private readonly WebApplicationFactory<Program> _factory;
    private readonly FakeContractRenewalApiClient _renewals = new()
    {
        ContractsResponse = new RenewableContractsResponse(true, null, ["Alice Example"],
        [
            new RenewableContractDto(ContractA, "A", "Alice Example", "Info A", string.Empty, true, 2),
            new RenewableContractDto(ContractB, "B", "Alice Example", "Info B", string.Empty, true, 1)
        ]),
        ItemsResponse = new RenewableContractItemsResponse(
        [
            new RenewableContractItemDto(ContractA, "A", ItemA1, "L1", "Lab One", "S1", "Old 1", "N1", "New 1", true, "S1"),
            new RenewableContractItemDto(ContractA, "A", ItemA2, "L2", "Lab Two", "S2", "Old 2", "N2", "New 2", true, "S2"),
            new RenewableContractItemDto(ContractB, "B", ItemB1, "L3", "Lab Three", "S3", "Old 3", "N3", "New 3", true, "S3")
        ])
    };

    public RenewContractsRenderTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IContractRenewalApiClient>();
                services.AddSingleton<IContractRenewalApiClient>(_renewals);
                services.RemoveAll<ICustomerApiClient>();
                services.AddSingleton<ICustomerApiClient>(new FakeCustomerApiClient());
            }));
    }

    private static List<string> Tags(string html, string pattern) => [.. Regex.Matches(html, pattern).Select(m => m.Value)];

    private async Task<(string Html, HttpResponseMessage Response)> PostAsync(IEnumerable<KeyValuePair<string, string>> fields)
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var page = await client.GetAsync(Url);
        var token = AntiforgeryTokenPattern().Match(await page.Content.ReadAsStringAsync()).Groups[1].Value;
        var cookie = string.Join("; ", page.Headers.TryGetValues("Set-Cookie", out var cookies) ? cookies.Select(c => c.Split(';')[0]) : []);

        var request = new HttpRequestMessage(HttpMethod.Post, "/Contract/RenewContracts")
        {
            Content = new FormUrlEncodedContent([new("__RequestVerificationToken", token), new("customerId", CustomerId.ToString()), .. fields])
        };
        request.Headers.Add("Cookie", cookie);
        var response = await client.SendAsync(request);
        return (await response.Content.ReadAsStringAsync(), response);
    }

    [Fact]
    public async Task Get_EachItemRowIsANamedCheckboxPostingItsIdentifierAndNoHiddenMirrorsExist()
    {
        var html = await _factory.CreateClient().GetStringAsync(Url);

        var rows = Tags(html, "<input[^>]*name=\"selectedItemIdentifiers\"[^>]*>");
        Assert.Equal(["S1", "S2", "S3"], rows.Select(r => Regex.Match(r, "value=\"([^\"]+)\"").Groups[1].Value));
        Assert.All(rows, r => Assert.Contains("checked", r, StringComparison.Ordinal));
        Assert.DoesNotContain("name=\"selectedParticipantSchemeIds\"", html, StringComparison.Ordinal);
        Assert.Equal(2, Tags(html, "<input[^>]*name=\"selectedContractIds\"[^>]*>").Count);
    }

    [Fact]
    public async Task Get_OffersAnUpdateItemsButtonWhenScriptingIsUnavailable()
    {
        var html = await _factory.CreateClient().GetStringAsync(Url);

        Assert.Matches("<noscript>\\s*<button[^>]*type=\"submit\"[^>]*name=\"command\"[^>]*value=\"refresh\"[^>]*>Update items</button>\\s*</noscript>", html);
    }

    // What a browser without scripting sends: contract A ticked, contract B un-ticked, rows S1 and S3 ticked, S2 un-ticked.
    [Fact]
    public async Task Post_WithoutScript_RenewsOnlyTickedItemsOfSelectedContracts()
    {
        var (_, response) = await PostAsync(
        [
            new("selectedContractIds", ContractA.ToString()),
            new("selectedItemIdentifiers", "S1"),
            new("selectedItemIdentifiers", "S3"),
            new("newContractSignatory", "Alice Example")
        ]);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal([ContractA], _renewals.LastRenewRequest!.ContractIds);
        Assert.Equal([ItemA1], _renewals.LastRenewRequest.ParticipantSchemeIds);
        Assert.Equal("Alice Example", _renewals.LastRenewRequest.NewContractSignatory);
    }

    [Fact]
    public async Task Post_UpdateItems_RedisplaysWithoutRenewingAndHidesRowsOfDeselectedContracts()
    {
        var (html, response) = await PostAsync(
        [
            new("selectedContractIds", ContractA.ToString()),
            new("selectedItemIdentifiers", "S1"),
            new("selectedItemIdentifiers", "S3"),
            new("command", "refresh")
        ]);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(_renewals.LastRenewRequest);
        var hiddenRows = Tags(html, "<tr[^>]*item-row[^>]*>").Where(t => t.Contains(" hidden", StringComparison.Ordinal)).ToList();
        var hidden = Assert.Single(hiddenRows);
        Assert.Contains(ContractB.ToString(), hidden, StringComparison.Ordinal);
        var ticked = Tags(html, "<input[^>]*name=\"selectedItemIdentifiers\"[^>]*>").Where(t => t.Contains("checked", StringComparison.Ordinal)).ToList();
        Assert.Equal(2, ticked.Count);
    }

    // Every item row un-ticked: the browser posts no selectedItemIdentifiers at all.
    [Fact]
    public async Task Post_NoItemTicked_ShowsTheErrorSummaryLinkedToTheFirstItemAndRenewsNothing()
    {
        var (html, response) = await PostAsync(
        [
            new("selectedContractIds", ContractA.ToString()),
            new("selectedContractIds", ContractB.ToString())
        ]);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(_renewals.LastRenewRequest);
        Assert.Contains("<title>Error: Renew Contracts - Proficiency Testing</title>", html, StringComparison.Ordinal);
        Assert.Contains("govuk-error-summary__title\">There is a problem</h2>", html, StringComparison.Ordinal);
        Assert.Contains(
            $"<a href=\"#item-row-select-{ItemA1}\">Select at least one item to appear on the new contract</a>",
            html,
            StringComparison.Ordinal);
        Assert.Contains(
            "<p id=\"selectedItemIdentifiers-error\" class=\"govuk-error-message\"><span class=\"govuk-visually-hidden\">Error:</span> Select at least one item to appear on the new contract</p>",
            html,
            StringComparison.Ordinal);
        Assert.Contains("class=\"govuk-form-group govuk-form-group--error\" id=\"renew-items\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "checked",
            string.Concat(Tags(html, "<input[^>]*name=\"selectedItemIdentifiers\"[^>]*>")),
            StringComparison.Ordinal);
    }

    // No contract ticked leaves no item rows to link to, so the summary links to the items section.
    [Fact]
    public async Task Post_NoContractTicked_LinksTheErrorToTheItemsSection()
    {
        var (html, _) = await PostAsync([new("selectedItemIdentifiers", "S1")]);

        Assert.Null(_renewals.LastRenewRequest);
        Assert.Contains("<a href=\"#renew-items\">Select at least one item to appear on the new contract</a>", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Post_AllItemsTicked_Renews()
    {
        var (_, response) = await PostAsync(
        [
            new("selectedContractIds", ContractA.ToString()),
            new("selectedContractIds", ContractB.ToString()),
            new("selectedItemIdentifiers", "S1"),
            new("selectedItemIdentifiers", "S2"),
            new("selectedItemIdentifiers", "S3")
        ]);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal([ItemA1, ItemA2, ItemB1], _renewals.LastRenewRequest!.ParticipantSchemeIds);
    }

    [Fact]
    public async Task Post_SomeItemsTicked_Renews()
    {
        var (_, response) = await PostAsync(
        [
            new("selectedContractIds", ContractA.ToString()),
            new("selectedContractIds", ContractB.ToString()),
            new("selectedItemIdentifiers", "S2")
        ]);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal([ItemA2], _renewals.LastRenewRequest!.ParticipantSchemeIds);
    }

    // After the error the page shows exactly what the user posted: contract choices, signatory, and no item re-ticked.
    [Fact]
    public async Task Post_NoItemTicked_KeepsTheUsersSelectionsAndReTicksNothing()
    {
        var (html, _) = await PostAsync(
        [
            new("selectedContractIds", ContractA.ToString()),
            new("newContractSignatory", "Alice Example")
        ]);

        Assert.Null(_renewals.LastRenewRequest);
        Assert.Contains("checked", Assert.Single(Tags(html, $"<input[^>]*value=\"{ContractA}\"[^>]*>")), StringComparison.Ordinal);
        Assert.DoesNotContain("checked", Assert.Single(Tags(html, $"<input[^>]*value=\"{ContractB}\"[^>]*>")), StringComparison.Ordinal);
        Assert.Matches("<option value=\"Alice Example\" selected=\"selected\">", html);
        var itemBoxes = Tags(html, "<input[^>]*name=\"selectedItemIdentifiers\"[^>]*>");
        Assert.Equal(3, itemBoxes.Count);
        Assert.All(itemBoxes, box => Assert.DoesNotContain("checked", box, StringComparison.Ordinal));
        Assert.DoesNotContain("checked", Assert.Single(Tags(html, "<input[^>]*id=\"SchemeSelectAllCheckBox\"[^>]*>")), StringComparison.Ordinal);
        Assert.DoesNotContain("checked", Assert.Single(Tags(html, "<input[^>]*id=\"ContractSelectAllCheckBox\"[^>]*>")), StringComparison.Ordinal);
        Assert.Matches("<form[^>]*autocomplete=\"off\"", html);
    }

    // Legacy keeps each Select All box as the user last set it, whatever the rows below it show.
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task Post_Refused_KeepsEachSelectAllBoxAsTheUserLeftIt(bool selectAllContracts, bool selectAllItems)
    {
        var fields = new List<KeyValuePair<string, string>>
        {
            new("selectedContractIds", ContractA.ToString()),
            new("selectedContractIds", ContractB.ToString())
        };
        if (selectAllContracts)
        {
            fields.Add(new("selectAllContracts", "true"));
        }
        if (selectAllItems)
        {
            fields.Add(new("selectAllItems", "true"));
        }

        var (html, _) = await PostAsync(fields);

        Assert.Null(_renewals.LastRenewRequest);
        Assert.Equal(selectAllContracts, Assert.Single(Tags(html, "<input[^>]*id=\"ContractSelectAllCheckBox\"[^>]*>")).Contains("checked", StringComparison.Ordinal));
        Assert.Equal(selectAllItems, Assert.Single(Tags(html, "<input[^>]*id=\"SchemeSelectAllCheckBox\"[^>]*>")).Contains("checked", StringComparison.Ordinal));
    }

    // The GOV.UK box is drawn on the label itself, so only a nested span may be visually hidden. Legacy showed an item
    // without a next-year scheme as a greyed, unticked checkbox that could not be selected.
    [Fact]
    public async Task Get_ShowsEveryCheckboxAndGreysOutItemsWithoutANextYearScheme()
    {
        _renewals.ItemsResponse = new RenewableContractItemsResponse(
        [
            new RenewableContractItemDto(ContractA, "A", ItemA1, "L1", "Lab One", "S1", "Old 1", "N1", "New 1", true, "S1"),
            new RenewableContractItemDto(ContractA, "A", ItemA2, "L2", "Lab Two", "S2", "Old 2", null, null, false, "S2")
        ]);

        var html = await _factory.CreateClient().GetStringAsync(Url);

        Assert.DoesNotContain("govuk-checkboxes__label govuk-visually-hidden", html, StringComparison.Ordinal);
        Assert.Contains("<span class=\"govuk-visually-hidden\">Select item S1</span></label>", html, StringComparison.Ordinal);
        Assert.Contains($"<span class=\"govuk-visually-hidden\">Select contract A</span></label>", html, StringComparison.Ordinal);
        var renewable = Assert.Single(Tags(html, "<input[^>]*value=\"S1\"[^>]*>"));
        var notAvailable = Assert.Single(Tags(html, "<input[^>]*value=\"S2\"[^>]*>"));
        Assert.Contains("checked", renewable, StringComparison.Ordinal);
        Assert.DoesNotContain("disabled", renewable, StringComparison.Ordinal);
        Assert.Contains("disabled=\"disabled\"", notAvailable, StringComparison.Ordinal);
        Assert.DoesNotContain("checked", notAvailable, StringComparison.Ordinal);
        Assert.Contains("Scheme Not Available", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Get_TicksBothSelectAllBoxesWhichPostTheirState()
    {
        var html = await _factory.CreateClient().GetStringAsync(Url);

        var contractSelectAll = Assert.Single(Tags(html, "<input[^>]*id=\"ContractSelectAllCheckBox\"[^>]*>"));
        var itemSelectAll = Assert.Single(Tags(html, "<input[^>]*id=\"SchemeSelectAllCheckBox\"[^>]*>"));
        Assert.Contains("name=\"selectAllContracts\" value=\"true\" checked", contractSelectAll, StringComparison.Ordinal);
        Assert.Contains("name=\"selectAllItems\" value=\"true\" checked", itemSelectAll, StringComparison.Ordinal);
        Assert.Contains("<input type=\"hidden\" name=\"selectionChanged\" id=\"SelectionChanged\" value=\"false\" />", html, StringComparison.Ordinal);
    }
}
