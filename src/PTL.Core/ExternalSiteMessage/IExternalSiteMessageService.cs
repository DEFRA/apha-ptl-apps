namespace PTL.Core.ExternalSiteMessage;

public interface IExternalSiteMessageService
{
    Task<ExternalSiteMessage> GetAsync(CancellationToken cancellationToken = default);

    // Throws ExternalSiteMessageValidationException when business rules are violated.
    Task<ExternalSiteMessage> UpdateAsync(string message, string importantMessage, string supportEmailAddress, CancellationToken cancellationToken = default);
}
