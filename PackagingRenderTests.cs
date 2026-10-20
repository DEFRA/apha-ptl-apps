using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;

[assembly: WebApplicationFactoryContentRoot("PTL.InternalWeb", @"C:\Users\ac000232\source\repos\apha-ptl-apps\src\PTL.InternalWeb", "PTL.InternalWeb.csproj", "1")]

public sealed class BrowserAuth(IOptionsMonitor<AuthenticationSchemeOptions> o, ILoggerFactory l, UrlEncoder e) : AuthenticationHandler<AuthenticationSchemeOptions>(o, l, e)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var roles = Uri.UnescapeDataString(Request.Cookies["test-roles"] ?? "Admin");
        var claims = new[]
        {
            new Claim("internalUserId", "142b79a4-9593-4ceb-81c1-63c01acdea4e"),
            new Claim(ClaimTypes.NameIdentifier, "11111111-1111-1111-1111-111111111111"),
            new Claim("fullName", "Test Admin"),
            new Claim("resolvedRoles", roles)
        };
        var id = new ClaimsIdentity(claims, "TestScheme");
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(id), "TestScheme")));
    }
}

public sealed class BrowserFactory : WebApplicationFactory<global::Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder host) => host.ConfigureServices(s =>
        s.AddAuthentication(o => { o.DefaultScheme = "TestScheme"; o.DefaultAuthenticateScheme = "TestScheme"; o.DefaultChallengeScheme = "TestScheme"; })
            .AddScheme<AuthenticationSchemeOptions, BrowserAuth>("TestScheme", _ => { }));
}

public static class Verifier
{
    private const string Contract = "cbbb24cf-de0f-4268-8d92-bd48d57dfcb1";
    private const string Removed = "cd2e89aa-19bb-4d59-8275-b76590603466";
    private const string Customer = "c5cda5f8-5dfb-4442-bb91-96f234da7bc4";
    private const string Base = "http://localhost:5260";
    private static int fail;

    private static void Check(bool ok, string what)
    {
        Console.WriteLine($"  [{(ok ? "PASS" : "FAIL")}] {what}");
        if (!ok) { fail++; }
    }

    private static int Scalar(string sql)
    {
        using var cn = new SqlConnection("Server=(localdb)\\MSSQLLocalDB;Database=ProficiencyTesting_verify;Integrated Security=true;TrustServerCertificate=true");
        cn.Open();
        using var c = cn.CreateCommand();
        c.CommandText = sql;
        return Convert.ToInt32(c.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
    }

    public static async Task<int> Main()
    {
        Environment.SetEnvironmentVariable("Api__BaseUrl", "http://localhost:5262");
        Environment.SetEnvironmentVariable("Entra__TenantId", "00000000-0000-0000-0000-000000000000");
        Environment.SetEnvironmentVariable("Entra__ClientId", "test-client-id");
        Environment.SetEnvironmentVariable("Entra__ClientSecret", "test-client-secret");
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");

        var factory = new BrowserFactory();
        factory.UseKestrel(5260);
        factory.StartServer();
        _ = (Action)(() =>
        {
            (null as IWebHostBuilder)!.ConfigureServices(s =>
            {
                s.AddAuthentication(o => { o.DefaultScheme = "TestScheme"; o.DefaultAuthenticateScheme = "TestScheme"; o.DefaultChallengeScheme = "TestScheme"; })
                    .AddScheme<AuthenticationSchemeOptions, BrowserAuth>("TestScheme", _ => { });
            });
        });

        Directory.CreateDirectory(@"C:\Users\ac000232\AppData\Local\Temp\ptl-shots");
        using var pw = await Playwright.CreateAsync();
        await using var browser = await pw.Chromium.LaunchAsync(new() { Channel = "msedge", Headless = true });
        var ctx = await browser.NewContextAsync(new() { ViewportSize = new() { Width = 1300, Height = 1100 } });
        var page = await ctx.NewPageAsync();
        page.Dialog += async (_, d) => { Check(false, "unexpected dialog: " + d.Message); await d.DismissAsync(); };

        // 1. Grid rendering on the contract that has stored removed + NFP items
        Console.WriteLine("\n=== 1. Grid: NFP struck price, removed row, button set");
        await page.GotoAsync($"{Base}/Contract/ContractItems/{Removed}");
        var nfpCount = Scalar($"SELECT COUNT(*) FROM tlnkParticipantScheme WHERE fldContractId='{Removed}' AND fldIsRemoved=0 AND fldNonFeePaying=1");
        var removedCount = Scalar($"SELECT COUNT(*) FROM tlnkParticipantScheme WHERE fldContractId='{Removed}' AND fldIsRemoved=1");
        var struckPrices = await page.Locator("td.ptl-removed:nth-child(4)").CountAsync();
        Check(struckPrices == nfpCount + removedCount, $"struck price cells = NFP {nfpCount} + removed {removedCount} (got {struckPrices})");
        var decoration = await page.Locator("td.ptl-removed:nth-child(4)").First.EvaluateAsync<string>("e => getComputedStyle(e).textDecorationLine");
        Check(decoration == "line-through", $"computed text-decoration = {decoration}");
        var itemRemovedRows = await page.Locator("tr:has-text('[Item Removed]')").CountAsync();
        Check(itemRemovedRows == removedCount, $"'[Item Removed]' rows = {removedCount} (got {itemRemovedRows})");
        var removedRow = page.Locator("tr:has-text('[Item Removed]')").First;
        Check(await removedRow.Locator("td.ptl-removed").CountAsync() == 2, "removed row: participant and price struck");
        Check(await removedRow.Locator("a:text-is('View')").CountAsync() == 1 && await removedRow.Locator("button:text-is('Remove')").CountAsync() == 0, "removed row: 'View' link, no Remove button");
        Check(await page.Locator("a:text-is('Reset')").CountAsync() == 0 && await page.Locator("a:text-is('Save')").CountAsync() == 0, "no Reset/Save while nothing pending");
        await page.ScreenshotAsync(new() { Path = @"C:\Users\ac000232\AppData\Local\Temp\ptl-shots\items-grid.png", FullPage = true });

        // 2. Remove -> confirmation -> pending
        Console.WriteLine("\n=== 2. Remove -> confirm -> Pending Deletion");
        await page.GotoAsync($"{Base}/Contract/ContractItems/{Contract}");
        var activeBefore = Scalar($"SELECT COUNT(*) FROM tlnkParticipantScheme WHERE fldContractId='{Contract}' AND fldIsRemoved=0");
        await page.Locator("button:text-is('Remove')").First.ClickAsync();
        Check(await page.Locator("body").InnerTextAsync() is var t1 && t1.Contains("Are you sure you want to remove this Contract Item?"), "confirmation page shown");
        await page.Locator("button:text-is('Remove')").ClickAsync();
        Check(await page.Locator("tr:has-text('Pending Deletion')").CountAsync() == 1, "row shows 'Pending Deletion'");
        Check(await page.Locator("tr:has-text('Pending Deletion') td.ptl-removed").CountAsync() == 2, "pending row participant + price struck");
        Check(Scalar($"SELECT COUNT(*) FROM tlnkParticipantScheme WHERE fldContractId='{Contract}' AND fldIsRemoved=0") == activeBefore, "database untouched while pending");
        Check(await page.Locator("a:text-is('Reset')").CountAsync() == 1 && await page.Locator("a:text-is('Save')").CountAsync() == 1, "Reset and Save buttons appear");

        // cancel path keeps pending
        await page.Locator("button:text-is('Remove')").First.ClickAsync();
        await page.Locator("button:text-is('Cancel')").ClickAsync();
        Check(await page.Locator("tr:has-text('Pending Deletion')").CountAsync() == 1, "cancelling a second removal keeps the first pending");
        await page.ScreenshotAsync(new() { Path = @"C:\Users\ac000232\AppData\Local\Temp\ptl-shots\items-pending.png", FullPage = true });

        // Reset
        await page.Locator("a:text-is('Reset')").ClickAsync();
        Check(await page.Locator("tr:has-text('Pending Deletion')").CountAsync() == 0, "Reset clears pending");

        // 3. Save -> audit page
        Console.WriteLine("\n=== 3. Save -> Audit Trail (required reason) -> saved");
        await page.Locator("button:text-is('Remove')").First.ClickAsync();
        await page.Locator("button:text-is('Remove')").ClickAsync();
        await page.Locator("a:text-is('Save')").ClickAsync();
        Check(await page.Locator("h1").InnerTextAsync() == "Audit Trail", "Audit Trail page");
        await page.Locator("button:text-is('Save')").ClickAsync();
        var summary = await page.Locator(".govuk-error-summary").InnerTextAsync();
        Check(summary.Contains("Please enter why you are making these changes before you continue"), "empty reason -> legacy error in summary");
        await page.ScreenshotAsync(new() { Path = @"C:\Users\ac000232\AppData\Local\Temp\ptl-shots\items-audit-error.png", FullPage = true });
        var auditBefore = Scalar("SELECT COUNT(*) FROM tblAuditParticipantScheme");
        await page.Locator("textarea").FillAsync("Browser verification removal");
        await page.Locator("button:text-is('Save')").ClickAsync();
        await page.WaitForURLAsync($"**/Contract/Index**");
        var body = await page.Locator("body").InnerTextAsync();
        Check(body.Contains("Contract items removed successfully."), "success notification on the contract list");
        Check(Scalar($"SELECT COUNT(*) FROM tlnkParticipantScheme WHERE fldContractId='{Contract}' AND fldIsRemoved=0") == activeBefore - 1, "exactly one item now removed in SQL");
        Check(Scalar("SELECT COUNT(*) FROM tblAuditParticipantScheme") == auditBefore + 1, "audit row written");
        Check(Scalar("SELECT COUNT(*) FROM tblAuditParticipantScheme WHERE fldAuditWhy='Browser verification removal'") == 1, "audit row carries the typed reason");

        // 4. Removed item now renders as [Item Removed]
        Console.WriteLine("\n=== 4. Saved removal now displays '[Item Removed]'");
        await page.GotoAsync($"{Base}/Contract/ContractItems/{Contract}");
        Check(await page.Locator("tr:has-text('[Item Removed]')").CountAsync() == 1, "'[Item Removed]' row present");

        await ctx.CloseAsync();
        Console.WriteLine($"\nFAILURES: {fail}");
        return fail;
    }
}

