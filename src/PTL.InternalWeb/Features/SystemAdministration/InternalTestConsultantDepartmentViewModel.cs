using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace PTL.InternalWeb.Features.SystemAdministration;

// Internal Test Consultant Department - legacy TCDepartment.aspx. Department edits and Active/Inactive toggles stay
// on the page (round-tripped through the form, where legacy used ViewState) until the page-level Save writes them.
public sealed class InternalTestConsultantDepartmentViewModel
{
    public List<InternalTestConsultantRowViewModel> Rows { get; set; } = [];

    // Index into Rows of the one row in edit mode (legacy GridView.EditIndex), with the values typed into it in Edit.
    public int? EditIndex { get; set; }

    public InternalTestConsultantEditViewModel Edit { get; set; } = new();

    public string? Message { get; set; }

    // Set by the controller only - never taken from the posted form.
    [BindNever]
    public bool MessageIsError { get; set; }
}

public sealed class InternalTestConsultantRowViewModel
{
    public Guid? UserId { get; set; }

    public string Username { get; set; } = string.Empty;

    public string FriendlyName { get; set; } = string.Empty;

    public string Department { get; set; } = string.Empty;

    public bool IsInactive { get; set; }

    // Display only: Save works the date out from the stored status, never from the posted value.
    public DateTime? InactiveDate { get; set; }

    public bool IsEditing { get; set; }
}

// Value typed into the Department box of the row in edit mode (legacy TxtDepartment, MaxLength 50).
public sealed class InternalTestConsultantEditViewModel
{
    [StringLength(50, ErrorMessage = "Department must be 50 characters or fewer")]
    public string Department { get; set; } = string.Empty;
}
