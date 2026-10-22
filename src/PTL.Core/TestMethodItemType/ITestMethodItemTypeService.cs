namespace PTL.Core.TestMethodItemType;

public interface ITestMethodItemTypeService
{
    // searchText matches legacy TestMethodItemTypeList.aspx.vb's ButtonSearchByMethodItemName_Click:
    // when non-blank, only items whose Name contains searchText (case-insensitive) are returned;
    // when blank, every item for yearId is returned - matching
    // TestMethodItemTypeCollection.GetSearchedList / GetSortedFilteredList.
    Task<IReadOnlyList<TestMethodItemType>> GetByYearAsync(int yearId, string? searchText = null, CancellationToken cancellationToken = default);

    Task<TestMethodItemType> CreateAsync(string name, Guid testReturnValueTypeId, bool noLongerInUse, int yearId, CancellationToken cancellationToken = default);

    // Null means no item with that id exists in yearId's result set. Throws
    // TestMethodItemTypeInUseException when the item is still referenced by a
    // Scheme/Distribution. Matches legacy's 3-way change-detection guard: if none of Name,
    // Return Type, or NoLongerInUse actually changed, the existing item is returned unmodified
    // with no split/write at all (GridViewTestMethodItemType_RowUpdating only acts "If Not
    // item.Name = newName Or Not item.ReturnValueTypeId = newReturnValueId Or Not
    // item.NoLongerInUse = newNoLongerInUse").
    Task<TestMethodItemType?> UpdateAsync(Guid testMethodItemTypeId, string name, Guid testReturnValueTypeId, bool noLongerInUse, int yearId, CancellationToken cancellationToken = default);

    // Allowed even when the item is in use - matches legacy ButtonSave_Click's explicit comment
    // that "No Longer in Use" can be changed regardless of the edit/remove usage guard.
    Task<TestMethodItemType?> SetNoLongerInUseAsync(Guid testMethodItemTypeId, bool noLongerInUse, int yearId, CancellationToken cancellationToken = default);

    // Year-scoped, unlike Category Items: hard-deletes only when the item started in yearId
    // (the year currently selected); otherwise soft-ends it (EndYearId = yearId - 1) to preserve
    // historical validity - matches legacy GridViewTestMethodItemType_RowDeleting exactly.
    Task<TestMethodItemTypeDeleteResult> DeleteAsync(Guid testMethodItemTypeId, int yearId, CancellationToken cancellationToken = default);
}

public sealed record TestMethodItemTypeDeleteResult(bool Success, string? Message);
