namespace PTL.Core.Distribution;

// One scheme's posted checkbox state on Save/Apply - legacy ticks each checkbox individually via
// an instant WebForms autopostback (Checkbox_CheckChanged); here the whole form (every scheme's
// ticked boxes) is evaluated together when Save/Apply is submitted. The persisted outcome is
// identical: a stage is stamped once, cascades to earlier stages, and is never un-set.
public sealed record PreparationStageUpdate(
    Guid MonthlyDistributionSchemeId,
    bool DiaryEntryChecked,
    bool PrepStartChecked,
    bool PrepCompleteChecked);

public sealed record PreparationSaveOutcome(bool Success);

public interface IPreparationService
{
    Task<PreparationSheetData?> GetPreparationAsync(int yearId, int monthId, CancellationToken cancellationToken = default);

    Task<PreparationSaveOutcome> SavePreparationAsync(
        int yearId, int monthId, IReadOnlyList<PreparationStageUpdate> updates, CancellationToken cancellationToken = default);
}

public sealed class PreparationService(IDistributionRepository distributionRepository) : IPreparationService
{
    public async Task<PreparationSheetData?> GetPreparationAsync(int yearId, int monthId, CancellationToken cancellationToken = default)
    {
        var schedule = await distributionRepository.GetMonthlyDistributionSchedulesAsync(yearId, monthId, cancellationToken);
        return schedule is null ? null : await distributionRepository.GetPreparationSheetAsync(schedule.Value.MonthlyDistributionId, cancellationToken);
    }

    // Legacy SetDiaryEntryStarted/SetPrepStarted/SetPrepCompleted: ticking a later stage stamps
    // every earlier, not-yet-set stage with the same timestamp; a stage already set is never
    // touched again.
    public async Task<PreparationSaveOutcome> SavePreparationAsync(
        int yearId, int monthId, IReadOnlyList<PreparationStageUpdate> updates, CancellationToken cancellationToken = default)
    {
        var sheet = await GetPreparationAsync(yearId, monthId, cancellationToken);
        if (sheet is null)
        {
            return new PreparationSaveOutcome(false);
        }

        var updatesById = updates.ToDictionary(u => u.MonthlyDistributionSchemeId);
        var dirty = new List<PreparationSchemeEntity>();
        var now = DateTime.UtcNow;

        foreach (var scheme in sheet.Schemes)
        {
            if (!updatesById.TryGetValue(scheme.MonthlyDistributionSchemeId, out var update))
            {
                continue;
            }

            ApplyStageCascade(scheme, update, now, dirty);
        }

        if (dirty.Count > 0)
        {
            await distributionRepository.UpdatePreparationAsync(dirty, cancellationToken);
        }

        return new PreparationSaveOutcome(true);
    }

    private static void ApplyStageCascade(PreparationSchemeEntity scheme, PreparationStageUpdate update, DateTime now, List<PreparationSchemeEntity> dirty)
    {
        var changed = false;

        if ((update.DiaryEntryChecked || update.PrepStartChecked || update.PrepCompleteChecked) && scheme.DiaryEntry is null)
        {
            scheme.DiaryEntry = now;
            changed = true;
        }

        if ((update.PrepStartChecked || update.PrepCompleteChecked) && scheme.PrepStartDate is null)
        {
            scheme.PrepStartDate = now;
            changed = true;
        }

        if (update.PrepCompleteChecked && scheme.PrepCompleteDate is null)
        {
            scheme.PrepCompleteDate = now;
            changed = true;
        }

        if (changed)
        {
            dirty.Add(scheme);
        }
    }
}
