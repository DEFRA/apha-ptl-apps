using Microsoft.AspNetCore.Mvc;
using PTL.Contracts.Distribution;
using PTL.Core.Distribution;

namespace PTL.Api.Controllers;

// Distribution Dashboard (legacy Distributions/MenuDistributions.aspx) - see
// docs/analysis/distribution-analysis.md and docs/migration/distribution-migration.md. This is
// the ONLY Distribution feature implemented so far; every other distribution workflow
// (Scheduling, Preparation, Packaging, Results, Tabulations) is out of scope.
[ApiController]
[Route("api/distributions")]
public sealed class DistributionController(IDistributionService distributionService) : ControllerBase
{
    // GET /api/distributions/dashboard?year={financialYearId} - one row per calendar month in the
    // financial year (April -> March), legacy MonthlyDistributionInfoCollection.FetchCollection(yearId).
    [HttpGet("dashboard")]
    public async Task<ActionResult<IReadOnlyList<DistributionDashboardMonthResponse>>> GetDashboard([FromQuery] int year, CancellationToken cancellationToken)
    {
        var months = await distributionService.GetDashboardAsync(year, cancellationToken);
        return Ok(months.Select(ToResponse).ToList());
    }

    // GET /api/distributions/years - the Year dropdown's options, legacy
    // DistributionYearInfoCollection.FetchCollection().
    [HttpGet("years")]
    public async Task<ActionResult<IReadOnlyList<DistributionYearOptionResponse>>> GetYears(CancellationToken cancellationToken)
    {
        var years = await distributionService.GetYearOptionsAsync(cancellationToken);
        return Ok(years.Select(y => new DistributionYearOptionResponse(y.YearId, y.Label)).ToList());
    }

    // GET /api/distributions/months/{yearId}/{monthId}/schedule - the Monthly Distributions
    // (Scheduling) screen, legacy MonthlyDistribution.FetchMonthlyDistribution(yearId, monthId).
    [HttpGet("months/{yearId:int}/{monthId:int}/schedule")]
    public async Task<ActionResult<MonthlyDistributionResponse>> GetSchedule(int yearId, int monthId, CancellationToken cancellationToken)
    {
        var result = await distributionService.GetMonthlyDistributionSchedulesAsync(yearId, monthId, cancellationToken);
        if (result is null)
        {
            return Ok(new MonthlyDistributionResponse(null, yearId, monthId, []));
        }

        var schemes = result.Value.Schemes.Select(ToResponse).ToList();
        return Ok(new MonthlyDistributionResponse(result.Value.MonthlyDistributionId, yearId, monthId, schemes));
    }

    // PUT /api/distributions/months/{yearId}/{monthId}/schedule - legacy MonthlyDistribution.
    // Save() (ButtonSave_Click/ButtonApply_Click), which persists every row in one logical unit.
    [HttpPut("months/{yearId:int}/{monthId:int}/schedule")]
    public async Task<ActionResult<MonthlyDistributionScheduleSaveResult>> SaveSchedule(
        int yearId, int monthId, [FromBody] IReadOnlyList<MonthlyDistributionScheduleRowRequest> rows, CancellationToken cancellationToken)
    {
        var updates = rows.Select(r => new MonthlyDistributionScheduleRowUpdate(
            r.MonthlyDistributionSchemeId, r.DistributionDate, r.OverseasPostingDate, r.DeadlineDate, r.ResultsIssueTargetDate, r.IsCancelled)).ToList();

        var outcome = await distributionService.SaveMonthlyDistributionScheduleAsync(yearId, monthId, updates, cancellationToken);

        var fieldErrors = outcome.FieldErrorsBySchemeId.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.Select(e => e.Message).ToArray());

        return Ok(new MonthlyDistributionScheduleSaveResult(outcome.Success, fieldErrors));
    }

    private static MonthlyDistributionSchemeResponse ToResponse(MonthlyDistributionSchemeEntity scheme) =>
        new(
            scheme.MonthlyDistributionSchemeId,
            scheme.SchemeId,
            scheme.SchemeIdentifier,
            scheme.SchemeName,
            scheme.DistributionReferenceFull,
            scheme.DistributionDate,
            scheme.OverseasPostingDate,
            scheme.DeadlineDate,
            scheme.ResultsIssueTargetDate,
            scheme.ParticipantCount,
            scheme.TotalSetsOfSamplesRequired,
            scheme.HasSampleNumbersDefined,
            scheme.HasIntendedResults,
            scheme.IsCancelled,
            scheme.IsAsAvailable);

    private static DistributionDashboardMonthResponse ToResponse(DistributionDashboardMonth month) =>
        new(
            month.YearId,
            month.MonthId,
            new DateTime(2000, month.MonthId, 1).ToString("MMM", System.Globalization.CultureInfo.InvariantCulture),
            month.MonthlyDistributionId,
            month.IsInitialisable,
            month.SchedulingCompletePercentage,
            month.PreparationCompletePercentage,
            month.PackagingCompletePercentage,
            month.ResultsCompletePercentage,
            month.TabulationsCompletePercentage);
}
