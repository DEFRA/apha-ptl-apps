using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace PTL.InternalWeb.Features.SchemeAdmin;

public sealed class CriterionItemTypeViewModel : IItemTypeListViewModel
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

    // IItemTypeListViewModel: shared _ItemTypeAdminList.cshtml rendering contract - see
    // CategoryItemTypeViewModel's remarks. Entity-bound field name strings here must match
    // SchemeAdminController exactly.
    string IItemTypeListViewModel.PageHeading => "Criterion Items";
    string IItemTypeListViewModel.PageDescription => "This page defines the Criterion Item Types that will be available for use on Tests.";
    string IItemTypeListViewModel.ColumnHeaderLabel => "Criterion item type name";
    string IItemTypeListViewModel.EmptyMessage => "There are no Criterion Item Types to display.";
    string IItemTypeListViewModel.AddHeading => "Add a Criterion Item";
    string IItemTypeListViewModel.AddNameLabel => "New criterion item name";
    int IItemTypeListViewModel.EditNameMaxLength => 50;
    int IItemTypeListViewModel.PageSize => PageSize;
    string IItemTypeListViewModel.ActionName => "CriterionItemType";
    string IItemTypeListViewModel.AddActionName => "CriterionItemTypeAdd";
    string IItemTypeListViewModel.SaveActionName => "CriterionItemTypeSave";
    string IItemTypeListViewModel.SaveAllNoLongerInUseActionName => "CriterionItemTypeSaveAllNoLongerInUse";
    string IItemTypeListViewModel.RemoveActionName => "CriterionItemTypeRemove";
    string IItemTypeListViewModel.EditFormId => "criterionItemTypeEditForm";
    string IItemTypeListViewModel.EditIdFieldName => "Edit.CriterionItemTypeId";
    string IItemTypeListViewModel.RemoveIdRouteParamName => "criterionItemTypeId";
    string IItemTypeListViewModel.PageIdsFieldName => "pageCriterionItemTypeIds";
    string IItemTypeListViewModel.CheckedIdsFieldName => "checkedCriterionItemTypeIds";
    IReadOnlyList<IItemTypeRowViewModel> IItemTypeListViewModel.Rows => Rows;
    string IItemTypeListViewModel.AddName => Add.Name;
    bool IItemTypeListViewModel.AddNoLongerInUse => Add.NoLongerInUse;
}

public sealed class CriterionItemTypeRowViewModel : IItemTypeRowViewModel
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

    Guid IItemTypeRowViewModel.ItemId => CriterionItemTypeId;
}

// Posted by the single "Add" button at the bottom of the list - matches legacy ButtonAdd_Click.
public sealed class CriterionItemTypeAddViewModel
{
    [Required(ErrorMessage = "Enter a new criterion item name")]
    [StringLength(50, ErrorMessage = "New criterion item name must be 50 characters or fewer")]
    public string Name { get; set; } = string.Empty;

    // Never JSON-bound here (classic MVC form post) - [JsonRequired] only satisfies the
    // under-posting analyzer; an unticked checkbox omitting the field still binds to false.
    [JsonRequired]
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

    [JsonRequired]
    public bool NoLongerInUse { get; set; }
}
