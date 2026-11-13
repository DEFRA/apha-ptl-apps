namespace PTL.Core.Contract.CreateOrder;

public enum OrderYear
{
    Current,
    Next
}

public enum CreateOrderAccess
{
    Allowed,
    NotFound,
    NotEligible
}

// Legacy PermissionState.CanOrderOnline / CanCreateNewCurrentOrder / CanCreateNewNextOrder: the two
// per-year flags already include CanOrderOnline, as legacy's CreateCurrent/NextOrderPageGuard needs both. The year labels
// ("2026/27") are for the Home cards, which legacy kept but disabled once that year's order was submitted.
public sealed record CreateOrderEligibility(bool CanOrderOnline, bool CanCreateCurrentYearOrder, bool CanCreateNextYearOrder, string CurrentYear = "", string NextYear = "");

// One distribution-month checkbox. Selected + AlreadyParticipating means the participant already
// holds that month (shown ticked and locked); Selected without it is a month in the pending order.
public sealed record CreateOrderMonth(string Label, int MonthNumber, bool Offered, bool Selected, bool Enabled, bool AlreadyParticipating);

public sealed record CreateOrderScheme(
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
    IReadOnlyList<CreateOrderMonth> Months)
{
    public decimal TotalPrice => PostagePrice + SchemePrice;

    public bool HasPendingMonths => Months.Any(m => m.Selected && !m.AlreadyParticipating);
}

public sealed record CreateOrderScheduleGroup(string Schedule, IReadOnlyList<CreateOrderScheme> Schemes);

public sealed record CreateOrderPage(
    OrderYear OrderYear,
    string Year,
    string CurrencySymbol,
    string QalNumber,
    string LabCode,
    bool IsAphaCustomer,
    IReadOnlyList<CreateOrderScheme> ParticipatingSchemes,
    IReadOnlyList<CreateOrderScheduleGroup> AvailableSchemes,
    decimal SchemeTotal,
    decimal PostageTotal,
    bool CanProceed,
    string PurchaseOrderNumber = "",
    IReadOnlyList<CreateOrderConsent>? Consents = null)
{
    public decimal OrderTotal => SchemeTotal + PostageTotal;
}

// A selected scheme whose Data Consent Declaration is active (legacy DivDataConsentDeclarationSection).
public sealed record CreateOrderConsent(Guid SchemeId, string Identifier, string Name, string Text, bool Given);

// Legacy BtnSubmit_Click inputs: the textbox, the terms checkbox and the ticked consent checkboxes.
public sealed record CreateOrderSubmission(string? PurchaseOrderNumber, bool TermsAccepted, IReadOnlyCollection<Guid> ConsentedSchemeIds);

// Field is "Schemes", "PurchaseOrderNumber", "TermsAccepted" or "DataConsent".
public sealed record CreateOrderValidationError(string Field, string Message);

// Errors empty means the save or submit went through (when Access is Allowed).
public sealed record CreateOrderSubmitOutcome(CreateOrderAccess Access, IReadOnlyList<CreateOrderValidationError> Errors)
{
    public bool Succeeded => Access == CreateOrderAccess.Allowed && Errors.Count == 0;
}

public sealed record CreateOrderResult(CreateOrderAccess Access, CreateOrderPage? Page = null);

// One checkbox change, as legacy Check_Clicked handles it: Field is "select", "importexport" or a
// three-letter month ("jan".."dec"), Checked the box's new state.
public sealed record CreateOrderLineChange(Guid SchemeId, string Field, bool Checked);
