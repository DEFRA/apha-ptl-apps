namespace PTL.Core.Invoice;

/// <summary>
/// Invoice-domain-specific GOV.UK Notify configuration - which template to use and who to notify
/// that a new invoice CSV is ready. The connection itself (API key/base URL) is shared
/// (PTL.Core.Notifications.NotifyOptions); this only configures the invoice feature's own
/// template id and recipient list (replacing legacy's InvoiceEmailFrom/InvoiceEmailTo appSettings).
/// </summary>
public sealed class InvoiceNotificationOptions
{
    // Nested under the shared Notification section - one notification configuration root for the
    // whole application, not a sibling top-level section per feature.
    public const string SectionName = "Notification:InvoiceNotification";

    public string TemplateId { get; set; } = string.Empty;

    public IReadOnlyList<string> Recipients { get; set; } = [];

    // Public base URL of PTLIMS (PTL.InternalWeb). The API cannot derive the web front-end's
    // address, so the download link emailed by Notify is built from this.
    public string DownloadBaseUrl { get; set; } = string.Empty;
}
