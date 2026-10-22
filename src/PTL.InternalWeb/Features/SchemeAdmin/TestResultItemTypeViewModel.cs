using System.ComponentModel.DataAnnotations;

namespace PTL.InternalWeb.Features.SchemeAdmin;

// See ReturnTypeItemListViewModelBase's remarks for why the shared rendering/field-name
// boilerplate lives there rather than here.
public sealed class TestResultItemTypeViewModel()
    : ReturnTypeItemListViewModelBase<TestResultItemTypeRowViewModel>(new(
        PageHeading: "Test Result Items",
        PageDescription: "This page defines the Test Result Items (and their Return Types) that will be available for use when setting up next year's Schemes.",
        ColumnHeaderLabel: "Test result item name",
        EmptyMessage: "There are no Test Result Items to display.",
        AddHeading: "Add a Test Result Item",
        AddNameLabel: "New test result item name",
        ActionName: "TestResultItemType",
        AddActionName: "TestResultItemTypeAdd",
        SaveActionName: "TestResultItemTypeSave",
        SaveAllNoLongerInUseActionName: "TestResultItemTypeSaveAllNoLongerInUse",
        RemoveActionName: "TestResultItemTypeRemove",
        EditFormId: "testResultItemTypeEditForm",
        EditIdFieldName: "Edit.TestResultItemTypeId",
        RemoveIdRouteParamName: "testResultItemTypeId",
        PageIdsFieldName: "pageTestResultItemTypeIds",
        CheckedIdsFieldName: "checkedTestResultItemTypeIds"))
{
    // "Add a new test result item" section at the bottom of the page - matches legacy's
    // TextboxNewName/DropDownNewReturnValueType/NewCheckboxNoLongerInUse/ButtonAdd.
    public TestResultItemTypeAddViewModel Add { get; set; } = new();

    protected override string AddName => Add.Name;
    protected override bool AddNoLongerInUse => Add.NoLongerInUse;
    protected override string? AddTestReturnValueTypeIdValue => Add.TestReturnValueTypeId?.ToString();
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

    Guid IItemTypeRowViewModel.ItemId => TestResultItemTypeId;
}

// Posted by the single "Add" button at the bottom of the list - matches legacy ButtonAdd_Click.
public sealed class TestResultItemTypeAddViewModel : ReturnTypeItemFormFieldsViewModelBase
{
    [Required(ErrorMessage = "Enter a new test result item name")]
    [StringLength(100, ErrorMessage = "New test result item name must be 100 characters or fewer")]
    public string Name { get; set; } = string.Empty;
}

// Posted by the row-level "Save" link against whichever row is in edit mode - matches legacy
// GridViewTestResultItemType_RowUpdating. Bound with an "Edit" prefix so its ModelState errors
// never bleed into the separate "Add" form on the same page.
public sealed class TestResultItemTypeEditViewModel : ReturnTypeItemFormFieldsViewModelBase
{
    [Required]
    public Guid? TestResultItemTypeId { get; set; }

    [Required(ErrorMessage = "Enter a new test result item name")]
    [StringLength(100, ErrorMessage = "New test result item name must be 100 characters or fewer")]
    public string Name { get; set; } = string.Empty;
}
