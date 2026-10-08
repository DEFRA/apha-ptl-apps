using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Logging;
using PTL.ApiClient;
using PTL.Contracts.Lookup;
using PTL.Contracts.Scheme;
using PTL.Core.Scheme;
using PTL.InternalWeb.Notifications;
using CoreScheme = PTL.Core.Scheme.Scheme;
using SchemeStartDate = PTL.Core.Scheme.SchemeStartDate;

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

    // Landing page for the Manage Schemes section (moved from the removed Menu feature) -
    // mirrors legacy Scheme Admin/MenuSchemes.aspx; the left nav (SideNavigationProvider)
    // supplies the actual section contents.
    public IActionResult ManageSchemes() => View();

    public async Task<IActionResult> Index(int? yearId = null, string? searchTerm = null, int page = 1, int pageSize = PTL.InternalWeb.Pagination.PaginationModel.DefaultPageSize, CancellationToken cancellationToken = default)
    {
        var years = await lookupApiClient.GetCurrentYearsAsync(cancellationToken);
        var resolvedYearId = yearId ?? (years.Count > 0 ? years[0].YearId : 0);

        var result = await schemeApiClient.GetSchemesForYearAsync(new SchemeSearchRequest(resolvedYearId, searchTerm, page, pageSize), cancellationToken);
        LogDisplayedSchemeListMessage(logger, resolvedYearId, searchTerm, result.Page, result.TotalCount, null);

        var yearOptions = years.Select(y => new SelectListItem(y.Year, y.YearId.ToString(CultureInfo.InvariantCulture))).ToList();
        var search = new SchemeYearSearchViewModel(resolvedYearId, yearOptions);

        return View(new SchemeListViewModel(result.Page, result.PageSize, result.TotalCount, searchTerm, result.Items, search));
    }

    // Legacy SchemeListForPrinting.aspx: the same spgaSchemeInfo family list as the main Scheme
    // List, but with no Search box, no Year filter, and no History column - just Identifier, Name,
    // and a View link per year that opens that scheme's printable worksheet.
    public async Task<IActionResult> PrintableSchemes(int page = 1, int pageSize = PTL.InternalWeb.Pagination.PaginationModel.DefaultPageSize, CancellationToken cancellationToken = default)
    {
        var result = await schemeApiClient.GetSchemeFamiliesAsync(page, pageSize, searchTerm: null, cancellationToken);
        LogDisplayedSchemeListMessage(logger, 0, null, result.Page, result.TotalCount, null);

        return View(new SchemeListViewModel(result.Page, result.PageSize, result.TotalCount, null, result.Items));
    }

    // Legacy PrintableScheme.aspx: a standalone worksheet (no site chrome) built from the same
    // spgSchemeBySchemeId fetch as Details/Edit, with a browser-native Print button.
    public async Task<IActionResult> PrintableScheme(Guid schemeId, CancellationToken cancellationToken)
    {
        var scheme = await schemeApiClient.GetSchemeAsync(schemeId, cancellationToken);
        if (scheme is null)
        {
            LogSchemeNotFoundMessage(logger, schemeId, null);
            return NotFound();
        }

        return View(PrintableSchemeWorksheetBuilder.Build(scheme));
    }

    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        var scheme = await schemeApiClient.GetSchemeAsync(id, cancellationToken);
        if (scheme is null)
        {
            LogSchemeNotFoundMessage(logger, id, null);
            return NotFound();
        }

        var model = ToFormViewModel(scheme);
        model.IsViewMode = true;
        await PopulateFormAsync(model, cancellationToken);
        return View(model);
    }

    public async Task<IActionResult> History(Guid sharedId, CancellationToken cancellationToken)
    {
        var history = await schemeApiClient.GetSchemeHistoryAsync(sharedId, cancellationToken);
        var years = await lookupApiClient.GetAllYearsAsync(cancellationToken);
        var yearLabels = years.ToDictionary(y => y.YearId, y => y.Year);

        var rows = history
            .Select(scheme => new SchemeHistoryRowViewModel(
                scheme.SchemeId,
                yearLabels.GetValueOrDefault(scheme.YearId, scheme.YearId.ToString(CultureInfo.InvariantCulture)),
                scheme.Identifier,
                scheme.Name))
            .ToList();

        LogSchemeHistoryMessage(logger, sharedId, null);
        return View(rows);
    }

    // The Scheme List's Next Year column offers Renew against the most recent scheme when next
    // year's does not exist yet (legacy Scheme.aspx?SchemeId=..&renewal=true -> Scheme.RenewScheme).
    // Reuses the Create view and its POST action - the draft is only persisted when the user Saves.
    [HttpGet]
    public async Task<IActionResult> Renew(Guid schemeId, CancellationToken cancellationToken)
    {
        var renewed = await schemeApiClient.RenewSchemeAsync(schemeId, cancellationToken);
        if (renewed is null)
        {
            LogSchemeNotFoundMessage(logger, schemeId, null);
            return NotFound();
        }

        var model = ToFormViewModel(renewed);

        // The draft is not yet saved - SchemeId must be null so the form posts to Create (which
        // server-generates it), while SharedId is preserved so the saved scheme joins the
        // existing family instead of starting a new one.
        model.SchemeId = null;

        await PopulateFormAsync(model, cancellationToken);
        return View("Create", model);
    }

    // Legacy has two separate entry points into Scheme.aspx, and the year is never chosen on the
    // form itself (SetYearTextBox is called on every path; SetYearDropDown is dead code):
    //   Web.sitemap "Create Scheme for Next Year" -> Scheme.aspx                 -> NewScheme(NextYearId)
    //   SchemeList's "Create Scheme for Current Year" -> Scheme.aspx?currentyear=true -> NewScheme(CurrentYearId)
    [HttpGet]
    public Task<IActionResult> Create(CancellationToken cancellationToken) =>
        CreateForYearAsync(currentYear: false, cancellationToken);

    [HttpGet]
    public Task<IActionResult> CreateForCurrentYear(CancellationToken cancellationToken) =>
        CreateForYearAsync(currentYear: true, cancellationToken);

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(SchemeFormViewModel model, string? testCommand = null, Guid? selectedItemTypeId = null, CancellationToken cancellationToken = default)
    {
        if (await TryHandleTestCommandAsync(model, testCommand, selectedItemTypeId, cancellationToken))
        {
            return View(model);
        }

        if (!ModelState.IsValid)
        {
            model.Instructions = SchemeInstructions.Sanitise(model.Instructions);
            await PopulateFormAsync(model, cancellationToken);
            return View(model);
        }

        var result = await schemeApiClient.CreateSchemeAsync(ToRequest(model), cancellationToken);
        if (!result.Success)
        {
            LogCreateFailedMessage(logger, null);
            AddErrors(result.FieldErrors);
            model.Instructions = SchemeInstructions.Sanitise(model.Instructions);
            await PopulateFormAsync(model, cancellationToken);
            return View(model);
        }

        if (result.Scheme is null)
        {
            LogCreateFailedMessage(logger, null);
            ModelState.AddModelError(string.Empty, "The scheme could not be created.");
            await PopulateFormAsync(model, cancellationToken);
            return View(model);
        }

        LogCreatedSchemeMessage(logger, result.Scheme.SchemeId, null);
        TempData.SetNotification(NotificationType.Success, "Scheme created successfully.");
        return RedirectToAction(nameof(Details), new { id = result.Scheme.SchemeId });
    }

    private async Task<IActionResult> CreateForYearAsync(bool currentYear, CancellationToken cancellationToken)
    {
        var settings = await lookupApiClient.GetSystemSettingsAsync(cancellationToken);
        var years = await lookupApiClient.GetCurrentYearsAsync(cancellationToken);

        // GetCurrentYearsAsync returns the current year followed by the next one, so it stands in
        // for the system settings year ids when those are not configured.
        var defaultYearId = currentYear
            ? ResolveYearId(settings.CurrentYearId, years, useFirst: true)
            : ResolveYearId(settings.NextYearId, years, useFirst: false);

        var model = new SchemeFormViewModel
        {
            RequiresAssessment = false,
            YearId = defaultYearId > 0 ? defaultYearId : null,
        };

        // An existing scheme carries its month locks on SchemeResponse.CanEdit; a new one has to
        // ask for them (legacy Scheme.NewScheme runs SetEditPermissions for the same reason).
        if (model.YearId is { } yearId)
        {
            ApplyMonthEditability(model, await lookupApiClient.GetSchemeMonthEditabilityAsync(yearId, cancellationToken));
        }

        await PopulateFormAsync(model, cancellationToken);
        return View("Create", model);
    }

    // Single-level conditions only, deliberately: configuredYearId wins when set, otherwise the
    // first (current year) or last (next year) entry of the current-years list, otherwise 0.
    private static int ResolveYearId(int configuredYearId, IReadOnlyList<YearResponse> years, bool useFirst)
    {
        if (configuredYearId > 0)
        {
            return configuredYearId;
        }

        if (years.Count == 0)
        {
            return 0;
        }

        return useFirst ? years[0].YearId : years[^1].YearId;
    }

    private static void ApplyMonthEditability(SchemeFormViewModel model, SchemeMonthEditabilityResponse editability)
    {
        model.CanEditJan = editability.Jan;
        model.CanEditFeb = editability.Feb;
        model.CanEditMar = editability.Mar;
        model.CanEditApr = editability.Apr;
        model.CanEditMay = editability.May;
        model.CanEditJun = editability.Jun;
        model.CanEditJul = editability.Jul;
        model.CanEditAug = editability.Aug;
        model.CanEditSep = editability.Sep;
        model.CanEditOct = editability.Oct;
        model.CanEditNov = editability.Nov;
        model.CanEditDec = editability.Dec;
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
        await PopulateFormAsync(model, cancellationToken);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, SchemeFormViewModel model, string? testCommand = null, Guid? selectedItemTypeId = null, CancellationToken cancellationToken = default)
    {
        if (await TryHandleTestCommandAsync(model, testCommand, selectedItemTypeId, cancellationToken))
        {
            return View(model);
        }

        if (!ModelState.IsValid)
        {
            model.Instructions = SchemeInstructions.Sanitise(model.Instructions);
            await PopulateFormAsync(model, cancellationToken);
            return View(model);
        }

        var result = await schemeApiClient.UpdateSchemeAsync(id, ToRequest(model), cancellationToken);
        if (!result.Success)
        {
            LogUpdateFailedMessage(logger, id, null);
            AddErrors(result.FieldErrors);
            model.Instructions = SchemeInstructions.Sanitise(model.Instructions);
            await PopulateFormAsync(model, cancellationToken);
            return View(model);
        }

        LogUpdatedSchemeMessage(logger, id, null);
        TempData.SetNotification(NotificationType.Success, "Scheme updated successfully.");
        return RedirectToAction(nameof(Details), new { id });
    }

    // Tests tab Add/Remove/Up/Down: mutate the staged tree and re-render without saving or
    // validating, mirroring legacy's ViewState-only behaviour. ModelState is cleared so the
    // re-rendered tree reflects the mutated model rather than the posted values.
    // The Tests tab always posts its hidden picker field, empty unless an Add button filled it,
    // so it has to be nullable or every Save fails to bind it.
    private async Task<bool> TryHandleTestCommandAsync(SchemeFormViewModel model, string? testCommand, Guid? selectedItemTypeId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(testCommand))
        {
            return false;
        }

        SchemeTestCommands.TryApply(model, testCommand, selectedItemTypeId ?? Guid.Empty);
        ModelState.Clear();
        model.Instructions = SchemeInstructions.Sanitise(model.Instructions);
        await PopulateFormAsync(model, cancellationToken);
        return true;
    }

    // Every GET and every failed POST must repopulate the Details tab's option lists and the
    // currency pricing grid before re-rendering, or the dropdowns come back empty.
    private async Task PopulateFormAsync(SchemeFormViewModel model, CancellationToken cancellationToken)
    {
        await PopulateYearLabelAsync(model, cancellationToken);
        await PopulatePostageOptionsAsync(model, cancellationToken);

        var schedules = await lookupApiClient.GetSchedulesAsync(cancellationToken);
        var scheduleCodes = await lookupApiClient.GetScheduleCodesAsync(cancellationToken);
        var days = await lookupApiClient.GetDaysAsync(cancellationToken);

        model.ScheduleOptions = schedules.Select(s => new SelectListItem(s.Schedule, s.ScheduleId.ToString())).ToList();
        model.ScheduleCodeOptions = scheduleCodes.Select(s => new SelectListItem(s.ScheduleCode, s.ScheduleCodeId.ToString())).ToList();
        model.DayOfWeekOptions = days.Select(d => new SelectListItem(d.Day, d.DayId.ToString())).ToList();

        await PopulateStartDateAsync(model, cancellationToken);
        await PopulatePricesAsync(model, cancellationToken);
        await PopulateStaffingOptionsAsync(model, cancellationToken);
        await PopulateTestItemTypeOptionsAsync(model, cancellationToken);
    }

    // Start Date is read-only and derived, never entered - legacy recalculates it whenever the year
    // or a distribution month changes (Scheme.RecalucalateStartDate), so it is recomputed on every
    // render rather than taken from the posted form.
    private async Task PopulateStartDateAsync(SchemeFormViewModel model, CancellationToken cancellationToken)
    {
        var settings = await lookupApiClient.GetSystemSettingsAsync(cancellationToken);
        if (settings.ContractStartDate != default && model.YearId is { } yearId && yearId > 0)
        {
            model.StartDate = SchemeStartDate.Calculate(ToStartDateInput(model, yearId), settings.ContractStartDate);
        }
    }

    private static CoreScheme ToStartDateInput(SchemeFormViewModel model, int yearId) => new()
    {
        YearId = yearId,
        DistributionAsAvailable = model.DistributionAsAvailable,
        DistributionMonthJan = model.DistributionMonthJan,
        DistributionMonthFeb = model.DistributionMonthFeb,
        DistributionMonthMar = model.DistributionMonthMar,
        DistributionMonthApr = model.DistributionMonthApr,
        DistributionMonthMay = model.DistributionMonthMay,
        DistributionMonthJun = model.DistributionMonthJun,
        DistributionMonthJul = model.DistributionMonthJul,
        DistributionMonthAug = model.DistributionMonthAug,
        DistributionMonthSep = model.DistributionMonthSep,
        DistributionMonthOct = model.DistributionMonthOct,
        DistributionMonthNov = model.DistributionMonthNov,
        DistributionMonthDec = model.DistributionMonthDec,
    };

    // Legacy offers a "None" entry on every consultant/assessor dropdown; leaving a required one
    // unset is what triggers the validation message.
    private async Task PopulateStaffingOptionsAsync(SchemeFormViewModel model, CancellationToken cancellationToken)
    {
        var consultants = await lookupApiClient.GetTestConsultantsAsync(cancellationToken);
        var assessors = await lookupApiClient.GetAssessorsAsync(cancellationToken);
        var viewers = await lookupApiClient.GetViewersAsync(cancellationToken);

        // Legacy's Primary Test Consultant dropdown opens on "- Please Select -" (it is required);
        // Deputy and Secondary are optional and open on "None".
        model.PrimaryTestConsultantOptions = WithPlaceholder(consultants.Where(c => !c.IsExternal).Select(c => new SelectListItem(c.FriendlyName, c.UserId.ToString())), "- Please Select -");
        model.TestConsultantOptions = WithNone(consultants.Select(c => new SelectListItem(c.FriendlyName, c.UserId.ToString())));
        model.AssessorOptions = WithNone(assessors.Select(a => new SelectListItem(a.FriendlyName, a.UserId.ToString())));
        model.ViewerOptions = viewers.Select(v => new SelectListItem(v.Name, v.ViewerId.ToString())).ToList();
    }

    private static List<SelectListItem> WithNone(IEnumerable<SelectListItem> options) =>
        WithPlaceholder(options, "None");

    private static List<SelectListItem> WithPlaceholder(IEnumerable<SelectListItem> options, string placeholder) =>
        [new SelectListItem(placeholder, string.Empty), .. options];

    // The five Tests tab "Add" dropdowns are scoped to the scheme's year, so only item types
    // valid for that year can be added.
    private async Task PopulateTestItemTypeOptionsAsync(SchemeFormViewModel model, CancellationToken cancellationToken)
    {
        var yearId = model.YearId.GetValueOrDefault();
        SchemeItemTypeKind[] kinds =
        [
            SchemeItemTypeKind.TestType,
            SchemeItemTypeKind.TestResultItemType,
            SchemeItemTypeKind.TestMethodItemType,
            SchemeItemTypeKind.CategoryItemType,
            SchemeItemTypeKind.CriterionItemType,
        ];

        var namesByKind = new Dictionary<SchemeItemTypeKind, IReadOnlyDictionary<Guid, string>>();
        var optionsByKind = new Dictionary<SchemeItemTypeKind, List<SelectListItem>>();

        foreach (var kind in kinds)
        {
            var itemTypes = await lookupApiClient.GetSchemeItemTypesAsync(kind, yearId, cancellationToken);
            namesByKind[kind] = itemTypes.ToDictionary(t => t.ItemTypeId, t => t.Name);
            optionsByKind[kind] = itemTypes.Select(t => new SelectListItem(t.Name, t.ItemTypeId.ToString())).ToList();
        }

        model.TestTypeOptions = optionsByKind[SchemeItemTypeKind.TestType];
        model.ResultItemTypeOptions = optionsByKind[SchemeItemTypeKind.TestResultItemType];
        model.MethodItemTypeOptions = optionsByKind[SchemeItemTypeKind.TestMethodItemType];
        model.CategoryItemTypeOptions = optionsByKind[SchemeItemTypeKind.CategoryItemType];
        model.CriterionItemTypeOptions = optionsByKind[SchemeItemTypeKind.CriterionItemType];

        SchemeTestCommands.ResolveNames(model, namesByKind);
    }

    // Legacy renders one price row per currency, whether or not the scheme already has a
    // tlnkSchemeCurrency row for it - so the full currency list drives the grid and any existing
    // price is merged in on top.
    private async Task PopulatePricesAsync(SchemeFormViewModel model, CancellationToken cancellationToken)
    {
        var currencies = await lookupApiClient.GetCurrenciesAsync(cancellationToken);
        var postedPrices = model.Prices.ToDictionary(p => p.CurrencyId);

        model.Prices = currencies
            .Select(currency =>
            {
                postedPrices.TryGetValue(currency.CurrencyId, out var posted);
                return new SchemeCurrencyPriceViewModel
                {
                    SchemeCurrencyId = posted?.SchemeCurrencyId ?? Guid.Empty,
                    CurrencyId = currency.CurrencyId,
                    CurrencyName = currency.Name,
                    CurrencySymbol = currency.Symbol,
                    Price = posted?.Price,
                };
            })
            .ToList();
    }

    private void AddErrors(IReadOnlyDictionary<string, string[]> fieldErrors) => this.AddFieldErrors(fieldErrors);

    // Legacy shows the year as a read-only textbox on every path - the entry point decides it and
    // the user never picks it (Scheme.aspx.vb SetYearTextBox). All years are searched, not just
    // current/next, so an older scheme being viewed still resolves its name.
    private async Task PopulateYearLabelAsync(SchemeFormViewModel model, CancellationToken cancellationToken)
    {
        if (model.YearId is not { } yearId)
        {
            model.YearLabel = string.Empty;
            return;
        }

        var years = await lookupApiClient.GetAllYearsAsync(cancellationToken);
        model.YearLabel = years.FirstOrDefault(y => y.YearId == yearId)?.Year
            ?? yearId.ToString(CultureInfo.InvariantCulture);
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
        model.StandardTabulationText,
        model.Prices
            .Where(p => p.Price.HasValue)
            .Select(p => new SchemeCurrencyPriceRequest(p.SchemeCurrencyId, p.CurrencyId, p.Price!.Value))
            .ToList(),
        model.ViewerIds,
        model.Tests
            .Select(t => new SchemeTestRequest(
                t.TestId,
                t.TestTypeId,
                [.. t.ResultItems.Select(i => new SchemeTestItemRequest(i.ItemId, i.ItemTypeId))],
                [.. t.MethodItems.Select(i => new SchemeTestItemRequest(i.ItemId, i.ItemTypeId))],
                [.. t.Categories.Select(c => new SchemeCategoryItemRequest(
                    c.CategoryItemId,
                    c.CategoryItemTypeId,
                    [.. c.Criteria.Select(cr => new SchemeTestItemRequest(cr.ItemId, cr.ItemTypeId))]))]))
            .ToList(),
        model.Tabulations
            .Select(t => new SchemeTabulationRequest(
                t.TabulationId,
                t.Name,
                t.IntendedResultsOnly,
                t.SingleParticipantTabulation,
                t.ShowRatings,
                t.Availability is SchemeTabulationAvailability.All or SchemeTabulationAvailability.ParticipantsOnly,
                t.Availability is SchemeTabulationAvailability.All or SchemeTabulationAvailability.ViewersAndTestConsultantsOnly,
                t.ResultItemIds,
                t.MethodItemIds))
            .ToList(),
        model.SharedId);

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
        StandardTabulationText = scheme.StandardTabulationText,
        CanEditJan = scheme.CanEdit?.Jan ?? true,
        CanEditFeb = scheme.CanEdit?.Feb ?? true,
        CanEditMar = scheme.CanEdit?.Mar ?? true,
        CanEditApr = scheme.CanEdit?.Apr ?? true,
        CanEditMay = scheme.CanEdit?.May ?? true,
        CanEditJun = scheme.CanEdit?.Jun ?? true,
        CanEditJul = scheme.CanEdit?.Jul ?? true,
        CanEditAug = scheme.CanEdit?.Aug ?? true,
        CanEditSep = scheme.CanEdit?.Sep ?? true,
        CanEditOct = scheme.CanEdit?.Oct ?? true,
        CanEditNov = scheme.CanEdit?.Nov ?? true,
        CanEditDec = scheme.CanEdit?.Dec ?? true,
        Prices = (scheme.Prices ?? [])
            .Select(p => new SchemeCurrencyPriceViewModel
            {
                SchemeCurrencyId = p.SchemeCurrencyId,
                CurrencyId = p.CurrencyId,
                CurrencyName = p.CurrencyName,
                CurrencySymbol = p.CurrencySymbol,
                Price = p.Price,
            })
            .ToList(),
        ViewerIds = (scheme.ViewerIds ?? []).ToList(),
        Tests = (scheme.Tests ?? [])
            .Select(t => new SchemeTestViewModel
            {
                TestId = t.TestId,
                TestTypeId = t.TestTypeId,
                TestType = t.TestType,
                ResultItems = [.. t.ResultItems.Select(i => new SchemeTestItemViewModel { ItemId = i.ItemId, ItemTypeId = i.ItemTypeId, Name = i.Name })],
                MethodItems = [.. t.MethodItems.Select(i => new SchemeTestItemViewModel { ItemId = i.ItemId, ItemTypeId = i.ItemTypeId, Name = i.Name })],
                Categories = [.. t.Categories.Select(c => new SchemeCategoryItemViewModel
                {
                    CategoryItemId = c.CategoryItemId,
                    CategoryItemTypeId = c.CategoryItemTypeId,
                    Name = c.Name,
                    Criteria = [.. c.Criteria.Select(cr => new SchemeTestItemViewModel { ItemId = cr.ItemId, ItemTypeId = cr.ItemTypeId, Name = cr.Name })],
                })],
            })
            .ToList(),
        Tabulations = (scheme.Tabulations ?? [])
            .Select(t => new SchemeTabulationViewModel
            {
                TabulationId = t.TabulationId,
                Name = t.Name,
                IntendedResultsOnly = t.IntendedResultsOnly,
                SingleParticipantTabulation = t.SingleParticipantTabulation,
                ShowRatings = t.ShowRatings,
                Availability = ToAvailability(t.AvailableToParticipants, t.AvailableToViewers),
                ResultItemIds = [.. t.ResultItemIds],
                MethodItemIds = [.. t.MethodItemIds],
            })
            .ToList()
    };

    private static SchemeTabulationAvailability ToAvailability(bool availableToParticipants, bool availableToViewers) =>
        (availableToParticipants, availableToViewers) switch
        {
            (true, true) => SchemeTabulationAvailability.All,
            (true, false) => SchemeTabulationAvailability.ParticipantsOnly,
            _ => SchemeTabulationAvailability.ViewersAndTestConsultantsOnly,
        };
}
