using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;

namespace PTL.ExternalWeb.Features.LoadTesting;

/// <summary>Sends an unauthenticated visitor to the load-testing sign-in instead of CIDM while the switch is on.</summary>
public sealed class LoadTestingLoginRedirectFilter(IOptions<LoadTestingOptions> options) : IResourceFilter
{
    public void OnResourceExecuting(ResourceExecutingContext context)
    {
        if (!options.Value.Enabled || context.HttpContext.User.Identity?.IsAuthenticated == true)
        {
            return;
        }

        var route = context.ActionDescriptor.RouteValues;
        var isAccountLogin = route.TryGetValue("controller", out var controller) && controller == "Account"
            && route.TryGetValue("action", out var action) && action == "Login";

        if (isAccountLogin && HttpMethods.IsGet(context.HttpContext.Request.Method))
        {
            var returnUrl = context.HttpContext.Request.Query["returnUrl"].FirstOrDefault();
            context.Result = new RedirectToActionResult(nameof(LoadTestingController.Login), "LoadTesting", new { returnUrl });
        }
    }

    public void OnResourceExecuted(ResourceExecutedContext context)
    {
    }
}
