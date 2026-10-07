using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using PTL.Auth.Entra.Events;
using PTL.Auth.Entra.Options;
using PTL.Auth.Entra.TokenRefresh;

namespace PTL.Auth.Entra;

/// <summary>Registers Microsoft Entra ID (OpenID Connect) sign-in for internal users.</summary>
public static class EntraAuthenticationExtensions
{
    public static WebApplicationBuilder AddEntraAuthentication(this WebApplicationBuilder builder)
    {
        // ClientId/ClientSecret/TenantId arrive as plain configuration - the ECS task definition's
        // "secrets" block resolves Parameter Store/Secrets Manager values into Entra__ClientId,
        // Entra__ClientSecret, Entra__TenantId environment variables before the container starts, and
        // ASP.NET Core's built-in environment variable configuration provider maps double-underscore
        // names to the "Entra:*" section automatically. Locally, the same "Entra:*" keys come from
        // dotnet user-secrets / appsettings.Development.json instead.
        builder.Services.AddOptions<EntraOptions>()
            .Bind(builder.Configuration.GetSection(EntraOptions.SectionName))
            .ValidateOnStart();
        builder.Services.AddSingleton<IValidateOptions<EntraOptions>, EntraOptionsValidator>();

        builder.Services.AddHttpClient(EntraTokenRefreshService.HttpClientName)
            .AddStandardResilienceHandler();
        builder.Services.AddSingleton<IEntraTokenRefreshService, EntraTokenRefreshService>();
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<EntraOpenIdConnectEvents>();
        builder.Services.AddSingleton<EntraCookieEvents>();

        // PT-LIMS's own dev HTTPS cert is often untrusted on a locked-down developer machine - same
        // local-only fallback rationale as PTL.Auth.Cidm's AddCidmAuthentication.
        var useLocalHttpFriendlyOidcSettings = builder.Environment.IsDevelopment();

        builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                options.LoginPath = "/Account/Login";
                options.LogoutPath = "/Account/Logout";
                options.SlidingExpiration = true;
                options.Cookie.HttpOnly = true;
                options.Cookie.SecurePolicy = useLocalHttpFriendlyOidcSettings
                    ? CookieSecurePolicy.SameAsRequest
                    : CookieSecurePolicy.Always;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.EventsType = typeof(EntraCookieEvents);
            })
            .AddOpenIdConnect(EntraAuthenticationDefaults.AuthenticationScheme, _ => { });

        // Configured via DI (rather than inline in AddOpenIdConnect above) so EntraOptions is resolved
        // through the validated IOptions<EntraOptions> pipeline instead of being parsed a second time.
        builder.Services.AddOptions<OpenIdConnectOptions>(EntraAuthenticationDefaults.AuthenticationScheme)
            .Configure<IOptions<EntraOptions>>((options, entraOptions) =>
            {
                var entra = entraOptions.Value;

                options.Authority = entra.Authority;
                options.ClientId = entra.ClientId;
                options.ClientSecret = entra.ClientSecret;
                options.ResponseType = entra.ResponseType;
                options.ResponseMode = useLocalHttpFriendlyOidcSettings ? "query" : entra.ResponseMode;
                options.CallbackPath = entra.CallbackPath;
                options.SignedOutCallbackPath = entra.SignedOutCallbackPath;
                // Entra ID's front-channel logout notifications are a real scenario for a
                // predominantly-internal, single-sign-on app - this path must be registered as the
                // app's "Front-channel logout URL" in the Entra ID app registration.
                options.RemoteSignOutPath = entra.FrontChannelLogoutPath;
                options.SaveTokens = true;
                options.UsePkce = true;
                options.GetClaimsFromUserInfoEndpoint = false;

                options.Scope.Clear();
                foreach (var scope in entra.Scopes)
                {
                    options.Scope.Add(scope);
                }

                if (useLocalHttpFriendlyOidcSettings)
                {
                    options.CorrelationCookie.SameSite = SameSiteMode.Lax;
                    options.CorrelationCookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
                    options.NonceCookie.SameSite = SameSiteMode.Lax;
                    options.NonceCookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
                }
                else
                {
                    options.NonceCookie.SameSite = SameSiteMode.None;
                    options.NonceCookie.SecurePolicy = CookieSecurePolicy.Always;
                    options.CorrelationCookie.SameSite = SameSiteMode.None;
                    options.CorrelationCookie.SecurePolicy = CookieSecurePolicy.Always;
                }

                options.EventsType = typeof(EntraOpenIdConnectEvents);
            });

        return builder;
    }
}
