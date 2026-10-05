namespace PTL.Core.Notifications;

/// <summary>
/// Shared outbound-notification abstraction for GOV.UK Notify (docs/migration/email-notification-
/// migration.md, docs/migration/invoice-migration.md). Every feature that needs to notify a
/// recipient calls through this one interface with its own template id - there is no
/// feature-specific notification framework.
/// </summary>
public interface INotifyClient
{
    Task SendEmailAsync(
        string templateId,
        string emailAddress,
        IReadOnlyDictionary<string, string>? personalisation = null,
        string? reference = null,
        CancellationToken cancellationToken = default);
}
