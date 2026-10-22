namespace PTL.Core.TestResultItemType;

public interface ITestResultItemTypeRepository
{
    // spgTestResultItemTypeByYearId
    Task<IReadOnlyList<TestResultItemType>> GetByYearAsync(int yearId, CancellationToken cancellationToken = default);

    // spiTestResultItemType
    Task CreateAsync(TestResultItemType testResultItemType, CancellationToken cancellationToken = default);

    // spuTestResultItemType
    Task UpdateAsync(TestResultItemType testResultItemType, CancellationToken cancellationToken = default);

    // spdTestResultItemType
    Task DeleteAsync(Guid testResultItemTypeId, CancellationToken cancellationToken = default);
}
