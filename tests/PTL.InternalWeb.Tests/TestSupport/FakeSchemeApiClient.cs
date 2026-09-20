using PTL.ApiClient;
using PTL.Contracts.Scheme;

namespace PTL.InternalWeb.Tests.TestSupport;

// Test double for ISchemeApiClient so SchemeController tests don't need a real HTTP call to
// PTL.Api. Configure each scripted response via the constructor/settable properties - mirrors
// FakeContractApiClient.
internal sealed class FakeSchemeApiClient : ISchemeApiClient
{
    public SchemeSearchResponse SearchResponse { get; set; } = new([], 0, 1, 20);
    public SchemeResponse? SchemeResponse { get; set; }
    public IReadOnlyList<SchemeHistoryResponse> HistoryResponse { get; set; } = [];
    public SchemeSaveResult SaveResult { get; set; } = new(true, null, new Dictionary<string, string[]>());

    public Task<SchemeResponse?> GetSchemeAsync(Guid schemeId, CancellationToken cancellationToken = default) =>
        Task.FromResult(SchemeResponse);

    public Task<SchemeSearchResponse> GetSchemesForYearAsync(SchemeSearchRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult(SearchResponse);

    public Task<IReadOnlyList<SchemeHistoryResponse>> GetSchemeHistoryAsync(Guid sharedId, CancellationToken cancellationToken = default) =>
        Task.FromResult(HistoryResponse);

    public Task<SchemeSaveResult> CreateSchemeAsync(CreateSchemeRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult(SaveResult);

    public Task<SchemeSaveResult> UpdateSchemeAsync(Guid schemeId, UpdateSchemeRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult(SaveResult);
}
