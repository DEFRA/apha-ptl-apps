using System.ComponentModel.DataAnnotations;

namespace PTL.InternalWeb.Features.SchemeAdmin;

// See ReturnTypeItemListViewModelBase's remarks for why the shared rendering/field-name
// boilerplate lives there rather than here.
public sealed class TestMethodItemTypeViewModel()
    : ReturnTypeItemListViewModelBase<TestMethodItemTypeRowViewModel>(new(
        PageHeading: "Test Method Items",
        PageDescription: "This page defines the Test Method Items (and their Return Types) that will be available for use when setting up next year's Schemes.",
        ColumnHeaderLabel: "Test method item name",
        EmptyMessage: "There are no Test Method Items to display.",
        AddHeading: "Add a Test Method Item",
        AddNameLabel: "New test method item name",
        ActionName: "TestMethodItemType",
        AddActionName: "TestMethodItemTypeAdd",
        SaveActionName: "TestMethodItemTypeSave",
        SaveAllNoLongerInUseActionName: "TestMethodItemTypeSaveAllNoLongerInUse",
        RemoveActionName: "TestMethodItemTypeRemove",
        EditFormId: "testMethodItemTypeEditForm",
        EditIdFieldName: "Edit.TestMethodItemTypeId",
        RemoveIdRouteParamName: "testMethodItemTypeId",
        PageIdsFieldName: "pageTestMethodItemTypeIds",
        CheckedIdsFieldName: "checkedTestMethodItemTypeIds"))
{
    // "Add a new test method item" section at the bottom of the page - matches legacy's
    // TextboxNewName/DropDownNewReturnValueType/NewCheckboxNoLongerInUse/ButtonAdd.
    public TestMethodItemTypeAddViewModel Add { get; set; } = new();

    protected override string AddName => Add.Name;
    protected override bool AddNoLongerInUse => Add.NoLongerInUse;
    protected override string? AddTestReturnValueTypeIdValue => Add.TestReturnValueTypeId?.ToString();
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

    Guid IItemTypeRowViewModel.ItemId => TestMethodItemTypeId;
}

// Posted by the single "Add" button at the bottom of the list - matches legacy ButtonAdd_Click.
public sealed class TestMethodItemTypeAddViewModel : ReturnTypeItemFormFieldsViewModelBase
{
    [Required(ErrorMessage = "Enter a new test method item name")]
    [StringLength(100, ErrorMessage = "New test method item name must be 100 characters or fewer")]
    public string Name { get; set; } = string.Empty;
}

// Posted by the row-level "Save" link against whichever row is in edit mode - matches legacy
// GridViewTestMethodItemType_RowUpdating. Bound with an "Edit" prefix so its ModelState errors
// never bleed into the separate "Add" form on the same page.
public sealed class TestMethodItemTypeEditViewModel : ReturnTypeItemFormFieldsViewModelBase
{
    [Required]
    public Guid? TestMethodItemTypeId { get; set; }

    [Required(ErrorMessage = "Enter a new test method item name")]
    [StringLength(100, ErrorMessage = "New test method item name must be 100 characters or fewer")]
    public string Name { get; set; } = string.Empty;
}
