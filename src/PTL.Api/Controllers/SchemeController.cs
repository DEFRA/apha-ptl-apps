using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using PTL.Contracts.Scheme;
using PTL.Core.Scheme;

namespace PTL.Api.Controllers;

// [NEEDS INVESTIGATION] Not yet protected with authorization: authentication/authorization are
// out of scope for this phase - assume the current caller is already authenticated with full
// access to Scheme functionality. Policies will be added in a later phase (see
// docs/migration/scheme-migration.md, "Authentication Mapping" - the legacy domain has
// inconsistent authorization enforcement that must not be carried forward mechanically).
[ApiController]
public sealed class SchemeController(ISchemeService schemeService, ILogger<SchemeController> logger) : ControllerBase
{
    private static readonly Action<ILogger, Guid, Exception?> LogSchemeNotFoundMessage =
        LoggerMessage.Define<Guid>(
            LogLevel.Information,
            new EventId(1, nameof(LogSchemeNotFoundMessage)),
            "Scheme {SchemeId} not found");

    [HttpGet("api/schemes/{schemeId:guid}")]
    public async Task<ActionResult<SchemeResponse>> GetScheme(Guid schemeId, CancellationToken cancellationToken)
    {
        var scheme = await schemeService.GetSchemeAsync(schemeId, cancellationToken);
        if (scheme is null)
        {
            LogSchemeNotFoundMessage(logger, schemeId, null);
            return NotFound();
        }

        return Ok(ToResponse(scheme));
    }

    // GET /api/schemes?year={yearId}[&searchTerm=&page=&pageSize=] - spgSchemeInfoByYearId
    // (server-enforced year filter) with in-memory search/paging - see SchemeService. Bound from
    // individual query parameters (not a single [FromQuery] SchemeSearchRequest) because the query
    // string key "year" does not match the SchemeSearchRequest.YearId property name and
    // PTL.Contracts must stay free of an ASP.NET Core MVC dependency for a [FromQuery(Name=...)]
    // attribute.
    [HttpGet("api/schemes")]
    public async Task<ActionResult<SchemeSearchResponse>> GetSchemes(
        [FromQuery] int year,
        [FromQuery] string? searchTerm,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await schemeService.SearchSchemesAsync(year, searchTerm, page, pageSize, cancellationToken);
        return Ok(new SchemeSearchResponse(result.Items.Select(ToSummaryResponse).ToList(), result.TotalCount, page, pageSize));
    }

    [HttpGet("api/schemes/families/{sharedId:guid}/history")]
    public async Task<ActionResult<IReadOnlyList<SchemeHistoryResponse>>> GetSchemeFamilyHistory(Guid sharedId, CancellationToken cancellationToken)
    {
        var history = await schemeService.GetSchemeFamilyHistoryAsync(sharedId, cancellationToken);
        return Ok(history.Select(ToHistoryResponse).ToList());
    }

    [HttpPost("api/schemes")]
    public async Task<ActionResult<SchemeResponse>> CreateScheme([FromBody] CreateSchemeRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var created = await schemeService.CreateSchemeAsync(ToEntity(request), cancellationToken);
            return CreatedAtAction(nameof(GetScheme), new { schemeId = created.SchemeId }, ToResponse(created));
        }
        catch (SchemeValidationException ex)
        {
            return ToValidationProblem(ex);
        }
    }

    [HttpPut("api/schemes/{schemeId:guid}")]
    public async Task<ActionResult<SchemeResponse>> UpdateScheme(Guid schemeId, [FromBody] UpdateSchemeRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var updated = await schemeService.UpdateSchemeAsync(schemeId, ToEntity(schemeId, request), cancellationToken);
            return updated is null ? NotFound() : Ok(ToResponse(updated));
        }
        catch (SchemeValidationException ex)
        {
            return ToValidationProblem(ex);
        }
    }

    private ActionResult ToValidationProblem(SchemeValidationException ex)
    {
        foreach (var error in ex.Errors)
        {
            ModelState.AddModelError(error.Field, error.Message);
        }

        return ValidationProblem(ModelState);
    }

    private static Scheme ToEntity(CreateSchemeRequest request) => new()
    {
        YearId = request.YearId,
        Identifier = request.Identifier,
        Name = request.Name,
        ScheduleId = request.ScheduleId,
        ScheduleCodeId = request.ScheduleCodeId,
        StartDate = request.StartDate,
        DistributionMonthApr = request.DistributionMonthApr,
        DistributionMonthMay = request.DistributionMonthMay,
        DistributionMonthJun = request.DistributionMonthJun,
        DistributionMonthJul = request.DistributionMonthJul,
        DistributionMonthAug = request.DistributionMonthAug,
        DistributionMonthSep = request.DistributionMonthSep,
        DistributionAsAvailable = request.DistributionAsAvailable,
        DistributionMonthOct = request.DistributionMonthOct,
        DistributionMonthNov = request.DistributionMonthNov,
        DistributionMonthDec = request.DistributionMonthDec,
        DistributionMonthJan = request.DistributionMonthJan,
        DistributionMonthFeb = request.DistributionMonthFeb,
        DistributionMonthMar = request.DistributionMonthMar,
        WeekNumber = request.WeekNumber,
        DayOfWeekId = request.DayOfWeekId,
        NumberOfSamples = request.NumberOfSamples,
        SampleOrigin = request.SampleOrigin,
        Deadline = request.Deadline,
        Subcontractor = request.Subcontractor,
        CombinedPackaging = request.CombinedPackaging,
        Postage = request.Postage,
        CustomsVolume = request.CustomsVolume,
        SamplePackingInstructions = request.SamplePackingInstructions,
        RequiresAssessment = request.RequiresAssessment,
        CommentsRequired = request.CommentsRequired,
        Pilot = request.Pilot,
        LimitedSampleAvailability = request.LimitedSampleAvailability,
        Accredited = request.Accredited,
        NoVLALabs = request.NoVLALabs,
        ComerciallyAvailable = request.ComerciallyAvailable,
        CustomsDescription = request.CustomsDescription,
        DataConsentDeclarationActive = request.DataConsentDeclarationActive,
        DataConsentDeclarationText = request.DataConsentDeclarationText,
        Instructions = request.Instructions,
        DateOfReceipt = request.DateOfReceipt,
        StorageConditions = request.StorageConditions,
        ConditionOnReceipt = request.ConditionOnReceipt,
        TestConsultant1 = request.TestConsultant1,
        TestConsultant2 = request.TestConsultant2,
        TestConsultant3 = request.TestConsultant3,
        TestConsultantTabulationId = request.TestConsultantTabulationId,
        UseExternalReference = request.UseExternalReference,
        StoreRatings = request.StoreRatings,
        Assessor1 = request.Assessor1,
        Assessor2 = request.Assessor2,
        Assessor3 = request.Assessor3,
        Assessor4 = request.Assessor4,
        StandardTabulationText = request.StandardTabulationText
    };

    private static Scheme ToEntity(Guid schemeId, UpdateSchemeRequest request) => new()
    {
        SchemeId = schemeId,
        YearId = request.YearId,
        Identifier = request.Identifier,
        Name = request.Name,
        ScheduleId = request.ScheduleId,
        ScheduleCodeId = request.ScheduleCodeId,
        StartDate = request.StartDate,
        DistributionMonthApr = request.DistributionMonthApr,
        DistributionMonthMay = request.DistributionMonthMay,
        DistributionMonthJun = request.DistributionMonthJun,
        DistributionMonthJul = request.DistributionMonthJul,
        DistributionMonthAug = request.DistributionMonthAug,
        DistributionMonthSep = request.DistributionMonthSep,
        DistributionAsAvailable = request.DistributionAsAvailable,
        DistributionMonthOct = request.DistributionMonthOct,
        DistributionMonthNov = request.DistributionMonthNov,
        DistributionMonthDec = request.DistributionMonthDec,
        DistributionMonthJan = request.DistributionMonthJan,
        DistributionMonthFeb = request.DistributionMonthFeb,
        DistributionMonthMar = request.DistributionMonthMar,
        WeekNumber = request.WeekNumber,
        DayOfWeekId = request.DayOfWeekId,
        NumberOfSamples = request.NumberOfSamples,
        SampleOrigin = request.SampleOrigin,
        Deadline = request.Deadline,
        Subcontractor = request.Subcontractor,
        CombinedPackaging = request.CombinedPackaging,
        Postage = request.Postage,
        CustomsVolume = request.CustomsVolume,
        SamplePackingInstructions = request.SamplePackingInstructions,
        RequiresAssessment = request.RequiresAssessment,
        CommentsRequired = request.CommentsRequired,
        Pilot = request.Pilot,
        LimitedSampleAvailability = request.LimitedSampleAvailability,
        Accredited = request.Accredited,
        NoVLALabs = request.NoVLALabs,
        ComerciallyAvailable = request.ComerciallyAvailable,
        CustomsDescription = request.CustomsDescription,
        DataConsentDeclarationActive = request.DataConsentDeclarationActive,
        DataConsentDeclarationText = request.DataConsentDeclarationText,
        Instructions = request.Instructions,
        DateOfReceipt = request.DateOfReceipt,
        StorageConditions = request.StorageConditions,
        ConditionOnReceipt = request.ConditionOnReceipt,
        TestConsultant1 = request.TestConsultant1,
        TestConsultant2 = request.TestConsultant2,
        TestConsultant3 = request.TestConsultant3,
        TestConsultantTabulationId = request.TestConsultantTabulationId,
        UseExternalReference = request.UseExternalReference,
        StoreRatings = request.StoreRatings,
        Assessor1 = request.Assessor1,
        Assessor2 = request.Assessor2,
        Assessor3 = request.Assessor3,
        Assessor4 = request.Assessor4,
        StandardTabulationText = request.StandardTabulationText
    };

    private static SchemeResponse ToResponse(Scheme scheme) => new(
        scheme.SchemeId,
        scheme.SharedId,
        scheme.YearId,
        scheme.Identifier,
        scheme.Name,
        scheme.ScheduleId,
        scheme.ScheduleCodeId,
        scheme.StartDate,
        scheme.DistributionMonthApr,
        scheme.DistributionMonthMay,
        scheme.DistributionMonthJun,
        scheme.DistributionMonthJul,
        scheme.DistributionMonthAug,
        scheme.DistributionMonthSep,
        scheme.DistributionAsAvailable,
        scheme.DistributionMonthOct,
        scheme.DistributionMonthNov,
        scheme.DistributionMonthDec,
        scheme.DistributionMonthJan,
        scheme.DistributionMonthFeb,
        scheme.DistributionMonthMar,
        scheme.WeekNumber,
        scheme.DayOfWeekId,
        scheme.NumberOfSamples,
        scheme.SampleNoSequence,
        scheme.SampleOrigin,
        scheme.Deadline,
        scheme.Subcontractor,
        scheme.CombinedPackaging,
        scheme.Postage,
        scheme.CustomsVolume,
        scheme.SamplePackingInstructions,
        scheme.RequiresAssessment,
        scheme.CommentsRequired,
        scheme.Pilot,
        scheme.LimitedSampleAvailability,
        scheme.Accredited,
        scheme.NoVLALabs,
        scheme.ComerciallyAvailable,
        scheme.CustomsDescription,
        scheme.DataConsentDeclarationActive,
        scheme.DataConsentDeclarationText,
        scheme.Instructions,
        scheme.DateOfReceipt,
        scheme.StorageConditions,
        scheme.ConditionOnReceipt,
        scheme.TestConsultant1,
        scheme.TestConsultant2,
        scheme.TestConsultant3,
        scheme.TestConsultantTabulationId,
        scheme.UseExternalReference,
        scheme.StoreRatings,
        scheme.Assessor1,
        scheme.Assessor2,
        scheme.Assessor3,
        scheme.Assessor4,
        scheme.StandardTabulationText,
        scheme.LastModified,
        scheme.IsReadOnly);

    private static SchemeSummaryResponse ToSummaryResponse(SchemeSummaryEntity scheme) => new(
        scheme.SharedId,
        scheme.YearId,
        scheme.CurrentSchemeId,
        scheme.CurrentIdentifier,
        scheme.CurrentName,
        scheme.NextSchemeId,
        scheme.NextIdentifier,
        scheme.NextName,
        scheme.RecentSchemeId,
        scheme.RecentIdentifier,
        scheme.RecentName);

    private static SchemeHistoryResponse ToHistoryResponse(SchemeHistoryEntity scheme) => new(
        scheme.SchemeId,
        scheme.SharedId,
        scheme.YearId,
        scheme.Identifier,
        scheme.Name);
}
