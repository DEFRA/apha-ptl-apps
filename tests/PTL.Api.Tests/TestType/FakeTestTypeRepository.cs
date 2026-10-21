using PTL.Core.TestType;

namespace PTL.Api.Tests.TestType;

internal sealed class FakeTestTypeRepository : ITestTypeRepository
{
    public Dictionary<int, List<PTL.Core.TestType.TestType>> ItemsByYear { get; } = [];
    public List<PTL.Core.TestType.TestType> Created { get; } = [];
    public List<PTL.Core.TestType.TestType> Updated { get; } = [];
    public List<Guid> Deleted { get; } = [];

    public Task<IReadOnlyList<PTL.Core.TestType.TestType>> GetByYearAsync(int yearId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<PTL.Core.TestType.TestType>>(
            ItemsByYear.TryGetValue(yearId, out var items) ? items : []);

    public Task CreateAsync(PTL.Core.TestType.TestType testType, CancellationToken cancellationToken = default)
    {
        Created.Add(testType);
        for (var year = testType.StartYearId; year <= testType.EndYearId; year++)
        {
            if (!ItemsByYear.TryGetValue(year, out var items))
            {
                items = [];
                ItemsByYear[year] = items;
            }

            items.Add(testType);
        }

        return Task.CompletedTask;
    }

    public Task UpdateAsync(PTL.Core.TestType.TestType testType, CancellationToken cancellationToken = default)
    {
        Updated.Add(testType);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid testTypeId, CancellationToken cancellationToken = default)
    {
        Deleted.Add(testTypeId);
        foreach (var items in ItemsByYear.Values)
        {
            items.RemoveAll(i => i.TestTypeId == testTypeId);
        }

        return Task.CompletedTask;
    }
}
