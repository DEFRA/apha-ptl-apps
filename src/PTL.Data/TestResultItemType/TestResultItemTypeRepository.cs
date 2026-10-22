using Dapper;
using PTL.Core.TestResultItemType;
using PTL.Data.Infrastructure;
using CoreTestResultItemType = PTL.Core.TestResultItemType.TestResultItemType;

namespace PTL.Data.TestResultItemType;

// The stored procedures are unchanged legacy objects (spgTestResultItemTypeByYearId,
// spiTestResultItemType, spuTestResultItemType, spdTestResultItemType) - this only adds a
// Dapper-based access path to them.
public sealed class TestResultItemTypeRepository(IDbConnectionFactory connectionFactory) : ITestResultItemTypeRepository
{
    public async Task<IReadOnlyList<CoreTestResultItemType>> GetByYearAsync(int yearId, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        return (await connection.QueryAsync<CoreTestResultItemType>(
            "EXEC dbo.spgTestResultItemTypeByYearId @YearId", new { YearId = yearId })).ToList();
    }

    public async Task CreateAsync(CoreTestResultItemType testResultItemType, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        await connection.ExecuteAsync(
            "EXEC dbo.spiTestResultItemType @TestResultItemTypeId, @SharedId, @TestResultItemType, @StartYearId, @EndYearId, @TestReturnValueTypeId, @NoLongerInUse",
            new
            {
                testResultItemType.TestResultItemTypeId,
                testResultItemType.SharedId,
                TestResultItemType = testResultItemType.Name,
                testResultItemType.StartYearId,
                testResultItemType.EndYearId,
                testResultItemType.TestReturnValueTypeId,
                testResultItemType.NoLongerInUse
            });
    }

    public async Task UpdateAsync(CoreTestResultItemType testResultItemType, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        await connection.ExecuteAsync(
            "EXEC dbo.spuTestResultItemType @TestResultItemTypeId, @SharedId, @TestResultItemType, @StartYearId, @EndYearId, @TestReturnValueTypeId, @NoLongerInUse",
            new
            {
                testResultItemType.TestResultItemTypeId,
                testResultItemType.SharedId,
                TestResultItemType = testResultItemType.Name,
                testResultItemType.StartYearId,
                testResultItemType.EndYearId,
                testResultItemType.TestReturnValueTypeId,
                testResultItemType.NoLongerInUse
            });
    }

    public async Task DeleteAsync(Guid testResultItemTypeId, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        await connection.ExecuteAsync("EXEC dbo.spdTestResultItemType @TestResultItemTypeId", new { TestResultItemTypeId = testResultItemTypeId });
    }
}
