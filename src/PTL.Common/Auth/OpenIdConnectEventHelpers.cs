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
    /// <summary>
    /// Resolves the validated principal against the application's own user record via
    /// <paramref name="resolve"/>; denies sign-in (redirecting instead of completing it) when the
    /// resolution says so, or adds the resolved claims to <paramref name="identity"/> otherwise.
    /// No-ops (sign-in proceeds exactly as before this hook existed) when <paramref name="resolve"/>
    /// is <see langword="null"/> - i.e. the consuming app hasn't registered a resolver.
    /// </summary>
    public static async Task ResolveOrDenyAsync(
        TokenValidatedContext context,
        ClaimsIdentity identity,
        Func<ClaimsPrincipal, CancellationToken, Task<IIdentityResolution>>? resolve)
    {
        if (resolve is null)
        {
            return;
        }

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

    /// <summary>Logs then hides raw OIDC failure detail from the user behind a generic error page.</summary>
    public static Task HandleRemoteFailureAsync(RemoteFailureContext context, Action<Exception?> logFailure)
    {
        logFailure(context.Failure);
        context.HandleResponse();
        context.Response.Redirect("/Home/Error");
        return Task.CompletedTask;
    }
}
