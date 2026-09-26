using Microsoft.AspNetCore.Authorization;
using PTL.ApiClient;
using PTL.Auth.Cidm;

var builder = WebApplication.CreateBuilder(args);
builder.AddPtlWebFrontEnd();
builder.AddCidmAuthentication();

// Authenticated by default - every page must opt OUT with [AllowAnonymous] rather than every new
// page having to remember to opt IN with [Authorize].
builder.Services.AddAuthorization(options =>
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
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
