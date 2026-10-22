using PTL.Core.Lookup;
using PTL.Core.ReturnTypeItems;

namespace PTL.Core.TestResultItemType;

// Implements the legacy TestResultItemTypeList.aspx.vb year-versioning algorithm: editing a row
// never mutates history in place, it splits the row into year-bounded slices instead (carrying a
// shared SharedId across every split row), so a change made this year never alters what a past
// Scheme/Distribution/Tabulation displayed. Matches TestTypeService's algorithm exactly, extended
// with a required Return Type foreign key and a 3-way (not 2-way) change-detection guard on
// Update. See docs/Parity-Analysis/11-InternalWeb-SchemeItemTypes-Parity-Analysis.md, Slice 4.
public sealed class TestResultItemTypeService(ITestResultItemTypeRepository repository, ILookupRepository lookupRepository) : ITestResultItemTypeService
{
    public async Task<IReadOnlyList<TestResultItemType>> GetByYearAsync(int yearId, string? searchText = null, CancellationToken cancellationToken = default)
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

    public async Task<TestResultItemType> CreateAsync(string name, Guid testReturnValueTypeId, bool noLongerInUse, int yearId, CancellationToken cancellationToken = default)
    {
        var trimmedName = name.Trim();
        Validate(trimmedName, testReturnValueTypeId);

        var settings = await lookupRepository.GetSystemSettingsAsync(cancellationToken);
        var returnValueTypeName = await ResolveReturnValueTypeNameAsync(testReturnValueTypeId, cancellationToken);
        var newItem = new TestResultItemType
        {
            TestResultItemTypeId = Guid.NewGuid(),
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

    public async Task<TestResultItemType?> UpdateAsync(Guid testResultItemTypeId, string name, Guid testReturnValueTypeId, bool noLongerInUse, int yearId, CancellationToken cancellationToken = default)
    {
        var trimmedName = name.Trim();
        Validate(trimmedName, testReturnValueTypeId);

        var existing = (await repository.GetByYearAsync(yearId, cancellationToken))
            .FirstOrDefault(i => i.TestResultItemTypeId == testResultItemTypeId);
        if (existing is null)
        {
            return null;
        }

        if (existing.ForeignInstances != 0)
        {
            throw new TestResultItemTypeInUseException(BuildInUseMessage(existing, "edited"));
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
            trimmedName,
            testReturnValueTypeId,
            returnValueTypeName,
            noLongerInUse,
            yearId,
            settings.CurrentYearId,
            settings.FinalYearId,
            repository.CreateAsync,
            repository.UpdateAsync,
            cancellationToken);
    }

    public async Task<TestResultItemType?> SetNoLongerInUseAsync(Guid testResultItemTypeId, bool noLongerInUse, int yearId, CancellationToken cancellationToken = default)
    {
        var existing = (await repository.GetByYearAsync(yearId, cancellationToken))
            .FirstOrDefault(i => i.TestResultItemTypeId == testResultItemTypeId);
        if (existing is null)
        {
            return null;
        }

        existing.NoLongerInUse = noLongerInUse;
        await repository.UpdateAsync(existing, cancellationToken);
        return existing;
    }

    public async Task<TestResultItemTypeDeleteResult> DeleteAsync(Guid testResultItemTypeId, int yearId, CancellationToken cancellationToken = default)
    {
        var existing = (await repository.GetByYearAsync(yearId, cancellationToken))
            .FirstOrDefault(i => i.TestResultItemTypeId == testResultItemTypeId);
        if (existing is null)
        {
            return new TestResultItemTypeDeleteResult(false, "Test result item was not found.");
        }

        if (existing.ForeignInstances != 0)
        {
            return new TestResultItemTypeDeleteResult(false, BuildInUseMessage(existing, "removed"));
        }

        // Matches legacy GridViewTestResultItemType_RowDeleting: hard-delete only when the row
        // started in the year currently selected; otherwise soft-end it at the end of the prior
        // year so historical validity (past Schemes/Distributions/Tabulations) is preserved.
        if (existing.StartYearId == yearId)
        {
            await repository.DeleteAsync(testResultItemTypeId, cancellationToken);
        }
        else
        {
            existing.EndYearId = yearId - 1;
            await repository.UpdateAsync(existing, cancellationToken);
        }

        return new TestResultItemTypeDeleteResult(true, null);
    }

    private async Task<string> ResolveReturnValueTypeNameAsync(Guid testReturnValueTypeId, CancellationToken cancellationToken)
    {
        var returnValueTypes = await lookupRepository.GetTestReturnValueTypesAsync(cancellationToken);
        return returnValueTypes.FirstOrDefault(r => r.TestReturnValueTypeId == testReturnValueTypeId)?.Name ?? string.Empty;
    }

    private static void Validate(string name, Guid testReturnValueTypeId)
    {
        var result = TestResultItemTypeValidator.Validate(name, testReturnValueTypeId);
        if (!result.IsValid)
        {
            throw new TestResultItemTypeValidationException(result.Errors);
        }
    }

    // Matches legacy's exact OnClientClick alert wording (TestResultItemTypeList.aspx.vb): "This
    // Test Result Item is being used on...".
    private static string BuildInUseMessage(TestResultItemType item, string action) =>
        item.ForeignInstances == -1
            ? $"This Test Result Item is being used on a distribution and cannot be {action}. It is also being used on the following Scheme(s): {item.ForeignInstancesString}"
            : $"This Test Result Item is being used on {item.ForeignInstances} Scheme(s): {item.ForeignInstancesString} and cannot be {action}.";
}
