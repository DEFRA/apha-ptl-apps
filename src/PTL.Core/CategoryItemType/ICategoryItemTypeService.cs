namespace PTL.Core.CategoryItemType;

public interface ICategoryItemTypeService
{
    Task<IReadOnlyList<CategoryItemType>> GetByYearAsync(int yearId, CancellationToken cancellationToken = default);

    Task<CategoryItemType> CreateAsync(string name, bool noLongerInUse, int yearId, CancellationToken cancellationToken = default);

    // Null means no item with that id exists in yearId's result set. Throws
    // CategoryItemTypeInUseException when the item is still referenced by a Scheme/Distribution -
    // matches legacy's Edit-link usage guard, enforced server-side here instead of client-side only.
    Task<CategoryItemType?> UpdateAsync(Guid categoryItemTypeId, string name, bool noLongerInUse, int yearId, CancellationToken cancellationToken = default);

    // Allowed even when the item is in use - matches legacy ButtonSave_Click's explicit comment
    // that "No Longer in Use" can be changed regardless of the edit/remove usage guard.
    Task<CategoryItemType?> SetNoLongerInUseAsync(Guid categoryItemTypeId, bool noLongerInUse, int yearId, CancellationToken cancellationToken = default);

    // Hard-deletes unconditionally regardless of which year the item started in - matches legacy
    // GridViewCategoryItemType_RowDeleting exactly (the one module of the 5 Scheme Admin item-type
    // screens that does not year-scope its delete - see docs/Parity-Analysis/
    // 11-InternalWeb-SchemeItemTypes-Parity-Analysis.md §11.3).
    Task<CategoryItemTypeDeleteResult> DeleteAsync(Guid categoryItemTypeId, CancellationToken cancellationToken = default);
}

public sealed record CategoryItemTypeDeleteResult(bool Success, string? Message);
