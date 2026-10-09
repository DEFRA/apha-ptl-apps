using PTL.ApiClient;

namespace PTL.InternalWeb.Features.SystemAdministration;

// Bundles the ApiClients SystemAdministrationController depends on into a single constructor
// parameter, keeping the controller's constructor under the analyzer's parameter-count threshold
// without splitting the controller itself - System Administration's sub-features deliberately
// share one controller (see SystemAdministrationController class remarks).
public sealed class SystemAdministrationApiClients
{
    public required IAdministrationChargeApiClient AdministrationCharge { get; init; }
    public required IWeightedPricingPlanApiClient WeightedPricingPlan { get; init; }
    public required IPostagePricingPlanApiClient PostagePricingPlan { get; init; }
    public required ICountryApiClient Country { get; init; }
    public required IExternalSiteMessageApiClient ExternalSiteMessage { get; init; }
    public required IUserApiClient User { get; init; }
    public required IRoleApiClient Role { get; init; }
    public required ILookupApiClient Lookup { get; init; }
    public required IExternalTestConsultantApiClient ExternalTestConsultant { get; init; }
    public required IViewerApiClient Viewer { get; init; }
}
