using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PTL.ApiClient;
using PTL.Contracts.Lookup;
using PTL.Contracts.TestResultItemType;
using PTL.InternalWeb.Tests.TestSupport;

namespace PTL.InternalWeb.Tests.Integration;

// Full-pipeline smoke tests so the Test Result Items Razor view actually renders at least once
// through the real MVC pipeline - mirrors TestTypeRouteSmokeTests. Covers user story 744590's
// acceptance criteria end-to-end (search blank/filtered listing, Return Type saved, in-use
// edit/remove protection with a dependency message).
public partial class TestResultItemTypeRouteSmokeTests : IClassFixture<WebApplicationFactory<Program>>
{
    [GeneratedRegex("__RequestVerificationToken[^>]*value=\"([^\"]+)\"", RegexOptions.None)]
    private static partial Regex AntiforgeryTokenPattern();

    private const int CurrentYearId = 2026;
    private const int NextYearId = 2027;
    private static readonly Guid ReturnTypeId = Guid.NewGuid();

    private readonly FakeTestResultItemTypeApiClient _fakeApiClient = new();

    private WebApplicationFactory<Program> CreateFactory(WebApplicationFactory<Program> factory) =>
        factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ITestResultItemTypeApiClient>();
                services.AddSingleton<ITestResultItemTypeApiClient>(_fakeApiClient);
                services.RemoveAll<ILookupApiClient>();
                services.AddSingleton<ILookupApiClient>(new FakeLookupApiClient
                {
                    Years = [new YearResponse(CurrentYearId, "2026"), new YearResponse(NextYearId, "2027")],
                    TestReturnValueTypes = [new TestReturnValueTypeResponse(ReturnTypeId, "Numeric")]
                });
            }));

    public TestResultItemTypeRouteSmokeTests(WebApplicationFactory<Program> factory) => _factory = CreateFactory(factory);

    private readonly WebApplicationFactory<Program> _factory;

    // AC: "Given a year is selected and the search field is left blank, When Search is selected,
    // Then all Test Result Items for that year are displayed."
    [Fact]
    public async Task TestResultItemType_BlankSearch_ReturnsAllItems()
    {
        _fakeApiClient.Items =
        [
            new TestResultItemTypeResponse(Guid.NewGuid(), Guid.NewGuid(), "Weight", ReturnTypeId, "Numeric", CurrentYearId, 9999, false, 0, string.Empty),
            new TestResultItemTypeResponse(Guid.NewGuid(), Guid.NewGuid(), "Length", ReturnTypeId, "Numeric", CurrentYearId, 9999, false, 0, string.Empty)
        ];
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/SchemeAdmin/TestResultItemType?yearId=" + CurrentYearId);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Weight", body, StringComparison.Ordinal);
        Assert.Contains("Length", body, StringComparison.Ordinal);
        Assert.Null(_fakeApiClient.LastSearch);
    }

    // AC: "Given a name is entered, When Search is selected, Then only matching Test Result Items
    // are returned."
    [Fact]
    public async Task TestResultItemType_WithSearch_PassesSearchTermThroughAndPreservesItInTheBox()
    {
        _fakeApiClient.Items = [new TestResultItemTypeResponse(Guid.NewGuid(), Guid.NewGuid(), "Weight", ReturnTypeId, "Numeric", CurrentYearId, 9999, false, 0, string.Empty)];
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/SchemeAdmin/TestResultItemType?yearId={CurrentYearId}&search=weig");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("weig", _fakeApiClient.LastSearch);
        Assert.Contains("value=\"weig\"", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TestResultItemType_BlankSearch_DoesNotRenderClearSearchLink()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/SchemeAdmin/TestResultItemType?yearId=" + CurrentYearId);
        var body = await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain("Clear search", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TestResultItemType_WithSearch_RendersClearSearchLinkThatReturnsToTheUnfilteredList()
    {
        _fakeApiClient.Items = [new TestResultItemTypeResponse(Guid.NewGuid(), Guid.NewGuid(), "Weight", ReturnTypeId, "Numeric", CurrentYearId, 9999, false, 0, string.Empty)];
        var client = _factory.CreateClient();

        var searchResponse = await client.GetAsync($"/SchemeAdmin/TestResultItemType?yearId={CurrentYearId}&search=weig");
        var searchBody = await searchResponse.Content.ReadAsStringAsync();
        Assert.Contains($"href=\"/SchemeAdmin/TestResultItemType?yearId={CurrentYearId}\"", searchBody, StringComparison.Ordinal);

        var clearResponse = await client.GetAsync($"/SchemeAdmin/TestResultItemType?yearId={CurrentYearId}");

        Assert.Equal(HttpStatusCode.OK, clearResponse.StatusCode);
        Assert.Null(_fakeApiClient.LastSearch);
    }

    // AC: "Given the administrator creates or edits an item, When a Return Type is selected and
    // saved, Then the item is stored with that Return Type."
    [Fact]
    public async Task TestResultItemTypeAdd_WithReturnType_PersistsAndDisplaysReturnTypeName()
    {
        var client = _factory.CreateClient();
        var (token, cookie) = await GetAntiforgeryAsync(client, "/SchemeAdmin/TestResultItemType?yearId=" + CurrentYearId);

        var request = new HttpRequestMessage(HttpMethod.Post, "/SchemeAdmin/TestResultItemTypeAdd")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["yearId"] = CurrentYearId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["Add.Name"] = "Weight",
                ["Add.TestReturnValueTypeId"] = ReturnTypeId.ToString(),
                ["Add.NoLongerInUse"] = "false"
            })
        };
        request.Headers.Add("Cookie", cookie);

        _fakeApiClient.Items = [new TestResultItemTypeResponse(Guid.NewGuid(), Guid.NewGuid(), "Weight", ReturnTypeId, "Numeric", CurrentYearId, 9999, false, 0, string.Empty)];

        var postResponse = await client.SendAsync(request);
        var body = await postResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, postResponse.StatusCode);
        Assert.Contains("Weight", body, StringComparison.Ordinal);
        Assert.Contains("Numeric", body, StringComparison.Ordinal);
        Assert.Equal(ReturnTypeId, _fakeApiClient.LastCreateRequest?.TestReturnValueTypeId);
    }

    // AC: "Given an item is in use by one or more schemes, When the administrator attempts to
    // edit or remove it, Then the action is prevented and a message explaining the scheme
    // dependency is displayed."
    [Fact]
    public async Task TestResultItemType_InUseOnDistribution_EditAttemptShowsDependencyMessage()
    {
        var itemId = Guid.NewGuid();
        _fakeApiClient.Items = [new TestResultItemTypeResponse(itemId, Guid.NewGuid(), "Weight", ReturnTypeId, "Numeric", CurrentYearId, 9999, false, -1, "Scheme Alpha")];
        var client = _factory.CreateClient();

        var editAttemptResponse = await client.GetAsync($"/SchemeAdmin/TestResultItemType?yearId={CurrentYearId}&editId={itemId}");
        var editAttemptBody = await editAttemptResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, editAttemptResponse.StatusCode);
        Assert.Contains("being used on a distribution", editAttemptBody, StringComparison.Ordinal);
        Assert.Contains("Scheme Alpha", editAttemptBody, StringComparison.Ordinal);
        Assert.DoesNotContain("Edit.Name", editAttemptBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TestResultItemTypeRemove_InUse_BlockedWithDependencyMessage()
    {
        var itemId = Guid.NewGuid();
        _fakeApiClient.Items = [new TestResultItemTypeResponse(itemId, Guid.NewGuid(), "Weight", ReturnTypeId, "Numeric", CurrentYearId, 9999, false, 2, "Scheme A, Scheme B")];
        _fakeApiClient.DeleteResponse = new TestResultItemTypeDeleteResponse(false, "This Test Result Item is being used on 2 Scheme(s): Scheme A, Scheme B and cannot be removed.");
        var client = _factory.CreateClient();
        var (token, cookie) = await GetAntiforgeryAsync(client, "/SchemeAdmin/TestResultItemType?yearId=" + CurrentYearId);

        var request = new HttpRequestMessage(HttpMethod.Post, "/SchemeAdmin/TestResultItemTypeRemove")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["testResultItemTypeId"] = itemId.ToString(),
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
