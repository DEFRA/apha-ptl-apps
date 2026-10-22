using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace PTL.InternalWeb.Features.SchemeAdmin;

// Implements IReturnTypeItemListViewModel so the shared _ReturnTypeItemAdminList.cshtml partial
// can render this and TestResultItemType from one copy instead of two near-identical ones. Entity-
// bound HTML field name strings here must match SchemeAdminController exactly - see
// IReturnTypeItemListViewModel's remarks.
public sealed class TestMethodItemTypeViewModel : IReturnTypeItemListViewModel
{
    public int SelectedYearId { get; set; }

    public IEnumerable<SelectListItem> YearOptions { get; set; } = [];

    // TextboxSearchMethodItemName - matches legacy TestMethodItemTypeList.aspx.vb's
    // ButtonSearchByMethodItemName_Click (MaxLength 50).
    public string? SearchText { get; set; }

    public List<TestMethodItemTypeRowViewModel> Rows { get; set; } = [];

    // Matches legacy GridViewTestMethodItemType.PageSize = 15 exactly - see §11.3.1 of the parity
    // analysis.
    public const int PageSize = 15;

    public int Page { get; set; } = 1;

    // Count of items for the selected year (after any search filter) before paging is applied -
    // drives PaginationModel.
    public int TotalRecords { get; set; }

    public IEnumerable<SelectListItem> ReturnValueTypeOptions { get; set; } = [];

    // "Add a new test method item" section at the bottom of the page - matches legacy's
    // TextboxNewName/DropDownNewReturnValueType/NewCheckboxNoLongerInUse/ButtonAdd.
    public TestMethodItemTypeAddViewModel Add { get; set; } = new();

    public string? Message { get; set; }

    public bool MessageIsError { get; set; }

    string IReturnTypeItemListViewModel.PageHeading => "Test Method Items";
    string IReturnTypeItemListViewModel.PageDescription => "This page defines the Test Method Items (and their Return Types) that will be available for use when setting up next year's Schemes.";
    string IReturnTypeItemListViewModel.ColumnHeaderLabel => "Test method item name";
    string IReturnTypeItemListViewModel.EmptyMessage => "There are no Test Method Items to display.";
    string IReturnTypeItemListViewModel.AddHeading => "Add a Test Method Item";
    string IReturnTypeItemListViewModel.AddNameLabel => "New test method item name";
    int IReturnTypeItemListViewModel.EditNameMaxLength => 100;
    int IReturnTypeItemListViewModel.PageSize => PageSize;
    string IReturnTypeItemListViewModel.ActionName => "TestMethodItemType";
    string IReturnTypeItemListViewModel.AddActionName => "TestMethodItemTypeAdd";
    string IReturnTypeItemListViewModel.SaveActionName => "TestMethodItemTypeSave";
    string IReturnTypeItemListViewModel.SaveAllNoLongerInUseActionName => "TestMethodItemTypeSaveAllNoLongerInUse";
    string IReturnTypeItemListViewModel.RemoveActionName => "TestMethodItemTypeRemove";
    string IReturnTypeItemListViewModel.EditFormId => "testMethodItemTypeEditForm";
    string IReturnTypeItemListViewModel.EditIdFieldName => "Edit.TestMethodItemTypeId";
    string IReturnTypeItemListViewModel.RemoveIdRouteParamName => "testMethodItemTypeId";
    string IReturnTypeItemListViewModel.PageIdsFieldName => "pageTestMethodItemTypeIds";
    string IReturnTypeItemListViewModel.CheckedIdsFieldName => "checkedTestMethodItemTypeIds";
    IReadOnlyList<IReturnTypeItemRowViewModel> IReturnTypeItemListViewModel.Rows => Rows;
    string IReturnTypeItemListViewModel.AddName => Add.Name;
    bool IReturnTypeItemListViewModel.AddNoLongerInUse => Add.NoLongerInUse;
    string? IReturnTypeItemListViewModel.AddTestReturnValueTypeId => Add.TestReturnValueTypeId?.ToString();
}

public sealed class TestMethodItemTypeRowViewModel : IReturnTypeItemRowViewModel
{
    public Guid TestMethodItemTypeId { get; set; }

    public string Name { get; set; } = string.Empty;

    public Guid TestReturnValueTypeId { get; set; }

    public string TestReturnValueTypeName { get; set; } = string.Empty;

    public bool NoLongerInUse { get; set; }

    // Non-zero blocks Edit/Remove - matches legacy's ForeignInstances-driven OnClientClick alert,
    // enforced server-side (see TestMethodItemTypeService.UpdateAsync/DeleteAsync).
    public int ForeignInstances { get; set; }

    public string ForeignInstancesString { get; set; } = string.Empty;

    public bool CanEditOrRemove => ForeignInstances == 0;

    // Only one row is ever in edit mode at a time - same convention as Test Result Items.
    public bool IsEditing { get; set; }

    // Replaces a native confirm() popup with a GOV.UK warning-text + explicit Yes/Cancel step.
    public bool IsConfirmingRemove { get; set; }

    Guid IReturnTypeItemRowViewModel.ItemId => TestMethodItemTypeId;
}

// Posted by the single "Add" button at the bottom of the list - matches legacy ButtonAdd_Click.
public sealed class TestMethodItemTypeAddViewModel
{
    [Required(ErrorMessage = "Enter a new test method item name")]
    [StringLength(100, ErrorMessage = "New test method item name must be 100 characters or fewer")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Select a return type")]
    public Guid? TestReturnValueTypeId { get; set; }

    // Never JSON-bound here (classic MVC form post) - [JsonRequired] only satisfies the
    // under-posting analyzer; an unticked checkbox omitting the field still binds to false.
    [JsonRequired]
    public bool NoLongerInUse { get; set; }
}

// Posted by the row-level "Save" link against whichever row is in edit mode - matches legacy
// GridViewTestMethodItemType_RowUpdating. Bound with an "Edit" prefix so its ModelState errors
// never bleed into the separate "Add" form on the same page.
public sealed class TestMethodItemTypeEditViewModel
{
    [Required]
    public Guid? TestMethodItemTypeId { get; set; }

    [Required(ErrorMessage = "Enter a new test method item name")]
    [StringLength(100, ErrorMessage = "New test method item name must be 100 characters or fewer")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Select a return type")]
    public Guid? TestReturnValueTypeId { get; set; }

    [JsonRequired]
    public bool NoLongerInUse { get; set; }
}
