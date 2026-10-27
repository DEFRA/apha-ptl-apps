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
}
