namespace PTL.Core.CategoryItemType;

public interface ICategoryItemTypeRepository
{
    // spgCategoryItemTypeByYearId
    Task<IReadOnlyList<CategoryItemType>> GetByYearAsync(int yearId, CancellationToken cancellationToken = default);

    // spiCategoryItemType
    Task CreateAsync(CategoryItemType categoryItemType, CancellationToken cancellationToken = default);

    // spuCategoryItemType
    Task UpdateAsync(CategoryItemType categoryItemType, CancellationToken cancellationToken = default);

    // spdCategoryItemType
    Task DeleteAsync(Guid categoryItemTypeId, CancellationToken cancellationToken = default);
}
