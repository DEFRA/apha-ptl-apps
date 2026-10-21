using Dapper;
using PTL.Core.CategoryItemType;
using PTL.Data.Infrastructure;
using CoreCategoryItemType = PTL.Core.CategoryItemType.CategoryItemType;

namespace PTL.Data.CategoryItemType;

// The stored procedures are unchanged legacy objects (spgCategoryItemTypeByYearId,
// spiCategoryItemType, spuCategoryItemType, spdCategoryItemType) - this only adds a Dapper-based
// access path to them.
public sealed class CategoryItemTypeRepository(IDbConnectionFactory connectionFactory) : ICategoryItemTypeRepository
{
    public async Task<IReadOnlyList<CoreCategoryItemType>> GetByYearAsync(int yearId, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        return (await connection.QueryAsync<CoreCategoryItemType>(
            "EXEC dbo.spgCategoryItemTypeByYearId @YearId", new { YearId = yearId })).ToList();
    }

    public async Task CreateAsync(CoreCategoryItemType categoryItemType, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        await connection.ExecuteAsync(
            "EXEC dbo.spiCategoryItemType @CategoryItemTypeId, @Name, @StartYearId, @EndYearId, @NoLongerInUse",
            new
            {
                categoryItemType.CategoryItemTypeId,
                categoryItemType.Name,
                categoryItemType.StartYearId,
                categoryItemType.EndYearId,
                categoryItemType.NoLongerInUse
            });
    }

    public async Task UpdateAsync(CoreCategoryItemType categoryItemType, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        await connection.ExecuteAsync(
            "EXEC dbo.spuCategoryItemType @CategoryItemTypeId, @Name, @StartYearId, @EndYearId, @NoLongerInUse",
            new
            {
                categoryItemType.CategoryItemTypeId,
                categoryItemType.Name,
                categoryItemType.StartYearId,
                categoryItemType.EndYearId,
                categoryItemType.NoLongerInUse
            });
    }

    public async Task DeleteAsync(Guid categoryItemTypeId, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        await connection.ExecuteAsync("EXEC dbo.spdCategoryItemType @CategoryItemTypeId", new { CategoryItemTypeId = categoryItemTypeId });
    }
}
