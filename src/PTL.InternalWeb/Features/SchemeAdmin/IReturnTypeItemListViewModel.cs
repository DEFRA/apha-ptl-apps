using Microsoft.AspNetCore.Mvc.Rendering;

namespace PTL.InternalWeb.Features.SchemeAdmin;

// Shared read-only contract so _ReturnTypeItemAdminList.cshtml can render the TestResultItemType/
// TestMethodItemType pages from one partial instead of 2 near-identical copies (SonarCloud flagged
// 41.2%/108 duplicated lines between them). Extends IItemTypeListViewModel (used by
// CategoryItemType/TestType/CriterionItemType) rather than re-declaring its ~20 shared members -
// SonarCloud separately flagged that near-total overlap as duplication once this interface was
// added - adding only the two things those 3 screens don't have: a Return Type column/dropdown per
// row (hence Rows/AddTestReturnValueTypeId) and a server-side Search-by-name box (SearchText).
// Entity-bound HTML field names inherited from IItemTypeListViewModel are exposed as literal
// strings for the same reason documented in its remarks and docs/Code-Duplication-Playbook.md §4.
public interface IReturnTypeItemListViewModel : IItemTypeListViewModel
{
    string? SearchText { get; }

    IEnumerable<SelectListItem> ReturnValueTypeOptions { get; }

    // Hides IItemTypeListViewModel.Rows - rows here carry an extra Return Type column, so the row
    // contract (IReturnTypeItemRowViewModel) isn't interchangeable with the base's.
    new IReadOnlyList<IReturnTypeItemRowViewModel> Rows { get; }

    // Add's Return Type - exposed as the already-stringified <option value> rather than Guid?, so
    // the partial can mark the matching <option> as selected without an asp-for dependency on a
    // concrete model path (Model here is the interface, not TestResultItemTypeAddViewModel etc.).
    string? AddTestReturnValueTypeId { get; }
}

// Adds the Return Type columns that CategoryItemType/TestType/CriterionItemType rows don't have -
// see IReturnTypeItemListViewModel's remarks.
public interface IReturnTypeItemRowViewModel : IItemTypeRowViewModel
{
    Guid TestReturnValueTypeId { get; }

    string TestReturnValueTypeName { get; }
}

