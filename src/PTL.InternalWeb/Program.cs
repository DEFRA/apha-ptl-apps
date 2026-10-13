using PTL.ApiClient;
using PTL.Auth.Entra;
using PTL.Auth.Entra.Events;
using PTL.Core.Contract.Document;
using PTL.InternalWeb.Features.Account;
using PTL.InternalWeb.Features.SystemAdministration;

var builder = WebApplication.CreateBuilder(args);
builder.AddPtlWebFrontEnd();
builder.AddEntraAuthentication();
builder.Services.AddScoped<IEntraInternalUserResolver, InternalUserResolver>();

// SystemAdministration: gated on the "Admin" role already resolved into the ResolvedRoles claim
// at sign-in (InternalUserResolver) - see SystemAdministrationPolicy remarks for why this checks
// our own database role, not an Entra ID group.
builder.Services.AddAuthorizationBuilder()
    .AddPolicy(SystemAdministrationPolicy.Name, policy => policy.RequireAssertion(context =>
        context.User.FindFirst(InternalUserClaimTypes.ResolvedRoles)?.Value
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Contains(SystemAdministrationPolicy.AdminRoleName, StringComparer.OrdinalIgnoreCase) ?? false));

// Mail merge runs in-process in this app; the template bytes come from S3 via PTL.Api's
// export-template endpoints, so nothing here touches the file system.
builder.Services.AddScoped<ITemplateMergeService, TemplateMergeService>();

// Bundled so SystemAdministrationController's constructor takes one parameter instead of 8 - see
// SystemAdministrationApiClients remarks.
builder.Services.AddScoped(sp => new SystemAdministrationApiClients
{
    AdministrationCharge = sp.GetRequiredService<IAdministrationChargeApiClient>(),
    WeightedPricingPlan = sp.GetRequiredService<IWeightedPricingPlanApiClient>(),
    PostagePricingPlan = sp.GetRequiredService<IPostagePricingPlanApiClient>(),
    Country = sp.GetRequiredService<ICountryApiClient>(),
    ExternalSiteMessage = sp.GetRequiredService<IExternalSiteMessageApiClient>(),
    User = sp.GetRequiredService<IUserApiClient>(),
    Role = sp.GetRequiredService<IRoleApiClient>(),
    Lookup = sp.GetRequiredService<ILookupApiClient>(),
    ExternalTestConsultant = sp.GetRequiredService<IExternalTestConsultantApiClient>(),
    Viewer = sp.GetRequiredService<IViewerApiClient>()
});

var app = builder.Build();
app.UsePtlWebFrontEnd();

await app.RunAsync();

// Exposes the generated Program class to WebApplicationFactory<Program> in PTL.InternalWeb.Tests.
public sealed partial class Program
{
    private Program() { }
}
