using PTL.Contracts.Contract;

namespace PTL.InternalWeb.Features.Contract;

// Legacy ContractItems.aspx has no search/filtering/paging - a single unpaginated page per
// contract - so this view model mirrors that exactly: just the aggregated read-model plus the
// CustomerId needed for the "Back" link (not carried on ContractItemsResponse itself).
public sealed record ContractItemsViewModel(ContractItemsResponse Items, Guid CustomerId);

