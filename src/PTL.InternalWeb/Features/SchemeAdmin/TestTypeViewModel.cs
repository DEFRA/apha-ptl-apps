using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace PTL.InternalWeb.Features.SchemeAdmin;

public sealed class TestTypeViewModel : IItemTypeListViewModel
{
    public int SelectedYearId { get; set; }

    public IEnumerable<SelectListItem> YearOptions { get; set; } = [];

    public List<TestTypeRowViewModel> Rows { get; set; } = [];

    // Matches legacy GridViewTestType.PageSize = 15 exactly - see §11.3.1 of the parity analysis.
    public const int PageSize = 15;

    public int Page { get; set; } = 1;

    // Count of items for the selected year before paging is applied - drives PaginationModel.
    public int TotalRecords { get; set; }

    // "Add a new test type" section at the bottom of the page - matches legacy's
    // TextboxNewName/NewCheckboxNoLongerInUse/ButtonAdd.
    public TestTypeAddViewModel Add { get; set; } = new();

    public string? Message { get; set; }

    public bool MessageIsError { get; set; }

    // IItemTypeListViewModel: shared _ItemTypeAdminList.cshtml rendering contract - see
    // CategoryItemTypeViewModel's remarks. Entity-bound field name strings here must match
    // SchemeAdminController exactly.
    string IItemTypeListViewModel.PageHeading => "Test Types";
    string IItemTypeListViewModel.PageDescription => "This page defines the types of Test that will be available for use when setting up next year's Schemes.";
    string IItemTypeListViewModel.ColumnHeaderLabel => "Test type name";
    string IItemTypeListViewModel.EmptyMessage => "There are no Test Types to display.";
    string IItemTypeListViewModel.AddHeading => "Add a Test Type";
    string IItemTypeListViewModel.AddNameLabel => "New test type name";
    int IItemTypeListViewModel.EditNameMaxLength => 30;
    int IItemTypeListViewModel.PageSize => PageSize;
    string IItemTypeListViewModel.ActionName => "TestType";
    string IItemTypeListViewModel.AddActionName => "TestTypeAdd";
    string IItemTypeListViewModel.SaveActionName => "TestTypeSave";
    string IItemTypeListViewModel.SaveAllNoLongerInUseActionName => "TestTypeSaveAllNoLongerInUse";
    string IItemTypeListViewModel.RemoveActionName => "TestTypeRemove";
    string IItemTypeListViewModel.EditFormId => "testTypeEditForm";
    string IItemTypeListViewModel.EditIdFieldName => "Edit.TestTypeId";
    string IItemTypeListViewModel.RemoveIdRouteParamName => "testTypeId";
    string IItemTypeListViewModel.PageIdsFieldName => "pageTestTypeIds";
    string IItemTypeListViewModel.CheckedIdsFieldName => "checkedTestTypeIds";
    IReadOnlyList<IItemTypeRowViewModel> IItemTypeListViewModel.Rows => Rows;
    string IItemTypeListViewModel.AddName => Add.Name;
    bool IItemTypeListViewModel.AddNoLongerInUse => Add.NoLongerInUse;
}

public sealed class TestTypeRowViewModel : IItemTypeRowViewModel
{
    public Guid TestTypeId { get; set; }

    public string Name { get; set; } = string.Empty;

    public bool NoLongerInUse { get; set; }

    // Non-zero blocks Edit/Remove - matches legacy's ForeignInstances-driven OnClientClick alert,
    // enforced server-side (see TestTypeService.UpdateAsync/DeleteAsync).
    public int ForeignInstances { get; set; }

    public string ForeignInstancesString { get; set; } = string.Empty;

    public bool CanEditOrRemove => ForeignInstances == 0;

    // Only one row is ever in edit mode at a time - same convention as Category Items.
    public bool IsEditing { get; set; }

    // Replaces a native confirm() popup with a GOV.UK warning-text + explicit Yes/Cancel step.
    public bool IsConfirmingRemove { get; set; }

    Guid IItemTypeRowViewModel.ItemId => TestTypeId;
}

// Posted by the single "Add" button at the bottom of the list - matches legacy ButtonAdd_Click.
public sealed class TestTypeAddViewModel
{
    [Required(ErrorMessage = "Enter a new test type name")]
    [StringLength(30, ErrorMessage = "New test type name must be 30 characters or fewer")]
    public string Name { get; set; } = string.Empty;

    // Never JSON-bound here (classic MVC form post) - [JsonRequired] only satisfies the
    // under-posting analyzer; an unticked checkbox omitting the field still binds to false.
    [JsonRequired]
    public bool NoLongerInUse { get; set; }
}

// Posted by the row-level "Save" link against whichever row is in edit mode - matches legacy
// GridViewTestType_RowUpdating. Bound with an "Edit" prefix so its ModelState errors never bleed
// into the separate "Add" form on the same page.
public sealed class TestTypeEditViewModel
{
    [Required]
    public Guid? TestTypeId { get; set; }

    [Required(ErrorMessage = "Enter a new test type name")]
    [StringLength(30, ErrorMessage = "New test type name must be 30 characters or fewer")]
    public string Name { get; set; } = string.Empty;

    [JsonRequired]
    public bool NoLongerInUse { get; set; }
}
