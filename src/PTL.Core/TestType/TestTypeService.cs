using PTL.Core.Lookup;

namespace PTL.Core.TestType;

// Implements the legacy TestTypeList.aspx.vb year-versioning algorithm: editing a row never
// mutates history in place, it splits the row into year-bounded slices instead (carrying a shared
// SharedId across every split row), so a change made this year never alters what a past
// Scheme/Distribution/Tabulation displayed. See docs/Parity-Analysis/
// 11-InternalWeb-SchemeItemTypes-Parity-Analysis.md §11.3 for the full algorithm this reproduces,
// and §11.2.5 for the two ways this differs from Category Items: SharedId propagation, the
// change-detection guard on Update, and year-scoped (not unconditional) delete.
public sealed class TestTypeService(ITestTypeRepository repository, ILookupRepository lookupRepository) : ITestTypeService
{
    public Task<IReadOnlyList<TestType>> GetByYearAsync(int yearId, CancellationToken cancellationToken = default) =>
        repository.GetByYearAsync(yearId, cancellationToken);

    public async Task<TestType> CreateAsync(string name, bool noLongerInUse, int yearId, CancellationToken cancellationToken = default)
    {
        var trimmedName = name.Trim();
        Validate(trimmedName);

        var settings = await lookupRepository.GetSystemSettingsAsync(cancellationToken);
        var newItem = new TestType
        {
            TestTypeId = Guid.NewGuid(),
            SharedId = Guid.NewGuid(),
            Name = trimmedName,
            StartYearId = yearId,
            EndYearId = settings.FinalYearId,
            NoLongerInUse = noLongerInUse
        };

        await repository.CreateAsync(newItem, cancellationToken);
        return newItem;
    }

    public async Task<TestType?> UpdateAsync(Guid testTypeId, string name, bool noLongerInUse, int yearId, CancellationToken cancellationToken = default)
    {
        var trimmedName = name.Trim();
        Validate(trimmedName);

        var existing = (await repository.GetByYearAsync(yearId, cancellationToken))
            .FirstOrDefault(i => i.TestTypeId == testTypeId);
        if (existing is null)
        {
            return null;
        }

        if (existing.ForeignInstances != 0)
        {
            throw new TestTypeInUseException(BuildInUseMessage(existing, "edited"));
        }

        // Matches legacy's change-detection guard - no split/write at all if nothing changed.
        if (existing.Name == trimmedName && existing.NoLongerInUse == noLongerInUse)
        {
            return existing;
        }

        var settings = await lookupRepository.GetSystemSettingsAsync(cancellationToken);
        return await SplitOnEditAsync(existing, trimmedName, noLongerInUse, yearId, settings.CurrentYearId, settings.FinalYearId, cancellationToken);
    }

    public async Task<TestType?> SetNoLongerInUseAsync(Guid testTypeId, bool noLongerInUse, int yearId, CancellationToken cancellationToken = default)
    {
        var existing = (await repository.GetByYearAsync(yearId, cancellationToken))
            .FirstOrDefault(i => i.TestTypeId == testTypeId);
        if (existing is null)
        {
            return null;
        }

        existing.NoLongerInUse = noLongerInUse;
        await repository.UpdateAsync(existing, cancellationToken);
        return existing;
    }

    public async Task<TestTypeDeleteResult> DeleteAsync(Guid testTypeId, int yearId, CancellationToken cancellationToken = default)
    {
        var existing = (await repository.GetByYearAsync(yearId, cancellationToken))
            .FirstOrDefault(i => i.TestTypeId == testTypeId);
        if (existing is null)
        {
            return new TestTypeDeleteResult(false, "Test type was not found.");
        }

        if (existing.ForeignInstances != 0)
        {
            return new TestTypeDeleteResult(false, BuildInUseMessage(existing, "removed"));
        }

        // Matches legacy GridViewTestType_RowDeleting: hard-delete only when the row started in
        // the year currently selected; otherwise soft-end it at the end of the prior year so
        // historical validity (past Schemes/Distributions/Tabulations) is preserved.
        if (existing.StartYearId == yearId)
        {
            await repository.DeleteAsync(testTypeId, cancellationToken);
        }
        else
        {
            existing.EndYearId = yearId - 1;
            await repository.UpdateAsync(existing, cancellationToken);
        }

        return new TestTypeDeleteResult(true, null);
    }

    // Mirrors TestTypeList.aspx.vb::GridViewTestType_RowUpdating exactly, including SharedId
    // propagation across every split row (absent on Category/Criterion Items' algorithm).
    private async Task<TestType> SplitOnEditAsync(
        TestType existing, string newName, bool newNoLongerInUse, int selectedYearId, int currentYearId, int finalYearId, CancellationToken cancellationToken)
    {
        if (existing.StartYearId == selectedYearId)
        {
            if (selectedYearId == currentYearId)
            {
                // A new row covers just this year with the edited values; the original row is
                // pushed forward to start next year, keeping its pre-edit values.
                var thisYear = new TestType
                {
                    TestTypeId = Guid.NewGuid(),
                    SharedId = existing.SharedId,
                    Name = newName,
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
            existing.NoLongerInUse = newNoLongerInUse;
            await repository.UpdateAsync(existing, cancellationToken);
            return existing;
        }

        if (selectedYearId == currentYearId)
        {
            var thisYearSplit = new TestType
            {
                TestTypeId = Guid.NewGuid(),
                SharedId = existing.SharedId,
                Name = newName,
                StartYearId = selectedYearId,
                EndYearId = selectedYearId,
                NoLongerInUse = newNoLongerInUse
            };
            await repository.CreateAsync(thisYearSplit, cancellationToken);

            var future = new TestType
            {
                TestTypeId = Guid.NewGuid(),
                SharedId = existing.SharedId,
                Name = existing.Name,
                StartYearId = selectedYearId + 1,
                EndYearId = finalYearId,
                NoLongerInUse = existing.NoLongerInUse
            };
            await repository.CreateAsync(future, cancellationToken);

            existing.EndYearId = selectedYearId - 1;
            await repository.UpdateAsync(existing, cancellationToken);
            return thisYearSplit;
        }

        var nextYearSplit = new TestType
        {
            TestTypeId = Guid.NewGuid(),
            SharedId = existing.SharedId,
            Name = newName,
            StartYearId = selectedYearId,
            EndYearId = finalYearId,
            NoLongerInUse = newNoLongerInUse
        };
        await repository.CreateAsync(nextYearSplit, cancellationToken);

        existing.EndYearId = nextYearSplit.StartYearId - 1;
        await repository.UpdateAsync(existing, cancellationToken);
        return nextYearSplit;
    }

    private static void Validate(string name)
    {
        var result = TestTypeValidator.Validate(name);
        if (!result.IsValid)
        {
            throw new TestTypeValidationException(result.Errors);
        }
    }

    // Matches legacy's exact OnClientClick alert wording (TestTypeList.aspx.vb): "This Test is
    // being used on...".
    private static string BuildInUseMessage(TestType item, string action) =>
        item.ForeignInstances == -1
            ? $"This Test is being used on a distribution and cannot be {action}. It is also being used on the following Scheme(s): {item.ForeignInstancesString}"
            : $"This Test is being used on {item.ForeignInstances} Scheme(s): {item.ForeignInstancesString} and cannot be {action}.";
}
