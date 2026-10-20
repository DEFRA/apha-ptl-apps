using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging.Abstractions;
using PTL.Contracts.Contract;
using PTL.InternalWeb.Features.Contract;
using PTL.InternalWeb.Tests.TestSupport;

namespace PTL.InternalWeb.Tests.Features.Contract;

// The Contracts list remembers the last search in a cookie; these tests drive it through the controller.
public class ContractControllerSearchStateTests
{
    private static readonly Guid CustomerId = Guid.Parse("88888888-0000-0000-0000-0000000000a1");

    private static (ContractController Controller, DefaultHttpContext Http, FakeContractApiClient Contracts) Create(
        string query = "", string? cookieJson = null, bool https = false, string pathBase = "")
    {
        var http = new DefaultHttpContext();
        http.Request.QueryString = new QueryString(query);
        http.Request.Scheme = https ? "https" : "http";
        http.Request.PathBase = pathBase;
        if (cookieJson is not null)
        {
            http.Request.Headers.Cookie = $"ptl-contract-search={Uri.EscapeDataString(cookieJson)}";
        }

        var contracts = new FakeContractApiClient();
        var controller = new ContractController(contracts, new FakeCustomerApiClient(), new FakeLookupApiClient(), new FakeImportPermitApiClient(), NullLogger<ContractController>.Instance, new FakeContractExportApiClient(), new FakeContractRenewalApiClient(), new FakeExportTemplateApiClient(), new FakeBulkExportApiClient(), new PTL.Core.Contract.Document.TemplateMergeService())
        {
            ControllerContext = new ControllerContext { HttpContext = http },
            TempData = new TempDataDictionary(http, new FakeTempDataProvider()),
        };
        return (controller, http, contracts);
    }

    private static string Json(Guid customerId, ContractPeriodFilter period, int page = 1, int pageSize = 25) =>
        JsonSerializer.Serialize(new ContractSearchViewModel(customerId, null, period, null, page, pageSize));

    private static string SetCookie(DefaultHttpContext http) => http.Response.Headers.SetCookie.ToString();

    [Fact]
    public async Task Index_WithExplicitCriteria_SavesTheSearchInASecureHttpOnlyCookie()
    {
        var (controller, http, _) = Create("?period=Historical&page=2");

        await controller.Index(CustomerId, null, ContractPeriodFilter.Historical, null, 2, 25);

        var cookie = SetCookie(http);
        Assert.Contains("ptl-contract-search=", cookie, StringComparison.Ordinal);
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Index_OverHttpsUnderAPathBase_SavesASecureCookieScopedToThePathBase()
    {
        var (controller, http, _) = Create("?period=All", https: true, pathBase: "/app");

        await controller.Index(CustomerId, null, ContractPeriodFilter.All, null, 1, 25);

        var cookie = SetCookie(http);
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/app", cookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Index_WithoutCriteria_RestoresTheSavedSearchAndDoesNotRewriteTheCookie()
    {
        var (controller, http, contracts) = Create(cookieJson: Json(CustomerId, ContractPeriodFilter.Historical, page: 3, pageSize: 50));

        await controller.Index(CustomerId, null);

        Assert.Equal(ContractPeriodFilter.Historical, contracts.LastSearchRequest!.Period);
        Assert.Equal(3, contracts.LastSearchRequest.Page);
        Assert.Equal(50, contracts.LastSearchRequest.PageSize);
        Assert.Empty(SetCookie(http));
    }

    [Theory]
    [InlineData("not-json")]
    [InlineData("null")]
    public async Task Index_WithAnUnreadableCookie_UsesTheDefaults(string cookieJson)
    {
        var (controller, _, contracts) = Create(cookieJson: cookieJson);

        await controller.Index(CustomerId, null);

        Assert.Equal(ContractPeriodFilter.CurrentAndNext, contracts.LastSearchRequest!.Period);
    }

    [Theory]
    [InlineData(99, 1, 25)]
    [InlineData(2, 0, 25)]
    [InlineData(2, 1, 0)]
    [InlineData(2, 1, 501)]
    public async Task Index_WithOutOfRangeSavedValues_UsesTheDefaults(int period, int page, int pageSize)
    {
        var json = Json(CustomerId, (ContractPeriodFilter)period, page, pageSize);
        var (controller, _, contracts) = Create(cookieJson: json);

        await controller.Index(CustomerId, null);

        Assert.Equal(ContractPeriodFilter.CurrentAndNext, contracts.LastSearchRequest!.Period);
        Assert.Equal(1, contracts.LastSearchRequest.Page);
    }

    [Fact]
    public async Task Index_WithASavedSearchForAnotherCustomer_UsesTheDefaults()
    {
        var (controller, _, contracts) = Create(cookieJson: Json(Guid.NewGuid(), ContractPeriodFilter.All));

        await controller.Index(CustomerId, null);

        Assert.Equal(ContractPeriodFilter.CurrentAndNext, contracts.LastSearchRequest!.Period);
    }

    [Fact]
    public async Task RenewContracts_Success_ForgetsTheSavedSearch()
    {
        var (controller, http, _) = Create(cookieJson: Json(CustomerId, ContractPeriodFilter.Historical));

        var result = await controller.RenewContracts(CustomerId, [Guid.NewGuid()], [Guid.NewGuid()], "Alice Example", CancellationToken.None);

        Assert.IsType<RedirectToActionResult>(result);
        var cookie = SetCookie(http);
        Assert.Contains("ptl-contract-search=;", cookie, StringComparison.Ordinal);
        Assert.Contains("expires=Thu, 01 Jan 1970", cookie, StringComparison.OrdinalIgnoreCase);
    }
}
