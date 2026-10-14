namespace PTL.Core.TestType;

public interface ITestTypeRepository
{
    // spgTestTypeByYearId
    Task<IReadOnlyList<TestType>> GetByYearAsync(int yearId, CancellationToken cancellationToken = default);

    // spiTestType
    Task CreateAsync(TestType testType, CancellationToken cancellationToken = default);

    // spuTestType
    Task UpdateAsync(TestType testType, CancellationToken cancellationToken = default);

    // spdTestType
    Task DeleteAsync(Guid testTypeId, CancellationToken cancellationToken = default);
}
