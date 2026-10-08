namespace PTL.InternalWeb.Features.SystemAdministration;

public sealed class ManageUserRolesViewModel
{
    public List<RoleColumnViewModel> Roles { get; set; } = [];

    public List<UserRoleRowViewModel> Rows { get; set; } = [];

    public string? Message { get; set; }

    public bool MessageIsError { get; set; }
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
}
