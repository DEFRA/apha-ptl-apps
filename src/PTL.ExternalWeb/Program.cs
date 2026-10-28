using Microsoft.AspNetCore.Authentication.Cookies;
using PTL.ApiClient;
using PTL.Auth.Cidm;
using PTL.Auth.Cidm.Events;
using PTL.ExternalWeb.Authorization;
using PTL.ExternalWeb.Features.Account;

var builder = WebApplication.CreateBuilder(args);
builder.AddPtlWebFrontEnd();
builder.AddCidmAuthentication();
builder.Services.AddScoped<ICidmExternalUserResolver, ExternalUserResolver>();

// Roles come from resolvedRoles (set at sign-in by ExternalUserResolver). Every endpoint requires
// a signed-in user unless it opts out with [AllowAnonymous].
builder.Services.AddExternalAuthorization();

// A refused request lands on the standard Access Denied page (which itself answers 403).
builder.Services.Configure<CookieAuthenticationOptions>(
    CookieAuthenticationDefaults.AuthenticationScheme,
    options => options.AccessDeniedPath = "/AccessDenied");

var app = builder.Build();
app.UsePtlWebFrontEnd();

await app.RunAsync();

// Exposes the generated Program class to WebApplicationFactory<Program> in PTL.ExternalWeb.Tests.
public sealed partial class Program
{
    private Program() { }
}
