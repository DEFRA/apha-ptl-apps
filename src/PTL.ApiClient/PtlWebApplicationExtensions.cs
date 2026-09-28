using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PTL.Common.Correlation;
using PTL.Common.Health;
using Serilog;
using Serilog.Formatting.Compact;

namespace PTL.ApiClient;

/// <summary>
/// Shared Program.cs bootstrap for every PTL Razor web front-end (PTL.InternalWeb,
/// PTL.ExternalWeb, ...) - identical for each, so it lives here rather than being
/// duplicated per app.
/// </summary>
public static class PtlWebApplicationExtensions
{
    public static WebApplicationBuilder AddPtlWebFrontEnd(this WebApplicationBuilder builder)
    {
        // Structured JSON to stdout only - ECS/Fargate storage is ephemeral, so no file sinks. The
        // awslogs driver on the container picks stdout/stderr up and ships it to CloudWatch Logs.
        builder.Host.UseSerilog((context, services, configuration) => configuration
            .ReadFrom.Configuration(context.Configuration)
            .Enrich.FromLogContext()
            .Enrich.WithMachineName()
            .Enrich.WithEnvironmentName()
            .WriteTo.Console(new CompactJsonFormatter()));

        builder.Services.AddControllersWithViews(options =>
        {
            // Default is false, which means ASP.NET Core skips a model's IValidatableObject.Validate()
            // entirely whenever any of its own properties fail attribute-based validation (e.g. [Required]) -
            // this is what made Customer/Participant/Contract/Scheme form validation appear "sequential"
            // (only the first failing [Required] property showed; the IValidatableObject-driven business
            // rule errors only appeared once every attribute-based validation had been fixed one at a time).
            options.ValidateComplexTypesIfChildValidationFails = true;

            // Default is "The value '{0}' is not valid for {1}." using the raw property name - replace
            // with a friendlier message using the field's [Display(Name)] where one is set.
            options.ModelBindingMessageProvider.SetAttemptedValueIsInvalidAccessor(
                (_, field) => $"Enter a valid value for {field}");
        });
        // Also enable Razor Pages (some projects in the solution use Razor Pages)
        builder.Services.AddRazorPages();

        // Typed client for calling PTL.Api, shared with other web front-ends via the PTL.ApiClient library.
        builder.Services.AddPtlApiClient(builder.Configuration);

        // Allow views to be located under Features/{Controller}/Views and Features/Shared
        builder.Services.Configure<RazorViewEngineOptions>(options =>
        {
            options.ViewLocationFormats.Insert(0, "/Features/{1}/Views/{0}.cshtml");
            options.ViewLocationFormats.Insert(1, "/Features/Shared/{0}.cshtml");
        });

        // Authentication scheme registration is deliberately NOT done here - each app's identity
        // provider is different (see AddPtlDefaultCookieAuthentication / PTL.Auth.Cidm's
        // AddCidmAuthentication), and a scheme can only be registered once per app.

        return builder;
    }

    /// <summary>
    /// Registers the plain username/password cookie authentication used today by PTL.InternalWeb.
    /// PTL.ExternalWeb does not call this - it registers cookie + CIDM OpenID Connect together via
    /// <c>AddCidmAuthentication</c> instead, since the two apps' identity providers are unrelated
    /// (Entra ID/SAML vs DEFRA Customer Identity) and a scheme can only be registered once per app.
    /// </summary>
    public static WebApplicationBuilder AddPtlDefaultCookieAuthentication(this WebApplicationBuilder builder)
    {
        builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                options.LoginPath = "/Account/Login";
                options.LogoutPath = "/Account/Logout";
            });

        return builder;
    }

    public static WebApplication UsePtlWebFrontEnd(this WebApplication app)
    {
        // Configure the HTTP request pipeline.
        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Home/Error");
        }

        // Correlation ID before request logging so the one-line-per-request log carries it, and so it's
        // available on HttpContext.Items for CorrelationIdDelegatingHandler when calling PTL.Api.
        app.UseCorrelationId();
        app.UseSerilogRequestLogging();

        // Serve static files from wwwroot
        app.UseStaticFiles();

        app.UseRouting();

        app.MapHealthEndpoints();

        app.UseAuthentication();
        app.UseAuthorization();

        app.MapStaticAssets();

        app.MapControllerRoute(
            name: "default",
            pattern: "{controller=Account}/{action=Login}/{id?}")
            .WithStaticAssets();

        // Ensure Razor Pages are available if any exist in the project
        app.MapRazorPages();

        return app;
    }
}
