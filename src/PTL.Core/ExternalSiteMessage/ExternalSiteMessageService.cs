using PTL.Core.Audit;

namespace PTL.Core.ExternalSiteMessage;

public sealed class ExternalSiteMessageService(IExternalSiteMessageRepository repository, IActingUserAccessor actingUser) : IExternalSiteMessageService
{
    public Task<ExternalSiteMessage> GetAsync(CancellationToken cancellationToken = default) =>
        repository.GetAsync(cancellationToken);

    public async Task<ExternalSiteMessage> UpdateAsync(string message, string importantMessage, string supportEmailAddress, CancellationToken cancellationToken = default)
    {
        var validation = ExternalSiteMessageValidator.Validate(importantMessage, supportEmailAddress);
        if (!validation.IsValid)
        {
            throw new ExternalSiteMessageValidationException(validation.Errors);
        }

        var updated = new ExternalSiteMessage
        {
            Message = message,
            ImportantMessage = importantMessage,
            SupportEmailAddress = supportEmailAddress
        };

        // Singleton row (tblExtWebsiteMessage Id = 0); the message text is not audited, only that it changed.
        var audit = new AuditEntry(actingUser.ActingUserId, "UpdateExternalSiteMessage", "ExternalSiteMessage", "0", "Updated external site message");

        await repository.UpdateAsync(updated, audit, cancellationToken);
        return updated;
    }
}
