using PTL.Core.Scheme;
using CoreScheme = PTL.Core.Scheme.Scheme;

namespace PTL.Api.Tests.Distribution;

// Only GetSchemeAsync is exercised by the Sample Numbers create path; the rest of ISchemeService
// is irrelevant here and throws if a test ever reaches it by accident.
internal sealed class FakeSchemeService : ISchemeService
{
    public Dictionary<Guid, CoreScheme> Schemes { get; } = [];

    // Scripted outcome of UpdateSchemeAsync; unset, it throws like the other unused members.
    public SchemeUpdateResult? UpdateResult { get; set; }

    public Exception? UpdateException { get; set; }

    public (Guid SchemeId, CoreScheme Scheme, string AuditWho, string? AuditWhy)? LastUpdate { get; private set; }

    public Task<CoreScheme?> GetSchemeAsync(Guid schemeId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Schemes.GetValueOrDefault(schemeId));

    public Task<SchemeSearchResult> SearchSchemesAsync(int yearId, string? searchTerm, int page, int pageSize, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<SchemeSearchResult> GetSchemeFamiliesAsync(int page, int pageSize, string? searchTerm, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<IReadOnlyList<SchemeHistoryEntity>> GetSchemeFamilyHistoryAsync(Guid sharedId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<ParticipantSchemeListResult> GetSchemesForParticipantAsync(Guid participantId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<CoreScheme?> RenewSchemeAsync(Guid schemeId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<CoreScheme> CreateSchemeAsync(CoreScheme scheme, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<SchemeUpdateResult> UpdateSchemeAsync(Guid schemeId, CoreScheme updatedFields, string auditWho, string? auditWhy, CancellationToken cancellationToken = default)
    {
        LastUpdate = (schemeId, updatedFields, auditWho, auditWhy);
        if (UpdateException is not null)
        {
            throw UpdateException;
        }

        return UpdateResult is null ? throw new NotSupportedException() : Task.FromResult(UpdateResult);
    }
}
