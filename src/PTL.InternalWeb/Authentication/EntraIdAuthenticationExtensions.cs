using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace PTL.InternalWeb.Authentication;

/// <summary>
/// Wires up Microsoft Entra ID single sign-on for PTL.InternalWeb: a cookie scheme holds the
/// local authenticated session, and the OpenID Connect scheme performs the SSO handshake with
/// Entra ID using the hybrid ("code id_token") flow.
/// </summary>
public static class EntraIdAuthenticationExtensions
{
    /// <summary>The persistent auth session cookie name, scoped to this app to avoid collisions with PTL.ExternalWeb.</summary>
    public const string AuthCookieName = ".PTL.InternalWeb.Auth";

    public static IServiceCollection AddEntraIdAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var entraIdOptions = configuration.GetSection(EntraIdOptions.SectionName).Get<EntraIdOptions>() ?? new EntraIdOptions();
        ValidateConfiguration(entraIdOptions);

        services.AddAuthentication(options =>
        {
            options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
            options.DefaultSignOutScheme = OpenIdConnectDefaults.AuthenticationScheme;
        })
        .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
        {
            options.Cookie.Name = AuthCookieName;
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.ExpireTimeSpan = TimeSpan.FromHours(8);
            options.SlidingExpiration = true;
            options.AccessDeniedPath = "/Account/AccessDenied";
        })
        .AddOpenIdConnect(OpenIdConnectDefaults.AuthenticationScheme, options =>
        {
            options.Authority = entraIdOptions.Authority;
            options.ClientId = entraIdOptions.ClientId;
            options.ClientSecret = entraIdOptions.ClientSecret;
            options.RequireHttpsMetadata = true;

            // Hybrid flow: the id_token is returned on the front channel (the "implicit" leg),
            // establishing the user's identity immediately, while the authorization code is
            // exchanged for the access/refresh tokens on the back channel using the confidential
            // client secret - the tokens themselves are never exposed to the browser. This is the
            // Microsoft identity platform recommended flow for a confidential, server-rendered app.
            options.ResponseType = OpenIdConnectResponseType.CodeIdToken;
            options.ResponseMode = OpenIdConnectResponseMode.FormPost;
            options.UsePkce = true;
            options.SaveTokens = true;
            options.GetClaimsFromUserInfoEndpoint = true;

            // Fixed redirect URIs required by the Entra ID App Registration.
            options.CallbackPath = "/signin-oidc";
            options.SignedOutCallbackPath = "/signout-oidc";

            options.Scope.Clear();
            options.Scope.Add("openid");
            options.Scope.Add("profile");
            options.Scope.Add("email");

            // Use the raw v2.0 claim names ("name", "oid", ...) instead of the legacy long-form
            // ClaimTypes.* URIs the handler maps to by default.
            options.MapInboundClaims = false;
            options.TokenValidationParameters.NameClaimType = "name";
            options.TokenValidationParameters.RoleClaimType = "roles";

            // The correlation/nonce cookies round-trip via a cross-site POST from Entra ID back to
            // /signin-oidc (ResponseMode=form_post), so they must be SameSite=None to survive it;
            // browsers require Secure whenever SameSite=None is used.
            options.CorrelationCookie.SameSite = SameSiteMode.None;
            options.CorrelationCookie.SecurePolicy = CookieSecurePolicy.Always;
            options.NonceCookie.SameSite = SameSiteMode.None;
            options.NonceCookie.SecurePolicy = CookieSecurePolicy.Always;

            options.Events.OnRemoteFailure = HandleRemoteFailure;
        });

        return services;
    }

    // A failed/cancelled sign-in (e.g. the user declines consent) must not surface the raw
    // exception to the browser - redirect to a friendly, anonymous-accessible page instead.
    internal static Task HandleRemoteFailure(RemoteFailureContext context)
    {
        context.HandleResponse();
        context.Response.Redirect("/Account/AccessDenied");
        return Task.CompletedTask;
    }

    private static void ValidateConfiguration(EntraIdOptions options)
    {
        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(options.TenantId))
        {
            missing.Add($"{EntraIdOptions.SectionName}:TenantId");
        }

        if (string.IsNullOrWhiteSpace(options.ClientId))
        {
            missing.Add($"{EntraIdOptions.SectionName}:ClientId");
        }

        if (string.IsNullOrWhiteSpace(options.ClientSecret))
        {
            missing.Add($"{EntraIdOptions.SectionName}:ClientSecret");
        }

        if (missing.Count > 0)
        {
            throw new InvalidOperationException(
                $"Missing required Entra ID configuration value(s): {string.Join(", ", missing)}. " +
                "ClientSecret must be supplied via an environment variable or AWS Secrets Manager, never committed to appsettings.json.");
        }
    }
}
