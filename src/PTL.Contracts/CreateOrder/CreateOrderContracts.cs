using System.Text.Json.Serialization;

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
    bool CanProceed,
    string PurchaseOrderNumber = "",
    IReadOnlyList<CreateOrderConsentResponse>? Consents = null)
{
    // Consents is only filled when the review page asks for it (legacy stage 2's data consent section).
    [JsonIgnore]
    public IReadOnlyList<CreateOrderConsentResponse> DataConsents => Consents ?? [];
}

// A selected scheme whose Data Consent Declaration is active: the participant must agree before the order is submitted.
public sealed record CreateOrderConsentResponse(Guid SchemeId, string Identifier, string Name, string Text, bool Given);

// Field is "select", "importexport" or a three-letter month ("jan".."dec").
public sealed record CreateOrderLineChangeRequest(Guid SchemeId, string Field, bool Checked);

// Legacy stage 2 Save: only the purchase order number is stored.
public sealed record CreateOrderSaveRequest(string? PurchaseOrderNumber);

public sealed record CreateOrderSubmitRequest(string? PurchaseOrderNumber, bool TermsAccepted, IReadOnlyList<Guid>? ConsentedSchemeIds);

// Field names the page can link to: "Schemes", "PurchaseOrderNumber", "TermsAccepted", "DataConsent".
public sealed record CreateOrderValidationErrorResponse(string Field, string Message);

public sealed record CreateOrderValidationResponse(IReadOnlyList<CreateOrderValidationErrorResponse> Errors);
