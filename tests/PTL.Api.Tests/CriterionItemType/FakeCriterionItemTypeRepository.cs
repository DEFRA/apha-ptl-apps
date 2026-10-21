using PTL.Core.CriterionItemType;

namespace PTL.Api.Tests.CriterionItemType;

internal sealed class FakeCriterionItemTypeRepository : ICriterionItemTypeRepository
{
    public Dictionary<int, List<PTL.Core.CriterionItemType.CriterionItemType>> ItemsByYear { get; } = [];
    public List<PTL.Core.CriterionItemType.CriterionItemType> Created { get; } = [];
    public List<PTL.Core.CriterionItemType.CriterionItemType> Updated { get; } = [];
    public List<Guid> Deleted { get; } = [];

    public Task<IReadOnlyList<PTL.Core.CriterionItemType.CriterionItemType>> GetByYearAsync(int yearId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<PTL.Core.CriterionItemType.CriterionItemType>>(
            ItemsByYear.TryGetValue(yearId, out var items) ? items : []);

    public Task CreateAsync(PTL.Core.CriterionItemType.CriterionItemType criterionItemType, CancellationToken cancellationToken = default)
    {
        Created.Add(criterionItemType);
        for (var year = criterionItemType.StartYearId; year <= criterionItemType.EndYearId; year++)
        {
            if (!ItemsByYear.TryGetValue(year, out var items))
            {
                items = [];
                ItemsByYear[year] = items;
            }

            items.Add(criterionItemType);
        }

        return Task.CompletedTask;
    }

    public Task UpdateAsync(PTL.Core.CriterionItemType.CriterionItemType criterionItemType, CancellationToken cancellationToken = default)
    {
        Updated.Add(criterionItemType);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid criterionItemTypeId, CancellationToken cancellationToken = default)
    {
        Deleted.Add(criterionItemTypeId);
        foreach (var items in ItemsByYear.Values)
        {
            items.RemoveAll(i => i.CriterionItemTypeId == criterionItemTypeId);
        }

        return Task.CompletedTask;
    }
}
