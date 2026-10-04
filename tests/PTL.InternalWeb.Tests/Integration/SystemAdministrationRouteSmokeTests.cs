using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PTL.ApiClient;
using PTL.InternalWeb.Tests.TestSupport;

namespace PTL.InternalWeb.Tests.Integration;

// Full-pipeline smoke tests so the System Administration Razor views (SystemAdministration,
// AdministrationCharge, WeightedPricingPlan) actually render at least once through the real MVC
// pipeline, rather than only being exercised via SystemAdministrationControllerTests.
public class SystemAdministrationRouteSmokeTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public SystemAdministrationRouteSmokeTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IAdministrationChargeApiClient>();
                services.AddSingleton<IAdministrationChargeApiClient>(new FakeAdministrationChargeApiClient());
                services.RemoveAll<IWeightedPricingPlanApiClient>();
                services.AddSingleton<IWeightedPricingPlanApiClient>(new FakeWeightedPricingPlanApiClient());
                services.RemoveAll<ILookupApiClient>();
                services.AddSingleton<ILookupApiClient>(new FakeLookupApiClient());
            }));
    }

    [Fact]
    public async Task SystemAdministration_ReturnsSuccess()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/SystemAdministration/SystemAdministration");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task SystemAdministration_RendersHeading()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/SystemAdministration/SystemAdministration");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("govuk-heading", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AdministrationCharge_ReturnsSuccess()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/SystemAdministration/AdministrationCharge");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task WeightedPricingPlan_ReturnsSuccess()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/SystemAdministration/WeightedPricingPlan");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}

