namespace PTL.Core.Scheme;

// Defined in Core (not Data) so SchemeService can depend on the abstraction without Core
// referencing Data; PTL.Data.Scheme.SchemeRepository implements this.
public interface ISchemeRepository
{
    Task<Scheme?> GetByIdAsync(Guid schemeId, CancellationToken cancellationToken = default);

    // Uses spgSchemeInfoByYearId (one row per scheme family active in that year).
    Task<IReadOnlyList<SchemeSummaryEntity>> GetSummariesByYearAsync(int yearId, CancellationToken cancellationToken = default);

    // Uses spgSchemeInfoBySharedId (family history, newest year first).
    Task<IReadOnlyList<SchemeHistoryEntity>> GetHistoryAsync(Guid sharedId, CancellationToken cancellationToken = default);

    // Uses spiScheme; the returned Scheme is re-read via GetByIdAsync so SampleNoSequence/IsReadOnly
    // (computed by the fetch procedure) are populated.
    Task<Scheme> CreateAsync(Scheme scheme, CancellationToken cancellationToken = default);

    // Uses spuScheme; returns null when no row was updated (scheme does not exist).
    Task<Scheme?> UpdateAsync(Scheme scheme, CancellationToken cancellationToken = default);
}
