using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PTL.ApiClient;
using PTL.Contracts.Lookup;
using PTL.Contracts.Search;
using PTL.InternalWeb.Tests.TestSupport;

namespace PTL.InternalWeb.Tests.Integration;

// Renders Features/Search/Views/Index.cshtml - legacy Contracts Admin/Search.aspx (Customers and
// Participants tabs).
public class SearchRenderTests : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly Guid CustomerId = Guid.Parse("77777777-0000-0000-0000-0000000000a1");
    private static readonly Guid ParticipantId = Guid.Parse("77777777-0000-0000-0000-0000000000a2");
    private static readonly Guid CountryId = Guid.Parse("77777777-0000-0000-0000-0000000000a3");

    private readonly WebApplicationFactory<Program> _factory;
    private readonly FakeSearchApiClient _search = new()
    {
        CustomerResults = [new CustomerSearchResultResponse(CustomerId, "QAL/00001", "ACC-1", "Sample Ltd", "Alice Example", "Sample Org", "France")],
        ParticipantResults = [new ParticipantSearchResultResponse(ParticipantId, CustomerId, "LAB1", "OLD1", "Lab One", "Bob Example")],
    };

    public SearchRenderTests(WebApplicationFactory<Program> factory) =>
        _factory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ISearchApiClient>();
                services.AddSingleton<ISearchApiClient>(_search);
                services.RemoveAll<ILookupApiClient>();
                services.AddSingleton<ILookupApiClient>(new FakeLookupApiClient { Countries = [new CountryResponse(CountryId, "France")] });
            }));

    private async Task<string> GetAsync(string url = "/Search/Index")
    {
        var response = await _factory.CreateClient().GetAsync(url);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync();
    }

    private static string MainContent(string html) => Regex.Match(html, "<main.*?</main>", RegexOptions.Singleline).Value;

    private static string Panel(string html, string id) =>
        Regex.Match(html, $"<div class=\"govuk-tabs__panel[^\"]*\" id=\"{id}\">.*?(?=<div class=\"govuk-tabs__panel|</div>\\s*</main>)", RegexOptions.Singleline).Value;

    private static List<string> Headers(string fragment) =>
        [.. Regex.Matches(fragment, "<th scope=\"col\" class=\"govuk-table__header\">(.*?)</th>").Select(m => m.Groups[1].Value)];

    [Fact]
    public async Task Get_RendersTheLegacyPageTitleHeadingIntroAndBothTabs()
    {
        var html = await GetAsync();
        var main = MainContent(html);

        Assert.Contains("<title>Search - ", html, StringComparison.Ordinal);
        Assert.Equal(1, Regex.Count(main, "<h1[ >]"));
        Assert.Contains("<h1 class=\"govuk-heading-l\">Search</h1>", main, StringComparison.Ordinal);
        Assert.Contains("This page can be used to search for Customers and Participants.", main, StringComparison.Ordinal);
        Assert.Contains("href=\"#customers\">Customers</a>", main, StringComparison.Ordinal);
        Assert.Contains("href=\"#participants\">Participants</a>", main, StringComparison.Ordinal);
        Assert.Contains("Use the search criteria below to identify the required Customer by either QAL Number, Name, Contact Name, Organisation or Country.", main, StringComparison.Ordinal);
        Assert.Contains("Use the search criteria below to identify the required Participant by either Lab Code, Lab Name or Contact Name.", main, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Get_CustomerTab_HasTheLegacyFieldsLabelsAndMaxLengths()
    {
        var panel = Panel(await GetAsync(), "customers");

        string[] labels = ["QAL Number", "Account Number", "Name", "Contact Name", "Organisation", "Country"];
        var rendered = Regex.Matches(panel, "<label class=\"govuk-label\" for=\"[^\"]+\">(.*?)</label>").Select(m => m.Groups[1].Value).ToList();
        Assert.Equal(labels, rendered);

        Assert.Matches("name=\"Customer.QalNumber\"[^>]*maxlength=\"10\"|maxlength=\"10\"[^>]*name=\"Customer.QalNumber\"", panel);
        Assert.Matches("name=\"Customer.AccountNumber\"[^>]*maxlength=\"20\"|maxlength=\"20\"[^>]*name=\"Customer.AccountNumber\"", panel);
        Assert.Matches("name=\"Customer.Name\"[^>]*maxlength=\"50\"|maxlength=\"50\"[^>]*name=\"Customer.Name\"", panel);
        Assert.Contains("<form method=\"get\" action=\"/Search/Index#customers\">", panel, StringComparison.Ordinal);
        Assert.Contains("<input type=\"hidden\" name=\"tab\" value=\"Customers\" />", panel, StringComparison.Ordinal);
        Assert.Contains(">Search</button>", panel, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Get_CountryDropDown_StartsWithNotSpecifiedThenTheCountries()
    {
        var panel = Panel(await GetAsync(), "customers");

        var select = Regex.Match(panel, "<select[^>]*name=\"Customer.CountryId\"[^>]*>(.*?)</select>", RegexOptions.Singleline).Groups[1].Value;
        var options = Regex.Matches(select, "<option value=\"([^\"]*)\"[^>]*>(.*?)</option>").Select(m => (m.Groups[1].Value, m.Groups[2].Value)).ToList();

        Assert.Equal([(string.Empty, "- Not Specified -"), (CountryId.ToString(), "France")], options);
    }

    [Fact]
    public async Task Get_ParticipantTab_HasTheLegacyFieldsLabelsAndMaxLengths()
    {
        var panel = Panel(await GetAsync(), "participants");

        var rendered = Regex.Matches(panel, "<label class=\"govuk-label\" for=\"[^\"]+\">(.*?)</label>").Select(m => m.Groups[1].Value).ToList();
        Assert.Equal(["Lab Code", "Lab Name", "Contact Name"], rendered);
        Assert.Matches("maxlength=\"8\"", panel);
        Assert.Contains("<form method=\"get\" action=\"/Search/Index#participants\">", panel, StringComparison.Ordinal);
        Assert.Contains("<input type=\"hidden\" name=\"tab\" value=\"Participants\" />", panel, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Get_BeforeASearch_ShowsNoResultsPanel()
    {
        var html = MainContent(await GetAsync());

        Assert.DoesNotContain("Search Results", html, StringComparison.Ordinal);
        Assert.Empty(_search.CustomerSearches);
        Assert.Empty(_search.ParticipantSearches);
    }

    [Fact]
    public async Task CustomerSearch_ShowsTheLegacyColumnsInOrderAndALinkToCustomerDetails()
    {
        var html = await GetAsync("/Search/Index?tab=Customers&Customer.QalNumber=QAL");
        var panel = Panel(html, "customers");

        Assert.Contains("<h2 class=\"govuk-heading-m\">Search Results:</h2>", panel, StringComparison.Ordinal);
        Assert.Equal(["QAL Number", "Account Number", "Name", "Contact Name", "Organisation", "Country", "Details"], Headers(panel));
        Assert.Contains("<td class=\"govuk-table__cell\">QAL/00001</td>", panel, StringComparison.Ordinal);
        Assert.Contains($"href=\"/Customer/Details/{CustomerId}\"", panel, StringComparison.Ordinal);
        Assert.Contains("value=\"QAL\"", panel, StringComparison.Ordinal);
        Assert.DoesNotContain("Search Results", Panel(html, "participants"), StringComparison.Ordinal);
        Assert.Equal("QAL", Assert.Single(_search.CustomerSearches).QalNumber);
        Assert.Empty(_search.ParticipantSearches);
        Assert.DoesNotContain("govuk-tabs__panel--hidden\" id=\"customers\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ParticipantSearch_ShowsTheLegacyColumnsInOrderAndALinkToParticipantDetails()
    {
        var html = await GetAsync("/Search/Index?tab=Participants&Participant.LabCode=LAB");
        var panel = Panel(html, "participants");

        Assert.Equal(["Lab Code", "Previous Lab Code", "Lab Name", "Contact Name", "Details"], Headers(panel));
        Assert.Contains("<td class=\"govuk-table__cell\">OLD1</td>", panel, StringComparison.Ordinal);
        Assert.Contains($"href=\"/Participant/Details/{ParticipantId}\"", panel, StringComparison.Ordinal);
        Assert.DoesNotContain("govuk-tabs__panel--hidden\" id=\"participants\"", html, StringComparison.Ordinal);
        Assert.Contains("govuk-tabs__panel--hidden\" id=\"customers\"", html, StringComparison.Ordinal);
        Assert.Equal("LAB", Assert.Single(_search.ParticipantSearches).LabCode);
        Assert.Empty(_search.CustomerSearches);
    }

    [Theory]
    [InlineData("/Search/Index?tab=Customers", "customers")]
    [InlineData("/Search/Index?tab=Participants", "participants")]
    public async Task ASearchWithNoMatches_ShowsTheLegacyEmptyMessage(string url, string panelId)
    {
        _search.CustomerResults = [];
        _search.ParticipantResults = [];

        var panel = Panel(await GetAsync(url), panelId);

        Assert.Contains("There are no Results to Display", panel, StringComparison.Ordinal);
        Assert.DoesNotContain("<table", panel, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OverLengthCriteria_ShowTheErrorSummaryAndDoNotSearch()
    {
        var html = await GetAsync("/Search/Index?tab=Customers&Customer.QalNumber=12345678901");

        Assert.Contains("QAL Number must be 10 characters or fewer", html, StringComparison.Ordinal);
        Assert.Contains("govuk-error-summary", html, StringComparison.Ordinal);
        Assert.Contains("href=\"#Customer_QalNumber\"", html, StringComparison.Ordinal);
        Assert.Contains("Error: ", Regex.Match(html, "<title>(.*?)</title>", RegexOptions.Singleline).Groups[1].Value, StringComparison.Ordinal);
        Assert.DoesNotContain("Search Results", MainContent(html), StringComparison.Ordinal);
        Assert.Empty(_search.CustomerSearches);
    }

    [Fact]
    public async Task OverLengthParticipantCriteria_ShowTheInlineError()
    {
        var html = await GetAsync("/Search/Index?tab=Participants&Participant.LabCode=123456789");

        Assert.Contains("Lab Code must be 8 characters or fewer", html, StringComparison.Ordinal);
        Assert.Contains("govuk-input--error", html, StringComparison.Ordinal);
        Assert.Empty(_search.ParticipantSearches);
    }

    [Fact]
    public async Task TheSideNavigation_ListsSearchUnderManageContracts()
    {
        var html = await GetAsync("/Contract/ManageContracts");

        Assert.Matches("<a [^>]*href=\"/Search(/Index)?\"[^>]*>\\s*Search\\s*</a>", html);
    }
}
