namespace PTL.Core.Invoice;

/// <summary>
/// Invoice-domain-specific GOV.UK Notify configuration - which template to use and who to notify
/// that a new invoice CSV is ready. The connection itself (API key/base URL) is shared
/// (PTL.Core.Notifications.NotifyOptions); this only configures the invoice feature's own
/// template id and recipient list (replacing legacy's InvoiceEmailFrom/InvoiceEmailTo appSettings).
/// </summary>
public sealed class InvoiceNotificationOptions
{
    // Nested under the shared GovUkNotify section - one Notify configuration root for the whole
    // application, not a sibling top-level section per feature.
    public const string SectionName = "GovUkNotify:InvoiceNotification";

    public string TemplateId { get; set; } = string.Empty;

    public IReadOnlyList<string> Recipients { get; set; } = [];

    // The GOV.UK Notify template's personalisation placeholder for the "send a file by email"
    // feature (e.g. ((link_to_file)) in the template body) - must match whatever key the Notify
    // portal template actually uses, hence configurable rather than hardcoded.
    public string FilePersonalisationKey { get; set; } = "link_to_file";
}
