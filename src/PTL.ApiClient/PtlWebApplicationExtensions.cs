using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PTL.Common.Correlation;
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

        builder.Services.AddControllersWithViews();
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
