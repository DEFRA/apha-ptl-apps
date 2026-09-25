namespace PTL.Core.Contract.ImportPermit;

public sealed class ImportPermitValidationException(string message) : Exception(message);

// Matches legacy ImportPermitDataAccess.UpdateImportPermit's ArgumentException guard: "Import
// permit expiry date is required when permit is received." - the only validation rule this
// feature has (ImportPermitRequired itself is read-only, never validated here).
public sealed class ImportPermitService(IImportPermitRepository importPermitRepository) : IImportPermitService
{
    public Task<IReadOnlyList<ImportPermitEntity>> GetByContractIdAsync(Guid contractId, CancellationToken cancellationToken = default) =>
        importPermitRepository.GetByContractIdAsync(contractId, cancellationToken);

    public Task UpdateAsync(Guid participantSchemeId, bool importPermitReceived, DateTime? importPermitExpiry, CancellationToken cancellationToken = default)
    {
        if (importPermitReceived && importPermitExpiry is null)
        {
            throw new ImportPermitValidationException("Import permit expiry date is required when permit is received.");
        }

        return importPermitRepository.UpdateAsync(participantSchemeId, importPermitReceived, importPermitExpiry, cancellationToken);
    }
}
