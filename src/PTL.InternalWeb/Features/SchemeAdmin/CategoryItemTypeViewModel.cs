using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace PTL.InternalWeb.Features.SchemeAdmin;

public sealed class CategoryItemTypeViewModel : IItemTypeListViewModel
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

    // IItemTypeListViewModel: shared _ItemTypeAdminList.cshtml rendering contract - see that file's
    // remarks. Entity-bound field name strings here must match SchemeAdminController exactly.
    string IItemTypeListViewModel.PageHeading => "Category Items";
    string IItemTypeListViewModel.PageDescription => "This page defines the Category Item Types that will be available for use on Tests.";
    string IItemTypeListViewModel.ColumnHeaderLabel => "Category item type name";
    string IItemTypeListViewModel.EmptyMessage => "There are no Category Item Types to display.";
    string IItemTypeListViewModel.AddHeading => "Add a Category Item";
    string IItemTypeListViewModel.AddNameLabel => "New category name";
    int IItemTypeListViewModel.EditNameMaxLength => 50;
    int IItemTypeListViewModel.PageSize => PageSize;
    string IItemTypeListViewModel.ActionName => "CategoryItemType";
    string IItemTypeListViewModel.AddActionName => "CategoryItemTypeAdd";
    string IItemTypeListViewModel.SaveActionName => "CategoryItemTypeSave";
    string IItemTypeListViewModel.SaveAllNoLongerInUseActionName => "CategoryItemTypeSaveAllNoLongerInUse";
    string IItemTypeListViewModel.RemoveActionName => "CategoryItemTypeRemove";
    string IItemTypeListViewModel.EditFormId => "categoryItemTypeEditForm";
    string IItemTypeListViewModel.EditIdFieldName => "Edit.CategoryItemTypeId";
    string IItemTypeListViewModel.RemoveIdRouteParamName => "categoryItemTypeId";
    string IItemTypeListViewModel.PageIdsFieldName => "pageCategoryItemTypeIds";
    string IItemTypeListViewModel.CheckedIdsFieldName => "checkedCategoryItemTypeIds";
    IReadOnlyList<IItemTypeRowViewModel> IItemTypeListViewModel.Rows => Rows;
    string IItemTypeListViewModel.AddName => Add.Name;
    bool IItemTypeListViewModel.AddNoLongerInUse => Add.NoLongerInUse;
}

public sealed class CategoryItemTypeRowViewModel : IItemTypeRowViewModel
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

    Guid IItemTypeRowViewModel.ItemId => CategoryItemTypeId;
}

// Posted by the single "Add" button at the bottom of the list - matches legacy
// ButtonAddCategory_Click.
public sealed class CategoryItemTypeAddViewModel
{
    [Required(ErrorMessage = "Enter a new category name")]
    [StringLength(50, ErrorMessage = "New category name must be 50 characters or fewer")]
    public string Name { get; set; } = string.Empty;

    // Never JSON-bound here (classic MVC form post) - [JsonRequired] only satisfies the
    // under-posting analyzer; an unticked checkbox omitting the field still binds to false.
    [JsonRequired]
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

    [JsonRequired]
    public bool NoLongerInUse { get; set; }
}
