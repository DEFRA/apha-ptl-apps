using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using PTL.Auth.Cidm.Events;
using PTL.Auth.Cidm.Options;
using PTL.Auth.Cidm.TokenRefresh;

namespace PTL.Auth.Cidm;

public static class CidmAuthenticationExtensions
{
    public static WebApplicationBuilder AddCidmAuthentication(this WebApplicationBuilder builder)
    {
        // ClientId/ClientSecret/ServiceId/Address/Policy all arrive as plain configuration - the ECS
        // task definition's "secrets" block resolves Parameter Store/Secrets Manager values into
        // Cidm__ClientId, Cidm__ClientSecret, Cidm__ServiceId, Cidm__Address, Cidm__Policy environment
        // variables before the container starts, and ASP.NET Core's built-in environment variable
        // configuration provider maps double-underscore names to the "Cidm:*" section automatically.
        // Locally, the same "Cidm:*" keys come from dotnet user-secrets / appsettings.Development.json
        // instead - the app itself never talks to AWS directly for this.
        builder.Services.AddOptions<CidmOptions>()
            .Bind(builder.Configuration.GetSection(CidmOptions.SectionName))
            .ValidateOnStart();
        builder.Services.AddSingleton<IValidateOptions<CidmOptions>, CidmOptionsValidator>();

        builder.Services.AddHttpClient(CidmTokenRefreshService.HttpClientName)
            .AddStandardResilienceHandler();
        builder.Services.AddSingleton<ICidmTokenRefreshService, CidmTokenRefreshService>();
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<CidmOpenIdConnectEvents>();
        builder.Services.AddSingleton<CidmCookieEvents>();

        // form_post (DEFRA's recommended response_mode) requires the nonce/correlation cookies to be
        // SameSite=None+Secure, which browsers only honour over HTTPS - a real constraint everywhere
        // this app is actually deployed, but not always practical on a locked-down developer machine
        // where trusting a local HTTPS dev cert is blocked by IT policy. Locally only, fall back to
        // response_mode=query (also explicitly supported by the CIDM guide) - its callback is a plain
        // top-level GET redirect, which SameSite=Lax (the ASP.NET Core default) already allows over
        // plain HTTP. Every deployed environment keeps form_post + SameSite=None, unchanged.
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
                // form_post's callback is a same-site top-level navigation once it lands on our own
                // /signin-oidc - the app's own session cookie itself doesn't need SameSite=None.
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.EventsType = typeof(CidmCookieEvents);
            })
            .AddOpenIdConnect(CidmAuthenticationDefaults.AuthenticationScheme, _ => { });

        // Configured via DI (rather than inline in AddOpenIdConnect above) so CidmOptions is resolved
        // through the validated IOptions<CidmOptions> pipeline instead of being parsed a second time.
        builder.Services.AddOptions<OpenIdConnectOptions>(CidmAuthenticationDefaults.AuthenticationScheme)
            .Configure<IOptions<CidmOptions>>((options, cidmOptions) =>
            {
                var cidm = cidmOptions.Value;

                options.MetadataAddress = cidm.MetadataAddress;
                options.ClientId = cidm.ClientId;
                options.ClientSecret = cidm.ClientSecret;
                options.ResponseType = cidm.ResponseType;
                options.ResponseMode = useLocalHttpFriendlyOidcSettings ? "query" : cidm.ResponseMode;
                options.CallbackPath = cidm.CallbackPath;
                options.SignedOutCallbackPath = cidm.SignedOutCallbackPath;
                options.SaveTokens = true;
                options.UsePkce = true;
                options.GetClaimsFromUserInfoEndpoint = false;

                options.Scope.Clear();
                foreach (var scope in cidm.AllScopes)
                {
                    options.Scope.Add(scope);
                }

                if (useLocalHttpFriendlyOidcSettings)
                {
                    // query mode's callback is a top-level GET redirect - SameSite=Lax (ASP.NET Core's
                    // own default) already covers it, so no override needed for local HTTP.
                    options.CorrelationCookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
                    options.NonceCookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
                }
                else
                {
                    // response_mode=form_post means CIDM's authorization response is a cross-site POST
                    // back to /signin-oidc - SameSite=Lax (or Strict) would silently drop these
                    // transient correlation cookies on that request, breaking state/nonce validation.
                    // Only these two short-lived cookies need SameSite=None; the app's own long-lived
                    // session cookie above does not.
                    options.NonceCookie.SameSite = SameSiteMode.None;
                    options.NonceCookie.SecurePolicy = CookieSecurePolicy.Always;
                    options.CorrelationCookie.SameSite = SameSiteMode.None;
                    options.CorrelationCookie.SecurePolicy = CookieSecurePolicy.Always;
                }

                options.EventsType = typeof(CidmOpenIdConnectEvents);
            });

        return builder;
    }
}

