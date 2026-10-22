using PTL.Core.Lookup;
using PTL.Core.ReturnTypeItems;

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
        return await ReturnTypeItemSplitOnEditAlgorithm.ApplyAsync(
            existing,
            new ReturnTypeItemSplitOnEditAlgorithm.Request(trimmedName, testReturnValueTypeId, returnValueTypeName, noLongerInUse, yearId, settings.CurrentYearId, settings.FinalYearId),
            repository.CreateAsync,
            repository.UpdateAsync,
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
