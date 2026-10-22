namespace PTL.Core.CriterionItemType;

public interface ICriterionItemTypeRepository
{
    // spgCriterionItemTypeByYearId
    Task<IReadOnlyList<CriterionItemType>> GetByYearAsync(int yearId, CancellationToken cancellationToken = default);

    // spiCriterionItemType
    Task CreateAsync(CriterionItemType criterionItemType, CancellationToken cancellationToken = default);

    // spuCriterionItemType
    Task UpdateAsync(CriterionItemType criterionItemType, CancellationToken cancellationToken = default);

    // spdCriterionItemType
    Task DeleteAsync(Guid criterionItemTypeId, CancellationToken cancellationToken = default);
}
