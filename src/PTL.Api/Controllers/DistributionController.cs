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
