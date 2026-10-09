using Microsoft.AspNetCore.Mvc.Rendering;

namespace PTL.InternalWeb.Features.SystemAdministration;

public sealed class RemoveUserViewModel
{
    public List<SelectListItem> UserOptions { get; set; } = [];

    public Guid? SelectedUserId { get; set; }

    // Matches legacy's success-state view swap (CreateUser's IsCreated, MultiView1's
    // UserDeleted view) - distinguishes "haven't removed yet" from "just removed successfully".
    public bool IsRemoved { get; set; }

    public string? Message { get; set; }

    public bool MessageIsError { get; set; }
}
