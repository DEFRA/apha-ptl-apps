using PTL.Core.TestResultItemType;

namespace PTL.Api.Tests.TestResultItemType;

internal sealed class FakeTestResultItemTypeRepository : ITestResultItemTypeRepository
{
    public Dictionary<int, List<PTL.Core.TestResultItemType.TestResultItemType>> ItemsByYear { get; } = [];
    public List<PTL.Core.TestResultItemType.TestResultItemType> Created { get; } = [];
    public List<PTL.Core.TestResultItemType.TestResultItemType> Updated { get; } = [];
    public List<Guid> Deleted { get; } = [];

    public Task<IReadOnlyList<PTL.Core.TestResultItemType.TestResultItemType>> GetByYearAsync(int yearId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<PTL.Core.TestResultItemType.TestResultItemType>>(
            ItemsByYear.TryGetValue(yearId, out var items) ? items : []);

    public Task CreateAsync(PTL.Core.TestResultItemType.TestResultItemType testResultItemType, CancellationToken cancellationToken = default)
    {
        Created.Add(testResultItemType);
        for (var year = testResultItemType.StartYearId; year <= testResultItemType.EndYearId; year++)
        {
            if (!ItemsByYear.TryGetValue(year, out var items))
            {
                items = [];
                ItemsByYear[year] = items;
            }

            items.Add(testResultItemType);
        }

        return Task.CompletedTask;
    }

    public Task UpdateAsync(PTL.Core.TestResultItemType.TestResultItemType testResultItemType, CancellationToken cancellationToken = default)
    {
        Updated.Add(testResultItemType);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid testResultItemTypeId, CancellationToken cancellationToken = default)
    {
        Deleted.Add(testResultItemTypeId);
        foreach (var items in ItemsByYear.Values)
        {
            items.RemoveAll(i => i.TestResultItemTypeId == testResultItemTypeId);
        }

        return Task.CompletedTask;
    }
}
