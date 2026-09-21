using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Logging;
using PTL.ApiClient;
using PTL.Contracts.Scheme;

namespace PTL.InternalWeb.Features.Scheme;

// Authentication/authorization are out of scope for this phase - assume the current user is
// already authenticated with full access to Scheme functionality. Policies will be added later
// (see docs/migration/scheme-migration.md, "Authentication Mapping").
public class SchemeController(ISchemeApiClient schemeApiClient, ILookupApiClient lookupApiClient, ILogger<SchemeController> logger) : Controller
{
    private static readonly Action<ILogger, int, string?, int, int, Exception?> LogDisplayedSchemeListMessage =
        LoggerMessage.Define<int, string?, int, int>(
            LogLevel.Information,
            new EventId(1, nameof(LogDisplayedSchemeListMessage)),
            "Displayed scheme list: yearId={YearId} searchTerm={SearchTerm} page={Page} totalResults={TotalCount}");

    private static readonly Action<ILogger, Guid, Exception?> LogSchemeNotFoundMessage =
        LoggerMessage.Define<Guid>(
            LogLevel.Information,
            new EventId(2, nameof(LogSchemeNotFoundMessage)),
            "Scheme {SchemeId} not found");

    private static readonly Action<ILogger, Exception?> LogCreateFailedMessage =
        LoggerMessage.Define(
            LogLevel.Warning,
            new EventId(3, nameof(LogCreateFailedMessage)),
            "Create failed for scheme");

    private static readonly Action<ILogger, Guid, Exception?> LogCreatedSchemeMessage =
        LoggerMessage.Define<Guid>(
            LogLevel.Information,
            new EventId(4, nameof(LogCreatedSchemeMessage)),
            "Created scheme {SchemeId}");

    private static readonly Action<ILogger, Guid, Exception?> LogUpdateFailedMessage =
        LoggerMessage.Define<Guid>(
            LogLevel.Warning,
            new EventId(5, nameof(LogUpdateFailedMessage)),
            "Update failed for scheme {SchemeId}");

    private static readonly Action<ILogger, Guid, Exception?> LogUpdatedSchemeMessage =
        LoggerMessage.Define<Guid>(
            LogLevel.Information,
            new EventId(6, nameof(LogUpdatedSchemeMessage)),
            "Updated scheme {SchemeId}");

    private static readonly Action<ILogger, Guid, Exception?> LogSchemeHistoryMessage =
        LoggerMessage.Define<Guid>(
            LogLevel.Information,
            new EventId(7, nameof(LogSchemeHistoryMessage)),
            "Displayed scheme family history for {SharedId}");

    public async Task<IActionResult> Index(int? yearId, string? searchTerm = null, int page = 1, int pageSize = 20, CancellationToken cancellationToken = default)
    {
        var years = await lookupApiClient.GetCurrentYearsAsync(cancellationToken);
        var resolvedYearId = yearId ?? (years.Count > 0 ? years[0].YearId : 0);

        var result = await schemeApiClient.GetSchemesForYearAsync(new SchemeSearchRequest(resolvedYearId, searchTerm, page, pageSize), cancellationToken);
        LogDisplayedSchemeListMessage(logger, resolvedYearId, searchTerm, page, result.TotalCount, null);

        var yearOptions = years.Select(y => new SelectListItem(y.Year, y.YearId.ToString(CultureInfo.InvariantCulture))).ToList();
        var search = new SchemeSearchViewModel(resolvedYearId, searchTerm, result.Page, result.PageSize);
        return View(new SchemeListViewModel(search, result.TotalCount, yearOptions, result.Items));
    }

    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        var scheme = await schemeApiClient.GetSchemeAsync(id, cancellationToken);
        if (scheme is null)
        {
            LogSchemeNotFoundMessage(logger, id, null);
            return NotFound();
        }

        return View(scheme);
    }

    public async Task<IActionResult> History(Guid sharedId, CancellationToken cancellationToken)
    {
        var history = await schemeApiClient.GetSchemeHistoryAsync(sharedId, cancellationToken);
        LogSchemeHistoryMessage(logger, sharedId, null);
        return View(history);
    }

    [HttpGet]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        var model = new SchemeFormViewModel { RequiresAssessment = false };
        await PopulateYearOptionsAsync(model, cancellationToken);
        await PopulatePostageOptionsAsync(model, cancellationToken);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(SchemeFormViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            await PopulateYearOptionsAsync(model, cancellationToken);
            await PopulatePostageOptionsAsync(model, cancellationToken);
            return View(model);
        }

        var result = await schemeApiClient.CreateSchemeAsync(ToRequest(model), cancellationToken);
        if (!result.Success)
        {
            LogCreateFailedMessage(logger, null);
            AddErrors(result.FieldErrors);
            await PopulateYearOptionsAsync(model, cancellationToken);
            await PopulatePostageOptionsAsync(model, cancellationToken);
            return View(model);
        }

        if (result.Scheme is null)
        {
            LogCreateFailedMessage(logger, null);
            ModelState.AddModelError(string.Empty, "The scheme could not be created.");
            await PopulateYearOptionsAsync(model, cancellationToken);
            await PopulatePostageOptionsAsync(model, cancellationToken);
            return View(model);
        }

        LogCreatedSchemeMessage(logger, result.Scheme.SchemeId, null);
        return RedirectToAction(nameof(Details), new { id = result.Scheme.SchemeId });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        var scheme = await schemeApiClient.GetSchemeAsync(id, cancellationToken);
        if (scheme is null)
        {
            return NotFound();
        }

        var model = ToFormViewModel(scheme);
        await PopulateYearOptionsAsync(model, cancellationToken);
        await PopulatePostageOptionsAsync(model, cancellationToken);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, SchemeFormViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            await PopulateYearOptionsAsync(model, cancellationToken);
            await PopulatePostageOptionsAsync(model, cancellationToken);
            return View(model);
        }

        var result = await schemeApiClient.UpdateSchemeAsync(id, ToRequest(model), cancellationToken);
        if (!result.Success)
        {
            LogUpdateFailedMessage(logger, id, null);
            AddErrors(result.FieldErrors);
            await PopulateYearOptionsAsync(model, cancellationToken);
            await PopulatePostageOptionsAsync(model, cancellationToken);
            return View(model);
        }

        LogUpdatedSchemeMessage(logger, id, null);
        return RedirectToAction(nameof(Details), new { id });
    }

    private void AddErrors(IReadOnlyDictionary<string, string[]> fieldErrors) => this.AddFieldErrors(fieldErrors);

    // Current + next year only, mirrors legacy Scheme.aspx.vb SetYearDropDown() reuse of
    // SystemObjects.YearCollection.FetchYearCollectionCurrent() (same lookup as Contract's).
    private async Task PopulateYearOptionsAsync(SchemeFormViewModel model, CancellationToken cancellationToken)
    {
        var years = await lookupApiClient.GetCurrentYearsAsync(cancellationToken);
        model.YearOptions = years.Select(y => new SelectListItem(y.Year, y.YearId.ToString(CultureInfo.InvariantCulture))).ToList();
    }

    // Postage plans are year-scoped (DropDownPostage in the legacy form) - reload against the
    // currently selected YearId so the option list matches the year the scheme belongs to.
    private async Task PopulatePostageOptionsAsync(SchemeFormViewModel model, CancellationToken cancellationToken)
    {
        var plans = await lookupApiClient.GetPostagePricingPlansForYearAsync(model.YearId.GetValueOrDefault(), cancellationToken);
        model.PostageOptions = plans.Select(p => new SelectListItem(p.Name, p.PostageId.ToString())).ToList();
    }

    private static SchemeRequest ToRequest(SchemeFormViewModel model) => new(
        model.YearId.GetValueOrDefault(),
        model.Identifier ?? string.Empty,
        model.Name ?? string.Empty,
        model.ScheduleId.GetValueOrDefault(),
        model.ScheduleCodeId.GetValueOrDefault(),
        model.StartDate,
        model.DistributionMonthApr,
        model.DistributionMonthMay,
        model.DistributionMonthJun,
        model.DistributionMonthJul,
        model.DistributionMonthAug,
        model.DistributionMonthSep,
        model.DistributionAsAvailable,
        model.DistributionMonthOct,
        model.DistributionMonthNov,
        model.DistributionMonthDec,
        model.DistributionMonthJan,
        model.DistributionMonthFeb,
        model.DistributionMonthMar,
        model.WeekNumber.GetValueOrDefault(),
        model.DayOfWeekId.GetValueOrDefault(),
        model.NumberOfSamples.GetValueOrDefault(),
        model.SampleOrigin ?? string.Empty,
        model.Deadline.GetValueOrDefault(),
        model.Subcontractor ?? string.Empty,
        model.CombinedPackaging,
        model.Postage,
        model.CustomsVolume,
        model.SamplePackingInstructions ?? string.Empty,
        model.RequiresAssessment,
        model.CommentsRequired,
        model.Pilot,
        model.LimitedSampleAvailability,
        model.Accredited,
        model.NoVLALabs,
        model.ComerciallyAvailable,
        model.CustomsDescription,
        model.DataConsentDeclarationActive,
        model.DataConsentDeclarationText,
        model.Instructions ?? string.Empty,
        model.DateOfReceipt,
        model.StorageConditions,
        model.ConditionOnReceipt,
        model.TestConsultant1,
        model.TestConsultant2,
        model.TestConsultant3,
        model.TestConsultantTabulationId,
        model.UseExternalReference,
        model.StoreRatings,
        model.Assessor1,
        model.Assessor2,
        model.Assessor3,
        model.Assessor4,
        model.StandardTabulationText);

    private static SchemeFormViewModel ToFormViewModel(SchemeResponse scheme) => new()
    {
        SchemeId = scheme.SchemeId,
        SharedId = scheme.SharedId,
        IsReadOnly = scheme.IsReadOnly,
        LastModified = scheme.LastModified,
        SampleNoSequence = scheme.SampleNoSequence,
        YearId = scheme.YearId,
        Identifier = scheme.Identifier,
        Name = scheme.Name,
        ScheduleId = scheme.ScheduleId,
        ScheduleCodeId = scheme.ScheduleCodeId,
        StartDate = scheme.StartDate,
        DistributionMonthApr = scheme.DistributionMonthApr,
        DistributionMonthMay = scheme.DistributionMonthMay,
        DistributionMonthJun = scheme.DistributionMonthJun,
        DistributionMonthJul = scheme.DistributionMonthJul,
        DistributionMonthAug = scheme.DistributionMonthAug,
        DistributionMonthSep = scheme.DistributionMonthSep,
        DistributionAsAvailable = scheme.DistributionAsAvailable,
        DistributionMonthOct = scheme.DistributionMonthOct,
        DistributionMonthNov = scheme.DistributionMonthNov,
        DistributionMonthDec = scheme.DistributionMonthDec,
        DistributionMonthJan = scheme.DistributionMonthJan,
        DistributionMonthFeb = scheme.DistributionMonthFeb,
        DistributionMonthMar = scheme.DistributionMonthMar,
        WeekNumber = scheme.WeekNumber,
        DayOfWeekId = scheme.DayOfWeekId,
        NumberOfSamples = scheme.NumberOfSamples,
        SampleOrigin = scheme.SampleOrigin,
        Deadline = scheme.Deadline,
        Subcontractor = scheme.Subcontractor,
        CombinedPackaging = scheme.CombinedPackaging,
        Postage = scheme.Postage,
        CustomsVolume = scheme.CustomsVolume,
        SamplePackingInstructions = scheme.SamplePackingInstructions,
        RequiresAssessment = scheme.RequiresAssessment,
        CommentsRequired = scheme.CommentsRequired,
        Pilot = scheme.Pilot,
        LimitedSampleAvailability = scheme.LimitedSampleAvailability,
        Accredited = scheme.Accredited,
        NoVLALabs = scheme.NoVLALabs,
        ComerciallyAvailable = scheme.ComerciallyAvailable,
        CustomsDescription = scheme.CustomsDescription,
        DataConsentDeclarationActive = scheme.DataConsentDeclarationActive,
        DataConsentDeclarationText = scheme.DataConsentDeclarationText,
        Instructions = scheme.Instructions,
        DateOfReceipt = scheme.DateOfReceipt,
        StorageConditions = scheme.StorageConditions,
        ConditionOnReceipt = scheme.ConditionOnReceipt,
        TestConsultant1 = scheme.TestConsultant1,
        TestConsultant2 = scheme.TestConsultant2,
        TestConsultant3 = scheme.TestConsultant3,
        TestConsultantTabulationId = scheme.TestConsultantTabulationId,
        UseExternalReference = scheme.UseExternalReference,
        StoreRatings = scheme.StoreRatings,
        Assessor1 = scheme.Assessor1,
        Assessor2 = scheme.Assessor2,
        Assessor3 = scheme.Assessor3,
        Assessor4 = scheme.Assessor4,
        StandardTabulationText = scheme.StandardTabulationText
    };
}
