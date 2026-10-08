using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Logging;
using PTL.ApiClient;
using PTL.Contracts.Participant;
using PTL.Contracts.Scheme;
using PTL.InternalWeb.Navigation;
using PTL.SharedUI.Notifications;

namespace PTL.InternalWeb.Features.ParticipantScheme;

// Authentication/authorization are out of scope for this phase - assume the current user is
// already authenticated with full access to Participant Scheme functionality.
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

    [HttpGet]
    public async Task<IActionResult> Create(
        Guid contractId,
        Guid customerId,
        Guid? participantId = null,
        Guid? schemeId = null,
        string? schemeSearchTerm = null,
        int schemeOptionsPage = 1,
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
            SchemeOptionsPage = schemeOptionsPage,
            IsReadOnly = contract.IsReadOnly
        };

        await PopulateParticipantOptionsAsync(model, cancellationToken);
        await PopulateSchemeContextAsync(model, cancellationToken);
        await PopulatePricingPlanAsync(model, isFreshLoad: true, cancellationToken);
        await PopulateGroupAddressOptionsAsync(model, cancellationToken);
        SetBreadcrumbContext(customerId, contractId);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ParticipantSchemeFormViewModel model, CancellationToken cancellationToken)
    {
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

            await PopulateParticipantOptionsAsync(model, cancellationToken);
            await PopulateSchemeContextAsync(model, cancellationToken);
            await PopulatePricingPlanAsync(model, isFreshLoad: false, cancellationToken);
            await PopulateGroupAddressOptionsAsync(model, cancellationToken);
            return View(model);
        }

        try
        {
            var result = await participantSchemeApiClient.CreateParticipantSchemeAsync(ToCreateRequest(model), cancellationToken);
            if (!result.Success)
            {
                LogCreateFailedMessage(logger, model.ContractId.GetValueOrDefault(), null);
                this.AddFieldErrors(result.FieldErrors);
                await PopulateParticipantOptionsAsync(model, cancellationToken);
                await PopulateSchemeContextAsync(model, cancellationToken);
                await PopulatePricingPlanAsync(model, isFreshLoad: false, cancellationToken);
                await PopulateGroupAddressOptionsAsync(model, cancellationToken);
                return View(model);
            }

            LogCreatedMessage(logger, result.ParticipantScheme!.ParticipantSchemeId, null);
            TempData.SetNotification(NotificationType.Success, "Contract item created successfully.");
            return RedirectToAction("ContractItems", "Contract", new { area = "", id = model.ContractId.GetValueOrDefault() });
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogSaveErrorMessage(logger, model.ContractId.GetValueOrDefault(), ex);
            ModelState.AddModelError(string.Empty, "The contract item could not be saved. Please try again.");
            await PopulateParticipantOptionsAsync(model, cancellationToken);
            await PopulateSchemeContextAsync(model, cancellationToken);
            await PopulatePricingPlanAsync(model, isFreshLoad: false, cancellationToken);
            await PopulateGroupAddressOptionsAsync(model, cancellationToken);
            return View(model);
        }
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
        await PopulateDataConsentAsync(model, cancellationToken);
        await PopulatePricingPlanAsync(model, isFreshLoad: true, cancellationToken);
        await PopulateGroupAddressOptionsAsync(model, cancellationToken);
        SetBreadcrumbContext(contract.CustomerId, participantScheme.ContractId);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, ParticipantSchemeFormViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            await PopulateDataConsentAsync(model, cancellationToken);
            await PopulatePricingPlanAsync(model, isFreshLoad: false, cancellationToken);
            await PopulateGroupAddressOptionsAsync(model, cancellationToken);
            return View(model);
        }

        try
        {
            var result = await participantSchemeApiClient.UpdateParticipantSchemeAsync(id, ToUpdateRequest(model), cancellationToken);
            if (!result.Success)
            {
                LogUpdateFailedMessage(logger, id, null);
                this.AddFieldErrors(result.FieldErrors);
                await PopulateDataConsentAsync(model, cancellationToken);
                await PopulatePricingPlanAsync(model, isFreshLoad: false, cancellationToken);
                await PopulateGroupAddressOptionsAsync(model, cancellationToken);
                return View(model);
            }

            LogUpdatedMessage(logger, id, null);
            TempData.SetNotification(NotificationType.Success, "Contract item updated successfully.");
            return RedirectToAction(nameof(Details), new { id });
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogSaveErrorMessage(logger, model.ContractId.GetValueOrDefault(), ex);
            ModelState.AddModelError(string.Empty, "The contract item could not be saved. Please try again.");
            await PopulateDataConsentAsync(model, cancellationToken);
            await PopulatePricingPlanAsync(model, isFreshLoad: false, cancellationToken);
            await PopulateGroupAddressOptionsAsync(model, cancellationToken);
            return View(model);
        }
    }

    private async Task PopulateParticipantOptionsAsync(ParticipantSchemeFormViewModel model, CancellationToken cancellationToken)
    {
        var participants = await participantApiClient.GetParticipantsAsync(model.CustomerId.GetValueOrDefault(), includeInactive: false, cancellationToken);
        model.ParticipantOptions = participants
            .Select(p => new SelectListItem($"{p.LabCode}: {p.LabName}", p.ParticipantId.ToString()))
            .ToList();
    }

    // Matches legacy GridViewSchemes (SchemeInfoCollection.FetchSchemeInfoCollectionByYearId, paged
    // at 15/page) - reuses the existing Scheme domain's search/paging rather than duplicating it.
    private async Task PopulateSchemeContextAsync(ParticipantSchemeFormViewModel model, CancellationToken cancellationToken)
    {
        if (model.SchemeId is { } schemeId)
        {
            var scheme = await schemeApiClient.GetSchemeAsync(schemeId, cancellationToken);
            model.SchemeDisplayName = scheme is null ? string.Empty : $"{scheme.Identifier}: {scheme.Name}";
            model.SchemeRequiresDataConsent = scheme?.DataConsentDeclarationActive ?? false;
            ApplySchemeMonths(model, scheme);
            return;
        }

        var request = new SchemeSearchRequest(model.YearId.GetValueOrDefault(), model.SchemeSearchTerm, model.SchemeOptionsPage.GetValueOrDefault(1), PTL.SharedUI.Pagination.PaginationModel.DefaultPageSize);
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

    // Matches legacy LoadPricingOptions: DdlPricingPlan only ever offers "Full scheme pricing" once
    // every scheme month the participant could possibly have is already selected (or none at all
    // when the whole scheme is fully available), otherwise it offers "Weighted pricing plan"/
    // "Pro rata"; hidden entirely when the contract's year has no weighted-pricing plan configured
    // (WeightedPricingYearCollection.PricingPlanExists).
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

        var numberOnScheme = CountSchemeMonths(model);
        var numberAvailableOnScheme = CountAvailableSchemeMonths(model);
        var numberOnParticipantScheme = CountParticipantSchemeMonths(model);

        if (numberOnParticipantScheme == numberOnScheme || (numberOnParticipantScheme == 0 && numberOnScheme == numberAvailableOnScheme))
        {
            model.PricingPlanOptions = [new SelectListItem("Full scheme pricing", "Full")];
            model.PricingPlan = "Full";
            return;
        }

        model.PricingPlanOptions =
        [
            new SelectListItem("Weighted pricing plan", WeightedPricingPlan),
            new SelectListItem("Pro rata", ProRataPricingPlan)
        ];

        if (isFreshLoad)
        {
            if (model.ParticipantSchemeId is null)
            {
                model.PricingPlan = WeightedPricingPlan;
            }
            else
            {
                model.PricingPlan = model.IsWeightedPricing.GetValueOrDefault() ? WeightedPricingPlan : ProRataPricingPlan;
            }
        }
    }

    private static int CountSchemeMonths(ParticipantSchemeFormViewModel m) => new[]
    {
        m.SchemeDistributionMonthJan, m.SchemeDistributionMonthFeb, m.SchemeDistributionMonthMar, m.SchemeDistributionMonthApr,
        m.SchemeDistributionMonthMay, m.SchemeDistributionMonthJun, m.SchemeDistributionMonthJul, m.SchemeDistributionMonthAug,
        m.SchemeDistributionMonthSep, m.SchemeDistributionMonthOct, m.SchemeDistributionMonthNov, m.SchemeDistributionMonthDec
    }.Count(x => x);

    private static int CountAvailableSchemeMonths(ParticipantSchemeFormViewModel m) => new[]
    {
        m.SchemeDistributionMonthJan && m.CanEditJan, m.SchemeDistributionMonthFeb && m.CanEditFeb,
        m.SchemeDistributionMonthMar && m.CanEditMar, m.SchemeDistributionMonthApr && m.CanEditApr,
        m.SchemeDistributionMonthMay && m.CanEditMay, m.SchemeDistributionMonthJun && m.CanEditJun,
        m.SchemeDistributionMonthJul && m.CanEditJul, m.SchemeDistributionMonthAug && m.CanEditAug,
        m.SchemeDistributionMonthSep && m.CanEditSep, m.SchemeDistributionMonthOct && m.CanEditOct,
        m.SchemeDistributionMonthNov && m.CanEditNov, m.SchemeDistributionMonthDec && m.CanEditDec
    }.Count(x => x);

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
        Price = participantScheme.Price,
        IsReadOnly = isReadOnly,
        GroupAddressId = participantScheme.GroupAddressId
    };
}
