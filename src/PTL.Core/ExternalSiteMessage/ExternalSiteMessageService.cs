using System.Net;
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
            // The editor posts its HTML entity-escaped (TinyMCE encoding "xml"); legacy HtmlDecoded it before saving,
            // otherwise the external pages show the tags as text.
            Message = WebUtility.HtmlDecode(message),
            ImportantMessage = WebUtility.HtmlDecode(importantMessage),
            SupportEmailAddress = supportEmailAddress
        };

        // Singleton row (tblExtWebsiteMessage Id = 0); the message text is not audited, only that it changed.
        var audit = new AuditEntry(actingUser.ActingUserId, "UpdateExternalSiteMessage", "ExternalSiteMessage", "0", "Updated external site message");

        await repository.UpdateAsync(updated, audit, cancellationToken);
        return updated;
    }
}
