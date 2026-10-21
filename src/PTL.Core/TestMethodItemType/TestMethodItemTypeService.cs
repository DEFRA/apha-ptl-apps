using PTL.Core.Lookup;

namespace PTL.Core.TestMethodItemType;

// Implements the legacy TestMethodItemTypeList.aspx.vb year-versioning algorithm: editing a row
// never mutates history in place, it splits the row into year-bounded slices instead (carrying a
// shared SharedId across every split row), so a change made this year never alters what a past
// Scheme/Distribution/Tabulation displayed. Identical algorithm to TestResultItemTypeService
// (Slice 4) - 3-way change-detection guard, required Return Type, year-scoped delete. See
// docs/Parity-Analysis/11-InternalWeb-SchemeItemTypes-Parity-Analysis.md, Slice 5.
public sealed class TestMethodItemTypeService(ITestMethodItemTypeRepository repository, ILookupRepository lookupRepository) : ITestMethodItemTypeService
{
    public async Task<IReadOnlyList<TestMethodItemType>> GetByYearAsync(int yearId, string? searchText = null, CancellationToken cancellationToken = default)
    {
        var items = await repository.GetByYearAsync(yearId, cancellationToken);
        if (string.IsNullOrWhiteSpace(searchText))
        {
            return items;
        }

        return items
            .Where(i => i.Name.Contains(searchText, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    public async Task<TestMethodItemType> CreateAsync(string name, Guid testReturnValueTypeId, bool noLongerInUse, int yearId, CancellationToken cancellationToken = default)
    {
        var trimmedName = name.Trim();
        Validate(trimmedName, testReturnValueTypeId);

        var settings = await lookupRepository.GetSystemSettingsAsync(cancellationToken);
        var returnValueTypeName = await ResolveReturnValueTypeNameAsync(testReturnValueTypeId, cancellationToken);
        var newItem = new TestMethodItemType
        {
            TestMethodItemTypeId = Guid.NewGuid(),
            SharedId = Guid.NewGuid(),
            Name = trimmedName,
            TestReturnValueTypeId = testReturnValueTypeId,
            TestReturnValueTypeName = returnValueTypeName,
            StartYearId = yearId,
            EndYearId = settings.FinalYearId,
            NoLongerInUse = noLongerInUse
        };

        await repository.CreateAsync(newItem, cancellationToken);
        return newItem;
    }

    public async Task<TestMethodItemType?> UpdateAsync(Guid testMethodItemTypeId, string name, Guid testReturnValueTypeId, bool noLongerInUse, int yearId, CancellationToken cancellationToken = default)
    {
        var trimmedName = name.Trim();
        Validate(trimmedName, testReturnValueTypeId);

        var existing = (await repository.GetByYearAsync(yearId, cancellationToken))
            .FirstOrDefault(i => i.TestMethodItemTypeId == testMethodItemTypeId);
        if (existing is null)
        {
            return null;
        }

        if (existing.ForeignInstances != 0)
        {
            throw new TestMethodItemTypeInUseException(BuildInUseMessage(existing, "edited"));
        }

        // Matches legacy's 3-way change-detection guard - no split/write at all if nothing changed.
        if (existing.Name == trimmedName && existing.TestReturnValueTypeId == testReturnValueTypeId && existing.NoLongerInUse == noLongerInUse)
        {
            return existing;
        }

        var settings = await lookupRepository.GetSystemSettingsAsync(cancellationToken);
        var returnValueTypeName = await ResolveReturnValueTypeNameAsync(testReturnValueTypeId, cancellationToken);
        return await SplitOnEditAsync(
            existing,
            new SplitOnEditRequest(trimmedName, testReturnValueTypeId, returnValueTypeName, noLongerInUse, yearId, settings.CurrentYearId, settings.FinalYearId),
            cancellationToken);
    }

    public async Task<TestMethodItemType?> SetNoLongerInUseAsync(Guid testMethodItemTypeId, bool noLongerInUse, int yearId, CancellationToken cancellationToken = default)
    {
        var existing = (await repository.GetByYearAsync(yearId, cancellationToken))
            .FirstOrDefault(i => i.TestMethodItemTypeId == testMethodItemTypeId);
        if (existing is null)
        {
            return null;
        }

        existing.NoLongerInUse = noLongerInUse;
        await repository.UpdateAsync(existing, cancellationToken);
        return existing;
    }

    public async Task<TestMethodItemTypeDeleteResult> DeleteAsync(Guid testMethodItemTypeId, int yearId, CancellationToken cancellationToken = default)
    {
        var existing = (await repository.GetByYearAsync(yearId, cancellationToken))
            .FirstOrDefault(i => i.TestMethodItemTypeId == testMethodItemTypeId);
        if (existing is null)
        {
            return new TestMethodItemTypeDeleteResult(false, "Test method item was not found.");
        }

        if (existing.ForeignInstances != 0)
        {
            return new TestMethodItemTypeDeleteResult(false, BuildInUseMessage(existing, "removed"));
        }

        // Matches legacy GridViewTestMethodItemType_RowDeleting: hard-delete only when the row
        // started in the year currently selected; otherwise soft-end it at the end of the prior
        // year so historical validity (past Schemes/Distributions/Tabulations) is preserved.
        if (existing.StartYearId == yearId)
        {
            await repository.DeleteAsync(testMethodItemTypeId, cancellationToken);
        }
        else
        {
            existing.EndYearId = yearId - 1;
            await repository.UpdateAsync(existing, cancellationToken);
        }

        return new TestMethodItemTypeDeleteResult(true, null);
    }

    // Mirrors TestMethodItemTypeList.aspx.vb::GridViewTestMethodItemType_RowUpdating exactly,
    // including SharedId propagation across every split row. The "future" row that is pushed
    // forward keeps the item's pre-edit Return Type (not the new one), matching
    // TestResultItemTypeService's same forward-the-original-values behaviour.
    //
    // Bundles the edited values and year context into one record - the method itself was flagged
    // for exceeding Sonar's 7-parameter limit (S107) once the Return Type fields were added on top
    // of TestTypeService's original 7-parameter SplitOnEditAsync.
    private sealed record SplitOnEditRequest(
        string NewName,
        Guid NewTestReturnValueTypeId,
        string NewTestReturnValueTypeName,
        bool NewNoLongerInUse,
        int SelectedYearId,
        int CurrentYearId,
        int FinalYearId);

    private async Task<TestMethodItemType> SplitOnEditAsync(TestMethodItemType existing, SplitOnEditRequest request, CancellationToken cancellationToken)
    {
        var (newName, newTestReturnValueTypeId, newTestReturnValueTypeName, newNoLongerInUse, selectedYearId, currentYearId, finalYearId) = request;
        if (existing.StartYearId == selectedYearId)
        {
            if (selectedYearId == currentYearId)
            {
                // A new row covers just this year with the edited values; the original row is
                // pushed forward to start next year, keeping its pre-edit values.
                var thisYear = new TestMethodItemType
                {
                    TestMethodItemTypeId = Guid.NewGuid(),
                    SharedId = existing.SharedId,
                    Name = newName,
                    TestReturnValueTypeId = newTestReturnValueTypeId,
                    TestReturnValueTypeName = newTestReturnValueTypeName,
                    StartYearId = selectedYearId,
                    EndYearId = selectedYearId,
                    NoLongerInUse = newNoLongerInUse
                };
                await repository.CreateAsync(thisYear, cancellationToken);

                existing.StartYearId = selectedYearId + 1;
                await repository.UpdateAsync(existing, cancellationToken);
                return thisYear;
            }

            // Editing a row that only starts next year - no history to protect, edit in place.
            existing.Name = newName;
            existing.TestReturnValueTypeId = newTestReturnValueTypeId;
            existing.TestReturnValueTypeName = newTestReturnValueTypeName;
            existing.NoLongerInUse = newNoLongerInUse;
            await repository.UpdateAsync(existing, cancellationToken);
            return existing;
        }

        if (selectedYearId == currentYearId)
        {
            var thisYearSplit = new TestMethodItemType
            {
                TestMethodItemTypeId = Guid.NewGuid(),
                SharedId = existing.SharedId,
                Name = newName,
                TestReturnValueTypeId = newTestReturnValueTypeId,
                TestReturnValueTypeName = newTestReturnValueTypeName,
                StartYearId = selectedYearId,
                EndYearId = selectedYearId,
                NoLongerInUse = newNoLongerInUse
            };
            await repository.CreateAsync(thisYearSplit, cancellationToken);

            var future = new TestMethodItemType
            {
                TestMethodItemTypeId = Guid.NewGuid(),
                SharedId = existing.SharedId,
                Name = existing.Name,
                TestReturnValueTypeId = existing.TestReturnValueTypeId,
                TestReturnValueTypeName = existing.TestReturnValueTypeName,
                StartYearId = selectedYearId + 1,
                EndYearId = finalYearId,
                NoLongerInUse = existing.NoLongerInUse
            };
            await repository.CreateAsync(future, cancellationToken);

            existing.EndYearId = selectedYearId - 1;
            await repository.UpdateAsync(existing, cancellationToken);
            return thisYearSplit;
        }

        var nextYearSplit = new TestMethodItemType
        {
            TestMethodItemTypeId = Guid.NewGuid(),
            SharedId = existing.SharedId,
            Name = newName,
            TestReturnValueTypeId = newTestReturnValueTypeId,
            TestReturnValueTypeName = newTestReturnValueTypeName,
            StartYearId = selectedYearId,
            EndYearId = finalYearId,
            NoLongerInUse = newNoLongerInUse
        };
        await repository.CreateAsync(nextYearSplit, cancellationToken);

        existing.EndYearId = nextYearSplit.StartYearId - 1;
        await repository.UpdateAsync(existing, cancellationToken);
        return nextYearSplit;
    }

    private async Task<string> ResolveReturnValueTypeNameAsync(Guid testReturnValueTypeId, CancellationToken cancellationToken)
    {
        var returnValueTypes = await lookupRepository.GetTestReturnValueTypesAsync(cancellationToken);
        return returnValueTypes.FirstOrDefault(r => r.TestReturnValueTypeId == testReturnValueTypeId)?.Name ?? string.Empty;
    }

    private static void Validate(string name, Guid testReturnValueTypeId)
    {
        var result = TestMethodItemTypeValidator.Validate(name, testReturnValueTypeId);
        if (!result.IsValid)
        {
            throw new TestMethodItemTypeValidationException(result.Errors);
        }
    }

    // Matches legacy's exact OnClientClick alert wording (TestMethodItemTypeList.aspx.vb): "This
    // Method Item is being used on...".
    private static string BuildInUseMessage(TestMethodItemType item, string action) =>
        item.ForeignInstances == -1
            ? $"This Method Item is being used on a distribution and cannot be {action}. It is also being used on the following Scheme(s): {item.ForeignInstancesString}"
            : $"This Method Item is being used on {item.ForeignInstances} Scheme(s): {item.ForeignInstancesString} and cannot be {action}.";
}
