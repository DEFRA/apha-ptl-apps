using Dapper;
using PTL.Core.TestMethodItemType;
using PTL.Data.Infrastructure;
using CoreTestMethodItemType = PTL.Core.TestMethodItemType.TestMethodItemType;

namespace PTL.Data.TestMethodItemType;

// The stored procedures are unchanged legacy objects (spgTestMethodItemTypeByYearId,
// spiTestMethodItemType, spuTestMethodItemType, spdTestMethodItemType) - this only adds a
// Dapper-based access path to them.
public sealed class TestMethodItemTypeRepository(IDbConnectionFactory connectionFactory) : ITestMethodItemTypeRepository
{
    public async Task<IReadOnlyList<CoreTestMethodItemType>> GetByYearAsync(int yearId, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        return (await connection.QueryAsync<CoreTestMethodItemType>(
            "EXEC dbo.spgTestMethodItemTypeByYearId @YearId", new { YearId = yearId })).ToList();
    }

    public async Task CreateAsync(CoreTestMethodItemType testMethodItemType, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        await connection.ExecuteAsync(
            "EXEC dbo.spiTestMethodItemType @TestMethodItemTypeId, @SharedId, @TestMethodItemType, @StartYearId, @EndYearId, @TestReturnValueTypeId, @NoLongerInUse",
            new
            {
                testMethodItemType.TestMethodItemTypeId,
                testMethodItemType.SharedId,
                TestMethodItemType = testMethodItemType.Name,
                testMethodItemType.StartYearId,
                testMethodItemType.EndYearId,
                testMethodItemType.TestReturnValueTypeId,
                testMethodItemType.NoLongerInUse
            });
    }

    public async Task UpdateAsync(CoreTestMethodItemType testMethodItemType, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        await connection.ExecuteAsync(
            "EXEC dbo.spuTestMethodItemType @TestMethodItemTypeId, @SharedId, @TestMethodItemType, @StartYearId, @EndYearId, @TestReturnValueTypeId, @NoLongerInUse",
            new
            {
                testMethodItemType.TestMethodItemTypeId,
                testMethodItemType.SharedId,
                TestMethodItemType = testMethodItemType.Name,
                testMethodItemType.StartYearId,
                testMethodItemType.EndYearId,
                testMethodItemType.TestReturnValueTypeId,
                testMethodItemType.NoLongerInUse
            });
    }

    public async Task DeleteAsync(Guid testMethodItemTypeId, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        await connection.ExecuteAsync("EXEC dbo.spdTestMethodItemType @TestMethodItemTypeId", new { TestMethodItemTypeId = testMethodItemTypeId });
    }
}
