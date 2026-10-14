using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PTL.ApiClient;
using PTL.Contracts.Lookup;
using PTL.Contracts.TestType;
using PTL.InternalWeb.Tests.TestSupport;

namespace PTL.InternalWeb.Tests.Integration;

// Full-pipeline smoke tests so the Test Types Razor view actually renders at least once through
// the real MVC pipeline - mirrors CategoryItemTypeRouteSmokeTests. Covers user story 744589's
// acceptance criteria end-to-end (year-scoped listing, create+refresh, in-use edit/remove
// protection with a dependency message).
public partial class TestTypeRouteSmokeTests : IClassFixture<WebApplicationFactory<Program>>
{
    [GeneratedRegex("__RequestVerificationToken[^>]*value=\"([^\"]+)\"", RegexOptions.None)]
    private static partial Regex AntiforgeryTokenPattern();

    private const int CurrentYearId = 2026;
    private const int NextYearId = 2027;

    private readonly FakeTestTypeApiClient _fakeApiClient = new();

    private WebApplicationFactory<Program> CreateFactory(WebApplicationFactory<Program> factory) =>
        factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ITestTypeApiClient>();
                services.AddSingleton<ITestTypeApiClient>(_fakeApiClient);
                services.RemoveAll<ILookupApiClient>();
                services.AddSingleton<ILookupApiClient>(new FakeLookupApiClient
                {
                    Years = [new YearResponse(CurrentYearId, "2026"), new YearResponse(NextYearId, "2027")]
                });
            }));

    public TestTypeRouteSmokeTests(WebApplicationFactory<Program> factory) => _factory = CreateFactory(factory);

    private readonly WebApplicationFactory<Program> _factory;

    // AC: "Given a year is selected from the Year dropdown, When the page refreshes, Then the test
    // types for that year are listed."
    [Fact]
    public async Task TestType_ReturnsSuccessAndRendersThreeColumns()
    {
        _fakeApiClient.Items = [new TestTypeResponse(Guid.NewGuid(), Guid.NewGuid(), "Chemical", CurrentYearId, 9999, false, 0, string.Empty)];
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/SchemeAdmin/TestType?yearId=" + CurrentYearId);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Test Type Name", body, StringComparison.Ordinal);
        Assert.Contains("No Longer in Use", body, StringComparison.Ordinal);
        Assert.Contains("Actions", body, StringComparison.Ordinal);
        Assert.Contains("Chemical", body, StringComparison.Ordinal);
    }

    // Matches legacy GridViewTestType.AllowPaging/PageSize = 15.
    [Fact]
    public async Task TestType_WithMoreThan15Items_RendersGovUkPagination()
    {
        _fakeApiClient.Items = Enumerable.Range(1, 16)
            .Select(i => new TestTypeResponse(Guid.NewGuid(), Guid.NewGuid(), $"Item {i:D2}", CurrentYearId, 9999, false, 0, string.Empty))
            .ToList();
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/SchemeAdmin/TestType?yearId=" + CurrentYearId);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("govuk-pagination", body, StringComparison.Ordinal);
        Assert.Contains("Item 15", body, StringComparison.Ordinal);
        Assert.DoesNotContain("Item 16", body, StringComparison.Ordinal);
    }

    // AC: "Given the administrator creates a new test type and saves, When the list refreshes,
    // Then the new test type is displayed."
    [Fact]
    public async Task TestTypeAdd_ThenListRefresh_ShowsNewItem()
    {
        var client = _factory.CreateClient();
        var (token, cookie) = await GetAntiforgeryAsync(client, "/SchemeAdmin/TestType?yearId=" + CurrentYearId);

        var request = new HttpRequestMessage(HttpMethod.Post, "/SchemeAdmin/TestTypeAdd")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["yearId"] = CurrentYearId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["Add.Name"] = "Microbiological",
                ["Add.NoLongerInUse"] = "false"
            })
        };
        request.Headers.Add("Cookie", cookie);

        // The fake API client's CreateAsync doesn't actually mutate Items, so the subsequent
        // redirect-and-refresh (the client auto-follows the 302 to TestType) is primed with the
        // post-create state up front - the real API would return the newly created row on the
        // next GetByYearAsync call.
        _fakeApiClient.Items = [new TestTypeResponse(Guid.NewGuid(), Guid.NewGuid(), "Microbiological", CurrentYearId, 9999, false, 0, string.Empty)];

        var postResponse = await client.SendAsync(request);
        var body = await postResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, postResponse.StatusCode);
        Assert.Contains("Microbiological", body, StringComparison.Ordinal);
        Assert.Equal("Microbiological", _fakeApiClient.LastCreateRequest?.Name);
    }

    // AC: "Given a test type is in use by one or more schemes, When the administrator attempts to
    // edit it, Then the edit is prevented and a message explaining the scheme dependency is
    // displayed." Edit/Remove are always shown - the dependency is collapsed in a govuk-details
    // disclosure, and attempting edit is still refused server-side.
    [Fact]
    public async Task TestType_InUseOnDistribution_EditLinkStillShownButEditAttemptShowsDependencyMessage()
    {
        var itemId = Guid.NewGuid();
        _fakeApiClient.Items = [new TestTypeResponse(itemId, Guid.NewGuid(), "Chemical", CurrentYearId, 9999, false, -1, "Scheme Alpha")];
        var client = _factory.CreateClient();

        var listResponse = await client.GetAsync("/SchemeAdmin/TestType?yearId=" + CurrentYearId);
        var listBody = await listResponse.Content.ReadAsStringAsync();
        Assert.Contains($"editId={itemId}", listBody, StringComparison.Ordinal);
        Assert.Contains("Scheme Alpha", listBody, StringComparison.Ordinal);

        var editAttemptResponse = await client.GetAsync($"/SchemeAdmin/TestType?yearId={CurrentYearId}&editId={itemId}");
        var editAttemptBody = await editAttemptResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, editAttemptResponse.StatusCode);
        Assert.Contains("being used on a distribution", editAttemptBody, StringComparison.Ordinal);
        Assert.Contains("Scheme Alpha", editAttemptBody, StringComparison.Ordinal);
        Assert.DoesNotContain("Edit.Name", editAttemptBody, StringComparison.Ordinal);
    }

    // AC: "Given a test type is in use by one or more schemes, When the administrator attempts to
    // remove it, Then the removal is prevented and a message explaining the scheme dependency is
    // displayed."
    [Fact]
    public async Task TestTypeRemove_InUseOnDistribution_BlockedWithDependencyMessage()
    {
        var itemId = Guid.NewGuid();
        _fakeApiClient.Items = [new TestTypeResponse(itemId, Guid.NewGuid(), "Chemical", CurrentYearId, 9999, false, 2, "Scheme A, Scheme B")];
        _fakeApiClient.DeleteResponse = new TestTypeDeleteResponse(false, "This Test is being used on 2 Scheme(s): Scheme A, Scheme B and cannot be removed.");
        var client = _factory.CreateClient();
        var (token, cookie) = await GetAntiforgeryAsync(client, "/SchemeAdmin/TestType?yearId=" + CurrentYearId);

        var request = new HttpRequestMessage(HttpMethod.Post, "/SchemeAdmin/TestTypeRemove")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["testTypeId"] = itemId.ToString(),
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
    public async Task TestTypeSaveAllNoLongerInUse_TicksOneRowAndUnticksAnother_BothPersistInOneSubmit()
    {
        var tickedId = Guid.NewGuid();
        var untickedId = Guid.NewGuid();
        _fakeApiClient.Items =
        [
            new TestTypeResponse(tickedId, Guid.NewGuid(), "Chemical", CurrentYearId, 9999, false, 0, string.Empty),
            new TestTypeResponse(untickedId, Guid.NewGuid(), "Physical", CurrentYearId, 9999, true, 0, string.Empty)
        ];
        var client = _factory.CreateClient();
        var (token, cookie) = await GetAntiforgeryAsync(client, "/SchemeAdmin/TestType?yearId=" + CurrentYearId);

        var request = new HttpRequestMessage(HttpMethod.Post, "/SchemeAdmin/TestTypeSaveAllNoLongerInUse")
        {
            Content = new FormUrlEncodedContent(
            [
                new KeyValuePair<string, string>("__RequestVerificationToken", token),
                new KeyValuePair<string, string>("yearId", CurrentYearId.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                new KeyValuePair<string, string>("page", "1"),
                new KeyValuePair<string, string>("pageTestTypeIds", tickedId.ToString()),
                new KeyValuePair<string, string>("pageTestTypeIds", untickedId.ToString()),
                new KeyValuePair<string, string>("checkedTestTypeIds", tickedId.ToString())
            ])
        };
        request.Headers.Add("Cookie", cookie);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, _fakeApiClient.AllNoLongerInUseRequests.Count);
        Assert.True(_fakeApiClient.AllNoLongerInUseRequests.Single(r => r.TestTypeId == tickedId).Request.NoLongerInUse);
        Assert.False(_fakeApiClient.AllNoLongerInUseRequests.Single(r => r.TestTypeId == untickedId).Request.NoLongerInUse);
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
