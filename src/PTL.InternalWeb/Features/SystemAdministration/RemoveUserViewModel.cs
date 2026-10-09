using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace PTL.InternalWeb.Features.SystemAdministration;

public sealed class RemoveUserViewModel
{
    public List<SelectListItem> UserOptions { get; set; } = [];

    public Guid? SelectedUserId { get; set; }

    // Matches legacy's success-state view swap (CreateUser's IsCreated, MultiView1's
    // UserDeleted view) - distinguishes "haven't removed yet" from "just removed successfully".
    // Server-computed display state, never posted by the view - excluded from binding.
    [BindNever]
    public bool IsRemoved { get; set; }

    public string? Message { get; set; }

    [BindNever]
    public bool MessageIsError { get; set; }
}
