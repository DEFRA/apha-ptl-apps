using PTL.Core.Lookup;

namespace PTL.Core.CategoryItemType;

// Implements the legacy CategoryItemTypeList.aspx.vb year-versioning algorithm: editing a row
// never mutates history in place, it splits the row into year-bounded slices instead, so a change
// made this year never alters what a past Scheme/Distribution/Tabulation displayed. See
// docs/Parity-Analysis/11-InternalWeb-SchemeItemTypes-Parity-Analysis.md §11.3 for the full
// algorithm this reproduces.
public sealed class CategoryItemTypeService(ICategoryItemTypeRepository repository, ILookupRepository lookupRepository) : ICategoryItemTypeService
{
    public Task<IReadOnlyList<CategoryItemType>> GetByYearAsync(int yearId, CancellationToken cancellationToken = default) =>
        repository.GetByYearAsync(yearId, cancellationToken);

    public async Task<CategoryItemType> CreateAsync(string name, bool noLongerInUse, int yearId, CancellationToken cancellationToken = default)
    {
        var trimmedName = name.Trim();
        Validate(trimmedName);

        var settings = await lookupRepository.GetSystemSettingsAsync(cancellationToken);
        var newItem = new CategoryItemType
        {
            CategoryItemTypeId = Guid.NewGuid(),
            Name = trimmedName,
            StartYearId = yearId,
            EndYearId = settings.FinalYearId,
            NoLongerInUse = noLongerInUse
        };

        await repository.CreateAsync(newItem, cancellationToken);
        return newItem;
    }

    public async Task<CategoryItemType?> UpdateAsync(Guid categoryItemTypeId, string name, bool noLongerInUse, int yearId, CancellationToken cancellationToken = default)
    {
        var trimmedName = name.Trim();
        Validate(trimmedName);

        var existing = (await repository.GetByYearAsync(yearId, cancellationToken))
            .FirstOrDefault(i => i.CategoryItemTypeId == categoryItemTypeId);
        if (existing is null)
        {
            return null;
        }

        if (existing.ForeignInstances != 0)
        {
            throw new CategoryItemTypeInUseException(BuildInUseMessage(existing, "edited"));
        }

        var settings = await lookupRepository.GetSystemSettingsAsync(cancellationToken);
        return await SplitOnEditAsync(existing, trimmedName, noLongerInUse, yearId, settings.CurrentYearId, settings.FinalYearId, cancellationToken);
    }

    public async Task<CategoryItemType?> SetNoLongerInUseAsync(Guid categoryItemTypeId, bool noLongerInUse, int yearId, CancellationToken cancellationToken = default)
    {
        var existing = (await repository.GetByYearAsync(yearId, cancellationToken))
            .FirstOrDefault(i => i.CategoryItemTypeId == categoryItemTypeId);
        if (existing is null)
        {
            return null;
        }

        existing.NoLongerInUse = noLongerInUse;
        await repository.UpdateAsync(existing, cancellationToken);
        return existing;
    }

    public async Task<CategoryItemTypeDeleteResult> DeleteAsync(Guid categoryItemTypeId, CancellationToken cancellationToken = default)
    {
        var currentYears = await lookupRepository.GetCurrentYearsAsync(cancellationToken);
        foreach (var year in currentYears)
        {
            var match = (await repository.GetByYearAsync(year.YearId, cancellationToken))
                .FirstOrDefault(i => i.CategoryItemTypeId == categoryItemTypeId);
            if (match is null)
            {
                continue;
            }

            if (match.ForeignInstances != 0)
            {
                return new CategoryItemTypeDeleteResult(false, BuildInUseMessage(match, "removed"));
            }

            await repository.DeleteAsync(categoryItemTypeId, cancellationToken);
            return new CategoryItemTypeDeleteResult(true, null);
        }

        return new CategoryItemTypeDeleteResult(false, "Category item was not found.");
    }

    // Mirrors CategoryItemTypeList.aspx.vb::GridViewCategoryItemType_RowUpdating exactly.
    private async Task<CategoryItemType> SplitOnEditAsync(
        CategoryItemType existing, string newName, bool newNoLongerInUse, int selectedYearId, int currentYearId, int finalYearId, CancellationToken cancellationToken)
    {
        if (existing.StartYearId == selectedYearId)
        {
            if (selectedYearId == currentYearId)
            {
                // A new row covers just this year with the edited values; the original row is
                // pushed forward to start next year, keeping its pre-edit values.
                var thisYear = new CategoryItemType
                {
                    CategoryItemTypeId = Guid.NewGuid(),
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
            var thisYearSplit = new CategoryItemType
            {
                CategoryItemTypeId = Guid.NewGuid(),
                Name = newName,
                StartYearId = selectedYearId,
                EndYearId = selectedYearId,
                NoLongerInUse = newNoLongerInUse
            };
            await repository.CreateAsync(thisYearSplit, cancellationToken);

            var future = new CategoryItemType
            {
                CategoryItemTypeId = Guid.NewGuid(),
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

        var nextYearSplit = new CategoryItemType
        {
            CategoryItemTypeId = Guid.NewGuid(),
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
        var result = CategoryItemTypeValidator.Validate(name);
        if (!result.IsValid)
        {
            throw new CategoryItemTypeValidationException(result.Errors);
        }
    }

    // Matches legacy's exact OnClientClick alert wording (CategoryItemTypeList.aspx.vb).
    private static string BuildInUseMessage(CategoryItemType item, string action) =>
        item.ForeignInstances == -1
            ? $"This Category Item is being used on a distribution and cannot be {action}. It is also being used on the following Scheme(s): {item.ForeignInstancesString}"
            : $"This Category Item is being used on {item.ForeignInstances} Scheme(s): {item.ForeignInstancesString} and cannot be {action}.";
}
