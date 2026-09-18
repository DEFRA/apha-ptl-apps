using Microsoft.EntityFrameworkCore;
using PTL.Core.Lookup;

namespace PTL.Data.Lookup;

// Wraps the existing spgaCountry / spgaCurrency / spgaCustomerType stored procedures via EF Core;
// all three take no parameters and return a plain list, so ToListAsync (no SingleOrDefaultAsync
// composition) is safe directly on FromSqlRaw - see ContractRepository/CustomerRepository for why
// composing over an EXEC ... statement is not safe for single-row lookups.
public sealed class LookupRepository(PtlDbContext dbContext) : ILookupRepository
{
    public async Task<IReadOnlyList<CountryEntity>> GetCountriesAsync(CancellationToken cancellationToken = default) =>
        await dbContext.Countries
            .FromSqlRaw("EXEC dbo.spgaCountry")
            .AsNoTracking()
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<CurrencyEntity>> GetCurrenciesAsync(CancellationToken cancellationToken = default) =>
        await dbContext.Currencies
            .FromSqlRaw("EXEC dbo.spgaCurrency")
            .AsNoTracking()
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<CustomerTypeEntity>> GetCustomerTypesAsync(CancellationToken cancellationToken = default) =>
        await dbContext.CustomerTypes
            .FromSqlRaw("EXEC dbo.spgaCustomerType")
            .AsNoTracking()
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<VatRatingEntity>> GetVatRatingsAsync(CancellationToken cancellationToken = default) =>
        await dbContext.VatRatings
            .FromSqlRaw("EXEC dbo.spgaVatRating")
            .AsNoTracking()
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<LabTypeEntity>> GetLabTypesAsync(CancellationToken cancellationToken = default) =>
        await dbContext.LabTypes
            .FromSqlRaw("EXEC dbo.spgaLabType")
            .AsNoTracking()
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<YearEntity>> GetCurrentYearsAsync(CancellationToken cancellationToken = default) =>
        await dbContext.Years
            .FromSqlRaw("EXEC dbo.spgaYearCurrent")
            .AsNoTracking()
            .ToListAsync(cancellationToken);
}
