using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace PTL.InternalWeb.Features.SchemeAdmin;

// Bespoke (does not implement IItemTypeListViewModel) - unlike Category/Test/Criterion Items,
// this screen has an extra Return Type column/dropdown on every row plus a server-side search box,
// so the shared _ItemTypeAdminList.cshtml partial's shape does not fit without changing it for the
// other 3 already-stable screens. See TestResultItemType.cshtml.
public sealed class TestResultItemTypeViewModel
{
    public int SelectedYearId { get; set; }

    public IEnumerable<SelectListItem> YearOptions { get; set; } = [];

    // TextboxSearchResultItemName - matches legacy TestResultItemTypeList.aspx.vb's
    // ButtonSearchByResultItemName_Click (MaxLength 50).
    public string? SearchText { get; set; }

    public List<TestResultItemTypeRowViewModel> Rows { get; set; } = [];

    // Matches legacy GridViewTestResultItemType.PageSize = 15 exactly - see §11.3.1 of the parity
    // analysis.
    public const int PageSize = 15;

    public int Page { get; set; } = 1;

    // Count of items for the selected year (after any search filter) before paging is applied -
    // drives PaginationModel.
    public int TotalRecords { get; set; }

    public IEnumerable<SelectListItem> ReturnValueTypeOptions { get; set; } = [];

    // "Add a new test result item" section at the bottom of the page - matches legacy's
    // TextboxNewName/DropDownNewReturnValueType/NewCheckboxNoLongerInUse/ButtonAdd.
    public TestResultItemTypeAddViewModel Add { get; set; } = new();

    public string? Message { get; set; }

    public bool MessageIsError { get; set; }
}

public sealed class TestResultItemTypeRowViewModel
{
    public Guid TestResultItemTypeId { get; set; }

    public string Name { get; set; } = string.Empty;

    public Guid TestReturnValueTypeId { get; set; }

    public string TestReturnValueTypeName { get; set; } = string.Empty;

    public bool NoLongerInUse { get; set; }

    // Non-zero blocks Edit/Remove - matches legacy's ForeignInstances-driven OnClientClick alert,
    // enforced server-side (see TestResultItemTypeService.UpdateAsync/DeleteAsync).
    public int ForeignInstances { get; set; }

    public string ForeignInstancesString { get; set; } = string.Empty;

    public bool CanEditOrRemove => ForeignInstances == 0;

    // Only one row is ever in edit mode at a time - same convention as Test Types.
    public bool IsEditing { get; set; }

    // Replaces a native confirm() popup with a GOV.UK warning-text + explicit Yes/Cancel step.
    public bool IsConfirmingRemove { get; set; }
}

// Posted by the single "Add" button at the bottom of the list - matches legacy ButtonAdd_Click.
public sealed class TestResultItemTypeAddViewModel
{
    [Required(ErrorMessage = "Enter a new test result item name")]
    [StringLength(100, ErrorMessage = "New test result item name must be 100 characters or fewer")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Select a return type")]
    public Guid? TestReturnValueTypeId { get; set; }

    // Never JSON-bound here (classic MVC form post) - [JsonRequired] only satisfies the
    // under-posting analyzer; an unticked checkbox omitting the field still binds to false.
    [JsonRequired]
    public bool NoLongerInUse { get; set; }
}

// Posted by the row-level "Save" link against whichever row is in edit mode - matches legacy
// GridViewTestResultItemType_RowUpdating. Bound with an "Edit" prefix so its ModelState errors
// never bleed into the separate "Add" form on the same page.
public sealed class TestResultItemTypeEditViewModel
{
    [Required]
    public Guid? TestResultItemTypeId { get; set; }

    [Required(ErrorMessage = "Enter a new test result item name")]
    [StringLength(100, ErrorMessage = "New test result item name must be 100 characters or fewer")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Select a return type")]
    public Guid? TestReturnValueTypeId { get; set; }

    [JsonRequired]
    public bool NoLongerInUse { get; set; }
}
