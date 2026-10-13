using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PTL.ApiClient;
using PTL.Contracts.CategoryItemType;
using PTL.Contracts.Lookup;
using PTL.InternalWeb.Tests.TestSupport;

namespace PTL.InternalWeb.Tests.Integration;

// Full-pipeline smoke tests so the Category Items Razor view actually renders at least once
// through the real MVC pipeline - mirrors SystemAdministrationRouteSmokeTests/SchemeRouteSmokeTests.
// Covers user story 744592's acceptance criteria end-to-end (year-scoped listing, create+refresh,
// in-use edit/remove protection with a dependency message).
public partial class CategoryItemTypeRouteSmokeTests : IClassFixture<WebApplicationFactory<Program>>
{
    [GeneratedRegex("__RequestVerificationToken[^>]*value=\"([^\"]+)\"", RegexOptions.None)]
    private static partial Regex AntiforgeryTokenPattern();

    private const int CurrentYearId = 2026;
    private const int NextYearId = 2027;

    private readonly FakeCategoryItemTypeApiClient _fakeApiClient = new();

    private WebApplicationFactory<Program> CreateFactory(WebApplicationFactory<Program> factory) =>
        factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ICategoryItemTypeApiClient>();
                services.AddSingleton<ICategoryItemTypeApiClient>(_fakeApiClient);
                services.RemoveAll<ILookupApiClient>();
                services.AddSingleton<ILookupApiClient>(new FakeLookupApiClient
                {
                    Years = [new YearResponse(CurrentYearId, "2026"), new YearResponse(NextYearId, "2027")]
                });
            }));

    public CategoryItemTypeRouteSmokeTests(WebApplicationFactory<Program> factory) => _factory = CreateFactory(factory);

    private readonly WebApplicationFactory<Program> _factory;

    // AC: "Given a year is selected, When the page refreshes, Then all Category Items for that
    // year are listed with their three columns."
    [Fact]
    public async Task CategoryItemType_ReturnsSuccessAndRendersThreeColumns()
    {
        _fakeApiClient.Items = [new CategoryItemTypeResponse(Guid.NewGuid(), "Colour", CurrentYearId, 9999, false, 0, string.Empty)];
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/SchemeAdmin/CategoryItemType?yearId=" + CurrentYearId);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Category Item Type Name", body, StringComparison.Ordinal);
        Assert.Contains("No Longer in Use", body, StringComparison.Ordinal);
        Assert.Contains("Actions", body, StringComparison.Ordinal);
        Assert.Contains("Colour", body, StringComparison.Ordinal);
    }

    // Matches legacy GridViewCategoryItemType.AllowPaging/PageSize = 15.
    [Fact]
    public async Task CategoryItemType_WithMoreThan15Items_RendersGovUkPagination()
    {
        _fakeApiClient.Items = Enumerable.Range(1, 16)
            .Select(i => new CategoryItemTypeResponse(Guid.NewGuid(), $"Item {i:D2}", CurrentYearId, 9999, false, 0, string.Empty))
            .ToList();
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/SchemeAdmin/CategoryItemType?yearId=" + CurrentYearId);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("govuk-pagination", body, StringComparison.Ordinal);
        Assert.Contains("Item 15", body, StringComparison.Ordinal);
        Assert.DoesNotContain("Item 16", body, StringComparison.Ordinal);
    }

    // AC: "Given the administrator creates a Category Item and saves, When the list refreshes,
    // Then the new item is displayed."
    [Fact]
    public async Task CategoryItemTypeAdd_ThenListRefresh_ShowsNewItem()
    {
        var client = _factory.CreateClient();
        var (token, cookie) = await GetAntiforgeryAsync(client, "/SchemeAdmin/CategoryItemType?yearId=" + CurrentYearId);

        var request = new HttpRequestMessage(HttpMethod.Post, "/SchemeAdmin/CategoryItemTypeAdd")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["yearId"] = CurrentYearId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["Add.Name"] = "Appearance",
                ["Add.NoLongerInUse"] = "false"
            })
        };
        request.Headers.Add("Cookie", cookie);

        // The fake API client's CreateAsync doesn't actually mutate Items, so the subsequent
        // redirect-and-refresh (the client auto-follows the 302 to CategoryItemType) is primed
        // with the post-create state up front - the real API would return the newly created row
        // on the next GetByYearAsync call.
        _fakeApiClient.Items = [new CategoryItemTypeResponse(Guid.NewGuid(), "Appearance", CurrentYearId, 9999, false, 0, string.Empty)];

        var postResponse = await client.SendAsync(request);
        var body = await postResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, postResponse.StatusCode);
        Assert.Contains("Appearance", body, StringComparison.Ordinal);
        Assert.Equal("Appearance", _fakeApiClient.LastCreateRequest?.Name);
    }

    // AC: "Given a Category Item is in use on a distribution, When edit is attempted, Then the
    // action is prevented with a message identifying the dependency." Edit/Remove are always
    // shown (matching legacy, which always shows the links too, then alert()s on click) - the
    // dependency is shown as a standing hint, and attempting edit is still refused server-side.
    [Fact]
    public async Task CategoryItemType_InUseOnDistribution_EditLinkStillShownButEditAttemptShowsDependencyMessage()
    {
        var itemId = Guid.NewGuid();
        _fakeApiClient.Items = [new CategoryItemTypeResponse(itemId, "Colour", CurrentYearId, 9999, false, -1, "Scheme Alpha")];
        var client = _factory.CreateClient();

        var listResponse = await client.GetAsync("/SchemeAdmin/CategoryItemType?yearId=" + CurrentYearId);
        var listBody = await listResponse.Content.ReadAsStringAsync();
        Assert.Contains($"editId={itemId}", listBody, StringComparison.Ordinal);
        Assert.Contains("Scheme Alpha", listBody, StringComparison.Ordinal);

        var editAttemptResponse = await client.GetAsync($"/SchemeAdmin/CategoryItemType?yearId={CurrentYearId}&editId={itemId}");
        var editAttemptBody = await editAttemptResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, editAttemptResponse.StatusCode);
        Assert.Contains("being used on a distribution", editAttemptBody, StringComparison.Ordinal);
        Assert.Contains("Scheme Alpha", editAttemptBody, StringComparison.Ordinal);
        Assert.DoesNotContain("Edit.Name", editAttemptBody, StringComparison.Ordinal);
    }

    // AC: "Given a Category Item is in use on a distribution, When removal is attempted, Then the
    // action is prevented with a message identifying the dependency."
    [Fact]
    public async Task CategoryItemTypeRemove_InUseOnDistribution_BlockedWithDependencyMessage()
    {
        var itemId = Guid.NewGuid();
        _fakeApiClient.Items = [new CategoryItemTypeResponse(itemId, "Colour", CurrentYearId, 9999, false, 2, "Scheme A, Scheme B")];
        _fakeApiClient.DeleteResponse = new CategoryItemTypeDeleteResponse(false, "This Category Item is being used on 2 Scheme(s): Scheme A, Scheme B and cannot be removed.");
        var client = _factory.CreateClient();
        var (token, cookie) = await GetAntiforgeryAsync(client, "/SchemeAdmin/CategoryItemType?yearId=" + CurrentYearId);

        var request = new HttpRequestMessage(HttpMethod.Post, "/SchemeAdmin/CategoryItemTypeRemove")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["categoryItemTypeId"] = itemId.ToString(),
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
    public async Task CategoryItemTypeSaveAllNoLongerInUse_TicksOneRowAndUnticksAnother_BothPersistInOneSubmit()
    {
        var tickedId = Guid.NewGuid();
        var untickedId = Guid.NewGuid();
        _fakeApiClient.Items =
        [
            new CategoryItemTypeResponse(tickedId, "Colour", CurrentYearId, 9999, false, 0, string.Empty),
            new CategoryItemTypeResponse(untickedId, "Odour", CurrentYearId, 9999, true, 0, string.Empty)
        ];
        var client = _factory.CreateClient();
        var (token, cookie) = await GetAntiforgeryAsync(client, "/SchemeAdmin/CategoryItemType?yearId=" + CurrentYearId);

        var request = new HttpRequestMessage(HttpMethod.Post, "/SchemeAdmin/CategoryItemTypeSaveAllNoLongerInUse")
        {
            Content = new FormUrlEncodedContent(
            [
                new KeyValuePair<string, string>("__RequestVerificationToken", token),
                new KeyValuePair<string, string>("yearId", CurrentYearId.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                new KeyValuePair<string, string>("page", "1"),
                new KeyValuePair<string, string>("pageCategoryItemTypeIds", tickedId.ToString()),
                new KeyValuePair<string, string>("pageCategoryItemTypeIds", untickedId.ToString()),
                new KeyValuePair<string, string>("checkedCategoryItemTypeIds", tickedId.ToString())
            ])
        };
        request.Headers.Add("Cookie", cookie);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, _fakeApiClient.AllNoLongerInUseRequests.Count);
        Assert.True(_fakeApiClient.AllNoLongerInUseRequests.Single(r => r.CategoryItemTypeId == tickedId).Request.NoLongerInUse);
        Assert.False(_fakeApiClient.AllNoLongerInUseRequests.Single(r => r.CategoryItemTypeId == untickedId).Request.NoLongerInUse);
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
