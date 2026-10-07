using Microsoft.AspNetCore.Authorization;
using PTL.ApiClient;
using PTL.Auth.Cidm;
using PTL.Auth.Cidm.Events;
using PTL.ExternalWeb.Features.Account;

var builder = WebApplication.CreateBuilder(args);
builder.AddPtlWebFrontEnd();
builder.AddCidmAuthentication();
builder.Services.AddScoped<ICidmExternalUserResolver, ExternalUserResolver>();

// Authenticated by default - every page must opt OUT with [AllowAnonymous] rather than every new
// page having to remember to opt IN with [Authorize].
builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build());

var app = builder.Build();
app.UsePtlWebFrontEnd();

await app.RunAsync();

// Exposes the generated Program class to WebApplicationFactory<Program> in PTL.ExternalWeb.Tests.
public sealed partial class Program
{
    private Program() { }
}
