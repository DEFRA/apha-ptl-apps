using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;

namespace PTL.ExternalWeb.Tests.TestSupport;

// Minimal IUrlHelper so controller unit tests can exercise Url.IsLocalUrl without a real routing context.
internal sealed class FakeUrlHelper : IUrlHelper
{
    public ActionContext ActionContext { get; } = new();

    public string? Action(UrlActionContext actionContext) => throw new NotImplementedException();

    public string? Content(string? contentPath) => contentPath;

    public bool IsLocalUrl(string? url) =>
        !string.IsNullOrEmpty(url) && url.StartsWith('/') && !url.StartsWith("//", StringComparison.Ordinal) && !url.StartsWith("/\\", StringComparison.Ordinal);

    public string? Link(string? routeName, object? values) => throw new NotImplementedException();

    public string? RouteUrl(UrlRouteContext routeContext) => throw new NotImplementedException();
}
