using PTL.Core.Lookup;

namespace PTL.Core.CriterionItemType;

// Implements the legacy CriterionItemTypeList.aspx.vb year-versioning algorithm: editing a row
// never mutates history in place, it splits the row into year-bounded slices instead, so a change
// made this year never alters what a past Scheme/Distribution/Tabulation displayed. See
// docs/Parity-Analysis/11-InternalWeb-SchemeItemTypes-Parity-Analysis.md §11.3 for the shared
// algorithm, and §11.2.2 for how this differs from Category Items: no SharedId, and a year-scoped
// (not unconditional) delete.
public sealed class CriterionItemTypeService(ICriterionItemTypeRepository repository, ILookupRepository lookupRepository) : ICriterionItemTypeService
{
    public Task<IReadOnlyList<CriterionItemType>> GetByYearAsync(int yearId, CancellationToken cancellationToken = default) =>
        repository.GetByYearAsync(yearId, cancellationToken);

    public async Task<CriterionItemType> CreateAsync(string name, bool noLongerInUse, int yearId, CancellationToken cancellationToken = default)
    {
        var trimmedName = name.Trim();
        Validate(trimmedName);

        var settings = await lookupRepository.GetSystemSettingsAsync(cancellationToken);
        var newItem = new CriterionItemType
        {
            CriterionItemTypeId = Guid.NewGuid(),
            Name = trimmedName,
            StartYearId = yearId,
            EndYearId = settings.FinalYearId,
            NoLongerInUse = noLongerInUse
        };

        await repository.CreateAsync(newItem, cancellationToken);
        return newItem;
    }

    public async Task<CriterionItemType?> UpdateAsync(Guid criterionItemTypeId, string name, bool noLongerInUse, int yearId, CancellationToken cancellationToken = default)
    {
        var trimmedName = name.Trim();
        Validate(trimmedName);

        var existing = (await repository.GetByYearAsync(yearId, cancellationToken))
            .FirstOrDefault(i => i.CriterionItemTypeId == criterionItemTypeId);
        if (existing is null)
        {
            return null;
        }

        if (existing.ForeignInstances != 0)
        {
            throw new CriterionItemTypeInUseException(BuildInUseMessage(existing, "edited"));
        }

        var settings = await lookupRepository.GetSystemSettingsAsync(cancellationToken);
        return await SplitOnEditAsync(existing, trimmedName, noLongerInUse, yearId, settings.CurrentYearId, settings.FinalYearId, cancellationToken);
    }

    public async Task<CriterionItemType?> SetNoLongerInUseAsync(Guid criterionItemTypeId, bool noLongerInUse, int yearId, CancellationToken cancellationToken = default)
    {
        var existing = (await repository.GetByYearAsync(yearId, cancellationToken))
            .FirstOrDefault(i => i.CriterionItemTypeId == criterionItemTypeId);
        if (existing is null)
        {
            return null;
        }

        existing.NoLongerInUse = noLongerInUse;
        await repository.UpdateAsync(existing, cancellationToken);
        return existing;
    }

    public async Task<CriterionItemTypeDeleteResult> DeleteAsync(Guid criterionItemTypeId, int yearId, CancellationToken cancellationToken = default)
    {
        var existing = (await repository.GetByYearAsync(yearId, cancellationToken))
            .FirstOrDefault(i => i.CriterionItemTypeId == criterionItemTypeId);
        if (existing is null)
        {
            return new CriterionItemTypeDeleteResult(false, "Criterion item was not found.");
        }

        if (existing.ForeignInstances != 0)
        {
            return new CriterionItemTypeDeleteResult(false, BuildInUseMessage(existing, "removed"));
        }

        // Matches legacy GridViewCriterionItemType_RowDeleting: hard-delete only when the row
        // started in the year currently selected; otherwise soft-end it at the end of the prior
        // year so historical validity (past Schemes/Distributions/Tabulations) is preserved.
        if (existing.StartYearId == yearId)
        {
            await repository.DeleteAsync(criterionItemTypeId, cancellationToken);
        }
        else
        {
            existing.EndYearId = yearId - 1;
            await repository.UpdateAsync(existing, cancellationToken);
        }

        return new CriterionItemTypeDeleteResult(true, null);
    }

    // Mirrors CriterionItemTypeList.aspx.vb::GridViewCriterionItemType_RowUpdating exactly -
    // including one genuine quirk not present on Category/Test Types' algorithm: when a pre-
    // existing row is edited mid-year (started in an earlier year, current year selected), the
    // "future" row carries the item's ORIGINAL (pre-edit) Name forward, but the NEW
    // (post-edit) NoLongerInUse value - not the original NoLongerInUse like Category/Test Types'
    // future row does. Confirmed directly from legacy source, not assumed:
    // "criterionItemTypeFuture.Name = item.Name" vs. "criterionItemTypeFuture.NoLongerInUse =
    // newNoLongerInUse" (not "item.NoLongerInUse"). Reproduced faithfully per the "preserve
    // existing behaviour unless explicitly approved" rule - flagged in the parity analysis as a
    // genuine, possibly-unintentional legacy inconsistency, not silently normalised here.
    private async Task<CriterionItemType> SplitOnEditAsync(
        CriterionItemType existing, string newName, bool newNoLongerInUse, int selectedYearId, int currentYearId, int finalYearId, CancellationToken cancellationToken)
    {
        if (existing.StartYearId == selectedYearId)
        {
            if (selectedYearId == currentYearId)
            {
                // A new row covers just this year with the edited values; the original row is
                // pushed forward to start next year, keeping its pre-edit values.
                var thisYear = new CriterionItemType
                {
                    CriterionItemTypeId = Guid.NewGuid(),
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
            var thisYearSplit = new CriterionItemType
            {
                CriterionItemTypeId = Guid.NewGuid(),
                Name = newName,
                StartYearId = selectedYearId,
                EndYearId = selectedYearId,
                NoLongerInUse = newNoLongerInUse
            };
            await repository.CreateAsync(thisYearSplit, cancellationToken);

            // Quirk (see method remarks): original Name, but the NEW NoLongerInUse value.
            var future = new CriterionItemType
            {
                CriterionItemTypeId = Guid.NewGuid(),
                Name = existing.Name,
                StartYearId = selectedYearId + 1,
                EndYearId = finalYearId,
                NoLongerInUse = newNoLongerInUse
            };
            await repository.CreateAsync(future, cancellationToken);

            existing.EndYearId = selectedYearId - 1;
            await repository.UpdateAsync(existing, cancellationToken);
            return thisYearSplit;
        }

        var nextYearSplit = new CriterionItemType
        {
            CriterionItemTypeId = Guid.NewGuid(),
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
        var result = CriterionItemTypeValidator.Validate(name);
        if (!result.IsValid)
        {
            throw new CriterionItemTypeValidationException(result.Errors);
        }
    }

    // Matches legacy's exact OnClientClick alert wording (CriterionItemTypeList.aspx.vb): "This
    // Criterion Item is being used on...".
    private static string BuildInUseMessage(CriterionItemType item, string action) =>
        item.ForeignInstances == -1
            ? $"This Criterion Item is being used on a distribution and cannot be {action}. It is also being used on the following Scheme(s): {item.ForeignInstancesString}"
            : $"This Criterion Item is being used on {item.ForeignInstances} Scheme(s): {item.ForeignInstancesString} and cannot be {action}.";
}
