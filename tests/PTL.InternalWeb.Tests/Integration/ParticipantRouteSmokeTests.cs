using System.Net;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PTL.ApiClient;
using PTL.Contracts.Customer;
using PTL.Contracts.Participant;
using PTL.InternalWeb.Tests.TestSupport;

namespace PTL.InternalWeb.Tests.Integration;

/// <summary>
/// Integration smoke tests for Participant routes.
/// Exercises the Participant Create/Details/Edit/Index Razor views which were showing 0% coverage.
/// Uses WebApplicationFactory to render views through the full ASP.NET Core pipeline.
/// </summary>
public partial class ParticipantRouteSmokeTests : IClassFixture<WebApplicationFactory<Program>>
{
    [GeneratedRegex("__RequestVerificationToken[^>]*value=\"([^\"]+)\"", RegexOptions.None)]
    private static partial Regex AntiforgeryTokenPattern();

    private readonly WebApplicationFactory<Program> _factory;
    private readonly FakeParticipantApiClient _fakeParticipantClient;
    private readonly FakeCustomerApiClient _fakeCustomerClient;
    private readonly Guid _customerId;
    private readonly Guid _participantId;

    public ParticipantRouteSmokeTests(WebApplicationFactory<Program> factory)
    {
        _customerId = Guid.NewGuid();
        _participantId = Guid.NewGuid();

        var customer = SampleCustomer(_customerId);
        var participant = SampleParticipant(_participantId, _customerId);

        _fakeParticipantClient = new FakeParticipantApiClient
        {
            ParticipantResponse = participant,
            SearchResponse = new ParticipantSearchResponse(
                [new ParticipantSummaryResponse(_participantId, _customerId, "ALI-001", "Alice Lab", "Alice Smith", true)],
                1, 1, 20)
        };

        _fakeCustomerClient = new FakeCustomerApiClient
        {
            CustomerResponse = customer,
            SearchResponse = new CustomerSearchResponse([], 0, 1, 20)
        };

        _factory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IParticipantApiClient>();
                services.AddSingleton<IParticipantApiClient>(_fakeParticipantClient);
                services.RemoveAll<ICustomerApiClient>();
                services.AddSingleton<ICustomerApiClient>(_fakeCustomerClient);
                services.RemoveAll<ILookupApiClient>();
                services.AddSingleton<ILookupApiClient>(new FakeLookupApiClient());
            }));
    }

    private static CustomerResponse SampleCustomer(Guid customerId) => new(
        customerId, "QAL/00001", string.Empty, "Sample Labs", string.Empty, Guid.NewGuid(), string.Empty,
        Guid.NewGuid(), string.Empty, string.Empty, "Alice", "Sample Labs", "1 Street",
        "District", string.Empty, string.Empty, string.Empty, Guid.NewGuid(), "01234 567890", string.Empty,
        string.Empty, "alice@example.com", Guid.NewGuid(), string.Empty, DateTime.UtcNow, string.Empty, false,
        string.Empty, "Sample Labs", "1 Street", "District", string.Empty, string.Empty,
        string.Empty, Guid.NewGuid(), string.Empty, string.Empty, string.Empty, string.Empty, true, false,
        null, null);

    private static ParticipantResponse SampleParticipant(Guid participantId, Guid customerId, bool isActive = true) =>
        new(participantId, Guid.NewGuid(), customerId, "ALI-001", "Alice Lab", Guid.NewGuid(),
            "Alice Smith", "Lab Name", "1 Street", "District", string.Empty, string.Empty, string.Empty,
            Guid.NewGuid(), "01234 567890", "01234 567891", "alice@example.com", "alice2@example.com",
            "Notes", isActive, isActive ? null : DateTime.UtcNow, false, null);

    [Fact]
    public async Task Index_ReturnsSuccess()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/Participant/Index?customerId={_customerId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Index_WithNoResults_RendersEmptyState()
    {
        _fakeParticipantClient.SearchResponse = new ParticipantSearchResponse([], 0, 1, 20);
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/Participant/Index?customerId={_customerId}");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("There are no participants to display.", body);
        Assert.DoesNotContain("searchTerm", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Create_ReturnsSuccess()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/Participant/Create?customerId={_customerId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private async Task<List<string>> ViewerCheckboxesAsync(bool isActive, Guid assigned, Guid available)
    {
        _fakeParticipantClient.ParticipantViewerAssignmentResponse = new ParticipantViewerAssignmentResponse(
            _customerId, "ALI-001", "Alice Lab", isActive,
            [new ViewerResponse(available, "Beta Viewer", string.Empty)],
            [new ViewerResponse(assigned, "Alpha Viewer", string.Empty)]);

        var html = await _factory.CreateClient().GetStringAsync($"/Participant/Viewers?participantId={_participantId}");

        // The transfer lists are only revealed (and only then named) by the script.
        Assert.Contains("id=\"viewerDualList\" hidden", html, StringComparison.Ordinal);
        Assert.Matches("<select[^>]*id=\"participantViewers\"(?![^>]*name=)[^>]*>", html);
        return [.. Regex.Matches(html, "<input[^>]*name=\"viewerIds\"[^>]*>").Select(m => m.Value)];
    }

    [Fact]
    public async Task Viewers_RendersEveryViewerAsACheckboxTickedWhenAssignedSoSavingWorksWithoutScript()
    {
        var assigned = Guid.NewGuid();
        var available = Guid.NewGuid();

        var checkboxes = await ViewerCheckboxesAsync(isActive: true, assigned, available);

        Assert.Equal(2, checkboxes.Count);
        Assert.All(checkboxes, c => Assert.Contains("type=\"checkbox\"", c, StringComparison.Ordinal));
        Assert.Contains("checked", checkboxes.Single(c => c.Contains($"value=\"{assigned}\"", StringComparison.Ordinal)), StringComparison.Ordinal);
        Assert.DoesNotContain("checked", checkboxes.Single(c => c.Contains($"value=\"{available}\"", StringComparison.Ordinal)), StringComparison.Ordinal);
    }

    // Legacy hides Add for an inactive participant, so its viewers can only be kept or removed.
    [Fact]
    public async Task Viewers_InactiveParticipant_OffersOnlyItsAssignedViewersAsCheckboxes()
    {
        var assigned = Guid.NewGuid();

        var checkboxes = await ViewerCheckboxesAsync(isActive: false, assigned, Guid.NewGuid());

        var only = Assert.Single(checkboxes);
        Assert.Contains($"value=\"{assigned}\"", only, StringComparison.Ordinal);
        Assert.Contains("checked", only, StringComparison.Ordinal);
    }

    // What a browser without scripting sends: only the ticked checkboxes, under the one viewerIds name.
    [Fact]
    public async Task Viewers_Post_WithoutScript_SavesExactlyTheTickedViewers()
    {
        var kept = Guid.NewGuid();
        var added = Guid.NewGuid();
        _fakeParticipantClient.ParticipantViewerAssignmentResponse = new ParticipantViewerAssignmentResponse(
            _customerId, "ALI-001", "Alice Lab", true,
            [new ViewerResponse(added, "Beta Viewer", string.Empty)],
            [new ViewerResponse(kept, "Alpha Viewer", string.Empty)]);
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var url = $"/Participant/Viewers?participantId={_participantId}";
        var (token, cookie) = await GetAntiforgeryAsync(client, url);

        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new FormUrlEncodedContent(
            [
                new("__RequestVerificationToken", token),
                new("customerId", _customerId.ToString()),
                new("viewerIds", kept.ToString()),
                new("viewerIds", added.ToString())
            ])
        };
        request.Headers.Add("Cookie", cookie);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(_participantId, _fakeParticipantClient.LastUpdateParticipantViewersParticipantId);
        Assert.Equal([kept, added], _fakeParticipantClient.LastUpdateParticipantViewersViewerIds);
    }

    [Fact]
    public async Task Details_ReturnsSuccess()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/Participant/Details/{_participantId}?customerId={_customerId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Details_RendersFullBreadcrumbTrail()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/Participant/Details/{_participantId}?customerId={_customerId}");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Contains("Manage Contracts", body);
        Assert.Contains("Customers", body);
        Assert.Contains("Participants", body);
        Assert.Contains("Participant Details", body);
    }

    // The Participants list is per-customer, and the Details route carries only the participant id,
    // so the crumb has to pick the customer up from the page itself.
    [Fact]
    public async Task Details_WithoutCustomerIdInUrl_StillLinksParticipantsBreadcrumbToTheCustomer()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/Participant/Details/{_participantId}");
        var body = await response.Content.ReadAsStringAsync();
        var breadcrumbs = BreadcrumbSection(body);

        Assert.Contains($"customerId={_customerId}", breadcrumbs, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain($"customerId={Guid.Empty}", breadcrumbs, StringComparison.OrdinalIgnoreCase);
    }

    private static string BreadcrumbSection(string body)
    {
        var start = body.IndexOf("govuk-breadcrumbs", StringComparison.Ordinal);
        Assert.True(start >= 0, "No breadcrumbs rendered.");
        var end = body.IndexOf("</nav>", start, StringComparison.Ordinal);
        return body[start..end];
    }

    [Fact]
    public async Task Details_InactiveParticipant_RendersInactiveDateRow()
    {
        _fakeParticipantClient.ParticipantResponse = SampleParticipant(_participantId, _customerId, isActive: false);
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/Participant/Details/{_participantId}?customerId={_customerId}");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Inactive date", body);
    }

    [Fact]
    public async Task Edit_ReturnsSuccess()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/Participant/Edit/{_participantId}?customerId={_customerId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Edit_RendersFullBreadcrumbTrail()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/Participant/Edit/{_participantId}?customerId={_customerId}");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Contains("Manage Contracts", body);
        Assert.Contains("Customers", body);
        Assert.Contains("Participants", body);
        Assert.Contains("Edit Participant", body);
    }

    [Fact]
    public async Task Create_Post_InvalidModel_RendersFieldErrors()
    {
        var client = _factory.CreateClient();
        var (token, cookie) = await GetAntiforgeryAsync(client, $"/Participant/Create?customerId={_customerId}");

        var request = new HttpRequestMessage(HttpMethod.Post, $"/Participant/Create?customerId={_customerId}")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["IsActive"] = "true",
                ["FirstName"] = new string('a', 60), // Too long
                ["Surname"] = new string('b', 60),   // Too long
            })
        };
        request.Headers.Add("Cookie", cookie);

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("govuk-form-group--error", body);
    }

    [Fact]
    public async Task Edit_Post_InvalidModel_RendersFieldErrors()
    {
        var client = _factory.CreateClient();
        var (token, cookie) = await GetAntiforgeryAsync(client, $"/Participant/Edit/{_participantId}?customerId={_customerId}");

        var request = new HttpRequestMessage(HttpMethod.Post, $"/Participant/Edit/{_participantId}?customerId={_customerId}")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["IsActive"] = "true",
            })
        };
        request.Headers.Add("Cookie", cookie);

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("govuk-form-group--error", body);
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
