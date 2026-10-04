namespace PTL.Contracts.Contract;

// One row on either grid of "Review Pending Orders" (legacy ReviewPendingOrders.aspx /
// spgaPendingContracts).
public sealed record PendingOrderSummaryResponse(
    Guid PendingContractId,
    Guid CustomerId,
    string QalNumber,
    string CustomerName,
    int YearId,
    string Year,
    DateTime? OrderSubmitDate);

// Legacy splits the list into two grids by SystemSettings CurrentYearId / NextYearId.
public sealed record PendingOrderListResponse(
    IReadOnlyList<PendingOrderSummaryResponse> CurrentYearOrders,
    IReadOnlyList<PendingOrderSummaryResponse> NextYearOrders);

// One month checkbox on the pending order scheme grid. Enabled/Selected are resolved server-side
// from the scheme's offered months, the distribution lock flags and the participant's existing
// contract, exactly as the legacy screen did on data bind.
public sealed record PendingOrderSchemeMonthResponse(
    string Label,
    int MonthNumber,
    bool Selected,
    bool Enabled,
    bool AlreadyParticipating);

// One scheme line on the pending order (legacy PendingParticipantScheme /
// spgPendingParticipantSchemeByPendingContractId). Months are in financial-year order (Apr..Mar),
// matching the legacy grid columns.
public sealed record PendingOrderSchemeResponse(
    Guid PendingParticipantSchemeId,
    Guid ParticipantId,
    string ParticipantName,
    Guid SchemeId,
    string SchemeIdentifier,
    string SchemeName,
    IReadOnlyList<PendingOrderSchemeMonthResponse> Months,
    bool ImportExportLicenceRequired,
    bool IsRemoved,
    decimal Price,
    decimal PostagePrice,
    decimal TotalPrice);

// The whole "Pending Order Details" screen (legacy PendingContractOrder.aspx).
public sealed record PendingOrderDetailsResponse(
    Guid PendingContractId,
    Guid CustomerId,
    string QalNumber,
    string CustomerName,
    int YearId,
    string Year,
    string PurchaseOrderNumber,
    string CurrencySymbol,
    IReadOnlyList<PendingOrderSchemeResponse> Schemes,
    decimal TotalSchemePrice,
    decimal TotalPostagePrice,
    decimal Total);

// Per-row edit posted by the month / Import-Export Licence checkboxes and the Add/Remove link,
// mirroring legacy's AutoPostBack Check_Clicked and AddRemove handlers.
public sealed record PendingOrderSchemeUpdateRequest(
    bool DistributionMonthJan,
    bool DistributionMonthFeb,
    bool DistributionMonthMar,
    bool DistributionMonthApr,
    bool DistributionMonthMay,
    bool DistributionMonthJun,
    bool DistributionMonthJul,
    bool DistributionMonthAug,
    bool DistributionMonthSep,
    bool DistributionMonthOct,
    bool DistributionMonthNov,
    bool DistributionMonthDec,
    bool ImportExportLicenceRequired,
    bool IsRemoved);

// Approve carries the (editable) purchase order number, matching legacy TextBoxPurchaseOrderNumber.
public sealed record PendingOrderApproveRequest(string PurchaseOrderNumber);

// Outcome of an Approve/Decline. NotFound distinguishes "no such pending order" from a validation
// failure so the caller can return 404 rather than redisplaying the page.
public sealed record PendingOrderDecisionResult(
    bool Success,
    bool NotFound,
    IReadOnlyDictionary<string, string[]> FieldErrors);
