namespace PTL.Core.Contract.ImportPermit;

// Defined in Core (not Data) so ImportPermitService can depend on the abstraction without Core
// referencing Data; PTL.Data.Contract.ImportPermit.ImportPermitRepository implements this.
public interface IImportPermitRepository
{
    // Uses spgImportPermitDetailsByContractId.
    Task<IReadOnlyList<ImportPermitEntity>> GetByContractIdAsync(Guid contractId, CancellationToken cancellationToken = default);

    // Uses spuUpdateImportPermit - deliberately does not take ImportPermitRequired: that flag is
    // read-only in legacy (sourced from ParticipantScheme's own ImportExportLicenceRequired) and is
    // never written by this stored procedure.
    Task UpdateAsync(Guid participantSchemeId, bool importPermitReceived, DateTime? importPermitExpiry, CancellationToken cancellationToken = default);
}
