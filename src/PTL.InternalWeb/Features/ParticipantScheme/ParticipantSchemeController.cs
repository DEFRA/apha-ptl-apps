using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Logging;
using PTL.ApiClient;
using PTL.Contracts.Participant;
using PTL.Contracts.Scheme;
using PTL.InternalWeb.Authorization;
using PTL.InternalWeb.Confirmation;
using PTL.InternalWeb.Features.Account;
using PTL.InternalWeb.Navigation;
using PTL.SharedUI.Notifications;

namespace PTL.InternalWeb.Features.ParticipantScheme;

// Legacy "Contracts Admin" folder - requires the Contracts Admin role. The one finer rule on this page is
// legacy's "Override Schemes" gate on the Override button (see ApplyOverridePermission).
[Authorize(Policy = PtlPolicies.ContractsAdmin)]
public class ParticipantSchemeController(
    IParticipantSchemeApiClient participantSchemeApiClient,
    IContractApiClient contractApiClient,
    IParticipantApiClient participantApiClient,
    ISchemeApiClient schemeApiClient,
    ILookupApiClient lookupApiClient,
    ILogger<ParticipantSchemeController> logger) : Controller
{
    private const string WeightedPricingPlan = "Weighted";
    private const string ProRataPricingPlan = "ProRata";
    private const string FullPricingPlan = "Full";

    private static readonly Action<ILogger, Guid, Exception?> LogNotFoundMessage =
        LoggerMessage.Define<Guid>(
            LogLevel.Information,
            new EventId(1, nameof(LogNotFoundMessage)),
            "Participant scheme {ParticipantSchemeId} not found");

    private static readonly Action<ILogger, Guid, Exception?> LogCreateFailedMessage =
        LoggerMessage.Define<Guid>(
            LogLevel.Warning,
            new EventId(2, nameof(LogCreateFailedMessage)),
            "Create failed for participant scheme under contract {ContractId}");

    private static readonly Action<ILogger, Guid, Exception?> LogCreatedMessage =
        LoggerMessage.Define<Guid>(
            LogLevel.Information,
            new EventId(3, nameof(LogCreatedMessage)),
            "Created participant scheme {ParticipantSchemeId}");

    private static readonly Action<ILogger, Guid, Exception?> LogUpdateFailedMessage =
        LoggerMessage.Define<Guid>(
            LogLevel.Warning,
            new EventId(4, nameof(LogUpdateFailedMessage)),
            "Update failed for participant scheme {ParticipantSchemeId}");

    private static readonly Action<ILogger, Guid, Exception?> LogUpdatedMessage =
        LoggerMessage.Define<Guid>(
            LogLevel.Information,
            new EventId(5, nameof(LogUpdatedMessage)),
            "Updated participant scheme {ParticipantSchemeId}");

    private static readonly Action<ILogger, Guid, Exception?> LogSaveErrorMessage =
        LoggerMessage.Define<Guid>(
            LogLevel.Error,
            new EventId(6, nameof(LogSaveErrorMessage)),
            "Unexpected error saving participant scheme for contract {ContractId}");

    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        var participantScheme = await participantSchemeApiClient.GetParticipantSchemeAsync(id, cancellationToken);
        if (participantScheme is null)
        {
            LogNotFoundMessage(logger, id, null);
            return NotFound();
        }

        var contract = await contractApiClient.GetContractAsync(participantScheme.ContractId, cancellationToken);
        if (contract is null)
        {
            return NotFound();
        }

        // Populated exactly as Edit does, then rendered read-only - legacy has no separate view screen.
        var fields = ToFormViewModel(participantScheme, contract.CustomerId, contract.YearId, isReadOnly: true);
        await PopulateDataConsentAsync(fields, cancellationToken);
        await PopulatePricingPlanAsync(fields, isFreshLoad: true, cancellationToken);
        await PopulateGroupAddressOptionsAsync(fields, cancellationToken);
        SetBreadcrumbContext(contract.CustomerId, participantScheme.ContractId);

        return View(new ParticipantSchemeDetailsViewModel(fields, participantScheme.IsRemoved || contract.IsReadOnly));
    }

    // Contract Items is this page's breadcrumb parent and Contracts its grandparent; neither id is
    // in the route here.
    private void SetBreadcrumbContext(Guid customerId, Guid contractId)
    {
        ViewData[BreadcrumbRouteValues.CustomerId] = customerId;
        ViewData[BreadcrumbRouteValues.ContractId] = contractId;
    }

    // page and pageSize are the names govuk-pagination and govuk-page-size-selector submit.
    [HttpGet]
    public async Task<IActionResult> Create(
        Guid contractId,
        Guid customerId,
        Guid? participantId = null,
        Guid? schemeId = null,
        string? schemeSearchTerm = null,
        int page = 1,
        int pageSize = PTL.SharedUI.Pagination.PaginationModel.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        var contract = await contractApiClient.GetContractAsync(contractId, cancellationToken);
        if (contract is null)
        {
            return NotFound();
        }

        var model = new ParticipantSchemeFormViewModel
        {
            ContractId = contractId,
            CustomerId = customerId,
            YearId = contract.YearId,
            ParticipantId = participantId,
            SchemeId = schemeId,
            SchemeSearchTerm = schemeSearchTerm,
            SchemeOptionsPage = page,
            SchemeOptionsPageSize = PTL.SharedUI.Pagination.PaginationModel.AvailablePageSizes.Contains(pageSize)
                ? pageSize
                : PTL.SharedUI.Pagination.PaginationModel.DefaultPageSize,
            IsReadOnly = contract.IsReadOnly
        };

        ApplyOverridePermission(model);
        await PopulateParticipantOptionsAsync(model, cancellationToken);
        await PopulateSchemeContextAsync(model, cancellationToken, applyDefaultMonths: true);
        await PopulatePricingPlanAsync(model, isFreshLoad: true, cancellationToken);
        await PopulateGroupAddressOptionsAsync(model, cancellationToken);
        SetBreadcrumbContext(customerId, contractId);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ParticipantSchemeFormViewModel model, string? confirmation = null, CancellationToken cancellationToken = default)
    {
        ApplyOverridePermission(model);

        // Cancel on the confirmation page posts the unsaved form back; show it exactly as it was.
        if (ConfirmationFlow.IsCancelled(confirmation))
        {
            ModelState.Clear();
            return await RedisplayCreateAsync(model, cancellationToken);
        }

        if (model.SchemeId is not null)
        {
            await ValidateDataConsentAsync(model, cancellationToken);
        }

        if (!ModelState.IsValid || model.ParticipantId is null || model.SchemeId is null)
        {
            if (model.ParticipantId is null)
            {
                ModelState.AddModelError(nameof(model.ParticipantId), "Select a Participant");
            }

            if (model.SchemeId is null)
            {
                ModelState.AddModelError(nameof(model.SchemeId), "Select a Scheme from the list");
            }

            return await RedisplayCreateAsync(model, cancellationToken);
        }

        if (!ConfirmationFlow.IsConfirmed(confirmation)
            && await InitialisedMonthConfirmationAsync(model, model.SchemeId.Value, null, cancellationToken) is { } confirmationPage)
        {
            return this.ConfirmationPage(confirmationPage);
        }

        try
        {
            var result = await participantSchemeApiClient.CreateParticipantSchemeAsync(ToCreateRequest(model), cancellationToken);
            if (!result.Success)
            {
                LogCreateFailedMessage(logger, model.ContractId.GetValueOrDefault(), null);
                this.AddFieldErrors(result.FieldErrors);
                return await RedisplayCreateAsync(model, cancellationToken);
            }

            LogCreatedMessage(logger, result.ParticipantScheme!.ParticipantSchemeId, null);
            TempData.SetNotification(NotificationType.Success, "Contract item created successfully.");
            return RedirectToAction("ContractItems", "Contract", new { area = "", id = model.ContractId.GetValueOrDefault() });
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogSaveErrorMessage(logger, model.ContractId.GetValueOrDefault(), ex);
            ModelState.AddModelError(string.Empty, "The contract item could not be saved. Please try again.");
            return await RedisplayCreateAsync(model, cancellationToken);
        }
    }

    private async Task<IActionResult> RedisplayCreateAsync(ParticipantSchemeFormViewModel model, CancellationToken cancellationToken)
    {
        await PopulateParticipantOptionsAsync(model, cancellationToken);
        await PopulateSchemeContextAsync(model, cancellationToken);
        await PopulatePricingPlanAsync(model, isFreshLoad: false, cancellationToken);
        await PopulateGroupAddressOptionsAsync(model, cancellationToken);
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        var participantScheme = await participantSchemeApiClient.GetParticipantSchemeAsync(id, cancellationToken);
        if (participantScheme is null)
        {
            return NotFound();
        }

        var contract = await contractApiClient.GetContractAsync(participantScheme.ContractId, cancellationToken);
        if (contract is null)
        {
            return NotFound();
        }

        var model = ToFormViewModel(participantScheme, contract.CustomerId, contract.YearId, participantScheme.IsRemoved || contract.IsReadOnly);
        ApplyOverridePermission(model);
        await PopulateDataConsentAsync(model, cancellationToken);
        await PopulateWarningsAsync(model, cancellationToken);
        await PopulatePricingPlanAsync(model, isFreshLoad: true, cancellationToken);
        await PopulateGroupAddressOptionsAsync(model, cancellationToken);
        SetBreadcrumbContext(contract.CustomerId, participantScheme.ContractId);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, ParticipantSchemeFormViewModel model, string? confirmation = null, CancellationToken cancellationToken = default)
    {
        ApplyOverridePermission(model);

        // Cancel on the confirmation page posts the unsaved form back; show it exactly as it was.
        if (ConfirmationFlow.IsCancelled(confirmation))
        {
            ModelState.Clear();
            return await RedisplayEditAsync(id, model, cancellationToken);
        }

        await ValidateDataConsentAsync(model, cancellationToken);

        if (!ModelState.IsValid)
        {
            return await RedisplayEditAsync(id, model, cancellationToken);
        }

        if (!ConfirmationFlow.IsConfirmed(confirmation) && model.SchemeId is { } schemeId)
        {
            var stored = await participantSchemeApiClient.GetParticipantSchemeAsync(id, cancellationToken);
            var storedMonths = stored is null ? null : ToFormViewModel(stored, model.CustomerId.GetValueOrDefault(), model.YearId.GetValueOrDefault(), isReadOnly: false).DistributionMonths();
            if (await InitialisedMonthConfirmationAsync(model, schemeId, storedMonths, cancellationToken) is { } confirmationPage)
            {
                return this.ConfirmationPage(confirmationPage);
            }
        }

        try
        {
            var result = await participantSchemeApiClient.UpdateParticipantSchemeAsync(id, ToUpdateRequest(model), cancellationToken);
            if (!result.Success)
            {
                LogUpdateFailedMessage(logger, id, null);
                this.AddFieldErrors(result.FieldErrors);
                return await RedisplayEditAsync(id, model, cancellationToken);
            }

            LogUpdatedMessage(logger, id, null);
            TempData.SetNotification(NotificationType.Success, "Contract item updated successfully.");
            return RedirectToAction(nameof(Details), new { id });
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogSaveErrorMessage(logger, model.ContractId.GetValueOrDefault(), ex);
            ModelState.AddModelError(string.Empty, "The contract item could not be saved. Please try again.");
            return await RedisplayEditAsync(id, model, cancellationToken);
        }
    }

    private async Task<IActionResult> RedisplayEditAsync(Guid id, ParticipantSchemeFormViewModel model, CancellationToken cancellationToken)
    {
        await PopulateEditRedisplayAsync(id, model, cancellationToken);
        await PopulatePricingPlanAsync(model, isFreshLoad: false, cancellationToken);
        await PopulateGroupAddressOptionsAsync(model, cancellationToken);
        return View(model);
    }

    // Legacy runs the Override button only for "Override Schemes" users (ButtonOverride.Visible); a posted
    // override flag from anyone else is ignored so the role cannot be bypassed by editing the form.
    private void ApplyOverridePermission(ParticipantSchemeFormViewModel model)
    {
        model.CanOverride = User.HasInternalRole(InternalRoleNames.OverrideSchemes);
        if (!model.CanOverride)
        {
            model.OverrideModeActive = false;
        }
    }

    // Legacy put alert('Warning: This Distribution has already been initialised.') on every such month
    // checkbox. Here the page shows the same warning inline, and Save asks for confirmation (shared
    // confirmation page) when the user actually changed one of those months.
    private async Task<ConfirmationPageViewModel?> InitialisedMonthConfirmationAsync(ParticipantSchemeFormViewModel model, Guid schemeId, bool[]? storedMonths, CancellationToken cancellationToken)
    {
        var context = await participantSchemeApiClient.GetParticipantSchemeContextAsync(model.ContractId.GetValueOrDefault(), schemeId, cancellationToken);
        var posted = model.DistributionMonths();
        var changed = context.InitialisedMonthIds
            .Where(monthId => monthId is >= 1 and <= 12 && posted[monthId - 1] != (storedMonths?[monthId - 1] ?? false))
            .Order()
            .ToList();
        if (changed.Count == 0)
        {
            return null;
        }

        return new ConfirmationPageViewModel
        {
            Title = "Change an initialised distribution?",
            Message = $"Warning: This Distribution has already been initialised ({string.Join(", ", changed.Select(MonthName))}).",
            ConfirmText = "Continue",
        };
    }

    private static string MonthName(int monthId) =>
        System.Globalization.CultureInfo.InvariantCulture.DateTimeFormat.GetMonthName(monthId);

    private async Task PopulateParticipantOptionsAsync(ParticipantSchemeFormViewModel model, CancellationToken cancellationToken)
    {
        var participants = await participantApiClient.GetParticipantsAsync(model.CustomerId.GetValueOrDefault(), includeInactive: false, cancellationToken);
        model.ParticipantOptions = participants
            .Select(p => new SelectListItem($"{p.LabCode}: {p.LabName}", p.ParticipantId.ToString()))
            .ToList();
    }

    // Matches legacy GridViewSchemes (SchemeInfoCollection.FetchSchemeInfoCollectionByYearId, paged
    // at 15/page) - reuses the existing Scheme domain's search/paging rather than duplicating it.
    private async Task PopulateSchemeContextAsync(ParticipantSchemeFormViewModel model, CancellationToken cancellationToken, bool applyDefaultMonths = false)
    {
        if (model.SchemeId is { } schemeId)
        {
            var scheme = await schemeApiClient.GetSchemeAsync(schemeId, cancellationToken);
            model.SchemeDisplayName = scheme is null ? string.Empty : $"{scheme.Identifier}: {scheme.Name}";
            model.SchemeRequiresDataConsent = scheme?.DataConsentDeclarationActive ?? false;
            ApplySchemeMonths(model, scheme);
            await ApplyNewItemContextAsync(model, schemeId, applyDefaultMonths, cancellationToken);
            return;
        }

        var request = new SchemeSearchRequest(model.YearId.GetValueOrDefault(), model.SchemeSearchTerm, model.SchemeOptionsPage.GetValueOrDefault(1), model.SchemeOptionsPageSize);
        var result = await schemeApiClient.GetSchemesForYearAsync(request, cancellationToken);
        model.SchemeOptions = result.Items;
        model.SchemeOptionsTotalCount = result.TotalCount;
    }

    private async Task PopulateDataConsentAsync(ParticipantSchemeFormViewModel model, CancellationToken cancellationToken)
    {
        var scheme = await schemeApiClient.GetSchemeAsync(model.SchemeId.GetValueOrDefault(), cancellationToken);
        model.SchemeRequiresDataConsent = scheme?.DataConsentDeclarationActive ?? false;
        ApplySchemeMonths(model, scheme);
    }

    // Legacy HasDataConsentBeenGiven: when the scheme asks for consent, Save is blocked until ticked.
    private async Task ValidateDataConsentAsync(ParticipantSchemeFormViewModel model, CancellationToken cancellationToken)
    {
        var scheme = await schemeApiClient.GetSchemeAsync(model.SchemeId.GetValueOrDefault(), cancellationToken);
        if (scheme?.DataConsentDeclarationActive == true && !model.DataConsentDeclarationGiven)
        {
            ModelState.AddModelError(nameof(model.DataConsentDeclarationGiven), "Accept 'Data Consent Declaration' before saving");
        }
    }

    // Legacy ParticipantScheme.SchemeId setter: which months are already posted (locked) for this
    // customer, and, on first selection (legacy Schemes_RowCommand), every month the scheme offers
    // and that is still open starts ticked.
    private async Task ApplyNewItemContextAsync(ParticipantSchemeFormViewModel model, Guid schemeId, bool applyDefaultMonths, CancellationToken cancellationToken)
    {
        var context = await participantSchemeApiClient.GetParticipantSchemeContextAsync(model.ContractId.GetValueOrDefault(), schemeId, cancellationToken);
        model.SetCanEditMonths(context.CanEdit);
        model.InitialisedMonthIds = context.InitialisedMonthIds;

        if (applyDefaultMonths)
        {
            var offered = model.SchemeOffersMonths();
            model.SetDistributionMonths([.. offered.Select((offeredMonth, i) => offeredMonth && context.CanEdit[i])]);
        }
    }

    private async Task PopulateWarningsAsync(ParticipantSchemeFormViewModel model, CancellationToken cancellationToken)
    {
        var context = await participantSchemeApiClient.GetParticipantSchemeContextAsync(model.ContractId.GetValueOrDefault(), model.SchemeId.GetValueOrDefault(), cancellationToken);
        model.InitialisedMonthIds = context.InitialisedMonthIds;
    }

    // Month checkboxes that are disabled post nothing, and CanEdit*/IsOverride* are display-only - so a
    // failed save is redisplayed from the stored item: locked months keep their stored tick (legacy
    // shows `scheme month And stored month`), unless Override mode is active where legacy shows them clear.
    private async Task PopulateEditRedisplayAsync(Guid id, ParticipantSchemeFormViewModel model, CancellationToken cancellationToken)
    {
        var existing = await participantSchemeApiClient.GetParticipantSchemeAsync(id, cancellationToken);
        if (existing is not null)
        {
            var stored = ToFormViewModel(existing, model.CustomerId.GetValueOrDefault(), model.YearId.GetValueOrDefault(), isReadOnly: false);
            model.ParticipantDisplayName = stored.ParticipantDisplayName;
            model.SchemeDisplayName = stored.SchemeDisplayName;
            model.SetCanEditMonths(stored.CanEditMonths());
            model.SetOverrideMonths(stored.OverrideMonths());

            var posted = model.DistributionMonths();
            var storedMonths = stored.DistributionMonths();
            var canEdit = stored.CanEditMonths();
            var overridden = stored.OverrideMonths();
            var overrideMode = model.OverrideModeActive.GetValueOrDefault();
            model.SetDistributionMonths([.. posted.Select((month, i) => canEdit[i] || (overrideMode && !overridden[i]) ? month : storedMonths[i])]);
        }

        await PopulateDataConsentAsync(model, cancellationToken);
        await PopulateWarningsAsync(model, cancellationToken);
    }

    // Matches legacy GroupAddressCollection.FetchGroupAddressCollection() (populates the "Select a
    // Group Address" dropdown) plus GroupAddress.GetGroupAddress/mCountries.GetCountry (resolves
    // the currently-selected GroupAddressId's Identifier/Address1/Country for display).
    private async Task PopulateGroupAddressOptionsAsync(ParticipantSchemeFormViewModel model, CancellationToken cancellationToken)
    {
        var groupAddresses = await lookupApiClient.GetGroupAddressesAsync(cancellationToken);
        var countries = await lookupApiClient.GetCountriesAsync(cancellationToken);
        var countryNamesById = countries.ToDictionary(c => c.CountryId, c => c.Country);

        model.GroupAddressOptions = groupAddresses
            .Select(g => new GroupAddressOptionViewModel(
                g.GroupAddressId,
                g.Identifier,
                g.Address1,
                countryNamesById.GetValueOrDefault(g.CountryId, string.Empty)))
            .ToList();

        var selected = model.GroupAddressId is { } groupAddressId
            ? model.GroupAddressOptions.FirstOrDefault(g => g.GroupAddressId == groupAddressId)
            : null;
        model.GroupAddressIdentifier = selected?.Identifier;
        model.GroupAddressAddress1 = selected?.Address1;
        model.GroupAddressCountry = selected?.CountryName;
    }

    // Matches legacy LoadPricingOptions(numOnScheme, numAvailableOnScheme, numOnParticipantScheme, isWeighted):
    // "Full scheme pricing" is the only option when the participant has every scheme month, or none yet
    // while the whole scheme is still open; otherwise "Weighted pricing plan" / "Pro rata" (new items
    // default to Weighted, saved ones to their stored plan). Hidden entirely when the contract's year has
    // no weighted-pricing plan (WeightedPricingYearCollection.PricingPlanExists). numOnScheme/
    // numAvailableOnScheme are the SCHEME's months (the latter minus months whose distribution is already
    // defined), not the participant's posted months. A brand-new item starts from zero months, as legacy
    // does when a Scheme is selected; later redisplays use the months actually chosen. The page script
    // (_ParticipantSchemeFields) repeats this on every month/consent change, replacing legacy's AutoPostBack.
    private async Task PopulatePricingPlanAsync(ParticipantSchemeFormViewModel model, bool isFreshLoad, CancellationToken cancellationToken)
    {
        var weightedYears = await lookupApiClient.GetWeightedPricingYearsAsync(cancellationToken);
        model.IsWeightedSchemeAvailable = weightedYears.Any(y => y.YearId == model.YearId.GetValueOrDefault());

        if (!model.IsWeightedSchemeAvailable)
        {
            model.PricingPlanOptions = [];
            model.PricingPlan = ProRataPricingPlan;
            return;
        }

        var numberOnParticipantScheme = isFreshLoad && model.ParticipantSchemeId is null ? 0 : CountParticipantSchemeMonths(model);

        if (numberOnParticipantScheme == model.NumberOnScheme || (numberOnParticipantScheme == 0 && model.NumberOnScheme == model.NumberAvailableOnScheme))
        {
            model.PricingPlanOptions = [new SelectListItem("Full scheme pricing", FullPricingPlan)];
            model.PricingPlan = FullPricingPlan;
            return;
        }

        model.PricingPlanOptions =
        [
            new SelectListItem("Weighted pricing plan", WeightedPricingPlan),
            new SelectListItem("Pro rata", ProRataPricingPlan)
        ];

        // A redisplay keeps the plan the user chose; only a plan that is no longer on offer is reset.
        if (isFreshLoad || model.PricingPlan is not (WeightedPricingPlan or ProRataPricingPlan))
        {
            model.PricingPlan = model.ParticipantSchemeId is null || model.IsWeightedPricing.GetValueOrDefault() ? WeightedPricingPlan : ProRataPricingPlan;
        }
    }

    private static int CountParticipantSchemeMonths(ParticipantSchemeFormViewModel m) => new[]
    {
        m.SchemeDistributionMonthJan && m.DistributionMonthJan, m.SchemeDistributionMonthFeb && m.DistributionMonthFeb,
        m.SchemeDistributionMonthMar && m.DistributionMonthMar, m.SchemeDistributionMonthApr && m.DistributionMonthApr,
        m.SchemeDistributionMonthMay && m.DistributionMonthMay, m.SchemeDistributionMonthJun && m.DistributionMonthJun,
        m.SchemeDistributionMonthJul && m.DistributionMonthJul, m.SchemeDistributionMonthAug && m.DistributionMonthAug,
        m.SchemeDistributionMonthSep && m.DistributionMonthSep, m.SchemeDistributionMonthOct && m.DistributionMonthOct,
        m.SchemeDistributionMonthNov && m.DistributionMonthNov, m.SchemeDistributionMonthDec && m.DistributionMonthDec
    }.Count(x => x);

    // A month checkbox is only ever offered when the scheme itself distributes that month at all -
    // matches legacy's `mScheme.DistributionMonthX And ...` guard throughout LoadFormFromObject.
    private static void ApplySchemeMonths(ParticipantSchemeFormViewModel model, SchemeResponse? scheme)
    {
        model.SchemeDistributionMonthJan = scheme?.DistributionMonthJan ?? true;
        model.SchemeDistributionMonthFeb = scheme?.DistributionMonthFeb ?? true;
        model.SchemeDistributionMonthMar = scheme?.DistributionMonthMar ?? true;
        model.SchemeDistributionMonthApr = scheme?.DistributionMonthApr ?? true;
        model.SchemeDistributionMonthMay = scheme?.DistributionMonthMay ?? true;
        model.SchemeDistributionMonthJun = scheme?.DistributionMonthJun ?? true;
        model.SchemeDistributionMonthJul = scheme?.DistributionMonthJul ?? true;
        model.SchemeDistributionMonthAug = scheme?.DistributionMonthAug ?? true;
        model.SchemeDistributionMonthSep = scheme?.DistributionMonthSep ?? true;
        model.SchemeDistributionMonthOct = scheme?.DistributionMonthOct ?? true;
        model.SchemeDistributionMonthNov = scheme?.DistributionMonthNov ?? true;
        model.SchemeDistributionMonthDec = scheme?.DistributionMonthDec ?? true;

        // Legacy Scheme.NumberDistributionsOnScheme / NumberDistributionsAvailableOnScheme.
        var offered = model.SchemeOffersMonths();
        var defined = scheme?.CanEdit;
        bool[] open = defined is null
            ? [.. offered.Select(_ => true)]
            : [defined.Jan, defined.Feb, defined.Mar, defined.Apr, defined.May, defined.Jun, defined.Jul, defined.Aug, defined.Sep, defined.Oct, defined.Nov, defined.Dec];
        model.NumberOnScheme = offered.Count(x => x);
        model.NumberAvailableOnScheme = offered.Where((offeredMonth, i) => offeredMonth && open[i]).Count();
        model.SchemeDistributionAsAvailable = scheme?.DistributionAsAvailable ?? false;
    }

    private static CreateParticipantSchemeRequest ToCreateRequest(ParticipantSchemeFormViewModel model)
    {
        var shared = ToUpdateRequest(model);
        return new CreateParticipantSchemeRequest(
            model.ContractId.GetValueOrDefault(),
            model.ParticipantId!.Value,
            model.SchemeId!.Value,
            shared.DistributionMonthJan,
            shared.DistributionMonthFeb,
            shared.DistributionMonthMar,
            shared.DistributionMonthApr,
            shared.DistributionMonthMay,
            shared.DistributionMonthJun,
            shared.DistributionMonthJul,
            shared.DistributionMonthAug,
            shared.DistributionMonthSep,
            shared.DistributionMonthOct,
            shared.DistributionMonthNov,
            shared.DistributionMonthDec,
            shared.NumberOfSetsRequired,
            shared.ExternalReference,
            shared.Contact,
            shared.ImportExportLicenceRequired,
            shared.CustomsCertificateRequired,
            shared.NonFeePaying,
            shared.PackingInstructions,
            shared.IsWeightedPricing,
            shared.DataConsentDeclarationGiven,
            shared.IsOverrideJan,
            shared.IsOverrideFeb,
            shared.IsOverrideMar,
            shared.IsOverrideApr,
            shared.IsOverrideMay,
            shared.IsOverrideJun,
            shared.IsOverrideJul,
            shared.IsOverrideAug,
            shared.IsOverrideSep,
            shared.IsOverrideOct,
            shared.IsOverrideNov,
            shared.IsOverrideDec,
            shared.GroupAddressId);
    }

    private static UpdateParticipantSchemeRequest ToUpdateRequest(ParticipantSchemeFormViewModel model) => new(
        model.DistributionMonthJan,
        model.DistributionMonthFeb,
        model.DistributionMonthMar,
        model.DistributionMonthApr,
        model.DistributionMonthMay,
        model.DistributionMonthJun,
        model.DistributionMonthJul,
        model.DistributionMonthAug,
        model.DistributionMonthSep,
        model.DistributionMonthOct,
        model.DistributionMonthNov,
        model.DistributionMonthDec,
        model.NumberOfSetsRequired.GetValueOrDefault(1),
        model.ExternalReference,
        model.Contact,
        model.ImportExportLicenceRequired,
        model.CustomsCertificateRequired,
        model.NonFeePaying,
        model.PackingInstructions,
        IsWeightedPricing(model),
        model.DataConsentDeclarationGiven,
        model.OverrideModeActive.GetValueOrDefault() && model.DistributionMonthJan,
        model.OverrideModeActive.GetValueOrDefault() && model.DistributionMonthFeb,
        model.OverrideModeActive.GetValueOrDefault() && model.DistributionMonthMar,
        model.OverrideModeActive.GetValueOrDefault() && model.DistributionMonthApr,
        model.OverrideModeActive.GetValueOrDefault() && model.DistributionMonthMay,
        model.OverrideModeActive.GetValueOrDefault() && model.DistributionMonthJun,
        model.OverrideModeActive.GetValueOrDefault() && model.DistributionMonthJul,
        model.OverrideModeActive.GetValueOrDefault() && model.DistributionMonthAug,
        model.OverrideModeActive.GetValueOrDefault() && model.DistributionMonthSep,
        model.OverrideModeActive.GetValueOrDefault() && model.DistributionMonthOct,
        model.OverrideModeActive.GetValueOrDefault() && model.DistributionMonthNov,
        model.OverrideModeActive.GetValueOrDefault() && model.DistributionMonthDec,
        model.GroupAddressId);

    // Matches legacy LoadObjectFromForm: "0"/Weighted -> True, "1"/Pro rata -> False, "2"/Full
    // scheme pricing -> True ("for full scheme pricing both the weighted plan and pro rata would
    // give the same price... given the weighted plan is the default it is set here"). When the
    // year has no weighted-pricing plan at all, Pro rata (False) is forced regardless of selection.
    private static bool IsWeightedPricing(ParticipantSchemeFormViewModel model) =>
        model.IsWeightedSchemeAvailable && model.PricingPlan != ProRataPricingPlan;

    private static ParticipantSchemeFormViewModel ToFormViewModel(ParticipantSchemeResponse participantScheme, Guid customerId, int yearId, bool isReadOnly) => new()
    {
        ParticipantSchemeId = participantScheme.ParticipantSchemeId,
        ContractId = participantScheme.ContractId,
        CustomerId = customerId,
        YearId = yearId,
        ParticipantId = participantScheme.ParticipantId,
        ParticipantDisplayName = participantScheme.ParticipantDisplayName,
        SchemeId = participantScheme.SchemeId,
        SchemeDisplayName = participantScheme.SchemeDisplayName,
        DistributionMonthJan = participantScheme.DistributionMonthJan,
        DistributionMonthFeb = participantScheme.DistributionMonthFeb,
        DistributionMonthMar = participantScheme.DistributionMonthMar,
        DistributionMonthApr = participantScheme.DistributionMonthApr,
        DistributionMonthMay = participantScheme.DistributionMonthMay,
        DistributionMonthJun = participantScheme.DistributionMonthJun,
        DistributionMonthJul = participantScheme.DistributionMonthJul,
        DistributionMonthAug = participantScheme.DistributionMonthAug,
        DistributionMonthSep = participantScheme.DistributionMonthSep,
        DistributionMonthOct = participantScheme.DistributionMonthOct,
        DistributionMonthNov = participantScheme.DistributionMonthNov,
        DistributionMonthDec = participantScheme.DistributionMonthDec,
        CanEditJan = participantScheme.CanEditJan,
        CanEditFeb = participantScheme.CanEditFeb,
        CanEditMar = participantScheme.CanEditMar,
        CanEditApr = participantScheme.CanEditApr,
        CanEditMay = participantScheme.CanEditMay,
        CanEditJun = participantScheme.CanEditJun,
        CanEditJul = participantScheme.CanEditJul,
        CanEditAug = participantScheme.CanEditAug,
        CanEditSep = participantScheme.CanEditSep,
        CanEditOct = participantScheme.CanEditOct,
        CanEditNov = participantScheme.CanEditNov,
        CanEditDec = participantScheme.CanEditDec,
        NumberOfSetsRequired = participantScheme.NumberOfSetsRequired,
        ExternalReference = participantScheme.ExternalReference,
        Contact = participantScheme.Contact,
        ImportExportLicenceRequired = participantScheme.ImportExportLicenceRequired,
        CustomsCertificateRequired = participantScheme.CustomsCertificateRequired,
        NonFeePaying = participantScheme.NonFeePaying,
        PackingInstructions = participantScheme.PackingInstructions,
        PricingPlan = participantScheme.IsWeightedPricing ? "Weighted" : "ProRata",
        IsWeightedPricing = participantScheme.IsWeightedPricing,
        DataConsentDeclarationGiven = participantScheme.DataConsentDeclarationGiven,
        IsOverrideJan = participantScheme.IsOverrideJan,
        IsOverrideFeb = participantScheme.IsOverrideFeb,
        IsOverrideMar = participantScheme.IsOverrideMar,
        IsOverrideApr = participantScheme.IsOverrideApr,
        IsOverrideMay = participantScheme.IsOverrideMay,
        IsOverrideJun = participantScheme.IsOverrideJun,
        IsOverrideJul = participantScheme.IsOverrideJul,
        IsOverrideAug = participantScheme.IsOverrideAug,
        IsOverrideSep = participantScheme.IsOverrideSep,
        IsOverrideOct = participantScheme.IsOverrideOct,
        IsOverrideNov = participantScheme.IsOverrideNov,
        IsOverrideDec = participantScheme.IsOverrideDec,
        IsReadOnly = isReadOnly,
        GroupAddressId = participantScheme.GroupAddressId
    };
}
