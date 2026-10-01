using PTL.Core.Contract.ImportPermit;

namespace PTL.Api.Tests.Contract;

internal sealed class FakeImportPermitRepository : IImportPermitRepository
{
    public IReadOnlyList<ImportPermitEntity> Permits { get; set; } = [];

    public List<(Guid ParticipantSchemeId, bool ImportPermitReceived, DateTime? ImportPermitExpiry)> UpdateCalls { get; } = [];

    public Task<IReadOnlyList<ImportPermitEntity>> GetByContractIdAsync(Guid contractId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Permits);

    public Task UpdateAsync(Guid participantSchemeId, bool importPermitReceived, DateTime? importPermitExpiry, CancellationToken cancellationToken = default)
    {
        UpdateCalls.Add((participantSchemeId, importPermitReceived, importPermitExpiry));
        return Task.CompletedTask;
    }
}
