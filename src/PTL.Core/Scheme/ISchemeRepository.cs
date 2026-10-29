namespace PTL.Core.Scheme;

// Defined in Core (not Data) so SchemeService can depend on the abstraction without Core
// referencing Data; PTL.Data.Scheme.SchemeRepository implements this.
public interface ISchemeRepository
{
    Task<Scheme?> GetByIdAsync(Guid schemeId, CancellationToken cancellationToken = default);

    // Uses spgSchemeInfoByYearId (one row per scheme family active in that year). NOTE: that
    // procedure returns NULL for every fldNextSchemeId/fldRecentSchemeId column - use
    // GetSummariesBySchemeIdAsync when the next-year scheme is required.
    Task<IReadOnlyList<SchemeSummaryEntity>> GetSummariesByYearAsync(int yearId, CancellationToken cancellationToken = default);

    // Uses spgaSchemeInfo - every scheme family, with its current, next and most recent scheme.
    // This is what the Scheme List screen shows; legacy
    // SchemeInfoCollection.FetchSchemeInfoCollection() takes no year and filters nothing.
    Task<IReadOnlyList<SchemeSummaryEntity>> GetAllSummariesAsync(CancellationToken cancellationToken = default);

    // Uses spgSchemeInfoBySchemeId - the only procedure that resolves a scheme's next-year
    // equivalent (self-join on fldSharedId with fldYearId + 1). Legacy
    // SchemeInfoCollection.FetchSchemeInfoCollectionBySchemeId.
    Task<IReadOnlyList<SchemeSummaryEntity>> GetSummariesBySchemeIdAsync(Guid schemeId, CancellationToken cancellationToken = default);

    // Uses spgSchemeInfoBySharedId (family history, newest year first).
    Task<IReadOnlyList<SchemeHistoryEntity>> GetHistoryAsync(Guid sharedId, CancellationToken cancellationToken = default);

    // Reuses the existing legacy spgCommercialSchemesBySsoId (@SsoId, @CurrentFinancialYear) exactly
    // as-is - SchemeList.aspx's "Commercial Schemes" grid (PtWebServicesBusinessObjects.SchemeListCollection,
    // ListType=0). No new procedure required.
    Task<IReadOnlyList<ParticipantSchemeEntity>> GetCommercialSchemesBySsoIdAsync(Guid ssoId, int currentFinancialYear, CancellationToken cancellationToken = default);

    // Reuses the existing legacy spgNonCommercialSchemesBySsoId (@SsoId, @CurrentFinancialYear) exactly
    // as-is - SchemeList.aspx's "Non-Commercial Schemes" grid (ListType=1). No new procedure required.
    Task<IReadOnlyList<ParticipantSchemeEntity>> GetNonCommercialSchemesBySsoIdAsync(Guid ssoId, int currentFinancialYear, CancellationToken cancellationToken = default);

    // Uses spiScheme; the returned Scheme is re-read via GetByIdAsync so SampleNoSequence/IsReadOnly
    // (computed by the fetch procedure) are populated.
    Task<Scheme> CreateAsync(Scheme scheme, CancellationToken cancellationToken = default);

    // Uses spuScheme; returns null when no row was updated (scheme does not exist).
    Task<Scheme?> UpdateAsync(Scheme scheme, CancellationToken cancellationToken = default);
}
