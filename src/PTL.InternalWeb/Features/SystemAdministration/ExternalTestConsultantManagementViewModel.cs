using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace PTL.InternalWeb.Features.SystemAdministration;

// Manage External Test Consultants - legacy ManageExternalTCs.aspx. The list is held on the page (round-tripped
// through the form, where legacy used ViewState) until the page-level Save/Apply writes it.
public sealed class ExternalTestConsultantManagementViewModel
{
    public List<ExternalTestConsultantRowViewModel> Rows { get; set; } = [];

    // Index into Rows of the one row in edit mode (legacy GridView.EditIndex), with the values typed into it in Edit.
    public int? EditIndex { get; set; }

    public ExternalTestConsultantEditViewModel Edit { get; set; } = new();

    // "Add a Test Consultant" section at the bottom of the page - matches legacy's
    // TextboxNewName/TextboxNewDepartment/TextboxNewEmail/ButtonAdd.
    public ExternalTestConsultantAddViewModel Add { get; set; } = new();

    public string? Message { get; set; }

    // Set by the controller only - never taken from the posted form.
    [BindNever]
    public bool MessageIsError { get; set; }
}

public sealed class ExternalTestConsultantRowViewModel
{
    // Null for a consultant added on this page that has not been saved yet.
    public Guid? ExternalTestConsultantId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Department { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public bool IsInactive { get; set; }

    // Display only: the API stamps the real date when the status change is saved.
    public DateTime? InactiveDate { get; set; }

    public bool IsNew => ExternalTestConsultantId is null;

    // Removed on this page but not yet deleted: the delete happens with the page-level Save/Apply.
    public bool IsRemoved { get; set; }

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

// Values typed into the row in edit mode - bound with an "Edit" prefix so its ModelState errors never bleed into
// the separate "Add a Test Consultant" form on the same page, matching legacy's two WebForms ValidationGroups.
public sealed class ExternalTestConsultantEditViewModel
{
    [Required(ErrorMessage = "Enter a name")]
    public string Name { get; set; } = string.Empty;

    public string Department { get; set; } = string.Empty;

    [Required(ErrorMessage = "Enter an email address")]
    [EmailAddress(ErrorMessage = "Enter an email address in the correct format")]
    public string Email { get; set; } = string.Empty;
}
