using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using PTL.ExternalWeb.Features.Account;
using PTL.ExternalWeb.Features.Lab;

namespace PTL.ExternalWeb.Tests.Features.Lab;

public class LabSelectionRequiredFilterTests
{
    private static AuthorizationFilterContext Context(ClaimsPrincipal user, params object[] endpointMetadata)
    {
        var httpContext = new DefaultHttpContext { User = user };
        var descriptor = new ActionDescriptor { EndpointMetadata = endpointMetadata };
        return new AuthorizationFilterContext(new ActionContext(httpContext, new RouteData(), descriptor), []);
    }

    private static ClaimsPrincipal User(string labCount, bool chosen = false, bool authenticated = true)
    {
        var claims = new List<Claim> { new(ExternalUserClaimTypes.LabCount, labCount) };
        if (chosen)
        {
            claims.Add(new Claim(ExternalUserClaimTypes.ParticipantId, Guid.NewGuid().ToString()));
        }

        return new ClaimsPrincipal(authenticated ? new ClaimsIdentity(claims, "Test") : new ClaimsIdentity(claims));
    }

    [Fact]
    public void SeveralLabsNoneChosen_RedirectsToTheLabSelection()
    {
        var context = Context(User("2"));

        new LabSelectionRequiredFilter().OnAuthorization(context);

        var redirect = Assert.IsType<RedirectToActionResult>(context.Result);
        Assert.Equal(("Select", "Lab"), (redirect.ActionName, redirect.ControllerName));
    }

    [Fact]
    public void LabAlreadyChosen_LeavesTheRequestAlone()
    {
        var context = Context(User("2", chosen: true));

        new LabSelectionRequiredFilter().OnAuthorization(context);

        Assert.Null(context.Result);
    }

    [Fact]
    public void OneLab_LeavesTheRequestAlone()
    {
        var context = Context(User("1"));

        new LabSelectionRequiredFilter().OnAuthorization(context);

        Assert.Null(context.Result);
    }

    [Fact]
    public void NotSignedIn_LeavesTheRequestAloneForAuthenticationToHandle()
    {
        var context = Context(User("2", authenticated: false));

        new LabSelectionRequiredFilter().OnAuthorization(context);

        Assert.Null(context.Result);
    }

    [Fact]
    public void ActionMarkedToSkipSelection_IsLeftAlone()
    {
        var context = Context(User("2"), new SkipLabSelectionAttribute());

        new LabSelectionRequiredFilter().OnAuthorization(context);

        Assert.Null(context.Result);
    }

    [Fact]
    public void AnonymousAction_IsLeftAlone()
    {
        var context = Context(User("2"), new AllowAnonymousAttribute());

        new LabSelectionRequiredFilter().OnAuthorization(context);

        Assert.Null(context.Result);
    }
}
