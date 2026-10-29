namespace PTL.Contracts.CreateOrder;

// Legacy PermissionState: the per-year flags already include CanOrderOnline.
public sealed record CreateOrderEligibilityResponse(bool CanOrderOnline, bool CanCreateCurrentYearOrder, bool CanCreateNextYearOrder);

public sealed record CreateOrderMonthResponse(string Label, int MonthNumber, bool Offered, bool Selected, bool Enabled, bool AlreadyParticipating);

public sealed record CreateOrderSchemeResponse(
    Guid SchemeId,
    string Identifier,
    string Name,
    string Schedule,
    decimal AnnualPrice,
    bool Selected,
    bool ImportPermitRequired,
    bool ImportPermitEnabled,
    decimal PostagePrice,
    decimal SchemePrice,
    decimal TotalPrice,
    bool HasPendingMonths,
    IReadOnlyList<CreateOrderMonthResponse> Months);

public sealed record CreateOrderScheduleGroupResponse(string Schedule, IReadOnlyList<CreateOrderSchemeResponse> Schemes);

public sealed record CreateOrderResponse(
    string Year,
    string CurrencySymbol,
    string QalNumber,
    string LabCode,
    bool IsAphaCustomer,
    IReadOnlyList<CreateOrderSchemeResponse> ParticipatingSchemes,
    IReadOnlyList<CreateOrderScheduleGroupResponse> AvailableSchemes,
    decimal SchemeTotal,
    decimal PostageTotal,
    decimal OrderTotal,
    bool CanProceed);

// Field is "select", "importexport" or a three-letter month ("jan".."dec").
public sealed record CreateOrderLineChangeRequest(Guid SchemeId, string Field, bool Checked);
