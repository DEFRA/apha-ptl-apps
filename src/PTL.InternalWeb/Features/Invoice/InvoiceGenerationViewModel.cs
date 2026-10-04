using PTL.Contracts.Invoice;

namespace PTL.InternalWeb.Features.Invoice;

public sealed class InvoiceGenerationViewModel
{
    public string FinancialYear { get; init; } = string.Empty;
    public int EligibleContractCount { get; init; }
    public int OptOutContractCount { get; init; }
    public int NonFeePayingItemCount { get; init; }
    public IReadOnlyList<string> NotificationRecipients { get; init; } = [];
    public bool CanReset { get; init; }

    // Legacy BtnGenerateInvoices.OnClientClick wording - preserved exactly.
    public const string GenerateConfirmation = "Are you sure you want to create an invoice for the latest billing period?";

    public static InvoiceGenerationViewModel From(PendingInvoiceSummaryResponse? pending, bool canReset) => new()
    {
        FinancialYear = pending?.FinancialYear ?? string.Empty,
        EligibleContractCount = pending?.EligibleContractCount ?? 0,
        OptOutContractCount = pending?.OptOutContractCount ?? 0,
        NonFeePayingItemCount = pending?.NonFeePayingItemCount ?? 0,
        NotificationRecipients = pending?.NotificationRecipients ?? [],
        CanReset = canReset
    };
}
