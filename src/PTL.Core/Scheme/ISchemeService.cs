namespace PTL.Core.Scheme;

public sealed record SchemeSearchResult(IReadOnlyList<SchemeSummaryEntity> Items, int TotalCount);

public interface ISchemeService
{
    Task<Scheme?> GetSchemeAsync(Guid schemeId, CancellationToken cancellationToken = default);

    // spgSchemeInfoByYearId (server-enforced year filter) with in-memory search-by-identifier/name
    // and paging applied afterwards - mirrors ContractService.SearchContractsAsync's approach.
    Task<SchemeSearchResult> SearchSchemesAsync(int yearId, string? searchTerm, int page, int pageSize, CancellationToken cancellationToken = default);

    // spgaSchemeInfo, paged. The Scheme List screen shows every scheme family with no year filter
    // and no search, matching legacy SchemeList.aspx. searchTerm is a simple case-insensitive
    // substring match against the family's displayed Name only (legacy Search.aspx "Scheme Name").
    Task<SchemeSearchResult> GetSchemeFamiliesAsync(int page, int pageSize, string? searchTerm, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SchemeHistoryEntity>> GetSchemeFamilyHistoryAsync(Guid sharedId, CancellationToken cancellationToken = default);

    // Legacy Scheme.RenewScheme: builds an unsaved draft for schemeId's year + 1, carried by the
    // same SharedId so it joins the existing family rather than starting a new one. Returns null
    // when schemeId does not exist.
    Task<Scheme?> RenewSchemeAsync(Guid schemeId, CancellationToken cancellationToken = default);

    // Throws SchemeValidationException when business rules are violated.
    Task<Scheme> CreateSchemeAsync(Scheme scheme, CancellationToken cancellationToken = default);

    // Returns null when schemeId does not exist. Throws SchemeValidationException when business
    // rules are violated, including when the existing scheme IsReadOnly (closed year).
    Task<Scheme?> UpdateSchemeAsync(Guid schemeId, Scheme updatedFields, CancellationToken cancellationToken = default);
}
