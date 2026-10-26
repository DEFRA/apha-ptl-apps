using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace PTL.InternalWeb.Features.SystemAdministration;

public sealed class ViewerManagementViewModel
{
    // Legacy held this list in ViewState until the page-level Save/Apply wrote it to the database;
    // here it is round-tripped through the page's form (Rows[i].*) instead.
    public List<ViewerRowViewModel> Rows { get; set; } = [];

    // Index into Rows of the one row in edit mode (legacy GridView.EditIndex), with the values typed
    // into it in Edit. Only applied to Rows by the row's own Save (the "update" command).
    public int? EditIndex { get; set; }

    public ViewerEditViewModel Edit { get; set; } = new();

    // "Add a Viewer" section at the bottom of the page - matches legacy's
    // TextboxNewName/TextboxNewEmail/ButtonAdd.
    public ViewerAddViewModel Add { get; set; } = new();

    public string? Message { get; set; }

    // Set by the controller only - never taken from the posted form.
    [BindNever]
    public bool MessageIsError { get; set; }

    // Dependency detail lines (assigned schemes/participants) shown under Message.
    public List<string> MessageDetails { get; set; } = [];
}

public sealed class ViewerRowViewModel
{
    // Null for a viewer added on this page that has not been saved yet.
    public Guid? ViewerId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public bool IsNew => ViewerId is null;

    // Removed on this page but not yet deleted: the delete happens with the page-level Save/Apply.
    public bool IsRemoved { get; set; }

    public bool IsEditing { get; set; }
}

public sealed class ViewerAddViewModel
{
    [Required(ErrorMessage = "Enter a name")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Enter an email address")]
    [EmailAddress(ErrorMessage = "Enter an email address in the correct format")]
    public string Email { get; set; } = string.Empty;
}

// Values typed into the row in edit mode - bound with an "Edit" prefix so its ModelState errors never
// bleed into the separate "Add a Viewer" form on the same page, matching legacy's two WebForms
// ValidationGroups ("Edit" vs "New").
public sealed class ViewerEditViewModel
{
    [Required(ErrorMessage = "Enter a name")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Enter an email address")]
    [EmailAddress(ErrorMessage = "Enter an email address in the correct format")]
    public string Email { get; set; } = string.Empty;
}
