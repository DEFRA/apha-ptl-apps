using PTL.Core.CategoryItemType;

namespace PTL.Api.Tests.CategoryItemType;

internal sealed class FakeCategoryItemTypeRepository : ICategoryItemTypeRepository
{
    public Dictionary<int, List<PTL.Core.CategoryItemType.CategoryItemType>> ItemsByYear { get; } = [];
    public List<PTL.Core.CategoryItemType.CategoryItemType> Created { get; } = [];
    public List<PTL.Core.CategoryItemType.CategoryItemType> Updated { get; } = [];
    public List<Guid> Deleted { get; } = [];

    public Task<IReadOnlyList<PTL.Core.CategoryItemType.CategoryItemType>> GetByYearAsync(int yearId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<PTL.Core.CategoryItemType.CategoryItemType>>(
            ItemsByYear.TryGetValue(yearId, out var items) ? items : []);

    public Task CreateAsync(PTL.Core.CategoryItemType.CategoryItemType categoryItemType, CancellationToken cancellationToken = default)
    {
        Created.Add(categoryItemType);
        for (var year = categoryItemType.StartYearId; year <= categoryItemType.EndYearId; year++)
        {
            if (!ItemsByYear.TryGetValue(year, out var items))
            {
                items = [];
                ItemsByYear[year] = items;
            }

            items.Add(categoryItemType);
        }

        return Task.CompletedTask;
    }

    public Task UpdateAsync(PTL.Core.CategoryItemType.CategoryItemType categoryItemType, CancellationToken cancellationToken = default)
    {
        Updated.Add(categoryItemType);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid categoryItemTypeId, CancellationToken cancellationToken = default)
    {
        Deleted.Add(categoryItemTypeId);
        foreach (var items in ItemsByYear.Values)
        {
            items.RemoveAll(i => i.CategoryItemTypeId == categoryItemTypeId);
        }

        return Task.CompletedTask;
    }
}
