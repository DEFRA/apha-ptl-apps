using System.Net;
using System.Text;
using PTL.Api.Infrastructure;
using PTL.Common.Graph;

namespace PTL.Api.Tests.User;

public class GraphStaffDirectoryServiceTests
{
    private sealed class RecordingHandler(HttpStatusCode status, string json) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
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

    private static (GraphStaffDirectoryService Service, RecordingHandler Handler, Factory Factory) Create(string json = """{"value":[]}""", HttpStatusCode status = HttpStatusCode.OK)
    {
        var handler = new RecordingHandler(status, json);
        var factory = new Factory(handler);
        return (new GraphStaffDirectoryService(factory), handler, factory);
    }

    private static string DecodedFilter(HttpRequestMessage request) =>
        Uri.UnescapeDataString(request.RequestUri!.Query.Split('&').Single(p => p.StartsWith("$filter=", StringComparison.Ordinal))[8..]);

    [Fact]
    public async Task SearchAsync_BlankSearch_ReturnsNothingWithoutCallingGraph()
    {
        var (service, handler, _) = Create();

        var results = await service.SearchAsync("  \\  ");

        Assert.Empty(results);
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
            "startsWith(onPremisesSamAccountName,'dt2') or startsWith(givenName,'dt2') or startsWith(surname,'dt2') or startsWith(displayName,'dt2')"
            + " or startsWith(onPremisesSamAccountName,'jsmith') or startsWith(givenName,'jsmith') or startsWith(surname,'jsmith') or startsWith(displayName,'jsmith')"
            + " or startsWith(onPremisesSamAccountName,'Jane') or startsWith(givenName,'Jane') or startsWith(surname,'Jane') or startsWith(displayName,'Jane')",
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

        var user = Assert.Single(await service.SearchAsync("jane"));

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

        var user = Assert.Single(await service.SearchAsync("al"));

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

        Assert.Equal(["dt2\\abe", "dt2\\zed"], results.Select(r => r.Username));
    }

    [Fact]
    public async Task SearchAsync_ReturnsAtMostFiftyUsers()
    {
        var users = string.Join(",", Enumerable.Range(0, 80).Select(i => $$"""{"onPremisesSamAccountName":"u{{i:D3}}","onPremisesDomainName":"dt2","displayName":"U{{i}}"}"""));
        var (service, _, _) = Create($$"""{"value":[{{users}}]}""");

        Assert.Equal(50, (await service.SearchAsync("u")).Count);
    }

    [Fact]
    public async Task SearchAsync_EmptyBody_ReturnsNothing()
    {
        var (service, _, _) = Create("null");

        Assert.Empty(await service.SearchAsync("jane"));
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
