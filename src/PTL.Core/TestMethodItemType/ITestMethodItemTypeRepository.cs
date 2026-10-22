namespace PTL.Core.TestMethodItemType;

public interface ITestMethodItemTypeRepository
{
    // spgTestMethodItemTypeByYearId
    Task<IReadOnlyList<TestMethodItemType>> GetByYearAsync(int yearId, CancellationToken cancellationToken = default);

    // spiTestMethodItemType
    Task CreateAsync(TestMethodItemType testMethodItemType, CancellationToken cancellationToken = default);

    // spuTestMethodItemType
    Task UpdateAsync(TestMethodItemType testMethodItemType, CancellationToken cancellationToken = default);

    // spdTestMethodItemType
    Task DeleteAsync(Guid testMethodItemTypeId, CancellationToken cancellationToken = default);
}
