using Dapper;
using PTL.Core.CriterionItemType;
using PTL.Data.Infrastructure;
using CoreCriterionItemType = PTL.Core.CriterionItemType.CriterionItemType;

namespace PTL.Data.CriterionItemType;

// The stored procedures are unchanged legacy objects (spgCriterionItemTypeByYearId,
// spiCriterionItemType, spuCriterionItemType, spdCriterionItemType) - this only adds a
// Dapper-based access path to them.
public sealed class CriterionItemTypeRepository(IDbConnectionFactory connectionFactory) : ICriterionItemTypeRepository
{
    public async Task<IReadOnlyList<CoreCriterionItemType>> GetByYearAsync(int yearId, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        return (await connection.QueryAsync<CoreCriterionItemType>(
            "EXEC dbo.spgCriterionItemTypeByYearId @YearId", new { YearId = yearId })).ToList();
    }

    public async Task CreateAsync(CoreCriterionItemType criterionItemType, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        await connection.ExecuteAsync(
            "EXEC dbo.spiCriterionItemType @CriterionItemTypeId, @Name, @StartYearId, @EndYearId, @NoLongerInUse",
            new
            {
                criterionItemType.CriterionItemTypeId,
                criterionItemType.Name,
                criterionItemType.StartYearId,
                criterionItemType.EndYearId,
                criterionItemType.NoLongerInUse
            });
    }

    public async Task UpdateAsync(CoreCriterionItemType criterionItemType, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        await connection.ExecuteAsync(
            "EXEC dbo.spuCriterionItemType @CriterionItemTypeId, @Name, @StartYearId, @EndYearId, @NoLongerInUse",
            new
            {
                criterionItemType.CriterionItemTypeId,
                criterionItemType.Name,
                criterionItemType.StartYearId,
                criterionItemType.EndYearId,
                criterionItemType.NoLongerInUse
            });
    }

    public async Task DeleteAsync(Guid criterionItemTypeId, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        await connection.ExecuteAsync("EXEC dbo.spdCriterionItemType @CriterionItemTypeId", new { CriterionItemTypeId = criterionItemTypeId });
    }
}
