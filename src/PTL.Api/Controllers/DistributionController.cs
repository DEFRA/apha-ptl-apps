using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using PTL.Contracts.Distribution;
using PTL.Core.Distribution;

namespace PTL.Api.Controllers;

// Distribution Dashboard (legacy Distributions/MenuDistributions.aspx) - see
// docs/analysis/distribution-analysis.md and docs/migration/distribution-migration.md. Scheduling,
// Sample Numbers, Intended Results, Participants, Sample Preparation and Sample Packaging are also
// implemented here - Results (dashboard) and Tabulations remain out of scope.
[ApiController]
[Route("api/distributions")]
public sealed class DistributionController(
    IDistributionService distributionService,
    ISampleNumbersService sampleNumbersService,
    IIntendedResultsService intendedResultsService,
    IDistributionParticipantsService distributionParticipantsService,
    IPreparationService preparationService,
    IPackagingService packagingService,
    IPackagingOutputService packagingOutputService,
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

    // GET /api/distributions/months/{yearId}/{monthId}/preparation - the Sample Preparation
    // screen, legacy PreparationSheet.FetchPreparationSheet.
    [HttpGet("months/{yearId:int}/{monthId:int}/preparation")]
    public async Task<ActionResult<PreparationSheetResponse>> GetPreparation(int yearId, int monthId, CancellationToken cancellationToken)
    {
        var sheet = await preparationService.GetPreparationAsync(yearId, monthId, cancellationToken);
        return sheet is null ? NotFound() : Ok(ToResponse(sheet));
    }

    // PUT /api/distributions/months/{yearId}/{monthId}/preparation - legacy
    // PreparationSheet.Save() (ButtonSave_Click/ButtonApply_Click both call this; the
    // save-vs-stay distinction is owned entirely by InternalWeb).
    [HttpPut("months/{yearId:int}/{monthId:int}/preparation")]
    public async Task<ActionResult<PreparationSaveResult>> SavePreparation(
        int yearId, int monthId, [FromBody] PreparationSaveRequest request, CancellationToken cancellationToken)
    {
        var updates = request.Updates.Select(u => new PreparationStageUpdate(
            u.MonthlyDistributionSchemeId, u.DiaryEntryChecked, u.PrepStartChecked, u.PrepCompleteChecked)).ToList();

        var outcome = await preparationService.SavePreparationAsync(yearId, monthId, updates, cancellationToken);
        return outcome.Success ? Ok(new PreparationSaveResult(true)) : NotFound();
    }

    // GET /api/distributions/months/{yearId}/{monthId}/packaging?mode={0|1|2}&week={1-4} - the
    // Sample Packaging screen, legacy GroupedPackagingSheet / PartGroupedPackagingSheet /
    // WeeklyPackagingSheet.Fetch*. Week is required for the two Combined options only.
    [HttpGet("months/{yearId:int}/{monthId:int}/packaging")]
    public async Task<ActionResult<PackagingSheetResponse>> GetPackaging(
        int yearId, int monthId, [FromQuery] int mode, [FromQuery] int week, CancellationToken cancellationToken)
    {
        if (!TryParsePackagingSelection(mode, week, out var packagingMode, out var packagingWeek))
        {
            return ValidationProblem(PackagingSelectionErrors());
        }

        var sheet = await packagingService.GetPackagingAsync(yearId, monthId, packagingMode, packagingWeek, cancellationToken);
        return sheet is null ? NotFound() : Ok(ToResponse(sheet));
    }

    // PUT /api/distributions/months/{yearId}/{monthId}/packaging - legacy ButtonSave_Click /
    // ButtonApply_Click, which save every packaging sheet the user touched (the save-vs-stay
    // distinction is owned by InternalWeb).
    [HttpPut("months/{yearId:int}/{monthId:int}/packaging")]
    public async Task<ActionResult<PackagingSaveResult>> SavePackaging(
        int yearId, int monthId, [FromBody] PackagingSaveRequest request, CancellationToken cancellationToken)
    {
        var updates = new List<PackagingStageUpdate>();
        foreach (var update in request.Updates)
        {
            if (!TryParsePackagingSelection(update.Mode, update.Week, out var packagingMode, out var packagingWeek))
            {
                return ValidationProblem(PackagingSelectionErrors());
            }

            updates.Add(new PackagingStageUpdate(packagingMode, packagingWeek, update.DistributionParticipantId, update.Packed, update.Posted));
        }

        var outcome = await packagingService.SavePackagingAsync(yearId, monthId, updates, cancellationToken);
        return outcome.Success ? Ok(new PackagingSaveResult(true, outcome.EmailFailures ?? [])) : NotFound();
    }

    // POST /api/distributions/months/{yearId}/{monthId}/packaging/labels - legacy Print Labels,
    // returned as printable label text rather than sent to a server-side label printer.
    [HttpPost("months/{yearId:int}/{monthId:int}/packaging/labels")]
    public async Task<ActionResult<PackagingLabelsResponse>> GetPackagingLabels(
        int yearId, int monthId, [FromBody] PackagingSelectionRequest request, CancellationToken cancellationToken)
    {
        if (!TryParsePackagingSelection(request.Mode, request.Week, out var packagingMode, out var packagingWeek))
        {
            return ValidationProblem(PackagingSelectionErrors());
        }

        var labels = await packagingOutputService.GetLabelsAsync(yearId, monthId, packagingMode, packagingWeek, request.SelectedKeys, cancellationToken);
        return labels is null
            ? NotFound()
            : Ok(new PackagingLabelsResponse([.. labels.Select(l => new PackagingLabelResponse(l.Lines))]));
    }

    // POST /api/distributions/months/{yearId}/{monthId}/packaging/customs - legacy Print Customs
    // (Participant Combined and Daily only): the Customs Pro-Forma Word document.
    [HttpPost("months/{yearId:int}/{monthId:int}/packaging/customs")]
    public async Task<IActionResult> GetPackagingCustoms(
        int yearId, int monthId, [FromBody] PackagingSelectionRequest request, CancellationToken cancellationToken)
    {
        if (!TryParsePackagingSelection(request.Mode, request.Week, out var packagingMode, out var packagingWeek))
        {
            return ValidationProblem(PackagingSelectionErrors());
        }

        var outcome = await packagingOutputService.GetCustomsDocumentAsync(yearId, monthId, packagingMode, packagingWeek, request.SelectedKeys, cancellationToken);
        return outcome.Success
            ? File(outcome.Content, "application/vnd.openxmlformats-officedocument.wordprocessingml.document", outcome.FileName)
            : Problem(detail: outcome.Error, statusCode: StatusCodes.Status400BadRequest);
    }

    private static bool TryParsePackagingSelection(int mode, int week, out PackagingMode packagingMode, out int packagingWeek)
    {
        packagingMode = (PackagingMode)mode;
        packagingWeek = 0;

        if (!Enum.IsDefined(packagingMode))
        {
            return false;
        }

        if (packagingMode == PackagingMode.Daily)
        {
            return true;
        }

        packagingWeek = week;
        return week is >= 1 and <= 4;
    }

    private ModelStateDictionary PackagingSelectionErrors()
    {
        ModelState.AddModelError("mode", "Select a valid Packaging option and, for Combined Packaging, a Week from 1 to 4.");
        return ModelState;
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

    // GET /api/distributions/schemes/{monthlyDistributionSchemeId}/intended-results - legacy
    // IntendedResultsSet.FetchIntendedResults, gated on HasSampleNumbersDefined - legacy's silent
    // LeavePage() when sample numbers are not yet defined, so a 404 here carries no error message.
    [HttpGet("schemes/{monthlyDistributionSchemeId:guid}/intended-results")]
    public async Task<ActionResult<IntendedResultsResponse>> GetIntendedResults(Guid monthlyDistributionSchemeId, CancellationToken cancellationToken)
    {
        var page = await intendedResultsService.GetIntendedResultsAsync(monthlyDistributionSchemeId, cancellationToken);
        return page is null ? NotFound() : Ok(ToResponse(page));
    }

    // PUT /api/distributions/schemes/{monthlyDistributionSchemeId}/intended-results - legacy
    // IntendedResults.aspx ButtonSave_Click -> IntendedResultsSet.Save().
    [HttpPut("schemes/{monthlyDistributionSchemeId:guid}/intended-results")]
    public async Task<ActionResult<IntendedResultsSaveResult>> SaveIntendedResults(
        Guid monthlyDistributionSchemeId, [FromBody] IntendedResultsSaveRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var update = new IntendedResultsUpdate(
            [.. request.Tests.Select(t => new IntendedResultsTestUpdate(
                t.TestId,
                [.. t.ResultItems.Select(r => new IntendedResultsResultItemUpdate(r.TestResultItemId, r.IsRange))],
                [.. t.MethodItems.Select(m => new IntendedResultsMethodItemUpdate(m.TestMethodItemId, m.IntendedResult))],
                [.. t.Samples.Select(s => new IntendedResultsSampleRowUpdate(
                    s.SampleId,
                    s.IsApplicable,
                    [.. s.Values.Select(v => new IntendedResultsSampleResultValueUpdate(v.SampleResultItemId, v.IntendedResult, v.IntendedResult2))]))]))],
            request.RequiresAuditReason,
            User?.Identity?.Name ?? "Guest",
            request.AuditWhy ?? string.Empty);

        var outcome = await intendedResultsService.SaveIntendedResultsAsync(monthlyDistributionSchemeId, update, cancellationToken);

        var intendedResultsFieldErrors = outcome.FieldErrorsByItemId.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.Select(e => new IntendedResultsFieldError(e.Field, e.Message)).ToArray());

        return Ok(new IntendedResultsSaveResult(
            outcome.Success,
            intendedResultsFieldErrors,
            outcome.AuditReasonRequired,
            outcome.AuditObjectName));
    }

    // GET /api/distributions/schemes/{monthlyDistributionSchemeId}/participants - legacy
    // DistributionParticipantList.aspx's LoadMonthlyDistributionScheme.
    [HttpGet("schemes/{monthlyDistributionSchemeId:guid}/participants")]
    public async Task<ActionResult<DistributionParticipantsResponse>> GetParticipants(Guid monthlyDistributionSchemeId, CancellationToken cancellationToken)
    {
        var data = await distributionParticipantsService.GetParticipantsAsync(monthlyDistributionSchemeId, cancellationToken);
        return data is null ? NotFound() : Ok(ToResponse(data));
    }

    // PUT /api/distributions/schemes/{monthlyDistributionSchemeId}/participants/{distributionParticipantId}/deadline -
    // legacy DistributionParticipant.SetNewDeadlineDate + MonthlyDistributionScheme.Save().
    [HttpPut("schemes/{monthlyDistributionSchemeId:guid}/participants/{distributionParticipantId:guid}/deadline")]
    public async Task<ActionResult<ChangeDeadlineResult>> ChangeDeadline(
        Guid monthlyDistributionSchemeId, Guid distributionParticipantId, [FromBody] ChangeDeadlineRequest request, CancellationToken cancellationToken)
    {
        var outcome = await distributionParticipantsService.ChangeDeadlineAsync(
            monthlyDistributionSchemeId, distributionParticipantId, request.NewDeadlineDate, request.Reason, cancellationToken);

        return Ok(new ChangeDeadlineResult(outcome.Success, outcome.NewDeadlineDateError, outcome.ReasonError));
    }

    // POST /api/distributions/schemes/{monthlyDistributionSchemeId}/repeat - legacy
    // MonthlyDistribution.RepeatDistribution + Save().
    [HttpPost("schemes/{monthlyDistributionSchemeId:guid}/repeat")]
    public async Task<ActionResult<RepeatDistributionResult>> RepeatDistribution(
        Guid monthlyDistributionSchemeId, [FromBody] RepeatDistributionRequest request, CancellationToken cancellationToken)
    {
        var selections = request.Selections.Select(s => new ParticipantSelection(s.Id, s.IsAdditional)).ToList();
        var outcome = await distributionParticipantsService.RepeatDistributionAsync(monthlyDistributionSchemeId, selections, cancellationToken);
        return Ok(new RepeatDistributionResult(outcome.Success, outcome.Error));
    }

    private static DistributionParticipantsResponse ToResponse(DistributionParticipantsData data) =>
        new(
            data.Scheme.MonthlyDistributionSchemeId,
            data.Scheme.YearId,
            data.Scheme.MonthId,
            data.Scheme.SchemeIdentifier,
            data.Scheme.SchemeName,
            data.Scheme.DistributionReferenceFull,
            !string.IsNullOrEmpty(data.Scheme.DistributionReferenceSuffix),
            [.. data.Participants.Select(p => new DistributionParticipantResponse(
                p.DistributionParticipantId, p.FullName, p.EffectiveDeadlineDate, p.DeadlineChangeReason, p.AdditionalParticipant))],
            [.. data.AdditionalParticipants.Select(p => new DistributionAdditionalParticipantResponse(p.ParticipantSchemeId, p.FullName))]);

    private static IntendedResultsResponse ToResponse(IntendedResultsPage page) =>
        new(
            page.MonthlyDistributionSchemeId,
            page.DistributionReferenceFull,
            page.SchemeIdentifier,
            page.SchemeName,
            page.RequiresAuditReason,
            [.. page.Tests.Select(t => new IntendedResultsTestResponse(
                t.TestId,
                t.TestName,
                [.. t.ResultItems.Select(r => new IntendedResultsResultItemResponse(r.TestResultItemId, r.Name, r.FormatString, r.ReturnValueType, r.IsRange, r.ExpectedLength))],
                [.. t.MethodItems.Select(m => new IntendedResultsMethodItemResponse(m.TestMethodItemId, m.Name, m.FormatString, m.ReturnValueType, m.IntendedResult))],
                [.. t.Samples.Select(s => new IntendedResultsSampleRowResponse(
                    s.SampleId,
                    s.SampleNumber,
                    s.InternalDescription,
                    s.IsApplicable,
                    [.. s.Values.Select(v => new IntendedResultsSampleResultValueResponse(v.SampleResultItemId, v.TestResultItemId, v.IntendedResult, v.IntendedResult2))],
                    s.ExternalDescription))]))],
            page.SchemeVersionDate,
            page.HasIntendedResults,
            page.DateOfReceipt,
            page.StorageConditions,
            page.ConditionOnReceipt,
            page.Instructions);

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
            scheme.IsAsAvailable,
            scheme.IsAccredited);

    private static PackagingSheetResponse ToResponse(PackagingSheet sheet) =>
        new(
            (int)sheet.Mode,
            sheet.Week,
            sheet.YearId,
            sheet.MonthId,
            [.. sheet.Blocks.Select(b => new PackagingBlockResponse(
                b.PrintKey,
                b.Date,
                b.Heading,
                b.PackingInfo,
                b.DueToBePosted,
                b.ShowAllPacked,
                b.ShowAllPosted,
                [.. b.Schemes.Select(ToResponse)]))]);

    private static PackagingSchemeResponse ToResponse(PackagingSchemeRow scheme) =>
        new(
            scheme.MonthlyDistributionSchemeId,
            scheme.Description,
            scheme.PreparationComplete,
            scheme.PackingInfo,
            scheme.CustomerPackingInfo,
            scheme.PSLevelPackingInfo,
            scheme.ImportPermitStatus,
            scheme.DistributionParticipantId,
            scheme.PackagedDate,
            scheme.PostedDate,
            (int)scheme.PackedTotal,
            (int)scheme.PostedTotal,
            [.. scheme.Participants.Select(p => new PackagingParticipantResponse(
                p.DistributionParticipantId, p.Description, p.PackagedDate, p.PostedDate, p.ImportPermitStatus))]);

    private static PreparationSheetResponse ToResponse(PreparationSheetData sheet) =>
        new(
            sheet.MonthlyDistributionId,
            sheet.YearId,
            sheet.MonthId,
            [.. sheet.Schemes.Select(ToResponse)]);

    private static PreparationSchemeResponse ToResponse(PreparationSchemeEntity scheme) =>
        new(
            scheme.MonthlyDistributionSchemeId,
            scheme.SchemeIdentifier,
            scheme.SchemeName,
            scheme.DistributionReferenceFull,
            scheme.NumberOfSetsRequired,
            scheme.SamplePackingInstructions,
            scheme.DiaryEntry,
            scheme.PrepStartDate,
            scheme.PrepCompleteDate,
            [.. scheme.Samples.Select(s => new PreparationSampleResponse(s.SampleId, s.SampleNumber, s.InternalDescription))]);

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
