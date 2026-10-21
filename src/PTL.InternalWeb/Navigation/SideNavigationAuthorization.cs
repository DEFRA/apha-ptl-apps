using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace PTL.InternalWeb.Navigation;

// Hides navigation entries the user's PTL roles do not satisfy, using the same policies that guard the pages
// (legacy MasterContent skipped menu children whose folder role the user lacked).
public static class SideNavigationAuthorization
{
    public static async Task<IReadOnlyList<SideNavigationItem>> FilterAsync(
        IEnumerable<SideNavigationItem> items,
        ClaimsPrincipal user,
        IAuthorizationService authorization)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(authorization);

        var visible = new List<SideNavigationItem>();
        foreach (var item in items)
        {
            if (item.RequiredPolicy is null || (await authorization.AuthorizeAsync(user, item.RequiredPolicy)).Succeeded)
            {
                visible.Add(item);
            }
        }

        return visible;
    }
}
