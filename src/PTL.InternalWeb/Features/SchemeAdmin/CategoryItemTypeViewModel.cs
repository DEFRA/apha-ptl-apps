using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace PTL.InternalWeb.Features.SchemeAdmin;

public sealed class CategoryItemTypeViewModel
{
    public int SelectedYearId { get; set; }

    public IEnumerable<SelectListItem> YearOptions { get; set; } = [];

    public List<CategoryItemTypeRowViewModel> Rows { get; set; } = [];

    // Matches legacy GridViewCategoryItemType.PageSize = 15 exactly (not this app's own
    // PaginationModel.DefaultPageSize of 25, which is unrelated to this screen's legacy parity).
    public const int PageSize = 15;

    public int Page { get; set; } = 1;

    // Count of items for the selected year before paging is applied - drives PaginationModel.
    public int TotalRecords { get; set; }

    // "Add a new category item" section at the bottom of the page - matches legacy's
    // TextboxNewCategoryName/NewCheckboxNoLongerInUse/ButtonAddCategory.
    public CategoryItemTypeAddViewModel Add { get; set; } = new();

    public string? Message { get; set; }

    public bool MessageIsError { get; set; }
}

public sealed class CategoryItemTypeRowViewModel
{
    public Guid CategoryItemTypeId { get; set; }

    public string Name { get; set; } = string.Empty;

    public bool NoLongerInUse { get; set; }

    // Non-zero blocks Edit/Remove - matches legacy's ForeignInstances-driven OnClientClick alert,
    // enforced server-side (see CategoryItemTypeService.UpdateAsync/DeleteAsync).
    public int ForeignInstances { get; set; }

    public string ForeignInstancesString { get; set; } = string.Empty;

    public bool CanEditOrRemove => ForeignInstances == 0;

    // Only one row is ever in edit mode at a time - same convention as Country Management.
    public bool IsEditing { get; set; }

    // Replaces a native confirm() popup with a GOV.UK warning-text + explicit Yes/Cancel step.
    public bool IsConfirmingRemove { get; set; }
}

// Posted by the single "Add" button at the bottom of the list - matches legacy
// ButtonAddCategory_Click.
public sealed class CategoryItemTypeAddViewModel
{
    [Required(ErrorMessage = "Enter a new category name")]
    [StringLength(50, ErrorMessage = "New category name must be 50 characters or fewer")]
    public string Name { get; set; } = string.Empty;

    public bool NoLongerInUse { get; set; }
}

// Posted by the row-level "Save" link against whichever row is in edit mode - matches legacy
// GridViewCategoryItemType_RowUpdating. Bound with an "Edit" prefix so its ModelState errors never
// bleed into the separate "Add" form on the same page - the modern equivalent of legacy's two
// WebForms ValidationGroups ("Existing" vs "New").
public sealed class CategoryItemTypeEditViewModel
{
    [Required]
    public Guid? CategoryItemTypeId { get; set; }

    [Required(ErrorMessage = "Enter a new category name")]
    [StringLength(50, ErrorMessage = "New category name must be 50 characters or fewer")]
    public string Name { get; set; } = string.Empty;

    public bool NoLongerInUse { get; set; }
}
