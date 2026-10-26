using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PTL.ApiClient;
using PTL.Contracts.Contract;
using PTL.InternalWeb.Tests.TestSupport;

namespace PTL.InternalWeb.Tests.Integration;

// Renders Features/Contract/Views/ImportPermits.cshtml - legacy Contracts Admin/ImportPermit.aspx,
// where Edit/Update/Cancel only change the in-memory list and Apply/Save write it to the database.
public class ImportPermitsRenderTests : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly Guid ContractId = Guid.Parse("dddddddd-0000-0000-0000-0000000000a1");
    private static readonly Guid CustomerId = Guid.Parse("dddddddd-0000-0000-0000-0000000000a2");
    private static readonly Guid First = Guid.Parse("dddddddd-0000-0000-0000-0000000000b1");
    private static readonly Guid Second = Guid.Parse("dddddddd-0000-0000-0000-0000000000b2");

    private static readonly string Url = $"/Contract/ImportPermits/{ContractId}";

    private readonly WebApplicationFactory<Program> _factory;
    private readonly FakeImportPermitApiClient _permits = new()
    {
        Permits =
        [
            new ImportPermitResponse(First, "PT0001", "AHS", "1476", true, false, null),
            new ImportPermitResponse(Second, "PT0002", "BHS", "1477", false, false, null)
        ]
    };

    public ImportPermitsRenderTests(WebApplicationFactory<Program> factory)
    {
        var contracts = new FakeContractApiClient
        {
            ContractResponse = new ContractResponse(
                ContractId, CustomerId, "Sample Laboratories Ltd", "QAL/00001", DateTime.UtcNow.Year, "UT3/306",
                string.Empty, "Alice Example", string.Empty, string.Empty, 0.1m, 25m, 2, 5m, 3, 4m, 1, 6m,
                null, null, null, string.Empty, null, true, false, "A", DateTime.UtcNow, string.Empty, false, false, false, null, null)
        };

        _factory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IImportPermitApiClient>();
                services.AddSingleton<IImportPermitApiClient>(_permits);
                services.RemoveAll<IContractApiClient>();
                services.AddSingleton<IContractApiClient>(contracts);
            }));
    }

    private static string MainContent(string html) => Regex.Match(html, "<main.*?</main>", RegexOptions.Singleline).Value;

    private async Task<string> PostAsync(string command, Guid? rowId, params (string Name, string Value)[] fields)
    {
        var response = await SendPostAsync(command, rowId, fields);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync();
    }

    private async Task<HttpResponseMessage> SendPostAsync(string command, Guid? rowId, params (string Name, string Value)[] fields)
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var page = await (await client.GetAsync(Url)).Content.ReadAsStringAsync();
        var token = Regex.Match(page, "__RequestVerificationToken[^>]*value=\"([^\"]+)\"").Groups[1].Value;

        var content = new List<KeyValuePair<string, string>>
        {
            new("__RequestVerificationToken", token),
            new("pending[0].ParticipantSchemeId", First.ToString()),
            new("pending[0].ImportPermitReceived", "false"),
            new("pending[0].ImportPermitExpiry", string.Empty),
            new("pending[1].ParticipantSchemeId", Second.ToString()),
            new("pending[1].ImportPermitReceived", "false"),
            new("pending[1].ImportPermitExpiry", string.Empty)
        };
        content.AddRange(fields.Select(f => new KeyValuePair<string, string>(f.Name, f.Value)));

        var query = $"{Url}?command={command}" + (rowId is null ? string.Empty : $"&rowId={rowId}");
        return await client.PostAsync(query, new FormUrlEncodedContent(content));
    }

    [Fact]
    public async Task Get_ShowsEditButtonsAndSaveCancel_WithoutApplyOrAnyUpdateImportPermitPost()
    {
        var html = MainContent(await _factory.CreateClient().GetStringAsync(Url));

        Assert.Contains($"formaction=\"/Contract/ImportPermits/{ContractId}?command=edit&amp;rowId={First}\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("?command=apply\"", html, StringComparison.Ordinal);
        Assert.Contains($"?command=save\"", html, StringComparison.Ordinal);
        Assert.Contains($"?command=cancel\"", html, StringComparison.Ordinal);
        Assert.Contains("name=\"pending[0].ImportPermitReceived\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("UpdateImportPermit", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Get_BreadcrumbGoesStraightFromContractsToImportPermits_WithoutContractItems()
    {
        var html = await _factory.CreateClient().GetStringAsync(Url);

        var crumbs = Regex.Match(html, "<nav class=\"govuk-breadcrumbs\".*?</nav>", RegexOptions.Singleline).Value;
        var labels = Regex.Matches(crumbs, "<li class=\"govuk-breadcrumbs__list-item\"[^>]*>(.*?)</li>", RegexOptions.Singleline)
            .Select(m => Regex.Replace(m.Groups[1].Value, "<[^>]+>", string.Empty).Trim())
            .ToList();

        Assert.Equal(["Home", "Manage Contracts", "Customers", "Contracts", "Import Permit(s)"], labels);
        Assert.Contains($"customerId={CustomerId}", crumbs, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Edit_RendersEditControlsAndUpdateAndCancelButtonsForThatRow()
    {
        var html = MainContent(await PostAsync("edit", First));

        Assert.Contains("name=\"editReceived\"", html, StringComparison.Ordinal);
        Assert.Contains("name=\"editExpiry\"", html, StringComparison.Ordinal);
        Assert.Contains("command=update", html, StringComparison.Ordinal);
        Assert.Contains("command=cancelEdit", html, StringComparison.Ordinal);
        Assert.Empty(_permits.UpdateCalls);
    }

    [Fact]
    public async Task Update_ShowsTheChangeOnScreen_WithoutWritingOrShowingABanner()
    {
        var html = await PostAsync("update", First, ("editReceived", "true"), ("editExpiry", "01/01/2027"));

        var main = MainContent(html);
        Assert.Contains("value=\"01/01/2027\"", main, StringComparison.Ordinal);
        Assert.Contains("<td class=\"govuk-table__cell\">\n                            Yes", main.Replace("\r\n", "\n"), StringComparison.Ordinal);
        Assert.DoesNotContain("name=\"editReceived\"", main, StringComparison.Ordinal);
        Assert.DoesNotContain("successfully", html, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(_permits.UpdateCalls);
    }

    [Fact]
    public async Task Update_ReceivedWithoutExpiry_ShowsTheErrorAndKeepsTheRowInEditMode()
    {
        var html = MainContent(await PostAsync("update", First, ("editReceived", "true"), ("editExpiry", string.Empty)));

        Assert.Contains("Expiry date required", html, StringComparison.Ordinal);
        Assert.Contains("name=\"editExpiry\"", html, StringComparison.Ordinal);
        Assert.Empty(_permits.UpdateCalls);
    }

    [Fact]
    public async Task Save_PersistsAllPermitsAndRedirectsToTheContractList()
    {
        var response = await SendPostAsync("save", null);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal($"/Contract/Index?customerId={CustomerId}", response.Headers.Location!.OriginalString);
        Assert.Equal(2, _permits.UpdateCalls.Count);
    }

    [Fact]
    public async Task Cancel_RedirectsToTheContractListWithoutWriting()
    {
        var response = await SendPostAsync("cancel", null);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Empty(_permits.UpdateCalls);
    }
}
