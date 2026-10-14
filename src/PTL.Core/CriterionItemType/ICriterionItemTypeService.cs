namespace PTL.Core.CriterionItemType;

public interface ICriterionItemTypeService
{
    Task<IReadOnlyList<CriterionItemType>> GetByYearAsync(int yearId, CancellationToken cancellationToken = default);

    Task<CriterionItemType> CreateAsync(string name, bool noLongerInUse, int yearId, CancellationToken cancellationToken = default);

    // Null means no item with that id exists in yearId's result set. Throws
    // CriterionItemTypeInUseException when the item is still referenced by a Scheme/Distribution.
    // Unlike Test Types, there is no change-detection guard - legacy's
    // GridViewCriterionItemType_RowUpdating always re-splits on Save, same as Category Items.
    Task<CriterionItemType?> UpdateAsync(Guid criterionItemTypeId, string name, bool noLongerInUse, int yearId, CancellationToken cancellationToken = default);

    // Allowed even when the item is in use - matches legacy ButtonSave_Click's explicit comment
    // that "No Longer in Use" can be changed regardless of the edit/remove usage guard.
    Task<CriterionItemType?> SetNoLongerInUseAsync(Guid criterionItemTypeId, bool noLongerInUse, int yearId, CancellationToken cancellationToken = default);

    // Year-scoped, like Test Types (not unconditional like Category Items): hard-deletes only when
    // the item started in yearId; otherwise soft-ends it (EndYearId = yearId - 1) - matches legacy
    // GridViewCriterionItemType_RowDeleting exactly.
    Task<CriterionItemTypeDeleteResult> DeleteAsync(Guid criterionItemTypeId, int yearId, CancellationToken cancellationToken = default);
}

public sealed record CriterionItemTypeDeleteResult(bool Success, string? Message);
