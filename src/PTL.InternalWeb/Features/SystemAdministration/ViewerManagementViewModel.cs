using System.ComponentModel.DataAnnotations;

namespace PTL.InternalWeb.Features.SystemAdministration;

public sealed class ViewerManagementViewModel
{
    public List<ViewerRowViewModel> Rows { get; set; } = [];

    // "Add a viewer" section at the bottom of the page - matches legacy's
    // TextboxNewName/TextboxNewEmail/ButtonAdd.
    public ViewerAddViewModel Add { get; set; } = new();

    public string? Message { get; set; }

    public bool MessageIsError { get; set; }
}

public sealed class ViewerRowViewModel
{
    public Guid ViewerId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    // A login has already been generated (SsoId is set) - the Generate Login link only ever
    // shows when this is false, matching the story's Business Rule.
    public bool HasLogin { get; set; }

    // Only one row is ever in edit mode at a time - toggled via the editId query/route value,
    // same convention as CountryManagement/ExternalTestConsultantManagement.
    public bool IsEditing { get; set; }

    // Pre-built, JS-string-escaped confirm() text for the Remove button - always at least "Are
    // you sure...?", enriched with the assigned schemes/participants listing when any exist
    // (matches legacy's GridViewTC_RowDataBound warning, built server-side here instead of in
    // markup so the JS-escaping only has to be done in one place).
    public string RemoveConfirmMessage { get; set; } = string.Empty;
}

public sealed class ViewerAddViewModel
{
    [Required(ErrorMessage = "Enter a name")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Enter an email address")]
    [EmailAddress(ErrorMessage = "Enter an email address in the correct format")]
    public string Email { get; set; } = string.Empty;
}

// Posted by the single "Save" button against whichever row is in edit mode - bound with an
// "Edit" prefix (see Views/ViewerManagement.cshtml) so its ModelState errors never bleed into the
// separate "Add a viewer" form rendered on the same page, matching CountryManagementEditViewModel.
public sealed class ViewerEditViewModel
{
    [Required]
    public Guid? ViewerId { get; set; }

    [Required(ErrorMessage = "Enter a name")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Enter an email address")]
    [EmailAddress(ErrorMessage = "Enter an email address in the correct format")]
    public string Email { get; set; } = string.Empty;
}
