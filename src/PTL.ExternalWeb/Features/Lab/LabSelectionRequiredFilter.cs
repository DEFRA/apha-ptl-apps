using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using PTL.ExternalWeb.Features.Account;

namespace PTL.ExternalWeb.Features.Lab;

/// <summary>Marks a controller or action that must work before a lab has been chosen (the lab selection itself, sign-out).</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class SkipLabSelectionAttribute : Attribute;

/// <summary>
/// Sends a Participant or Lab User with several labs who has not chosen one to the lab selection page, wherever they are
/// heading: every page they could reach is about one lab, so none can be shown until they pick. Anonymous pages and those
/// marked <see cref="SkipLabSelectionAttribute"/> are left alone.
/// </summary>
public sealed class LabSelectionRequiredFilter : IAuthorizationFilter
{
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var user = context.HttpContext.User;
        if (user.Identity?.IsAuthenticated != true || !user.MustSelectLab())
        {
            return;
        }

        if (context.ActionDescriptor.EndpointMetadata.Any(metadata => metadata is SkipLabSelectionAttribute or IAllowAnonymous))
        {
            return;
        }

        context.Result = new RedirectToActionResult(nameof(LabController.Select), "Lab", routeValues: null);
    }
}
