using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using PTL.ApiClient;
using PTL.Auth.Cidm;
using PTL.Auth.Cidm.Events;
using PTL.ExternalWeb.Authorization;
using PTL.ExternalWeb.Features.Account;
using PTL.ExternalWeb.Features.Lab;
using PTL.ExternalWeb.Features.Tabulation;
using PTL.ExternalWeb.Infrastructure;
using PTL.ExternalWeb.Infrastructure.Pdf;

var builder = WebApplication.CreateBuilder(args);
builder.AddPtlWebFrontEnd();
builder.AddCidmAuthentication();
builder.Services.AddScoped<ICidmExternalUserResolver, ExternalUserResolver>();
builder.Services.AddScoped<IRazorViewRenderer, RazorViewRenderer>();
builder.Services.AddScoped<ITabulationPdfService, TabulationPdfService>();
// One Chromium browser is shared for the app's lifetime.
builder.Services.AddSingleton<IHtmlToPdfRenderer, PlaywrightHtmlToPdfRenderer>();

// Roles come from resolvedRoles (set at sign-in by ExternalUserResolver). Every endpoint requires
// a signed-in user unless it opts out with [AllowAnonymous].
builder.Services.AddExternalAuthorization();

// A Participant or Lab User with several labs must choose one before any page about a lab is shown.
builder.Services.Configure<MvcOptions>(options => options.Filters.Add<LabSelectionRequiredFilter>());

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
