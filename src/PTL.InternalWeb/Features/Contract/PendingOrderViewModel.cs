using System.ComponentModel.DataAnnotations;
using PTL.Contracts.Contract;

namespace PTL.InternalWeb.Features.Contract;

// Review Pending Orders (legacy ReviewPendingOrders.aspx) - two grids, split by contract year.
public sealed record PendingOrderListViewModel(
    IReadOnlyList<PendingOrderSummaryResponse> CurrentYearOrders,
    IReadOnlyList<PendingOrderSummaryResponse> NextYearOrders);

// The Approve / Decline form. Legacy had no purchase order validator here (Contract's own 255 rule fired on
// Approve) and a 2000 character regex on the comments.
public sealed class PendingOrderDecisionForm
{
    [StringLength(255, ErrorMessage = "The purchase order number must be 255 characters or fewer")]
    public string? PurchaseOrderNumber { get; set; }

    [StringLength(2000, ErrorMessage = "The comments field allows maximum 2000 characters")]
    public string? Comments { get; set; }
}

// Pending Order Details (legacy PendingContractOrder.aspx). The API already returns each scheme's
// months in financial-year order with their Enabled/Selected state resolved, so the view renders
// them as-is. Form is set when the page is redisplayed after a failed Approve/Decline.
public sealed record PendingOrderDetailsViewModel(PendingOrderDetailsResponse Order, PendingOrderDecisionForm? Form = null);
