using System.Net;
using System.Text;
using PTL.Common.Graph;
using PTL.InternalWeb.Infrastructure;

namespace PTL.InternalWeb.Tests.Infrastructure;

public class GraphStaffDirectoryServiceTests
{
    private sealed class RecordingHandler(HttpStatusCode status, params string[] pages) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var json = pages[Math.Min(Requests.Count, pages.Length - 1)];
            Requests.Add(request);
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") });
        }
    }

    private sealed class Factory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public string? LastName { get; private set; }

        public HttpClient CreateClient(string name)
        {
            LastName = name;
            return new HttpClient(handler, disposeHandler: false) { BaseAddress = new Uri("https://graph.example.test/v1.0/") };
        }
    }

    private static (GraphStaffDirectoryService Service, RecordingHandler Handler, Factory Factory) Create(string json = """{"value":[]}""", HttpStatusCode status = HttpStatusCode.OK) =>
        Create(new RecordingHandler(status, json));

    // Serves each page in turn; the last page is repeated if more requests arrive.
    private static (GraphStaffDirectoryService Service, RecordingHandler Handler, Factory Factory) CreatePaged(params string[] pages) =>
        Create(new RecordingHandler(HttpStatusCode.OK, pages));

    private static (GraphStaffDirectoryService Service, RecordingHandler Handler, Factory Factory) Create(RecordingHandler handler)
    {
        var factory = new Factory(handler);
        return (new GraphStaffDirectoryService(factory), handler, factory);
    }

    private static string UserPage(int first, int count, string? nextLink = null)
    {
        var users = string.Join(",", Enumerable.Range(first, count).Select(i => $$"""{"onPremisesSamAccountName":"u{{i:D3}}","onPremisesDomainName":"dt2","displayName":"U{{i}}"}"""));
        var next = nextLink is null ? string.Empty : $$""","@odata.nextLink":"{{nextLink}}" """;
        return $$"""{"value":[{{users}}]{{next}}}""";
    }

    private static string DecodedFilter(HttpRequestMessage request) =>
        Uri.UnescapeDataString(request.RequestUri!.Query.Split('&').Single(p => p.StartsWith("$filter=", StringComparison.Ordinal))[8..]);

    [Fact]
    public async Task SearchAsync_BlankSearch_ReturnsNothingWithoutCallingGraph()
    {
        var (service, handler, _) = Create();

        var results = await service.SearchAsync("  \\  ");

        Assert.Empty(results.Users);
        Assert.False(results.IsTruncated);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task SearchAsync_UsesTheNamedGraphClientAndSelectsTheNeededFields()
    {
        var (service, handler, factory) = Create();

        await service.SearchAsync("smith");

        Assert.Equal("Graph", factory.LastName);
        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.StartsWith("https://graph.example.test/v1.0/users?", request.RequestUri!.ToString(), StringComparison.Ordinal);
        Assert.Contains("$select=id,onPremisesSamAccountName,onPremisesDomainName,mail,givenName,surname,displayName,accountEnabled", request.RequestUri.Query, StringComparison.Ordinal);
        Assert.Contains("$top=100", request.RequestUri.Query, StringComparison.Ordinal);
    }

    // Graph rejects any filter on onPremisesSamAccountName (HTTP 400 Request_UnsupportedQuery) outside advanced query mode.
    [Fact]
    public async Task SearchAsync_UsesGraphAdvancedQueryMode()
    {
        var (service, handler, _) = Create();

        await service.SearchAsync("smith");

        var request = Assert.Single(handler.Requests);
        Assert.Contains("$count=true", request.RequestUri!.Query, StringComparison.Ordinal);
        Assert.Equal("eventual", Assert.Single(request.Headers.GetValues("ConsistencyLevel")));
    }

    [Fact]
    public async Task SearchAsync_MatchesEachPieceAgainstAccountFirstNameSurnameAndDisplayName()
    {
        var (service, handler, _) = Create();

        await service.SearchAsync("dt2\\jsmith  Jane");

        Assert.Equal(
            "(startsWith(onPremisesSamAccountName,'dt2') or startsWith(givenName,'dt2') or startsWith(surname,'dt2') or startsWith(displayName,'dt2')"
            + " or startsWith(onPremisesSamAccountName,'jsmith') or startsWith(givenName,'jsmith') or startsWith(surname,'jsmith') or startsWith(displayName,'jsmith')"
            + " or startsWith(onPremisesSamAccountName,'Jane') or startsWith(givenName,'Jane') or startsWith(surname,'Jane') or startsWith(displayName,'Jane'))"
            + " and accountEnabled eq true",
            DecodedFilter(Assert.Single(handler.Requests)));
    }

    [Fact]
    public async Task SearchAsync_SingleQuoteIsDoubledAndSpecialCharactersAreUrlEncoded()
    {
        var (service, handler, _) = Create();

        await service.SearchAsync("O'Brien&x=1");

        var request = Assert.Single(handler.Requests);
        Assert.Contains("startsWith(surname,'O''Brien&x=1')", DecodedFilter(request), StringComparison.Ordinal);
        Assert.DoesNotContain("&x=1", request.RequestUri!.Query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SearchAsync_LimitsTheNumberAndLengthOfSearchTerms()
    {
        var (service, handler, _) = Create();

        await service.SearchAsync($"a b c d e f g {new string('x', 200)} A");

        var filter = DecodedFilter(Assert.Single(handler.Requests));
        Assert.Equal(5, filter.Split(" or startsWith(onPremisesSamAccountName", StringSplitOptions.None).Length);
        Assert.DoesNotContain("'f'", filter, StringComparison.Ordinal);

        var (service2, handler2, _) = Create();
        await service2.SearchAsync(new string('x', 200));
        Assert.Contains($"'{new string('x', 64)}'", DecodedFilter(Assert.Single(handler2.Requests)), StringComparison.Ordinal);
        Assert.DoesNotContain(new string('x', 65), DecodedFilter(handler2.Requests[0]), StringComparison.Ordinal);
    }

    [Fact]
    public async Task SearchAsync_BuildsTheLegacyStyleLowercaseUsernameWithTheDomainCutAtTheFirstDot()
    {
        var (service, _, _) = Create("""
            {"value":[
              {"onPremisesSamAccountName":"JSmith","onPremisesDomainName":"DT2.local","mail":"jane@example.test","givenName":"Jane","surname":"Smith","displayName":"Jane Smith","accountEnabled":true}
            ]}
            """);

        var user = Assert.Single((await service.SearchAsync("jane")).Users);

        Assert.Equal("dt2\\jsmith", user.Username);
        Assert.Equal("jane@example.test", user.Email);
        Assert.Equal("Jane Smith", user.FriendlyName);
        Assert.Equal("Jane", user.FirstName);
        Assert.Equal("Smith", user.LastName);
    }

    [Fact]
    public async Task SearchAsync_MissingOptionalFieldsFallBackSensibly()
    {
        var (service, _, _) = Create("""
            {"value":[{"onPremisesSamAccountName":"asam","givenName":"Al","surname":"Sam"}]}
            """);

        var user = Assert.Single((await service.SearchAsync("al")).Users);

        Assert.Equal("asam", user.Username);
        Assert.Equal(string.Empty, user.Email);
        Assert.Equal("Al Sam", user.FriendlyName);
    }

    [Fact]
    public async Task SearchAsync_SkipsDisabledAccountsAndUsersWithNoOnPremisesAccountName_AndSortsByUsername()
    {
        var (service, _, _) = Create("""
            {"value":[
              {"onPremisesSamAccountName":"zed","onPremisesDomainName":"dt2","displayName":"Zed","accountEnabled":true},
              {"onPremisesSamAccountName":"off","onPremisesDomainName":"dt2","displayName":"Off","accountEnabled":false},
              {"onPremisesSamAccountName":null,"displayName":"Cloud only","accountEnabled":true},
              {"onPremisesSamAccountName":"abe","onPremisesDomainName":"dt2","displayName":"Abe"}
            ]}
            """);

        var results = await service.SearchAsync("e");

        Assert.Equal(["dt2\\abe", "dt2\\zed"], results.Users.Select(r => r.Username));
    }

    [Fact]
    public async Task SearchAsync_ExcludesDisabledAccountsInTheQueryItselfSoTheyCannotUseUpAPage()
    {
        var (service, handler, _) = Create();

        await service.SearchAsync("smith");

        Assert.EndsWith(" and accountEnabled eq true", DecodedFilter(Assert.Single(handler.Requests)), StringComparison.Ordinal);
    }

    // Legacy returned every match; only a safety cap applies here.
    [Fact]
    public async Task SearchAsync_MoreThanFiftyMatches_ReturnsThemAllWithoutTruncation()
    {
        var (service, _, _) = CreatePaged(UserPage(0, 80));

        var results = await service.SearchAsync("u");

        Assert.Equal(80, results.Users.Count);
        Assert.False(results.IsTruncated);
    }

    [Fact]
    public async Task SearchAsync_FollowsNextPageLinksAndSendsTheAdvancedQueryHeaderOnEveryPage()
    {
        var (service, handler, _) = CreatePaged(
            UserPage(0, 100, nextLink: "https://graph.example.test/v1.0/users?$skiptoken=page2"),
            UserPage(100, 30));

        var results = await service.SearchAsync("u");

        Assert.Equal(130, results.Users.Count);
        Assert.False(results.IsTruncated);
        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal("https://graph.example.test/v1.0/users?$skiptoken=page2", handler.Requests[1].RequestUri!.ToString());
        Assert.All(handler.Requests, r => Assert.Equal("eventual", Assert.Single(r.Headers.GetValues("ConsistencyLevel"))));
    }

    [Fact]
    public async Task SearchAsync_MoreMatchesThanTheCap_ReturnsTheFirstOnesSortedAndFlagsTruncation()
    {
        var pages = Enumerable.Range(0, 6)
            .Select(i => UserPage(i * 100, 100, nextLink: "https://graph.example.test/v1.0/users?$skiptoken=next"))
            .ToArray();
        var (service, _, _) = CreatePaged(pages);

        var results = await service.SearchAsync("u");

        Assert.Equal(500, results.Users.Count);
        Assert.True(results.IsTruncated);
        Assert.Equal("dt2\\u000", results.Users[0].Username);
    }

    [Fact]
    public async Task SearchAsync_RunsOutOfPagesWhileStillLinked_FlagsTruncation()
    {
        var pages = Enumerable.Range(0, 10)
            .Select(i => UserPage(i, 1, nextLink: "https://graph.example.test/v1.0/users?$skiptoken=next"))
            .ToArray();
        var (service, handler, _) = CreatePaged(pages);

        var results = await service.SearchAsync("u");

        Assert.Equal(10, handler.Requests.Count);
        Assert.True(results.IsTruncated);
    }

    // The Graph bearer token is attached to every request from this client.
    [Fact]
    public async Task SearchAsync_NextLinkForAnotherHost_IsNotFollowed()
    {
        var (service, handler, _) = CreatePaged(UserPage(0, 5, nextLink: "https://evil.example.test/v1.0/users?$skiptoken=x"));

        await Assert.ThrowsAsync<GraphException>(() => service.SearchAsync("u"));

        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task SearchAsync_EmptyBody_ReturnsNothing()
    {
        var (service, _, _) = Create("null");

        Assert.Empty((await service.SearchAsync("jane")).Users);
    }

    [Fact]
    public async Task SearchAsync_GraphFailure_ThrowsWithTheStatusOnly()
    {
        var (service, _, _) = Create("""{"error":{"message":"secret detail"}}""", HttpStatusCode.Forbidden);

        var exception = await Assert.ThrowsAsync<GraphException>(() => service.SearchAsync("jane"));

        Assert.Contains("403", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("secret detail", exception.Message, StringComparison.Ordinal);
    }
}
