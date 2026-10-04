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

    // GOV.UK Notify's "send a file by email" feature - the file is delivered as a single
    // personalisation value (see NotifyFileAttachment), not a separate upload call/endpoint.
    Task SendEmailWithFileAsync(NotifyFileEmailRequest request, CancellationToken cancellationToken = default);
}

/// <summary>Arguments for <see cref="INotifyClient.SendEmailWithFileAsync"/>.</summary>
public sealed record NotifyFileEmailRequest(
    string TemplateId,
    string EmailAddress,
    string FilePersonalisationKey,
    byte[] FileContent,
    string Filename)
{
    public IReadOnlyDictionary<string, string>? Personalisation { get; init; }

    public string? Reference { get; init; }
}
