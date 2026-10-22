namespace PTL.Core.ReturnTypeItems;

// Implemented by TestResultItemType/TestMethodItemType so ReturnTypeItemSplitOnEditAlgorithm can
// run their identical year-versioning "split on edit" algorithm once instead of twice - see that
// class's remarks. CreateSplit is a static factory (not a constructor call) because each entity
// also needs to mint its own type-specific Id (TestResultItemTypeId/TestMethodItemTypeId), which
// isn't part of this shared shape.
public interface ISplitOnEditItem<TSelf> where TSelf : ISplitOnEditItem<TSelf>
{
    Guid SharedId { get; }

    string Name { get; set; }

    Guid TestReturnValueTypeId { get; set; }

    string TestReturnValueTypeName { get; set; }

    int StartYearId { get; set; }

    int EndYearId { get; set; }

    bool NoLongerInUse { get; set; }

    static abstract TSelf CreateSplit(Guid sharedId, string name, Guid testReturnValueTypeId, string testReturnValueTypeName, int startYearId, int endYearId, bool noLongerInUse);
}

// Shared by TestResultItemTypeService/TestMethodItemTypeService. Mirrors
// TestResultItemTypeList.aspx.vb::GridViewTestResultItemType_RowUpdating exactly: editing a row
// never mutates history in place, it splits the row into year-bounded slices instead (carrying a
// shared SharedId across every split row), so a change made this year never alters what a past
// Scheme/Distribution/Tabulation displayed. The "future"/pushed-forward row always keeps the
// item's pre-edit Return Type, matching TestTypeService's forward-the-original-values behaviour.
// Previously this entire algorithm (~49 lines) was duplicated verbatim between the two services -
// SonarCloud flagged ~19.6% duplication on each.
public static class ReturnTypeItemSplitOnEditAlgorithm
{
    // Bundles the edited values and year context into one record - ApplyAsync was flagged for
    // exceeding Sonar's 7-parameter limit (S107) once these were passed individually.
    public sealed record Request(
        string NewName,
        Guid NewTestReturnValueTypeId,
        string NewTestReturnValueTypeName,
        bool NewNoLongerInUse,
        int SelectedYearId,
        int CurrentYearId,
        int FinalYearId);

    public static async Task<TItem> ApplyAsync<TItem>(
        TItem existing,
        Request request,
        Func<TItem, CancellationToken, Task> createAsync,
        Func<TItem, CancellationToken, Task> updateAsync,
        CancellationToken cancellationToken)
        where TItem : class, ISplitOnEditItem<TItem>
    {
        var (newName, newTestReturnValueTypeId, newTestReturnValueTypeName, newNoLongerInUse, selectedYearId, currentYearId, finalYearId) = request;
        if (existing.StartYearId == selectedYearId)
        {
            if (selectedYearId == currentYearId)
            {
                // A new row covers just this year with the edited values; the original row is
                // pushed forward to start next year, keeping its pre-edit values.
                var thisYear = TItem.CreateSplit(existing.SharedId, newName, newTestReturnValueTypeId, newTestReturnValueTypeName, selectedYearId, selectedYearId, newNoLongerInUse);
                await createAsync(thisYear, cancellationToken);

                existing.StartYearId = selectedYearId + 1;
                await updateAsync(existing, cancellationToken);
                return thisYear;
            }

            // Editing a row that only starts next year - no history to protect, edit in place.
            existing.Name = newName;
            existing.TestReturnValueTypeId = newTestReturnValueTypeId;
            existing.TestReturnValueTypeName = newTestReturnValueTypeName;
            existing.NoLongerInUse = newNoLongerInUse;
            await updateAsync(existing, cancellationToken);
            return existing;
        }

        if (selectedYearId == currentYearId)
        {
            var thisYearSplit = TItem.CreateSplit(existing.SharedId, newName, newTestReturnValueTypeId, newTestReturnValueTypeName, selectedYearId, selectedYearId, newNoLongerInUse);
            await createAsync(thisYearSplit, cancellationToken);

            var future = TItem.CreateSplit(existing.SharedId, existing.Name, existing.TestReturnValueTypeId, existing.TestReturnValueTypeName, selectedYearId + 1, finalYearId, existing.NoLongerInUse);
            await createAsync(future, cancellationToken);

            existing.EndYearId = selectedYearId - 1;
            await updateAsync(existing, cancellationToken);
            return thisYearSplit;
        }

        var nextYearSplit = TItem.CreateSplit(existing.SharedId, newName, newTestReturnValueTypeId, newTestReturnValueTypeName, selectedYearId, finalYearId, newNoLongerInUse);
        await createAsync(nextYearSplit, cancellationToken);

        existing.EndYearId = nextYearSplit.StartYearId - 1;
        await updateAsync(existing, cancellationToken);
        return nextYearSplit;
    }
}
