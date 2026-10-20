using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;

namespace PTL.Common.Auth;

/// <summary>
/// Shared OpenID Connect event handling - the parts of TokenValidated/RemoteFailure that are
/// identical across every PTL auth provider, differing only in which resolver and log message is
/// used by the caller.
/// </summary>
public static class OpenIdConnectEventHelpers
{
    private const string AccessTokenName = "access_token";

    // Claims every provider's app code reads after sign-in; each provider adds its own on top via RetainedClaims.
    private static readonly string[] CommonRetainedClaimTypes =
    [
        ClaimTypes.NameIdentifier,
        ClaimTypes.Name,
        ClaimTypes.GivenName,
        ClaimTypes.Surname,
        ClaimTypes.Email,
        "sub",
        "name",
        "given_name",
        "family_name",
        "email"
    ];

    /// <summary>
    /// Builds the allow-list of claim types kept on the session cookie: the common identity claims plus the
    /// provider-specific ones the app also reads. Everything else only bloats the cookie past the WAF's 10 KB
    /// cookie-header limit.
    /// </summary>
    public static IReadOnlySet<string> RetainedClaims(params string[] providerSpecificClaimTypes)
    {
        var retained = new HashSet<string>(CommonRetainedClaimTypes, StringComparer.Ordinal);
        retained.UnionWith(providerSpecificClaimTypes);
        return retained;
    }

    /// <summary>
    /// Resolves the validated principal against the application's own user record via
    /// <paramref name="resolve"/>; denies sign-in (redirecting instead of completing it) when the
    /// resolution says so, or adds the resolved claims to <paramref name="identity"/> otherwise.
    /// No-ops (sign-in proceeds exactly as before this hook existed) when <paramref name="resolve"/>
    /// is <see langword="null"/> - i.e. the consuming app hasn't registered a resolver. Unless sign-in
    /// was denied, claims present before resolution whose type is not in
    /// <paramref name="retainedClaimTypes"/> are then removed (claims the resolver adds are kept);
    /// <see langword="null"/> keeps every claim.
    /// </summary>
    public static async Task ResolveOrDenyAsync(
        TokenValidatedContext context,
        ClaimsIdentity identity,
        Func<ClaimsPrincipal, CancellationToken, Task<IIdentityResolution>>? resolve,
        IReadOnlySet<string>? retainedClaimTypes = null)
    {
        var claimsBeforeResolution = identity.Claims.ToList();

        if (resolve is not null)
        {
            var resolution = await resolve(context.Principal!, context.HttpContext.RequestAborted);
            if (!resolution.IsAllowed)
            {
                // Runs before the OIDC handler ever signs the principal into the cookie scheme, so
                // denying here means no local session is ever created for this sign-in attempt.
                context.HandleResponse();
                context.HttpContext.Response.Redirect(resolution.DenialRedirectPath ?? "/");
                return;
            }

            foreach (var (claimType, claimValue) in resolution.Claims ?? new Dictionary<string, string>())
            {
                identity.AddClaim(new Claim(claimType, claimValue));
            }
        }

        if (retainedClaimTypes is null)
        {
            return;
        }

        foreach (var claim in claimsBeforeResolution.Where(claim => !retainedClaimTypes.Contains(claim.Type)))
        {
            identity.RemoveClaim(claim);
        }
    }

    /// <summary>
    /// Drops the access token from the tokens saved on the session cookie. The apps never call an API with it,
    /// and it is the largest avoidable part of the cookie, which must stay under the WAF's 10 KB cookie-header limit.
    /// </summary>
    public static Task RemoveAccessTokenAsync(TicketReceivedContext context)
    {
        if (context.Properties is { } properties)
        {
            properties.StoreTokens(properties.GetTokens().Where(token => token.Name != AccessTokenName).ToList());
        }

        return Task.CompletedTask;
    }

    /// <summary>Logs then hides raw OIDC failure detail from the user behind a generic error page.</summary>
    public static Task HandleRemoteFailureAsync(RemoteFailureContext context, Action<Exception?> logFailure)
    {
        logFailure(context.Failure);
        context.HandleResponse();
        context.Response.Redirect("/Home/Error");
        return Task.CompletedTask;
    }
}
