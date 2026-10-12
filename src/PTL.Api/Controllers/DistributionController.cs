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
public sealed class DistributionController(
    IDistributionService distributionService,
    ISampleNumbersService sampleNumbersService,
    ILogger<DistributionController> logger) : ControllerBase
{
    private static readonly Action<ILogger, Guid, Exception?> LogStructureCreationFailedMessage =
        LoggerMessage.Define<Guid>(
            LogLevel.Warning,
            new EventId(1, nameof(LogStructureCreationFailedMessage)),
            "Sample Numbers structure could not be created for distribution scheme {MonthlyDistributionSchemeId}");

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

    // GET /api/distributions/schemes/{monthlyDistributionSchemeId}/sample-numbers - the Sample
    // Numbers screen, legacy IntendedResultsSet.FetchIntendedResults(mdsId). On a distribution
    // that has never been defined this also materialises the structure from the Scheme (legacy
    // NewIntendedResults), which is why a GET can return a validation problem.
    [HttpGet("schemes/{monthlyDistributionSchemeId:guid}/sample-numbers")]
    public async Task<ActionResult<SampleNumbersResponse>> GetSampleNumbers(Guid monthlyDistributionSchemeId, CancellationToken cancellationToken)
    {
        try
        {
            var page = await sampleNumbersService.GetSampleNumbersAsync(monthlyDistributionSchemeId, cancellationToken);
            return page is null ? NotFound() : Ok(ToResponse(page));
        }
        catch (DistributionStructureException ex)
        {
            LogStructureCreationFailedMessage(logger, monthlyDistributionSchemeId, ex);
            ModelState.AddModelError(nameof(DistributionStructure), ex.Message);
            return ValidationProblem(ModelState);
        }
    }

    // PUT /api/distributions/schemes/{monthlyDistributionSchemeId}/sample-numbers - legacy
    // SampleNumbers.aspx LoadObjectFromForm -> IntendedResultsSet.Save().
    [HttpPut("schemes/{monthlyDistributionSchemeId:guid}/sample-numbers")]
    public async Task<ActionResult<SampleNumbersSaveResult>> SaveSampleNumbers(
        Guid monthlyDistributionSchemeId, [FromBody] SampleNumbersSaveRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var update = new SampleNumbersUpdate(
            request.Comments,
            [.. request.Samples.Select(s => new SampleNumberRowUpdate(
                s.SampleId,
                s.SampleNumber,
                s.InternalDescription,
                s.ExternalDescription,
                s.TabulationDescription,
                s.TestConsultantsDescription,
                [.. s.Allocations.Select(a => new SampleAllocationUpdate(a.SampleTestId, a.IsApplicable))]))],
            User?.Identity?.Name ?? "Guest",
            request.AuditWhy ?? string.Empty);

        var outcome = await sampleNumbersService.SaveSampleNumbersAsync(monthlyDistributionSchemeId, update, cancellationToken);

        var fieldErrors = outcome.FieldErrorsBySampleId.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.Select(e => new SampleNumbersFieldError(e.Field, e.Message)).ToArray());

        return Ok(new SampleNumbersSaveResult(
            outcome.Success,
            [.. outcome.PageErrors.Select(e => e.Message)],
            fieldErrors,
            outcome.AuditReasonRequired,
            outcome.AuditObjectName));
    }

    private static SampleNumbersResponse ToResponse(SampleNumbersPage page) =>
        new(
            page.MonthlyDistributionSchemeId,
            page.DistributionReferenceFull,
            page.SchemeIdentifier,
            page.SchemeName,
            page.NumberOfSetsRequired,
            page.Comments,
            page.RequiresAssessment,
            page.IsReadyForAssessment,
            [.. page.Samples.Select(s => new SampleNumberRowResponse(
                s.SampleId,
                s.SampleNumber,
                s.InternalDescription,
                s.ExternalDescription,
                s.TabulationDescription,
                s.TestConsultantsDescription,
                [.. s.Allocations.Select(a => new SampleAllocationResponse(a.SampleTestId, a.TestId, a.IsApplicable))]))],
            [.. page.Tests.Select(t => new SampleNumbersTestResponse(
                t.TestId,
                t.TestName,
                t.TestResultItemNames,
                t.TestMethodItemNames,
                t.CategoryItemNames))]);

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
            new DateTime(2000, month.MonthId, 1, 0, 0, 0, DateTimeKind.Utc).ToString("MMM", System.Globalization.CultureInfo.InvariantCulture),
            month.MonthlyDistributionId,
            month.IsInitialisable,
            month.SchedulingCompletePercentage,
            month.PreparationCompletePercentage,
            month.PackagingCompletePercentage,
            month.ResultsCompletePercentage,
            month.TabulationsCompletePercentage);
}
