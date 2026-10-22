using PTL.Core.TestMethodItemType;

namespace PTL.Api.Tests.TestMethodItemType;

internal sealed class FakeTestMethodItemTypeRepository : ITestMethodItemTypeRepository
{
    public Dictionary<int, List<PTL.Core.TestMethodItemType.TestMethodItemType>> ItemsByYear { get; } = [];
    public List<PTL.Core.TestMethodItemType.TestMethodItemType> Created { get; } = [];
    public List<PTL.Core.TestMethodItemType.TestMethodItemType> Updated { get; } = [];
    public List<Guid> Deleted { get; } = [];

    public Task<IReadOnlyList<PTL.Core.TestMethodItemType.TestMethodItemType>> GetByYearAsync(int yearId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<PTL.Core.TestMethodItemType.TestMethodItemType>>(
            ItemsByYear.TryGetValue(yearId, out var items) ? items : []);

    public Task CreateAsync(PTL.Core.TestMethodItemType.TestMethodItemType testMethodItemType, CancellationToken cancellationToken = default)
    {
        Created.Add(testMethodItemType);
        for (var year = testMethodItemType.StartYearId; year <= testMethodItemType.EndYearId; year++)
        {
            if (!ItemsByYear.TryGetValue(year, out var items))
            {
                items = [];
                ItemsByYear[year] = items;
            }

            items.Add(testMethodItemType);
        }

        return Task.CompletedTask;
    }

    public Task UpdateAsync(PTL.Core.TestMethodItemType.TestMethodItemType testMethodItemType, CancellationToken cancellationToken = default)
    {
        Updated.Add(testMethodItemType);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid testMethodItemTypeId, CancellationToken cancellationToken = default)
    {
        Deleted.Add(testMethodItemTypeId);
        foreach (var items in ItemsByYear.Values)
        {
            items.RemoveAll(i => i.TestMethodItemTypeId == testMethodItemTypeId);
        }

        return Task.CompletedTask;
    }
}
