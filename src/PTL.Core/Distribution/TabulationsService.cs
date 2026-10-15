namespace PTL.Core.Distribution;

// Monthly Distributions - Tabulations (legacy Scheduling/Tabulations.aspx), read side only: the
// Send/Amend/Comments actions are shown in their legacy state but not executed here.
public interface ITabulationsService
{
    // Null when the month has not been initialised (legacy hides the Tabulations link for it).
    Task<TabulationsPage?> GetTabulationsAsync(int yearId, int monthId, CancellationToken cancellationToken = default);
}

public sealed class TabulationsService(IDistributionRepository distributionRepository, TimeProvider timeProvider) : ITabulationsService
{
    public async Task<TabulationsPage?> GetTabulationsAsync(int yearId, int monthId, CancellationToken cancellationToken = default)
    {
        var schedule = await distributionRepository.GetMonthlyDistributionSchedulesAsync(yearId, monthId, cancellationToken);
        if (schedule is null)
        {
            return null;
        }

        var schemes = await distributionRepository.GetTabulationsAsync(schedule.Value.MonthlyDistributionId, cancellationToken);
        return ToPage(yearId, monthId, schemes, timeProvider.GetLocalNow().DateTime);
    }

    public static TabulationsPage ToPage(int yearId, int monthId, IEnumerable<TabulationsSchemeEntity> schemes, DateTime now) =>
        new(yearId, monthId, [.. schemes.Select(s => ToScheme(s, now))]);

    private static TabulationsScheme ToScheme(TabulationsSchemeEntity scheme, DateTime now)
    {
        // Legacy shows the Assessors (1-4) instead of the Test Consultants (1-3) for assessment schemes.
        IReadOnlyList<string> consultants = scheme.RequiresAssessment
            ? [scheme.Assessor1Name ?? string.Empty, scheme.Assessor2Name ?? string.Empty, scheme.Assessor3Name ?? string.Empty, scheme.Assessor4Name ?? string.Empty]
            : [scheme.TestConsultant1Name ?? string.Empty, scheme.TestConsultant2Name ?? string.Empty, scheme.TestConsultant3Name ?? string.Empty];

        return new TabulationsScheme(
            scheme.MonthlyDistributionSchemeId,
            $"{scheme.SchemeIdentifier}: {scheme.SchemeName}",
            $"{scheme.DistributionReference}{scheme.DistributionReferenceSuffix}/{scheme.ScheduleCode}",
            scheme.DeadlineDate,
            scheme.ResultsIssueTargetDate,
            scheme.RequiresAssessment,
            consultants,
            [.. scheme.Tabulations.Select(t => new TabulationRow(
                t.TabulationId, t.Name, t.TabulationId == scheme.TestConsultantTabulationId, t.PublishCount))],
            TabulationActionRules.Compute(scheme, now));
    }
}
