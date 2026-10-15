namespace PTL.Core.Distribution;

// Holds an attachment uploaded on View Comments until Save writes it to the database (legacy Session).
// Cancel deletes it.
public interface IAttachmentStagingStore
{
    Task SaveAsync(Guid monthlyDistributionSchemeId, Guid token, StagedAttachment attachment, CancellationToken cancellationToken = default);

    // Null when the token does not exist for this scheme.
    Task<StagedAttachment?> GetAsync(Guid monthlyDistributionSchemeId, Guid token, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid monthlyDistributionSchemeId, Guid token, CancellationToken cancellationToken = default);
}

public sealed record StagedAttachment(string FileName, string ContentType, byte[] FileData);
