using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace PTL.ApiClient;

/// <summary>
/// Gates a route behind a shared-secret header instead of real auth/authz -
/// this exists purely to stop bots/scanners from probing (and needlessly
/// triggering) the Api-connectivity check, not as an access-control
/// boundary. Matters more here than in PTL.Api: the web front-ends are
/// internet-facing, so this endpoint really is reachable by internet bots
/// unless gated. Note: an AWS ALB target-group health check cannot send
/// custom headers, so this only works for callers that can set one (a curl
/// from an ops box, an internal monitoring canary) - never point an
/// ALB/ECS health check directly at a key-gated route.
/// </summary>
public sealed class ReadinessKeyFilter(IConfiguration configuration) : IEndpointFilter
{
    public const string HeaderName = "X-Readiness-Key";

    public ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var expectedKey = configuration["HealthCheck:ReadinessKey"];
        var providedKey = context.HttpContext.Request.Headers[HeaderName].ToString();

        if (string.IsNullOrEmpty(expectedKey) || !KeysMatch(providedKey, expectedKey))
        {
            // 404, not 401/403 - a route that demands auth confirms its own
            // existence to a scanner; this should look like nothing is here.
            return ValueTask.FromResult<object?>(Results.NotFound());
        }

        return next(context);
    }

    private static bool KeysMatch(string provided, string expected)
    {
        var providedBytes = Encoding.UTF8.GetBytes(provided);
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        return providedBytes.Length == expectedBytes.Length
            && CryptographicOperations.FixedTimeEquals(providedBytes, expectedBytes);
    }
}
