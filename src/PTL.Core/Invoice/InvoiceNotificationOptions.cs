namespace PTL.Core.Invoice;

/// <summary>
/// Invoice-domain-specific GOV.UK Notify configuration - who to notify that a new invoice CSV is
/// ready. The connection itself (API key/base URL) is shared
/// (PTL.Core.Notifications.NotifyOptions); this only configures the invoice feature's own
/// recipient list (replacing legacy's InvoiceEmailFrom/InvoiceEmailTo appSettings).
/// </summary>
public sealed class InvoiceNotificationOptions
{
    // Nested under the shared Notification section - one notification configuration root for the
    // whole application, not a sibling top-level section per feature.
    public const string SectionName = "Notification:InvoiceNotification";

    /// <summary>
    /// GOV.UK Notify template id for the invoice-ready email. Fixed - the same template is used in
    /// every environment, so it is not configuration.
    /// </summary>
    public const string TemplateId = "b2eb52ee-abda-49f9-80c3-6871326f25a8";

    public IReadOnlyList<string> Recipients { get; set; } = [];
}
