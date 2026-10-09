namespace PTL.Core.Distribution;

public sealed record MonthlyDistributionScheduleRowUpdate(
    Guid MonthlyDistributionSchemeId,
    DateTime DistributionDate,
    DateTime OverseasPostingDate,
    DateTime DeadlineDate,
    DateTime ResultsIssueTargetDate,
    bool IsCancelled);

public sealed record MonthlyDistributionScheduleSaveOutcome(
    bool Success,
    IReadOnlyDictionary<Guid, IReadOnlyList<MonthlyDistributionScheduleError>> FieldErrorsBySchemeId);

public interface IDistributionService
{
    Task<IReadOnlyList<DistributionDashboardMonth>> GetDashboardAsync(int financialYearId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DistributionYearOption>> GetYearOptionsAsync(CancellationToken cancellationToken = default);

    Task<(Guid MonthlyDistributionId, IReadOnlyList<MonthlyDistributionSchemeEntity> Schemes)?> GetMonthlyDistributionSchedulesAsync(
        int yearId, int monthId, CancellationToken cancellationToken = default);

    Task<MonthlyDistributionScheduleSaveOutcome> SaveMonthlyDistributionScheduleAsync(
        int yearId, int monthId, IReadOnlyList<MonthlyDistributionScheduleRowUpdate> rows, CancellationToken cancellationToken = default);
}

public sealed class DistributionService(IDistributionRepository distributionRepository) : IDistributionService
{
    public async Task<IReadOnlyList<DistributionDashboardMonth>> GetDashboardAsync(int financialYearId, CancellationToken cancellationToken = default)
    {
        var allMonths = await distributionRepository.GetMonthlyDistributionSummariesAsync(cancellationToken);
        return DistributionDashboardBuilder.Build(allMonths, financialYearId);
    }

    public async Task<IReadOnlyList<DistributionYearOption>> GetYearOptionsAsync(CancellationToken cancellationToken = default)
    {
        var distributionYears = await distributionRepository.GetDistributionYearsAsync(cancellationToken);
        return DistributionYearOptionsBuilder.Build(distributionYears, DateTime.UtcNow);
    }

    public Task<(Guid MonthlyDistributionId, IReadOnlyList<MonthlyDistributionSchemeEntity> Schemes)?> GetMonthlyDistributionSchedulesAsync(
        int yearId, int monthId, CancellationToken cancellationToken = default) =>
        distributionRepository.GetMonthlyDistributionSchedulesAsync(yearId, monthId, cancellationToken);

    // Legacy's whole-page "Dates" ValidationGroup: if ANY row fails chronological-order
    // validation, NOTHING is persisted - not even the rows that would otherwise be valid.
    public async Task<MonthlyDistributionScheduleSaveOutcome> SaveMonthlyDistributionScheduleAsync(
        int yearId, int monthId, IReadOnlyList<MonthlyDistributionScheduleRowUpdate> rows, CancellationToken cancellationToken = default)
    {
        var fieldErrors = new Dictionary<Guid, IReadOnlyList<MonthlyDistributionScheduleError>>();
        foreach (var row in rows)
        {
            var errors = MonthlyDistributionScheduleValidator.Validate(
                row.DistributionDate, row.OverseasPostingDate, row.DeadlineDate, row.ResultsIssueTargetDate);
            if (errors.Count > 0)
            {
                fieldErrors[row.MonthlyDistributionSchemeId] = errors;
            }
        }

        if (fieldErrors.Count > 0)
        {
            return new MonthlyDistributionScheduleSaveOutcome(false, fieldErrors);
        }

        var existing = await distributionRepository.GetMonthlyDistributionSchedulesAsync(yearId, monthId, cancellationToken);
        if (existing is null)
        {
            return new MonthlyDistributionScheduleSaveOutcome(false, fieldErrors);
        }

        var existingById = existing.Value.Schemes.ToDictionary(s => s.MonthlyDistributionSchemeId);
        var updated = new List<MonthlyDistributionSchemeEntity>();
        foreach (var row in rows)
        {
            if (!existingById.TryGetValue(row.MonthlyDistributionSchemeId, out var scheme))
            {
                continue;
            }

            // Round-trip every field the Scheduling screen doesn't edit (Comments/
            // HasIntendedResults/HasSampleNumbersDefined/StoreRatings/SchemeVersionDate) exactly as
            // fetched - only the dates and IsCancelled come from the posted form.
            scheme.DistributionDate = row.DistributionDate;
            scheme.OverseasPostingDate = row.OverseasPostingDate;
            scheme.DeadlineDate = row.DeadlineDate;
            scheme.ResultsIssueTargetDate = row.ResultsIssueTargetDate;
            scheme.IsCancelled = row.IsCancelled;
            updated.Add(scheme);
        }

        await distributionRepository.UpdateMonthlyDistributionSchemesAsync(updated, cancellationToken);
        return new MonthlyDistributionScheduleSaveOutcome(true, fieldErrors);
    }
}
