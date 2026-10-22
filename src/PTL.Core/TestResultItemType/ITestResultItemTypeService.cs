namespace PTL.Core.TestResultItemType;

public interface ITestResultItemTypeService
{
    // searchText matches legacy TestResultItemTypeList.aspx.vb's ButtonSearchByResultItemName_Click:
    // when non-blank, only items whose Name contains searchText (case-insensitive) are returned;
    // when blank, every item for yearId is returned - matching
    // TestResultItemTypeCollection.GetSearchedList / GetSortedFilteredList.
    Task<IReadOnlyList<TestResultItemType>> GetByYearAsync(int yearId, string? searchText = null, CancellationToken cancellationToken = default);

    Task<TestResultItemType> CreateAsync(string name, Guid testReturnValueTypeId, bool noLongerInUse, int yearId, CancellationToken cancellationToken = default);

    // Null means no item with that id exists in yearId's result set. Throws
    // TestResultItemTypeInUseException when the item is still referenced by a
    // Scheme/Distribution. Matches legacy's 3-way change-detection guard: if none of Name,
    // Return Type, or NoLongerInUse actually changed, the existing item is returned unmodified
    // with no split/write at all (GridViewTestResultItemType_RowUpdating only acts "If Not
    // item.Name = newName Or Not item.ReturnValueTypeId = newReturnValueId Or Not
    // item.NoLongerInUse = newNoLongerInUse").
    Task<TestResultItemType?> UpdateAsync(Guid testResultItemTypeId, string name, Guid testReturnValueTypeId, bool noLongerInUse, int yearId, CancellationToken cancellationToken = default);

    // Allowed even when the item is in use - matches legacy ButtonSave_Click's explicit comment
    // that "No Longer in Use" can be changed regardless of the edit/remove usage guard.
    Task<TestResultItemType?> SetNoLongerInUseAsync(Guid testResultItemTypeId, bool noLongerInUse, int yearId, CancellationToken cancellationToken = default);

    // Year-scoped, unlike Category Items: hard-deletes only when the item started in yearId
    // (the year currently selected); otherwise soft-ends it (EndYearId = yearId - 1) to preserve
    // historical validity - matches legacy GridViewTestResultItemType_RowDeleting exactly.
    Task<TestResultItemTypeDeleteResult> DeleteAsync(Guid testResultItemTypeId, int yearId, CancellationToken cancellationToken = default);
}

public sealed record TestResultItemTypeDeleteResult(bool Success, string? Message);
