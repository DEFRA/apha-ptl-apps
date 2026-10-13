namespace PTL.Core.ExternalSiteMessage;

public interface IExternalSiteMessageRepository
{
    // spgaMainPageMessage - throws if the singleton row is missing, matching legacy
    // DataPortal_Fetch ("tblExtWebsiteMessage must have a row with Id = 0").
    Task<ExternalSiteMessage> GetAsync(CancellationToken cancellationToken = default);

    // spuMainPageMessage
    Task UpdateAsync(ExternalSiteMessage message, CancellationToken cancellationToken = default);
}
