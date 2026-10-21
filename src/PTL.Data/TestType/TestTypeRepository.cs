using Dapper;
using PTL.Core.TestType;
using PTL.Data.Infrastructure;
using CoreTestType = PTL.Core.TestType.TestType;

namespace PTL.Data.TestType;

// The stored procedures are unchanged legacy objects (spgTestTypeByYearId, spiTestType,
// spuTestType, spdTestType) - this only adds a Dapper-based access path to them.
public sealed class TestTypeRepository(IDbConnectionFactory connectionFactory) : ITestTypeRepository
{
    public async Task<IReadOnlyList<CoreTestType>> GetByYearAsync(int yearId, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        return (await connection.QueryAsync<CoreTestType>(
            "EXEC dbo.spgTestTypeByYearId @YearId", new { YearId = yearId })).ToList();
    }

    public async Task CreateAsync(CoreTestType testType, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        await connection.ExecuteAsync(
            "EXEC dbo.spiTestType @TestTypeId, @SharedId, @TestType, @StartYearId, @EndYearId, @NoLongerInUse",
            new
            {
                testType.TestTypeId,
                testType.SharedId,
                TestType = testType.Name,
                testType.StartYearId,
                testType.EndYearId,
                testType.NoLongerInUse
            });
    }

    public async Task UpdateAsync(CoreTestType testType, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        await connection.ExecuteAsync(
            "EXEC dbo.spuTestType @TestTypeId, @SharedId, @TestType, @StartYearId, @EndYearId, @NoLongerInUse",
            new
            {
                testType.TestTypeId,
                testType.SharedId,
                TestType = testType.Name,
                testType.StartYearId,
                testType.EndYearId,
                testType.NoLongerInUse
            });
    }

    public async Task DeleteAsync(Guid testTypeId, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        await connection.ExecuteAsync("EXEC dbo.spdTestType @TestTypeId", new { TestTypeId = testTypeId });
    }
}
