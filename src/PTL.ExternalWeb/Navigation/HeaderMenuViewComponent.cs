using Microsoft.AspNetCore.Mvc;
using PTL.ApiClient;
using PTL.ExternalWeb.Features.Home;

namespace PTL.ExternalWeb.Navigation;

// A view component (not a partial) because the menu needs the per-year "order submitted" state from the API.
public sealed class HeaderMenuViewComponent(ICreateOrderApiClient createOrderApiClient) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync() =>
        View(await OrderEligibilityLookup.GetAsync(UserClaimsPrincipal, createOrderApiClient, HttpContext.RequestAborted));
}
