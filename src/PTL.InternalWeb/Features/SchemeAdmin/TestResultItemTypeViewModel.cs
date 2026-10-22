using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace PTL.InternalWeb.Features.SchemeAdmin;

// Implements IReturnTypeItemListViewModel so the shared _ReturnTypeItemAdminList.cshtml partial
// can render this and TestMethodItemType from one copy instead of two near-identical ones. Entity-
// bound HTML field name strings here must match SchemeAdminController exactly - see
// IReturnTypeItemListViewModel's remarks.
public sealed class TestResultItemTypeViewModel : IReturnTypeItemListViewModel
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

    string IReturnTypeItemListViewModel.PageHeading => "Test Result Items";
    string IReturnTypeItemListViewModel.PageDescription => "This page defines the Test Result Items (and their Return Types) that will be available for use when setting up next year's Schemes.";
    string IReturnTypeItemListViewModel.ColumnHeaderLabel => "Test result item name";
    string IReturnTypeItemListViewModel.EmptyMessage => "There are no Test Result Items to display.";
    string IReturnTypeItemListViewModel.AddHeading => "Add a Test Result Item";
    string IReturnTypeItemListViewModel.AddNameLabel => "New test result item name";
    int IReturnTypeItemListViewModel.EditNameMaxLength => 100;
    int IReturnTypeItemListViewModel.PageSize => PageSize;
    string IReturnTypeItemListViewModel.ActionName => "TestResultItemType";
    string IReturnTypeItemListViewModel.AddActionName => "TestResultItemTypeAdd";
    string IReturnTypeItemListViewModel.SaveActionName => "TestResultItemTypeSave";
    string IReturnTypeItemListViewModel.SaveAllNoLongerInUseActionName => "TestResultItemTypeSaveAllNoLongerInUse";
    string IReturnTypeItemListViewModel.RemoveActionName => "TestResultItemTypeRemove";
    string IReturnTypeItemListViewModel.EditFormId => "testResultItemTypeEditForm";
    string IReturnTypeItemListViewModel.EditIdFieldName => "Edit.TestResultItemTypeId";
    string IReturnTypeItemListViewModel.RemoveIdRouteParamName => "testResultItemTypeId";
    string IReturnTypeItemListViewModel.PageIdsFieldName => "pageTestResultItemTypeIds";
    string IReturnTypeItemListViewModel.CheckedIdsFieldName => "checkedTestResultItemTypeIds";
    IReadOnlyList<IReturnTypeItemRowViewModel> IReturnTypeItemListViewModel.Rows => Rows;
    string IReturnTypeItemListViewModel.AddName => Add.Name;
    bool IReturnTypeItemListViewModel.AddNoLongerInUse => Add.NoLongerInUse;
    string? IReturnTypeItemListViewModel.AddTestReturnValueTypeId => Add.TestReturnValueTypeId?.ToString();
}

public sealed class TestResultItemTypeRowViewModel : IReturnTypeItemRowViewModel
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

    Guid IReturnTypeItemRowViewModel.ItemId => TestResultItemTypeId;
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
