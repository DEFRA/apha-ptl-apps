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

    // Posted as one uniquely named checkbox per role (Rows[i].RoleSelections[roleId]=true), like the separately
    // named boxes of legacy's per-row CheckBoxUserRole grid. Boxes sharing one name read as an unfenced checkbox
    // group to accessibility checkers, and each box here is a standalone yes/no already labelled by its row and column.
    public Dictionary<Guid, bool> RoleSelections { get; set; } = [];

    [BindNever]
    public List<Guid> SelectedRoleIds
    {
        get => [.. RoleSelections.Where(selection => selection.Value).Select(selection => selection.Key)];
        set => RoleSelections = value.Distinct().ToDictionary(id => id, _ => true);
    }
}
