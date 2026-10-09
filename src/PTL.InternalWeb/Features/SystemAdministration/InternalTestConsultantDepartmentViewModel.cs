using System.ComponentModel.DataAnnotations;

namespace PTL.InternalWeb.Features.SystemAdministration;

public sealed class InternalTestConsultantDepartmentViewModel
{
    public List<InternalTestConsultantRowViewModel> Rows { get; set; } = [];

    public string? Message { get; set; }

    public bool MessageIsError { get; set; }
}

public sealed class InternalTestConsultantRowViewModel
{
    public Guid UserId { get; set; }

    public string Username { get; set; } = string.Empty;

    public string FriendlyName { get; set; } = string.Empty;

    public string Department { get; set; } = string.Empty;

    public bool IsInactive { get; set; }

    public DateTime? InactiveDate { get; set; }

    public bool IsEditing { get; set; }
}

// Posted fields use an "Edit" prefix (see Views/InternalTestConsultantDepartment.cshtml) so
// ModelState errors stay scoped to this one row, matching CountryManagementEditViewModel.
// IsInactive/InactiveDate are round-tripped unchanged from the row being edited - spuUserDept
// updates Department/IsInactive/InactiveDate together, so a department-only save must still pass
// through the row's existing status untouched.
public sealed class InternalTestConsultantEditViewModel
{
    [Required]
    public Guid? UserId { get; set; }

    public string Department { get; set; } = string.Empty;

    [Required]
    public bool? IsInactive { get; set; }

    public DateTime? InactiveDate { get; set; }
}
