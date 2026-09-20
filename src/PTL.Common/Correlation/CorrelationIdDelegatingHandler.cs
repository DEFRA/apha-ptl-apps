using Microsoft.AspNetCore.Http;

namespace PTL.Common.Correlation;

/// <summary>
/// Propagates the current request's correlation ID onto outgoing <see cref="HttpClient"/> calls,
/// so a downstream service (e.g. PTL.Api, called from PTL.InternalWeb/PTL.ExternalWeb) logs under
/// the same ID.
/// </summary>
/// <param name="httpContextAccessor">Accessor for the current request, if there is one.</param>
public sealed class CorrelationIdDelegatingHandler(IHttpContextAccessor httpContextAccessor) : DelegatingHandler
{
    /// <inheritdoc />
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var correlationId = httpContextAccessor.HttpContext?
            .Items[CorrelationIdMiddlewareExtensions.HeaderName]?.ToString();

        if (!string.IsNullOrEmpty(correlationId))
        {
            request.Headers.Remove(CorrelationIdMiddlewareExtensions.HeaderName);
            request.Headers.Add(CorrelationIdMiddlewareExtensions.HeaderName, correlationId);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
