using PTL.Core.Scheme;
using CoreScheme = PTL.Core.Scheme.Scheme;

namespace PTL.Api.Tests.Distribution;

// Only GetSchemeAsync is exercised by the Sample Numbers create path; the rest of ISchemeService
// is irrelevant here and throws if a test ever reaches it by accident.
internal sealed class FakeSchemeService : ISchemeService
{
    public Dictionary<Guid, CoreScheme> Schemes { get; } = [];

    public Task<CoreScheme?> GetSchemeAsync(Guid schemeId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Schemes.GetValueOrDefault(schemeId));

    public Task<SchemeSearchResult> SearchSchemesAsync(int yearId, string? searchTerm, int page, int pageSize, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<SchemeSearchResult> GetSchemeFamiliesAsync(int page, int pageSize, string? searchTerm, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<IReadOnlyList<SchemeHistoryEntity>> GetSchemeFamilyHistoryAsync(Guid sharedId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<CoreScheme?> RenewSchemeAsync(Guid schemeId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<CoreScheme> CreateSchemeAsync(CoreScheme scheme, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<CoreScheme?> UpdateSchemeAsync(Guid schemeId, CoreScheme updatedFields, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
}
