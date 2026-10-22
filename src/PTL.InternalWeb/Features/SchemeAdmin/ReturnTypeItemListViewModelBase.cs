using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace PTL.InternalWeb.Features.SchemeAdmin;

// Entity-specific strings for ReturnTypeItemListViewModelBase - see its remarks. Field names here
// must match SchemeAdminController's [Bind(Prefix)]/action-parameter names exactly.
public sealed record ReturnTypeItemListMetadata(
    string PageHeading, string PageDescription, string ColumnHeaderLabel, string EmptyMessage,
    string AddHeading, string AddNameLabel, string ActionName, string AddActionName,
    string SaveActionName, string SaveAllNoLongerInUseActionName, string RemoveActionName,
    string EditFormId, string EditIdFieldName, string RemoveIdRouteParamName,
    string PageIdsFieldName, string CheckedIdsFieldName);

// Shared IItemTypeListViewModel/IReturnTypeItemListViewModel implementation for
// TestResultItemTypeViewModel/TestMethodItemTypeViewModel - previously each concrete class
// re-declared the full ~18-member explicit interface implementation block verbatim (SonarCloud
// flagged ~21%/28 duplicated lines between them). Concrete classes now supply only their entity
// identity (ReturnTypeItemListMetadata) and the 3 values that read from their own typed Add view
// model.
public abstract class ReturnTypeItemListViewModelBase<TRow>(ReturnTypeItemListMetadata metadata) : IReturnTypeItemListViewModel
    where TRow : IReturnTypeItemRowViewModel
{
    public int SelectedYearId { get; set; }

    public IEnumerable<SelectListItem> YearOptions { get; set; } = [];

    public string? SearchText { get; set; }

    public List<TRow> Rows { get; set; } = [];

    // Matches legacy GridViewTestResultItemType/GridViewTestMethodItemType.PageSize = 15 exactly -
    // see §11.3.1 of the parity analysis. Same value for both entities.
    public const int PageSize = 15;

    public int Page { get; set; } = 1;

    // Count of items for the selected year (after any search filter) before paging is applied -
    // drives PaginationModel.
    public int TotalRecords { get; set; }

    public IEnumerable<SelectListItem> ReturnValueTypeOptions { get; set; } = [];

    public string? Message { get; set; }

    public bool MessageIsError { get; set; }

    protected abstract string AddName { get; }

    protected abstract bool AddNoLongerInUse { get; }

    protected abstract string? AddTestReturnValueTypeIdValue { get; }

    string IItemTypeListViewModel.PageHeading => metadata.PageHeading;
    string IItemTypeListViewModel.PageDescription => metadata.PageDescription;
    string IItemTypeListViewModel.ColumnHeaderLabel => metadata.ColumnHeaderLabel;
    string IItemTypeListViewModel.EmptyMessage => metadata.EmptyMessage;
    string IItemTypeListViewModel.AddHeading => metadata.AddHeading;
    string IItemTypeListViewModel.AddNameLabel => metadata.AddNameLabel;
    int IItemTypeListViewModel.EditNameMaxLength => 100;
    int IItemTypeListViewModel.PageSize => PageSize;
    string IItemTypeListViewModel.ActionName => metadata.ActionName;
    string IItemTypeListViewModel.AddActionName => metadata.AddActionName;
    string IItemTypeListViewModel.SaveActionName => metadata.SaveActionName;
    string IItemTypeListViewModel.SaveAllNoLongerInUseActionName => metadata.SaveAllNoLongerInUseActionName;
    string IItemTypeListViewModel.RemoveActionName => metadata.RemoveActionName;
    string IItemTypeListViewModel.EditFormId => metadata.EditFormId;
    string IItemTypeListViewModel.EditIdFieldName => metadata.EditIdFieldName;
    string IItemTypeListViewModel.RemoveIdRouteParamName => metadata.RemoveIdRouteParamName;
    string IItemTypeListViewModel.PageIdsFieldName => metadata.PageIdsFieldName;
    string IItemTypeListViewModel.CheckedIdsFieldName => metadata.CheckedIdsFieldName;
    IReadOnlyList<IReturnTypeItemRowViewModel> IReturnTypeItemListViewModel.Rows => (IReadOnlyList<IReturnTypeItemRowViewModel>)Rows;
    IReadOnlyList<IItemTypeRowViewModel> IItemTypeListViewModel.Rows => (IReadOnlyList<IItemTypeRowViewModel>)Rows;
    string IItemTypeListViewModel.AddName => AddName;
    bool IItemTypeListViewModel.AddNoLongerInUse => AddNoLongerInUse;
    string? IReturnTypeItemListViewModel.AddTestReturnValueTypeId => AddTestReturnValueTypeIdValue;
}

// Fields shared by TestResultItemTypeAddViewModel/TestResultItemTypeEditViewModel and their
// TestMethodItemType equivalents - only the Name validation messages are entity-specific (kept on
// the concrete classes so DataAnnotations attribute text stays unchanged), so these are 100%
// identical and safe to share without any behaviour or message-text change.
public abstract class ReturnTypeItemFormFieldsViewModelBase
{
    [Required(ErrorMessage = "Select a return type")]
    public Guid? TestReturnValueTypeId { get; set; }

    // Never JSON-bound here (classic MVC form post) - [JsonRequired] only satisfies the
    // under-posting analyzer; an unticked checkbox omitting the field still binds to false.
    [JsonRequired]
    public bool NoLongerInUse { get; set; }
}
