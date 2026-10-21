using Microsoft.AspNetCore.Mvc.Rendering;

namespace PTL.InternalWeb.Features.SchemeAdmin;

// Shared read-only contract so _ItemTypeAdminList.cshtml can render the CategoryItemType/TestType/
// CriterionItemType pages from one partial instead of 3 near-identical copies. Entity-bound HTML
// field names (Edit id field, bulk checkbox field names, route param names) are exposed as literal
// strings here rather than derived, since they're tied to SchemeAdminController's [Bind(Prefix)]
// and action-parameter names - renaming/deriving them would silently break model binding.
public interface IItemTypeListViewModel
{
    string PageHeading { get; }

    string PageDescription { get; }

    string ColumnHeaderLabel { get; }

    string EmptyMessage { get; }

    string AddHeading { get; }

    string AddNameLabel { get; }

    int EditNameMaxLength { get; }

    int PageSize { get; }

    string ActionName { get; }

    string AddActionName { get; }

    string SaveActionName { get; }

    string SaveAllNoLongerInUseActionName { get; }

    string RemoveActionName { get; }

    string EditFormId { get; }

    string EditIdFieldName { get; }

    string RemoveIdRouteParamName { get; }

    string PageIdsFieldName { get; }

    string CheckedIdsFieldName { get; }

    int SelectedYearId { get; }

    IEnumerable<SelectListItem> YearOptions { get; }

    IReadOnlyList<IItemTypeRowViewModel> Rows { get; }

    int Page { get; }

    int TotalRecords { get; }

    string? Message { get; }

    bool MessageIsError { get; }

    string AddName { get; }

    bool AddNoLongerInUse { get; }
}

public interface IItemTypeRowViewModel
{
    Guid ItemId { get; }

    string Name { get; }

    bool NoLongerInUse { get; }

    int ForeignInstances { get; }

    string ForeignInstancesString { get; }

    bool CanEditOrRemove { get; }

    bool IsEditing { get; }

    bool IsConfirmingRemove { get; }
}
