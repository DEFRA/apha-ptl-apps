using PTL.Contracts.Invoice;

namespace PTL.InternalWeb.Features.Invoice;

public sealed class InvoiceGenerationViewModel
{
    public string FinancialYear { get; init; } = string.Empty;
    public int EligibleContractCount { get; init; }
    public int OptOutContractCount { get; init; }
    public int NonFeePayingItemCount { get; init; }
    public string NotificationFromEmail { get; init; } = string.Empty;
    public string NotificationToEmail { get; init; } = string.Empty;
    public bool CanReset { get; init; }

    // Generate is only meaningful while there are pending (not-yet-invoiced) contracts. Every
    // eligible contract is marked invoiced by Generate, so this naturally becomes false right
    // after a successful run, and only becomes true again once Reset un-marks them.
    public bool CanGenerate => EligibleContractCount > 0;

    // Legacy BtnGenerateInvoices.OnClientClick wording - preserved exactly.
    public const string GenerateConfirmation = "Are you sure you want to create an invoice for the latest billing period?";

    public static InvoiceGenerationViewModel From(PendingInvoiceSummaryResponse? pending, bool canReset, InvoiceNotificationDisplayOptions notificationDisplay) => new()
    {
        FinancialYear = pending?.FinancialYear ?? string.Empty,
        EligibleContractCount = pending?.EligibleContractCount ?? 0,
        OptOutContractCount = pending?.OptOutContractCount ?? 0,
        NonFeePayingItemCount = pending?.NonFeePayingItemCount ?? 0,
        NotificationFromEmail = notificationDisplay.FromEmail,
        NotificationToEmail = notificationDisplay.ToEmail,
        CanReset = canReset
    };
}
