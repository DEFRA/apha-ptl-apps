using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PTL.ApiClient;
using PTL.Contracts.CriterionItemType;
using PTL.Contracts.Lookup;
using PTL.InternalWeb.Tests.TestSupport;

namespace PTL.InternalWeb.Tests.Integration;

// Full-pipeline smoke tests so the Criterion Items Razor view actually renders at least once
// through the real MVC pipeline - mirrors TestTypeRouteSmokeTests. Covers user story 744593's
// acceptance criteria end-to-end (year-scoped listing, create+refresh, in-use edit/remove
// protection with a dependency message).
public partial class CriterionItemTypeRouteSmokeTests : IClassFixture<WebApplicationFactory<Program>>
{
    [GeneratedRegex("__RequestVerificationToken[^>]*value=\"([^\"]+)\"", RegexOptions.None)]
    private static partial Regex AntiforgeryTokenPattern();

    private const int CurrentYearId = 2026;
    private const int NextYearId = 2027;

    private readonly FakeCriterionItemTypeApiClient _fakeApiClient = new();

    private WebApplicationFactory<Program> CreateFactory(WebApplicationFactory<Program> factory) =>
        factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ICriterionItemTypeApiClient>();
                services.AddSingleton<ICriterionItemTypeApiClient>(_fakeApiClient);
                services.RemoveAll<ILookupApiClient>();
                services.AddSingleton<ILookupApiClient>(new FakeLookupApiClient
                {
                    Years = [new YearResponse(CurrentYearId, "2026"), new YearResponse(NextYearId, "2027")]
                });
            }));

    public CriterionItemTypeRouteSmokeTests(WebApplicationFactory<Program> factory) => _factory = CreateFactory(factory);

    private readonly WebApplicationFactory<Program> _factory;

    // AC: "Given a year is selected, When the page refreshes, Then all Criterion Items for that
    // year are listed with their three columns."
    [Fact]
    public async Task CriterionItemType_ReturnsSuccessAndRendersThreeColumns()
    {
        _fakeApiClient.Items = [new CriterionItemTypeResponse(Guid.NewGuid(), "Appearance", CurrentYearId, 9999, false, 0, string.Empty)];
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/SchemeAdmin/CriterionItemType?yearId=" + CurrentYearId);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Criterion item type name", body, StringComparison.Ordinal);
        Assert.Contains("No longer in use", body, StringComparison.Ordinal);
        Assert.Contains("Actions", body, StringComparison.Ordinal);
        Assert.Contains("Appearance", body, StringComparison.Ordinal);
    }

    // Matches legacy GridViewCriterionItemType.AllowPaging/PageSize = 15.
    [Fact]
    public async Task CriterionItemType_WithMoreThan15Items_RendersGovUkPagination()
    {
        _fakeApiClient.Items = Enumerable.Range(1, 16)
            .Select(i => new CriterionItemTypeResponse(Guid.NewGuid(), $"Item {i:D2}", CurrentYearId, 9999, false, 0, string.Empty))
            .ToList();
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/SchemeAdmin/CriterionItemType?yearId=" + CurrentYearId);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("govuk-pagination", body, StringComparison.Ordinal);
        Assert.Contains("Item 15", body, StringComparison.Ordinal);
        Assert.DoesNotContain("Item 16", body, StringComparison.Ordinal);
    }

    // AC: "Given the administrator creates a Criterion Item and saves, When the list refreshes,
    // Then the new item is displayed."
    [Fact]
    public async Task CriterionItemTypeAdd_ThenListRefresh_ShowsNewItem()
    {
        var client = _factory.CreateClient();
        var (token, cookie) = await GetAntiforgeryAsync(client, "/SchemeAdmin/CriterionItemType?yearId=" + CurrentYearId);

        var request = new HttpRequestMessage(HttpMethod.Post, "/SchemeAdmin/CriterionItemTypeAdd")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["yearId"] = CurrentYearId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["Add.Name"] = "Odour",
                ["Add.NoLongerInUse"] = "false"
            })
        };
        request.Headers.Add("Cookie", cookie);

        // The fake API client's CreateAsync doesn't actually mutate Items, so the subsequent
        // redirect-and-refresh (the client auto-follows the 302 to CriterionItemType) is primed
        // with the post-create state up front - the real API would return the newly created row
        // on the next GetByYearAsync call.
        _fakeApiClient.Items = [new CriterionItemTypeResponse(Guid.NewGuid(), "Odour", CurrentYearId, 9999, false, 0, string.Empty)];

        var postResponse = await client.SendAsync(request);
        var body = await postResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, postResponse.StatusCode);
        Assert.Contains("Odour", body, StringComparison.Ordinal);
        Assert.Equal("Odour", _fakeApiClient.LastCreateRequest?.Name);
    }

    // AC: "Given a Criterion Item is in use on a distribution, When edit is attempted, Then the
    // action is prevented with a message identifying the dependency." Edit/Remove are always
    // shown - the dependency is collapsed in a govuk-details disclosure, and attempting edit is
    // still refused server-side.
    [Fact]
    public async Task CriterionItemType_InUseOnDistribution_EditLinkStillShownButEditAttemptShowsDependencyMessage()
    {
        var itemId = Guid.NewGuid();
        _fakeApiClient.Items = [new CriterionItemTypeResponse(itemId, "Appearance", CurrentYearId, 9999, false, -1, "Scheme Alpha")];
        var client = _factory.CreateClient();

        var listResponse = await client.GetAsync("/SchemeAdmin/CriterionItemType?yearId=" + CurrentYearId);
        var listBody = await listResponse.Content.ReadAsStringAsync();
        Assert.Contains($"editId={itemId}", listBody, StringComparison.Ordinal);
        Assert.Contains("Scheme Alpha", listBody, StringComparison.Ordinal);

        var editAttemptResponse = await client.GetAsync($"/SchemeAdmin/CriterionItemType?yearId={CurrentYearId}&editId={itemId}");
        var editAttemptBody = await editAttemptResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, editAttemptResponse.StatusCode);
        Assert.Contains("being used on a distribution", editAttemptBody, StringComparison.Ordinal);
        Assert.Contains("Scheme Alpha", editAttemptBody, StringComparison.Ordinal);
        Assert.DoesNotContain("Edit.Name", editAttemptBody, StringComparison.Ordinal);
    }

    // AC: "Given a Criterion Item is in use on a distribution, When removal is attempted, Then the
    // action is prevented with a message identifying the dependency."
    [Fact]
    public async Task CriterionItemTypeRemove_InUseOnDistribution_BlockedWithDependencyMessage()
    {
        var itemId = Guid.NewGuid();
        _fakeApiClient.Items = [new CriterionItemTypeResponse(itemId, "Appearance", CurrentYearId, 9999, false, 2, "Scheme A, Scheme B")];
        _fakeApiClient.DeleteResponse = new CriterionItemTypeDeleteResponse(false, "This Criterion Item is being used on 2 Scheme(s): Scheme A, Scheme B and cannot be removed.");
        var client = _factory.CreateClient();
        var (token, cookie) = await GetAntiforgeryAsync(client, "/SchemeAdmin/CriterionItemType?yearId=" + CurrentYearId);

        var request = new HttpRequestMessage(HttpMethod.Post, "/SchemeAdmin/CriterionItemTypeRemove")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["criterionItemTypeId"] = itemId.ToString(),
                ["yearId"] = CurrentYearId.ToString(System.Globalization.CultureInfo.InvariantCulture)
            })
        };
        request.Headers.Add("Cookie", cookie);

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Scheme A, Scheme B", body, StringComparison.Ordinal);
        Assert.Contains("govuk-notification-banner", body, StringComparison.Ordinal);
    }

    // Covers updating several rows' "No Longer in Use" flag in a single submit (one ticked, one
    // unticked) - the bulk form, not a per-row request.
    [Fact]
    public async Task CriterionItemTypeSaveAllNoLongerInUse_TicksOneRowAndUnticksAnother_BothPersistInOneSubmit()
    {
        var tickedId = Guid.NewGuid();
        var untickedId = Guid.NewGuid();
        _fakeApiClient.Items =
        [
            new CriterionItemTypeResponse(tickedId, "Appearance", CurrentYearId, 9999, false, 0, string.Empty),
            new CriterionItemTypeResponse(untickedId, "Odour", CurrentYearId, 9999, true, 0, string.Empty)
        ];
        var client = _factory.CreateClient();
        var (token, cookie) = await GetAntiforgeryAsync(client, "/SchemeAdmin/CriterionItemType?yearId=" + CurrentYearId);

        var request = new HttpRequestMessage(HttpMethod.Post, "/SchemeAdmin/CriterionItemTypeSaveAllNoLongerInUse")
        {
            Content = new FormUrlEncodedContent(
            [
                new KeyValuePair<string, string>("__RequestVerificationToken", token),
                new KeyValuePair<string, string>("yearId", CurrentYearId.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                new KeyValuePair<string, string>("page", "1"),
                new KeyValuePair<string, string>("pageCriterionItemTypeIds", tickedId.ToString()),
                new KeyValuePair<string, string>("pageCriterionItemTypeIds", untickedId.ToString()),
                new KeyValuePair<string, string>("checkedCriterionItemTypeIds", tickedId.ToString())
            ])
        };
        request.Headers.Add("Cookie", cookie);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, _fakeApiClient.AllNoLongerInUseRequests.Count);
        Assert.True(_fakeApiClient.AllNoLongerInUseRequests.Single(r => r.CriterionItemTypeId == tickedId).Request.NoLongerInUse);
        Assert.False(_fakeApiClient.AllNoLongerInUseRequests.Single(r => r.CriterionItemTypeId == untickedId).Request.NoLongerInUse);
    }

    // Locks in the error-summary anchor fix - see CategoryItemTypeSave_MissingName_... remarks.
    [Fact]
    public async Task CriterionItemTypeSave_MissingName_RendersErrorSummaryWithMatchingAnchor()
    {
        var itemId = Guid.NewGuid();
        _fakeApiClient.Items = [new CriterionItemTypeResponse(itemId, "Appearance", CurrentYearId, 9999, false, 0, string.Empty)];
        var client = _factory.CreateClient();
        var (token, cookie) = await GetAntiforgeryAsync(client, "/SchemeAdmin/CriterionItemType?yearId=" + CurrentYearId);

        var request = new HttpRequestMessage(HttpMethod.Post, "/SchemeAdmin/CriterionItemTypeSave")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["yearId"] = CurrentYearId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["page"] = "1",
                ["Edit.CriterionItemTypeId"] = itemId.ToString(),
                ["Edit.Name"] = string.Empty
            })
        };
        request.Headers.Add("Cookie", cookie);

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("govuk-error-summary", body, StringComparison.Ordinal);
        Assert.Contains("href=\"#Edit_Name\"", body, StringComparison.Ordinal);
        Assert.Contains("id=\"Edit_Name\"", body, StringComparison.Ordinal);
    }

    private static async Task<(string Token, string Cookie)> GetAntiforgeryAsync(HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        var body = await response.Content.ReadAsStringAsync();
        var token = AntiforgeryTokenPattern().Match(body).Groups[1].Value;
        var cookie = string.Join("; ", response.Headers.TryGetValues("Set-Cookie", out var cookies)
            ? cookies.Select(c => c.Split(';')[0])
            : []);
        return (token, cookie);
    }
}
