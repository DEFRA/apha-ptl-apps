using Microsoft.AspNetCore.Http;

namespace PTL.Common.Audit;

/// <summary>
/// Forwards the signed-in actor ID to PTL.Api on outgoing calls. PTL.Api has no identity context
/// of its own (it is only reachable from the web front-ends), so this is the same trust boundary
/// as the explicit actingUserId request fields already used elsewhere.
/// </summary>
/// <param name="httpContextAccessor">Accessor for the current request, if there is one.</param>
public sealed class ActingUserDelegatingHandler(IHttpContextAccessor httpContextAccessor) : DelegatingHandler
{
    private const string ExternalTestConsultantClaimType = "testConsultantId";

    /// <inheritdoc />
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var user = httpContextAccessor.HttpContext?.User;
        var actingUserId = user?.FindFirst(ActingUser.ClaimType)?.Value
            ?? user?.FindFirst(ExternalTestConsultantClaimType)?.Value;

        if (!string.IsNullOrEmpty(actingUserId))
        {
            request.Headers.Remove(ActingUser.HeaderName);
            request.Headers.Add(ActingUser.HeaderName, actingUserId);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
