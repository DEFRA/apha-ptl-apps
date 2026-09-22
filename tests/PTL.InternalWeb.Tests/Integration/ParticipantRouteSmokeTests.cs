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
    }

    [Fact]
    public async Task Create_ReturnsSuccess()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/Participant/Create?customerId={_customerId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Details_ReturnsSuccess()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/Participant/Details/{_participantId}?customerId={_customerId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
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
