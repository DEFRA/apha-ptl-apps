using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace PTL.InternalWeb.Features.SchemeAdmin;

public sealed class CriterionItemTypeViewModel
{
    public int SelectedYearId { get; set; }

    public IEnumerable<SelectListItem> YearOptions { get; set; } = [];

    public List<CriterionItemTypeRowViewModel> Rows { get; set; } = [];

    // Matches legacy GridViewCriterionItemType.PageSize = 15 exactly - see §11.3.1 of the parity analysis.
    public const int PageSize = 15;

    public int Page { get; set; } = 1;

    // Count of items for the selected year before paging is applied - drives PaginationModel.
    public int TotalRecords { get; set; }

    // "Add a new criterion item" section at the bottom of the page - matches legacy's
    // TextboxNewName/NewCheckboxNoLongerInUse/ButtonAdd.
    public CriterionItemTypeAddViewModel Add { get; set; } = new();

    public string? Message { get; set; }

    public bool MessageIsError { get; set; }
}

public sealed class CriterionItemTypeRowViewModel
{
    public Guid CriterionItemTypeId { get; set; }

    public string Name { get; set; } = string.Empty;

    public bool NoLongerInUse { get; set; }

    // Non-zero blocks Edit/Remove - matches legacy's ForeignInstances-driven OnClientClick alert,
    // enforced server-side (see CriterionItemTypeService.UpdateAsync/DeleteAsync).
    public int ForeignInstances { get; set; }

    public string ForeignInstancesString { get; set; } = string.Empty;

    public bool CanEditOrRemove => ForeignInstances == 0;

    // Only one row is ever in edit mode at a time - same convention as Category Items/Test Types.
    public bool IsEditing { get; set; }

    // Replaces a native confirm() popup with a GOV.UK warning-text + explicit Yes/Cancel step.
    public bool IsConfirmingRemove { get; set; }
}

// Posted by the single "Add" button at the bottom of the list - matches legacy ButtonAdd_Click.
public sealed class CriterionItemTypeAddViewModel
{
    [Required(ErrorMessage = "Enter a new criterion item name")]
    [StringLength(50, ErrorMessage = "New criterion item name must be 50 characters or fewer")]
    public string Name { get; set; } = string.Empty;

    public bool NoLongerInUse { get; set; }
}

// Posted by the row-level "Save" link against whichever row is in edit mode - matches legacy
// GridViewCriterionItemType_RowUpdating. Bound with an "Edit" prefix so its ModelState errors
// never bleed into the separate "Add" form on the same page.
public sealed class CriterionItemTypeEditViewModel
{
    [Required]
    public Guid? CriterionItemTypeId { get; set; }

    [Required(ErrorMessage = "Enter a new criterion item name")]
    [StringLength(50, ErrorMessage = "New criterion item name must be 50 characters or fewer")]
    public string Name { get; set; } = string.Empty;

    public bool NoLongerInUse { get; set; }
}
