namespace PTL.InternalWeb.Features.Invoice;

// Purely informational text shown on the Invoice Generation page before Generate is clicked
// (legacy's LblEmailDetails sentence). Deliberately separate from PTL.Api's
// GovUkNotify:InvoiceNotification config (which governs the actual email send) - InternalWeb only
// ever calls PTL.Api's /api/invoices endpoints, it never talks to GOV.UK Notify itself, so this
// page's own config is display text only and is allowed to be maintained independently.
public sealed class InvoiceNotificationDisplayOptions
{
    public const string SectionName = "InvoiceNotification";

    public string FromEmail { get; set; } = string.Empty;
    public string ToEmail { get; set; } = string.Empty;
}
