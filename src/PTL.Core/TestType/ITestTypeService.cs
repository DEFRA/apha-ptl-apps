namespace PTL.Core.TestType;

public interface ITestTypeService
{
    Task<IReadOnlyList<TestType>> GetByYearAsync(int yearId, CancellationToken cancellationToken = default);

    Task<TestType> CreateAsync(string name, bool noLongerInUse, int yearId, CancellationToken cancellationToken = default);

    // Null means no item with that id exists in yearId's result set. Throws TestTypeInUseException
    // when the item is still referenced by a Scheme/Distribution. Matches legacy's change-detection
    // guard: if neither Name nor NoLongerInUse actually changed, the existing item is returned
    // unmodified with no split/write at all (GridViewTestType_RowUpdating only acts "If Not
    // item.Name = newName Or Not item.NoLongerInUse = newNoLongerInUse").
    Task<TestType?> UpdateAsync(Guid testTypeId, string name, bool noLongerInUse, int yearId, CancellationToken cancellationToken = default);

    // Allowed even when the item is in use - matches legacy ButtonSave_Click's explicit comment
    // that "No Longer in Use" can be changed regardless of the edit/remove usage guard.
    Task<TestType?> SetNoLongerInUseAsync(Guid testTypeId, bool noLongerInUse, int yearId, CancellationToken cancellationToken = default);

    // Year-scoped, unlike Category Items: hard-deletes only when the item started in yearId
    // (the year currently selected); otherwise soft-ends it (EndYearId = yearId - 1) to preserve
    // historical validity - matches legacy GridViewTestType_RowDeleting exactly.
    Task<TestTypeDeleteResult> DeleteAsync(Guid testTypeId, int yearId, CancellationToken cancellationToken = default);
}

public sealed record TestTypeDeleteResult(bool Success, string? Message);
