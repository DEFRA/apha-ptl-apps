namespace PTL.Core.Invoice;

/// <summary>
/// Invoice-domain-specific GOV.UK Notify configuration - which template to use and who to notify
/// that a new invoice CSV is ready. The connection itself (API key/base URL) is shared
/// (PTL.Core.Notifications.NotifyOptions); this only configures the invoice feature's own
/// template id and recipient list (replacing legacy's InvoiceEmailFrom/InvoiceEmailTo appSettings).
/// </summary>
public sealed class InvoiceNotificationOptions
{
    public const string SectionName = "InvoiceNotification";

    public string TemplateId { get; set; } = string.Empty;

    public IReadOnlyList<string> Recipients { get; set; } = [];
}
