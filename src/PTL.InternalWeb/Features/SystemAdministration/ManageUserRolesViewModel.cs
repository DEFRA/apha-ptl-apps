using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using PTL.InternalWeb.Authorization;

namespace PTL.InternalWeb.Features.SystemAdministration;

public sealed class ManageUserRolesViewModel
{
    public List<RoleColumnViewModel> Roles { get; set; } = [];

    public List<UserRoleRowViewModel> Rows { get; set; } = [];

    // Set by the controller only - never taken from the posted form.
    [BindNever]
    public Guid? CurrentUserId { get; set; }

    // Legacy PreventAdminUncheck: the signed-in user's own Admin box cannot be changed (PTL.Core refuses it too).
    public bool IsOwnAdminRole(UserRoleRowViewModel row, RoleColumnViewModel role)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(role);

        return CurrentUserId is { } id && row.UserId == id && string.Equals(role.Name, PtlRoles.Admin, StringComparison.OrdinalIgnoreCase);
    }
}

public sealed class RoleColumnViewModel
{
    public Guid RoleId { get; set; }

    public string Name { get; set; } = string.Empty;
}

public sealed class UserRoleRowViewModel
{
    public Guid UserId { get; set; }

    public string Username { get; set; } = string.Empty;

    public string FriendlyName { get; set; } = string.Empty;

    // Bound from the posted checkbox values (one checkbox per role, all sharing this same
    // indexed name) - matches legacy's per-row CheckBoxUserRole grid, just batched into one Save
    // instead of a save-per-click.
    public List<Guid> SelectedRoleIds { get; set; } = [];

    // Comma-joined role ids as loaded on the page; a single field keeps the form under the 1,024-value limit.
    // Null (absent) means unknown, so the row is always saved. "" must stay "" (a user with no roles), not become null.
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string? OriginalRoleIds { get; set; }

    public bool IsUnchanged()
    {
        if (OriginalRoleIds is null)
        {
            return false;
        }

        var original = new HashSet<Guid>();
        foreach (var part in OriginalRoleIds.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!Guid.TryParse(part, out var roleId))
            {
                return false;
            }

            original.Add(roleId);
        }

        return original.SetEquals(SelectedRoleIds);
    }
}
