using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PTL.ApiClient;
using PTL.Contracts.GroupAddress;
using PTL.Contracts.Lookup;
using PTL.Core.GroupAddress;
using PTL.InternalWeb.Tests.TestSupport;
using GroupAddressLookupResponse = PTL.Contracts.Lookup.GroupAddressResponse;
using GroupAddressDataResponse = PTL.Contracts.GroupAddress.GroupAddressResponse;

namespace PTL.InternalWeb.Tests.Integration;

public class GroupAddressRouteSmokeTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly Guid _groupAddressId = Guid.NewGuid();
    private readonly FakeGroupAddressApiClient _fakeApiClient;

    public GroupAddressRouteSmokeTests(WebApplicationFactory<Program> factory)
    {
        _fakeApiClient = new FakeGroupAddressApiClient
        {
            GroupAddresses =
            [
                new GroupAddressDataResponse(_groupAddressId, "PTL-001", "1 Sample Street", "Second Line", "Third Line", "Fourth Line", "Fifth Line", Guid.Empty, "020 1234 5678", "Fragile")
            ],
            GroupAddress = new GroupAddressDataResponse(_groupAddressId, "PTL-001", "1 Sample Street", "Second Line", "Third Line", "Fourth Line", "Fifth Line", Guid.Empty, "020 1234 5678", "Fragile")
        };

        _factory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IGroupAddressApiClient>();
                services.AddSingleton<IGroupAddressApiClient>(_fakeApiClient);
                services.RemoveAll<ILookupApiClient>();
                services.AddSingleton<ILookupApiClient>(new FakeLookupApiClient
                {
                    Countries =
                    [
                        new CountryResponse(Guid.NewGuid(), "United Kingdom")
                    ],
                    GroupAddresses =
                    [
                        new GroupAddressLookupResponse(_groupAddressId, "PTL-001", "1 Sample Street", Guid.Empty)
                    ]
                });
            }));
    }

    [Fact]
    public async Task Index_ReturnsSuccess()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/GroupAddress/Index");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Create_ReturnsSuccess()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/GroupAddress/Create");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Index_RendersDetailsLinkAndPaginationMarkup()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/GroupAddress/Index?page=1&pageSize=20");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("/GroupAddress/Details/", body);
        Assert.Contains("govuk-pagination", body);
        Assert.DoesNotContain("Edit</a>", body);
    }

    [Fact]
    public void Validate_RequiresAddress2()
    {
        var result = GroupAddressValidator.Validate(new GroupAddress
        {
            GroupAddressId = Guid.NewGuid(),
            Identifier = "PTL-001",
            Address1 = "1 Sample Street",
            Address2 = string.Empty,
            Address3 = string.Empty,
            Address4 = string.Empty,
            Address5 = string.Empty,
            CountryId = Guid.NewGuid(),
            Telephone = "020 1234 5678",
            PackingInstructions = string.Empty
        });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Field == nameof(GroupAddress.Address2));
    }

    [Fact]
    public void Validate_AllowsBlankAddress3To5AndTelephoneAndPackingInstructions_WhenIdentifierAddress1Address2AndCountryAreSet()
    {
        var result = GroupAddressValidator.Validate(new GroupAddress
        {
            GroupAddressId = Guid.NewGuid(),
            Identifier = "PTL-001",
            Address1 = "1 Sample Street",
            Address2 = "Second Line",
            Address3 = string.Empty,
            Address4 = string.Empty,
            Address5 = string.Empty,
            CountryId = Guid.NewGuid(),
            Telephone = string.Empty,
            PackingInstructions = string.Empty
        });

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Validate_RequiresCountry()
    {
        var result = GroupAddressValidator.Validate(new GroupAddress
        {
            GroupAddressId = Guid.NewGuid(),
            Identifier = "PTL-001",
            Address1 = "1 Sample Street",
            Address2 = "Second Line",
            Address3 = string.Empty,
            Address4 = string.Empty,
            Address5 = string.Empty,
            CountryId = Guid.Empty,
            Telephone = string.Empty,
            PackingInstructions = string.Empty
        });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Field == nameof(GroupAddress.CountryId));
    }

    [Fact]
    public async Task Details_RendersBreadcrumbs()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/GroupAddress/Details/{_groupAddressId}");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Manage Contracts", body);
        Assert.Contains("Group Addresses", body);
        Assert.Contains("Group Address Details", body);
    }
}
