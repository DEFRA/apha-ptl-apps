namespace PTL.Core.ExternalSiteMessage;

public sealed class ExternalSiteMessageService(IExternalSiteMessageRepository repository) : IExternalSiteMessageService
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

        await repository.UpdateAsync(updated, cancellationToken);
        return updated;
    }
}
