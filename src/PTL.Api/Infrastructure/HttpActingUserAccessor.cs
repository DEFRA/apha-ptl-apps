using Microsoft.AspNetCore.Http;
using PTL.Common.Audit;
using PTL.Core.Audit;

namespace PTL.Api.Infrastructure;

// Reads the acting user PTL.InternalWeb forwards (see ActingUserDelegatingHandler). Absent or
// malformed means "unknown caller" - the change is still audited, just without an acting user.
public sealed class HttpActingUserAccessor(IHttpContextAccessor httpContextAccessor) : IActingUserAccessor
{
    public Guid? ActingUserId =>
        Guid.TryParse(httpContextAccessor.HttpContext?.Request.Headers[ActingUser.HeaderName].ToString(), out var id) ? id : null;
}
