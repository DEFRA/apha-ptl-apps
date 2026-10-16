using PTL.Core.Distribution;

namespace PTL.Api.Tests.Distribution;

// Records deletes so tests can assert exactly which staged tokens were discarded.
internal sealed class FakeAttachmentStagingStore : IAttachmentStagingStore
{
    public Dictionary<(Guid SchemeId, Guid Token), StagedAttachment> Files { get; } = [];

    public List<(Guid SchemeId, Guid Token)> Deleted { get; } = [];

    public Task SaveAsync(Guid monthlyDistributionSchemeId, Guid token, StagedAttachment attachment, CancellationToken cancellationToken = default)
    {
        Files[(monthlyDistributionSchemeId, token)] = attachment;
        return Task.CompletedTask;
    }

    public Task<StagedAttachment?> GetAsync(Guid monthlyDistributionSchemeId, Guid token, CancellationToken cancellationToken = default) =>
        Task.FromResult(Files.GetValueOrDefault((monthlyDistributionSchemeId, token)));

    public Task DeleteAsync(Guid monthlyDistributionSchemeId, Guid token, CancellationToken cancellationToken = default)
    {
        Deleted.Add((monthlyDistributionSchemeId, token));
        Files.Remove((monthlyDistributionSchemeId, token));
        return Task.CompletedTask;
    }
}
