using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PTL.ApiClient;
using PTL.Contracts.Invoice;
using PTL.InternalWeb.Tests.TestSupport;

namespace PTL.InternalWeb.Tests.Integration;

// Renders Views/Shared/ConfirmationPage.cshtml through the real MVC pipeline for one stateless flow
// (Generate Invoices) and one flow that must carry an unsaved form (Schedule Cancel/Commence).
public class ConfirmationPageRenderTests : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly Guid SchemeId = Guid.Parse("eeeeeeee-0000-0000-0000-0000000000a1");

    private readonly WebApplicationFactory<Program> _factory;
    private readonly FakeInvoiceApiClient _invoices = new()
    {
        Pending = new PendingInvoiceSummaryResponse(2026, "2025/26", 5, 0, 0, ["ops@example.com"]),
    };

    public ConfirmationPageRenderTests(WebApplicationFactory<Program> factory) =>
        _factory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IInvoiceApiClient>();
                services.AddSingleton<IInvoiceApiClient>(_invoices);
            }));

    private static string MainContent(string html) => Regex.Match(html, "<main.*?</main>", RegexOptions.Singleline).Value;

    private async Task<string> PostAsync(string url, params (string Name, string Value)[] fields)
    {
        var client = _factory.CreateClient();
        var page = await (await client.GetAsync("/Invoice/Index")).Content.ReadAsStringAsync();
        var token = Regex.Match(page, "__RequestVerificationToken[^>]*value=\"([^\"]+)\"").Groups[1].Value;

        var content = new List<KeyValuePair<string, string>> { new("__RequestVerificationToken", token) };
        content.AddRange(fields.Select(f => new KeyValuePair<string, string>(f.Name, f.Value)));

        var response = await client.PostAsync(url, new FormUrlEncodedContent(content));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync();
    }

    [Fact]
    public async Task InvoiceIndex_NoLongerUsesABrowserConfirmDialog()
    {
        var html = await _factory.CreateClient().GetStringAsync("/Invoice/Index");

        Assert.DoesNotContain("confirm(", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Generate_NotConfirmed_RendersTheConfirmationPageWithoutGenerating()
    {
        var html = MainContent(await PostAsync("/Invoice/Generate"));

        Assert.Contains("<h1 class=\"govuk-heading-l\">Generate Invoices</h1>", html, StringComparison.Ordinal);
        Assert.Contains("Are you sure you want to create an invoice for the latest billing period?", html, StringComparison.Ordinal);
        Assert.Contains("action=\"/Invoice/Generate\"", html, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmation\" value=\"confirm\"", html, StringComparison.Ordinal);
        Assert.Contains("href=\"/Invoice/Index\"", html, StringComparison.Ordinal);
        Assert.False(_invoices.GenerateCalled);
    }

    [Fact]
    public async Task ToggleCancelled_NotConfirmed_CarriesTheUnsavedFormThroughConfirmAndCancel()
    {
        var html = MainContent(await PostAsync(
            $"/Distribution/ToggleCancelled?year=2026&monthId=4&schemeId={SchemeId}",
            ("Schemes[0].MonthlyDistributionSchemeId", SchemeId.ToString()),
            ("Schemes[0].IsCancelled", "false"),
            ("Schemes[0].DistributionDate", "2026-04-20")));

        Assert.Contains("Are you sure you wish to Cancel this Distribution?", html, StringComparison.Ordinal);
        Assert.Equal(2, Regex.Count(html, "name=\"Schemes\\[0\\]\\.DistributionDate\" value=\"2026-04-20\""));
        Assert.Contains("name=\"confirmation\" value=\"confirm\"", html, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmation\" value=\"cancel\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("__RequestVerificationToken\" value=\"\"", html, StringComparison.Ordinal);
    }
}
