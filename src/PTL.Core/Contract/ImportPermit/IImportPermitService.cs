namespace PTL.Core.Contract.ImportPermit;

public interface IImportPermitService
{
    Task<IReadOnlyList<ImportPermitEntity>> GetByContractIdAsync(Guid contractId, CancellationToken cancellationToken = default);

    Task UpdateAsync(Guid participantSchemeId, bool importPermitReceived, DateTime? importPermitExpiry, CancellationToken cancellationToken = default);
}
