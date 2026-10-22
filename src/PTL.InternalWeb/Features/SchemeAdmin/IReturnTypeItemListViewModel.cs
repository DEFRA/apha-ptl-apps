using Microsoft.AspNetCore.Mvc.Rendering;

namespace PTL.InternalWeb.Features.SchemeAdmin;

// Shared read-only contract so _ReturnTypeItemAdminList.cshtml can render the TestResultItemType/
// TestMethodItemType pages from one partial instead of 2 near-identical copies (SonarCloud flagged
// 41.2%/108 duplicated lines between them). Parallels IItemTypeListViewModel (used by
// CategoryItemType/TestType/CriterionItemType) but adds the two things those 3 screens don't have:
// a Return Type column/dropdown per row and a server-side Search-by-name box. Entity-bound HTML
// field names are exposed as literal strings for the same reason as IItemTypeListViewModel - see
// its remarks and docs/Code-Duplication-Playbook.md §4.
public interface IReturnTypeItemListViewModel
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

    string? SearchText { get; }

    IEnumerable<SelectListItem> ReturnValueTypeOptions { get; }

    IReadOnlyList<IReturnTypeItemRowViewModel> Rows { get; }

    int Page { get; }

    int TotalRecords { get; }

    string? Message { get; }

    bool MessageIsError { get; }

    string AddName { get; }

    bool AddNoLongerInUse { get; }

    // Add's Return Type - exposed as the already-stringified <option value> rather than Guid?, so
    // the partial can mark the matching <option> as selected without an asp-for dependency on a
    // concrete model path (Model here is the interface, not TestResultItemTypeAddViewModel etc.).
    string? AddTestReturnValueTypeId { get; }
}

public interface IReturnTypeItemRowViewModel
{
    Guid ItemId { get; }

    string Name { get; }

    Guid TestReturnValueTypeId { get; }

    string TestReturnValueTypeName { get; }

    bool NoLongerInUse { get; }

    int ForeignInstances { get; }

    string ForeignInstancesString { get; }

    bool CanEditOrRemove { get; }

    bool IsEditing { get; }

    bool IsConfirmingRemove { get; }
}
