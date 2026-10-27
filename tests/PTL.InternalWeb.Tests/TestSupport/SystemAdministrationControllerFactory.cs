using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using PTL.InternalWeb.Features.SystemAdministration;

namespace PTL.InternalWeb.Tests.TestSupport;

// Builds a SystemAdministrationController with in-memory TempData, real DataAnnotations validation
// (TryValidateModel) and fakes, defaulting the clients a test doesn't care about.
internal static class SystemAdministrationControllerFactory
{
    private static readonly ServiceProvider Services = new ServiceCollection()
        .AddLogging()
        .AddControllers()
        .Services
        .BuildServiceProvider();

    public static SystemAdministrationController Create(
        FakeCountryApiClient? countryApiClient = null,
        FakeViewerApiClient? viewerApiClient = null)
    {
        var httpContext = new DefaultHttpContext { RequestServices = Services };

        return new SystemAdministrationController(
            new SystemAdministrationApiClients
            {
                AdministrationCharge = new FakeAdministrationChargeApiClient(),
                WeightedPricingPlan = new FakeWeightedPricingPlanApiClient(),
                PostagePricingPlan = new FakePostagePricingPlanApiClient(),
                Country = countryApiClient ?? new FakeCountryApiClient(),
                ExternalSiteMessage = new FakeExternalSiteMessageApiClient(),
                User = new FakeUserApiClient(),
                Role = new FakeRoleApiClient(),
                Lookup = new FakeLookupApiClient(),
                ExternalTestConsultant = new FakeExternalTestConsultantApiClient(),
                Viewer = viewerApiClient ?? new FakeViewerApiClient()
            },
            NullLogger<SystemAdministrationController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext, RouteData = new RouteData() },
            TempData = new TempDataDictionary(httpContext, new FakeTempDataProvider())
        };
    }
}
