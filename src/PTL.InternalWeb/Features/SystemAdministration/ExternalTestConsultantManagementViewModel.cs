using System.ComponentModel.DataAnnotations;

namespace PTL.InternalWeb.Features.SystemAdministration;

public sealed class ExternalTestConsultantManagementViewModel
{
    public List<ExternalTestConsultantRowViewModel> Rows { get; set; } = [];

    // "Add a new consultant" section at the bottom of the page - matches legacy's
    // TextboxNewName/TextboxNewDepartment/TextboxNewEmail/ButtonAdd.
    public ExternalTestConsultantAddViewModel Add { get; set; } = new();

    public string? Message { get; set; }

    public bool MessageIsError { get; set; }
}

public sealed class ExternalTestConsultantRowViewModel
{
    public Guid ExternalTestConsultantId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Department { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public bool IsInactive { get; set; }

    public DateTime? InactiveDate { get; set; }

    // A login has already been generated (SsoId is set) - the Generate Login link only ever
    // shows when this is false, matching the story's Business Rule.
    public bool HasLogin { get; set; }

    // Only one row is ever in edit mode at a time - toggled via the editId query/route value,
    // same convention as CountryManagement/InternalTestConsultantDepartment.
    public bool IsEditing { get; set; }
}

public sealed class ExternalTestConsultantAddViewModel
{
    [Required(ErrorMessage = "Enter a name")]
    public string Name { get; set; } = string.Empty;

    public string Department { get; set; } = string.Empty;

    [Required(ErrorMessage = "Enter an email address")]
    [EmailAddress(ErrorMessage = "Enter an email address in the correct format")]
    public string Email { get; set; } = string.Empty;
}

// Posted by the single "Save" button against whichever row is in edit mode - bound with an
// "Edit" prefix (see Views/ExternalTestConsultantManagement.cshtml) so its ModelState errors
// never bleed into the separate "Add a new consultant" form rendered on the same page, matching
// CountryManagementEditViewModel.
public sealed class ExternalTestConsultantEditViewModel
{
    [Required]
    public Guid? ExternalTestConsultantId { get; set; }

    [Required(ErrorMessage = "Enter a name")]
    public string Name { get; set; } = string.Empty;

    public string Department { get; set; } = string.Empty;

    [Required(ErrorMessage = "Enter an email address")]
    [EmailAddress(ErrorMessage = "Enter an email address in the correct format")]
    public string Email { get; set; } = string.Empty;
}
