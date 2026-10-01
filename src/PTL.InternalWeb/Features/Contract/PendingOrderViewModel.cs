using PTL.Contracts.Contract;

namespace PTL.InternalWeb.Features.Contract;

// Review Pending Orders (legacy ReviewPendingOrders.aspx) - two grids, split by contract year.
public sealed record PendingOrderListViewModel(
    IReadOnlyList<PendingOrderSummaryResponse> CurrentYearOrders,
    IReadOnlyList<PendingOrderSummaryResponse> NextYearOrders);

// Pending Order Details (legacy PendingContractOrder.aspx). The API already returns each scheme's
// months in financial-year order with their Enabled/Selected state resolved, so the view renders
// them as-is.
public sealed record PendingOrderDetailsViewModel(PendingOrderDetailsResponse Order);
