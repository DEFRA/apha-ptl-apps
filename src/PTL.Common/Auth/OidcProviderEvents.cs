using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace PTL.Common.Auth;

/// <summary>
/// OpenID Connect events shared by every PTL auth provider (CIDM, Entra ID): resolves the signed-in
/// identity against the app's own user record, trims the session cookie, and hides failure detail from
/// the user. A provider supplies only its resolver call, retained claims and any provider-specific
/// claim mapping or protocol hooks.
/// </summary>
/// <typeparam name="TResolver">The provider's optional per-request user resolver.</typeparam>
/// <typeparam name="TResolution">The outcome the resolver returns.</typeparam>
public abstract class OidcProviderEvents<TResolver, TResolution> : OpenIdConnectEvents
    where TResolver : class
    where TResolution : IIdentityResolution
{
    private readonly ILogger _logger;
    private readonly string _providerName;
    private readonly IReadOnlySet<string> _retainedClaimTypes;

    protected OidcProviderEvents(ILogger logger, string providerName, IReadOnlySet<string> retainedClaimTypes)
    {
        _logger = logger;
        _providerName = providerName;
        _retainedClaimTypes = retainedClaimTypes;
    }

    public override async Task TokenValidated(TokenValidatedContext context)
    {
        if (context.Principal?.Identity is not ClaimsIdentity identity)
        {
            return;
        }

        MapClaims(context.Principal, identity);

        // Resolved per-request rather than constructor-injected (this class is a singleton) - if
        // the consuming app hasn't registered one (or RequestServices isn't set, as in some unit
        // test contexts), sign-in proceeds exactly as before this hook existed.
        var resolver = context.HttpContext.RequestServices?.GetService<TResolver>();
        Func<ClaimsPrincipal, CancellationToken, Task<IIdentityResolution>>? resolve = resolver is null
            ? null
            : async (principal, ct) => await ResolveAsync(resolver, principal, ct);

        await OpenIdConnectEventHelpers.ResolveOrDenyAsync(context, identity, resolve, _retainedClaimTypes);
    }

    public override Task TicketReceived(TicketReceivedContext context) =>
        OpenIdConnectEventHelpers.RemoveAccessTokenAsync(context);

    public override Task RemoteFailure(RemoteFailureContext context) =>
        OpenIdConnectEventHelpers.HandleRemoteFailureAsync(
            context,
            exception => OidcProviderLog.AuthenticationFailed(_logger, _providerName, exception));

    /// <summary>Adjusts the validated identity's claims before the resolver runs. Does nothing by default.</summary>
    protected virtual void MapClaims(ClaimsPrincipal principal, ClaimsIdentity identity)
    {
    }

    /// <summary>Calls the provider's own resolver.</summary>
    protected abstract Task<TResolution> ResolveAsync(TResolver resolver, ClaimsPrincipal principal, CancellationToken cancellationToken);
}

internal static partial class OidcProviderLog
{
    [LoggerMessage(Level = LogLevel.Error, Message = "{Provider} authentication failed")]
    public static partial void AuthenticationFailed(ILogger logger, string provider, Exception? exception);
}
