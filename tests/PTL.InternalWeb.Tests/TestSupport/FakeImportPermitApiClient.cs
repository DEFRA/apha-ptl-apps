using PTL.ApiClient;
using PTL.Contracts.Contract;

namespace PTL.InternalWeb.Tests.TestSupport;

// Test double for IImportPermitApiClient so ContractController tests don't need a real HTTP call
// to PTL.Api.
internal sealed class FakeImportPermitApiClient : IImportPermitApiClient
{
    public IReadOnlyList<ImportPermitResponse> Permits { get; set; } = [];

    public bool UpdateResult { get; set; } = true;

    public List<(Guid ParticipantSchemeId, UpdateImportPermitRequest Request)> UpdateCalls { get; } = [];

    public Task<IReadOnlyList<ImportPermitResponse>> GetImportPermitsAsync(Guid contractId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Permits);

    public Task<bool> UpdateImportPermitAsync(Guid participantSchemeId, UpdateImportPermitRequest request, CancellationToken cancellationToken = default)
    {
        UpdateCalls.Add((participantSchemeId, request));
        return Task.FromResult(UpdateResult);
    }
}
