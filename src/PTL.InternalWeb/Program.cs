using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using PTL.ApiClient;
using PTL.Auth.Entra;
using PTL.Auth.Entra.Events;
using PTL.Common.Graph;
using PTL.Core.Contract.Document;
using PTL.Core.User;
using PTL.InternalWeb.Authorization;
using PTL.InternalWeb.Features.Account;
using PTL.InternalWeb.Features.SystemAdministration;
using PTL.InternalWeb.Infrastructure;
using PTL.InternalWeb.SessionTimeout;

var builder = WebApplication.CreateBuilder(args);
builder.AddPtlWebFrontEnd();
builder.AddEntraAuthentication();
builder.Services.AddScoped<IEntraInternalUserResolver, InternalUserResolver>();

// Client-side inactivity warning (see _SessionTimeoutWarning.cshtml/session-timeout.js) - a UX/
// security layer independent of the 14-day sliding auth cookie and Entra token refresh above.
builder.Services.AddOptions<SessionTimeoutOptions>()
    .Bind(builder.Configuration.GetSection(SessionTimeoutOptions.SectionName))
    .ValidateOnStart();
builder.Services.AddSingleton<IValidateOptions<SessionTimeoutOptions>, SessionTimeoutOptionsValidator>();

// Roles come from the PTL database (resolved at sign-in by InternalUserResolver), never from Entra ID
// groups or app roles. Every endpoint requires a signed-in user unless it opts out with [AllowAnonymous].
builder.Services.AddPtlAuthorization();

// A refused request lands on the standard Access Denied page (which itself answers 403).
builder.Services.Configure<CookieAuthenticationOptions>(
    CookieAuthenticationDefaults.AuthenticationScheme,
    options => options.AccessDeniedPath = "/AccessDenied");

// Logs every refused request ("non-Admin request is refused AND LOGGED") - without this
// registration ASP.NET Core silently uses its default handler and refusals go unlogged.
builder.Services.AddSingleton<IAuthorizationMiddlewareResultHandler, LoggingAuthorizationMiddlewareResultHandler>();

// Mail merge runs in-process in this app; the template bytes come from S3 via PTL.Api's
// export-template endpoints, so nothing here touches the file system.
builder.Services.AddScoped<ITemplateMergeService, TemplateMergeService>();

// The Create User staff search calls Microsoft Graph from here, so the Graph app registration's secrets
// (the "Graph" configuration section) are held by this app only - PTL.Api no longer has them.
builder.Services.AddGraphClient(builder.Configuration);
builder.Services.AddScoped<IStaffDirectoryService, GraphStaffDirectoryService>();

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
