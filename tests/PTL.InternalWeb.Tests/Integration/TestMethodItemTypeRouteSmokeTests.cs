using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PTL.ApiClient;
using PTL.Contracts.Lookup;
using PTL.Contracts.TestMethodItemType;
using PTL.InternalWeb.Tests.TestSupport;

namespace PTL.InternalWeb.Tests.Integration;

// Full-pipeline smoke tests so the Test Method Items Razor view actually renders at least once
// through the real MVC pipeline - mirrors TestResultItemTypeRouteSmokeTests. Covers user story
// 744591's acceptance criteria end-to-end (search blank/filtered listing, Return Type saved,
// in-use edit/remove protection with a dependency message).
public partial class TestMethodItemTypeRouteSmokeTests : IClassFixture<WebApplicationFactory<Program>>
{
    [GeneratedRegex("__RequestVerificationToken[^>]*value=\"([^\"]+)\"", RegexOptions.None)]
    private static partial Regex AntiforgeryTokenPattern();

    private const int CurrentYearId = 2026;
    private const int NextYearId = 2027;
    private static readonly Guid ReturnTypeId = Guid.NewGuid();

    private readonly FakeTestMethodItemTypeApiClient _fakeApiClient = new();

    private WebApplicationFactory<Program> CreateFactory(WebApplicationFactory<Program> factory) =>
        factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ITestMethodItemTypeApiClient>();
                services.AddSingleton<ITestMethodItemTypeApiClient>(_fakeApiClient);
                services.RemoveAll<ILookupApiClient>();
                services.AddSingleton<ILookupApiClient>(new FakeLookupApiClient
                {
                    Years = [new YearResponse(CurrentYearId, "2026"), new YearResponse(NextYearId, "2027")],
                    TestReturnValueTypes = [new TestReturnValueTypeResponse(ReturnTypeId, "Numeric")]
                });
            }));

    public TestMethodItemTypeRouteSmokeTests(WebApplicationFactory<Program> factory) => _factory = CreateFactory(factory);

    private readonly WebApplicationFactory<Program> _factory;

    // AC: "Given a year is selected and the search field is left blank, When Search is selected,
    // Then all Test Method Items for that year are displayed."
    [Fact]
    public async Task TestMethodItemType_BlankSearch_ReturnsAllItems()
    {
        _fakeApiClient.Items =
        [
            new TestMethodItemTypeResponse(Guid.NewGuid(), Guid.NewGuid(), "Titration", ReturnTypeId, "Numeric", CurrentYearId, 9999, false, 0, string.Empty),
            new TestMethodItemTypeResponse(Guid.NewGuid(), Guid.NewGuid(), "Gravimetric", ReturnTypeId, "Numeric", CurrentYearId, 9999, false, 0, string.Empty)
        ];
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/SchemeAdmin/TestMethodItemType?yearId=" + CurrentYearId);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Titration", body, StringComparison.Ordinal);
        Assert.Contains("Gravimetric", body, StringComparison.Ordinal);
        Assert.Null(_fakeApiClient.LastSearch);
    }

    [Fact]
    public async Task TestMethodItemType_WithSearch_PassesSearchTermThroughAndPreservesItInTheBox()
    {
        _fakeApiClient.Items = [new TestMethodItemTypeResponse(Guid.NewGuid(), Guid.NewGuid(), "Titration", ReturnTypeId, "Numeric", CurrentYearId, 9999, false, 0, string.Empty)];
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/SchemeAdmin/TestMethodItemType?yearId={CurrentYearId}&search=titr");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("titr", _fakeApiClient.LastSearch);
        Assert.Contains("value=\"titr\"", body, StringComparison.Ordinal);
    }

    // AC: "Given the administrator creates a Test Method Item with a Return Type, When Save is
    // selected, Then the item is stored and available for assignment to tests."
    [Fact]
    public async Task TestMethodItemTypeAdd_WithReturnType_PersistsAndDisplaysReturnTypeName()
    {
        var client = _factory.CreateClient();
        var (token, cookie) = await GetAntiforgeryAsync(client, "/SchemeAdmin/TestMethodItemType?yearId=" + CurrentYearId);

        var request = new HttpRequestMessage(HttpMethod.Post, "/SchemeAdmin/TestMethodItemTypeAdd")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["yearId"] = CurrentYearId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["Add.Name"] = "Titration",
                ["Add.TestReturnValueTypeId"] = ReturnTypeId.ToString(),
                ["Add.NoLongerInUse"] = "false"
            })
        };
        request.Headers.Add("Cookie", cookie);

        _fakeApiClient.Items = [new TestMethodItemTypeResponse(Guid.NewGuid(), Guid.NewGuid(), "Titration", ReturnTypeId, "Numeric", CurrentYearId, 9999, false, 0, string.Empty)];

        var postResponse = await client.SendAsync(request);
        var body = await postResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, postResponse.StatusCode);
        Assert.Contains("Titration", body, StringComparison.Ordinal);
        Assert.Contains("Numeric", body, StringComparison.Ordinal);
        Assert.Equal(ReturnTypeId, _fakeApiClient.LastCreateRequest?.TestReturnValueTypeId);
    }

    // AC: "Given a method item is in use on a distribution, When edit is attempted, Then the
    // action is prevented and a message identifying the dependency is displayed."
    [Fact]
    public async Task TestMethodItemType_InUseOnDistribution_EditAttemptShowsDependencyMessage()
    {
        var itemId = Guid.NewGuid();
        _fakeApiClient.Items = [new TestMethodItemTypeResponse(itemId, Guid.NewGuid(), "Titration", ReturnTypeId, "Numeric", CurrentYearId, 9999, false, -1, "Scheme Alpha")];
        var client = _factory.CreateClient();

        var editAttemptResponse = await client.GetAsync($"/SchemeAdmin/TestMethodItemType?yearId={CurrentYearId}&editId={itemId}");
        var editAttemptBody = await editAttemptResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, editAttemptResponse.StatusCode);
        Assert.Contains("being used on a distribution", editAttemptBody, StringComparison.Ordinal);
        Assert.Contains("Scheme Alpha", editAttemptBody, StringComparison.Ordinal);
        Assert.DoesNotContain("Edit.Name", editAttemptBody, StringComparison.Ordinal);
    }

    // AC: "Given a method item is in use on a distribution, When removal is attempted, Then the
    // action is prevented and a message identifying the dependency is displayed."
    [Fact]
    public async Task TestMethodItemTypeRemove_InUse_BlockedWithDependencyMessage()
    {
        var itemId = Guid.NewGuid();
        _fakeApiClient.Items = [new TestMethodItemTypeResponse(itemId, Guid.NewGuid(), "Titration", ReturnTypeId, "Numeric", CurrentYearId, 9999, false, 2, "Scheme A, Scheme B")];
        _fakeApiClient.DeleteResponse = new TestMethodItemTypeDeleteResponse(false, "This Method Item is being used on 2 Scheme(s): Scheme A, Scheme B and cannot be removed.");
        var client = _factory.CreateClient();
        var (token, cookie) = await GetAntiforgeryAsync(client, "/SchemeAdmin/TestMethodItemType?yearId=" + CurrentYearId);

        var request = new HttpRequestMessage(HttpMethod.Post, "/SchemeAdmin/TestMethodItemTypeRemove")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["testMethodItemTypeId"] = itemId.ToString(),
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
