using PTL.ApiClient;
using PTL.Contracts.Scheme;

namespace PTL.ExternalWeb.Tests.TestSupport;

// Only GetSchemesForParticipantAsync is exercised by SchemeController; the rest of ISchemeApiClient
// is irrelevant to PTL.ExternalWeb's read-only Scheme List screen.
internal sealed class FakeSchemeApiClient : ISchemeApiClient
{
    public ParticipantSchemeListResponse ParticipantSchemes { get; set; } = new([], []);

    public Task<ParticipantSchemeListResponse> GetSchemesForParticipantAsync(Guid participantId, CancellationToken cancellationToken = default) =>
        Task.FromResult(ParticipantSchemes);

    public Task<SchemeResponse?> GetSchemeAsync(Guid schemeId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<SchemeSearchResponse> GetSchemesForYearAsync(SchemeSearchRequest request, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<SchemeSearchResponse> GetSchemeFamiliesAsync(int page, int pageSize, string? searchTerm = null, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<IReadOnlyList<SchemeHistoryResponse>> GetSchemeHistoryAsync(Guid sharedId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<SchemeResponse?> RenewSchemeAsync(Guid schemeId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<SchemeSaveResult> CreateSchemeAsync(SchemeRequest request, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<SchemeSaveResult> UpdateSchemeAsync(Guid schemeId, SchemeRequest request, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
}
