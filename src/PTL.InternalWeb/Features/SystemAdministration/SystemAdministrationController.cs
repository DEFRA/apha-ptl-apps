using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using PTL.ApiClient;
using PTL.Contracts.AdministrationCharge;
using PTL.Contracts.Country;
using PTL.Contracts.ExternalSiteMessage;
using PTL.Contracts.Lookup;
using PTL.Contracts.PostagePricingPlan;
using PTL.Contracts.User;

namespace PTL.InternalWeb.Features.SystemAdministration;

// Authentication/authorization are out of scope for this phase - assume the current user is
// already authenticated with full access, same as every other InternalWeb controller.
public class SystemAdministrationController(
    IAdministrationChargeApiClient administrationChargeApiClient,
    IWeightedPricingPlanApiClient weightedPricingPlanApiClient,
    IPostagePricingPlanApiClient postagePricingPlanApiClient,
    ICountryApiClient countryApiClient,
    IExternalSiteMessageApiClient externalSiteMessageApiClient,
    IUserApiClient userApiClient,
    IRoleApiClient roleApiClient,
    ILookupApiClient lookupApiClient,
    ILogger<SystemAdministrationController> logger) : Controller
{
    private static readonly Action<ILogger, Guid, Guid, Exception?> LogSetPriceFailedMessage =
        LoggerMessage.Define<Guid, Guid>(
            LogLevel.Warning,
            new EventId(1, nameof(LogSetPriceFailedMessage)),
            "Failed to save administration charge {AdministrationChargeId} price for currency {CurrencyId}");

    private static readonly Action<ILogger, Exception?> LogRenewBlockedMessage =
        LoggerMessage.Define(
            LogLevel.Information,
            new EventId(2, nameof(LogRenewBlockedMessage)),
            "Weighted pricing plan renewal was blocked: no percentages configured for the current financial year");

    private static readonly Action<ILogger, int, Exception?> LogRenewedMessage =
        LoggerMessage.Define<int>(
            LogLevel.Information,
            new EventId(3, nameof(LogRenewedMessage)),
            "Renewed weighted pricing plan for financial year {YearId}");

    private static readonly Action<ILogger, Guid, Exception?> LogSetPostagePriceFailedMessage =
        LoggerMessage.Define<Guid>(
            LogLevel.Warning,
            new EventId(4, nameof(LogSetPostagePriceFailedMessage)),
            "Failed to save postage pricing plan {PostageId} price");

    private static readonly Action<ILogger, Exception?> LogPostageRenewBlockedMessage =
        LoggerMessage.Define(
            LogLevel.Information,
            new EventId(5, nameof(LogPostageRenewBlockedMessage)),
            "Postage pricing plan renewal was blocked: no plan configured for the current financial year");

    private static readonly Action<ILogger, int, Exception?> LogPostageRenewedMessage =
        LoggerMessage.Define<int>(
            LogLevel.Information,
            new EventId(6, nameof(LogPostageRenewedMessage)),
            "Renewed postage pricing plan for financial year {YearId}");

    private static readonly Action<ILogger, Guid, Exception?> LogCountryRemoveBlockedMessage =
        LoggerMessage.Define<Guid>(
            LogLevel.Information,
            new EventId(7, nameof(LogCountryRemoveBlockedMessage)),
            "Country {CountryId} could not be removed");
    // Landing page for the System Administration section (moved from the removed Menu feature) -
    // shows only a heading and a short description, relying on the left nav (SideNavigationProvider)
    // for the actual section contents (Administration Charges, Weighted Charging Plan, etc.).
    public IActionResult SystemAdministration() => View();

    public async Task<IActionResult> AdministrationCharge(CancellationToken cancellationToken) =>
        View(await BuildAdministrationChargeViewModelAsync(cancellationToken));

    // Whole-grid submit: every cell is (re)saved, not just the ones the user actually changed -
    // idempotent for unchanged cells, and far simpler than diffing against the previously loaded
    // values. Mirrors the legacy page's end result (Save persists every price shown on screen).
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AdministrationCharge(AdministrationChargeListViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        foreach (var charge in model.Charges)
        {
            foreach (var price in charge.Prices)
            {
                var result = await administrationChargeApiClient.SetPriceAsync(
                    new UpdateAdministrationChargePriceRequest(charge.AdministrationChargeId, price.CurrencyId, price.Price),
                    cancellationToken);
                if (!result.Success)
                {
                    LogSetPriceFailedMessage(logger, charge.AdministrationChargeId, price.CurrencyId, null);
                    ModelState.AddModelError(string.Empty, "One or more prices could not be saved. Please try again.");
                }
            }
        }

        return ModelState.IsValid ? RedirectToAction(nameof(AdministrationCharge)) : View(model);
    }

    public async Task<IActionResult> WeightedPricingPlan(int? yearId, CancellationToken cancellationToken) =>
        View(await BuildWeightedPricingPlanViewModelAsync(yearId, message: null, messageIsError: false, cancellationToken));

    // yearId is whatever year was selected in the dropdown when Renew was clicked (posted via a
    // hidden field) - matches legacy btnRenew_Click, which rebuilds the same page around the
    // already-selected Session("YearId") rather than switching to the newly renewed year.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Renew(int? yearId, CancellationToken cancellationToken)
    {
        var result = await weightedPricingPlanApiClient.RenewAsync(cancellationToken);
        if (result.Success)
        {
            LogRenewedMessage(logger, yearId.GetValueOrDefault(), null);
        }
        else
        {
            LogRenewBlockedMessage(logger, null);
        }

        return View("WeightedPricingPlan", await BuildWeightedPricingPlanViewModelAsync(yearId, result.Message, messageIsError: !result.Success, cancellationToken));
    }

    public async Task<IActionResult> PostagePricingPlan(int? yearId, Guid? editId, CancellationToken cancellationToken) =>
        View(await BuildPostagePricingPlanViewModelAsync(yearId, editId, message: null, messageIsError: false, editValues: null, cancellationToken));

    // Only the row matching model.PostageId is ever in edit mode when this posts - matches legacy
    // GridViewPostagePricingPlan_Updating, which only acts on the row with EditIndex set.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PostagePricingPlanSave(PostagePricingPlanEditViewModel model, CancellationToken cancellationToken)
    {
        if (ModelState.IsValid)
        {
            var result = await postagePricingPlanApiClient.SetPriceAsync(
                new UpdatePostagePricingPlanPriceRequest(model.PostageId, model.UKPrice, model.EUPrice, model.NonEUPrice),
                cancellationToken);

            if (result.Success)
            {
                return RedirectToAction(nameof(PostagePricingPlan), new { yearId = model.YearId });
            }

            LogSetPostagePriceFailedMessage(logger, model.PostageId, null);
            foreach (var (field, messages) in result.FieldErrors)
            {
                foreach (var errorMessage in messages)
                {
                    ModelState.AddModelError(field, errorMessage);
                }
            }
        }

        return View(nameof(PostagePricingPlan), await BuildPostagePricingPlanViewModelAsync(model.YearId, model.PostageId, message: null, messageIsError: false, model, cancellationToken));
    }

    // yearId is whatever year was selected in the dropdown when Renew was clicked (posted via a
    // hidden field) - matches legacy btnRenew_Click, which rebuilds the same page around the
    // already-selected Session("YearId") rather than switching to the newly renewed year.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PostagePricingPlanRenew(int? yearId, CancellationToken cancellationToken)
    {
        var result = await postagePricingPlanApiClient.RenewAsync(cancellationToken);
        if (result.Success)
        {
            LogPostageRenewedMessage(logger, yearId.GetValueOrDefault(), null);
        }
        else
        {
            LogPostageRenewBlockedMessage(logger, null);
        }

        return View(nameof(PostagePricingPlan), await BuildPostagePricingPlanViewModelAsync(yearId, editId: null, result.Message, messageIsError: !result.Success, editValues: null, cancellationToken));
    }

    public async Task<IActionResult> CountryManagement(Guid? editId, CancellationToken cancellationToken) =>
        View(await BuildCountryManagementViewModelAsync(editId, message: null, messageIsError: false, addValues: null, editValues: null, cancellationToken));

    // Matches legacy ButtonAdd_Click - Name and Country Type are required, and the name must not
    // already exist (CountryValidator/CountryService, surfaced here as a plain ModelState error).
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CountryManagementAdd([Bind(Prefix = "Add")] CountryManagementAddViewModel model, CancellationToken cancellationToken)
    {
        if (ModelState.IsValid)
        {
            var result = await countryApiClient.CreateCountryAsync(new CountrySaveRequest(model.Country, model.CountryTypeId!.Value), cancellationToken);
            if (result.Success)
            {
                return RedirectToAction(nameof(CountryManagement));
            }

            foreach (var (field, messages) in result.FieldErrors)
            {
                foreach (var errorMessage in messages)
                {
                    ModelState.AddModelError($"Add.{field}", errorMessage);
                }
            }
        }

        return View(nameof(CountryManagement), await BuildCountryManagementViewModelAsync(editId: null, message: null, messageIsError: false, model, editValues: null, cancellationToken));
    }

    // Only the row matching model.CountryId is ever in edit mode when this posts - matches legacy
    // GridViewCountry_Updating, which only acts on the row with EditIndex set. Posted field names
    // use an "Edit" prefix (see Views/CountryManagement.cshtml) so ModelState errors stay scoped
    // to this row and never bleed into the separate "Add a new country" form on the same page.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CountryManagementSave([Bind(Prefix = "Edit")] CountryManagementEditViewModel model, CancellationToken cancellationToken)
    {
        if (ModelState.IsValid)
        {
            var result = await countryApiClient.UpdateCountryAsync(model.CountryId, new CountrySaveRequest(model.Country, model.CountryTypeId!.Value), cancellationToken);
            if (result.Success)
            {
                return RedirectToAction(nameof(CountryManagement));
            }

            foreach (var (field, messages) in result.FieldErrors)
            {
                foreach (var errorMessage in messages)
                {
                    ModelState.AddModelError($"Edit.{field}", errorMessage);
                }
            }
        }

        return View(nameof(CountryManagement), await BuildCountryManagementViewModelAsync(model.CountryId, message: null, messageIsError: false, addValues: null, model, cancellationToken));
    }

    // Persists immediately (unlike legacy, which only removed the row from an in-memory
    // collection until the page-level Save button was clicked) - see issue discussion on
    // replicating the Postage Pricing Plan "immediate per-action persistence" convention. Blocked
    // removals (country still referenced by a Customer/Participant/GroupAddress record) surface
    // legacy's exact warning message as a notification banner rather than a JS alert.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CountryManagementRemove(Guid countryId, CancellationToken cancellationToken)
    {
        var result = await countryApiClient.DeleteCountryAsync(countryId, cancellationToken);
        if (!result.Success)
        {
            LogCountryRemoveBlockedMessage(logger, countryId, null);
        }

        return View(nameof(CountryManagement), await BuildCountryManagementViewModelAsync(editId: null, result.Message, messageIsError: !result.Success, addValues: null, editValues: null, cancellationToken));
    }

    public async Task<IActionResult> ExternalSiteManagement(CancellationToken cancellationToken)
    {
        var message = await externalSiteMessageApiClient.GetExternalSiteMessageAsync(cancellationToken);
        return View(BuildExternalSiteManagementViewModel(message));
    }

    // Save publishes all three areas together in one request - matches legacy ButtonSave_Click,
    // which writes Message/ImportantMessage/SupportEmailAddress in a single spuMainPageMessage
    // call. Cancel needs no action method of its own - the view's Cancel link is a plain GET back
    // to this action, which re-fetches from the API and so discards any unsaved edits, matching
    // legacy ButtonCancel_Click's "revert to the last saved version" behaviour.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ExternalSiteManagement(ExternalSiteManagementViewModel model, CancellationToken cancellationToken)
    {
        if (ModelState.IsValid)
        {
            var result = await externalSiteMessageApiClient.UpdateExternalSiteMessageAsync(
                new ExternalSiteMessageSaveRequest(model.Message, model.ImportantMessage, model.SupportEmailAddress),
                cancellationToken);

            if (result.Success)
            {
                return RedirectToAction(nameof(ExternalSiteManagement));
            }

            foreach (var (field, messages) in result.FieldErrors)
            {
                foreach (var errorMessage in messages)
                {
                    ModelState.AddModelError(field, errorMessage);
                }
            }
        }

        return View(model);
    }

    private static ExternalSiteManagementViewModel BuildExternalSiteManagementViewModel(ExternalSiteMessageResponse message) => new()
    {
        Message = message.Message,
        ImportantMessage = message.ImportantMessage,
        SupportEmailAddress = message.SupportEmailAddress
    };

    public IActionResult CreateUser() => View(new CreateUserViewModel());

    // Matches legacy GetADList - searches the staff directory (stubbed - see
    // IStaffDirectoryService) and shows "No valid search results found" whether nobody matched at
    // all, or everybody who matched already has a PT-LIMS account (both collapse to an empty
    // result set, same as legacy).
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateUserSearch(CreateUserViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(nameof(CreateUser), model);
        }

        var results = await userApiClient.SearchStaffDirectoryAsync(model.SearchTerm, cancellationToken);
        model.HasSearched = true;
        model.ResultOptions = BuildResultOptions(results);

        if (model.ResultOptions.Count == 0)
        {
            model.Message = "No valid search results found";
            model.MessageIsError = true;
        }

        return View(nameof(CreateUser), model);
    }

    // Only the candidate matching model.SelectedCandidate is ever created - matches legacy
    // Button_CreateUser_Click, which only acts on DropDownList_Users.SelectedValue. SearchTerm is
    // round-tripped via a hidden field so the dropdown can be rebuilt if validation fails.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateUserConfirm(CreateUserViewModel model, CancellationToken cancellationToken)
    {
        if (ModelState.IsValid && !string.IsNullOrEmpty(model.SelectedCandidate))
        {
            var candidate = UnpackCandidate(model.SelectedCandidate);
            var result = await userApiClient.CreateUserAsync(
                new CreateUserRequest(candidate.Username, candidate.Email, candidate.FriendlyName, candidate.FirstName, candidate.LastName, model.Department),
                cancellationToken);

            if (result.Success)
            {
                return View(nameof(CreateUser), new CreateUserViewModel { IsCreated = true, Message = "User Added Successfully" });
            }

            foreach (var (field, messages) in result.FieldErrors)
            {
                foreach (var errorMessage in messages)
                {
                    ModelState.AddModelError(field, errorMessage);
                }
            }
        }

        model.HasSearched = true;
        var results = await userApiClient.SearchStaffDirectoryAsync(model.SearchTerm, cancellationToken);
        model.ResultOptions = BuildResultOptions(results);
        return View(nameof(CreateUser), model);
    }

    private static List<SelectListItem> BuildResultOptions(IReadOnlyList<StaffDirectoryUserResponse> results) =>
        results.Select(r => new SelectListItem($"{r.FriendlyName} : {r.Username}", PackCandidate(r))).ToList();

    private static string PackCandidate(StaffDirectoryUserResponse candidate) =>
        string.Join('|', candidate.Username, candidate.Email, candidate.FriendlyName, candidate.FirstName, candidate.LastName);

    private static StaffDirectoryUserResponse UnpackCandidate(string packed)
    {
        var parts = packed.Split('|');
        return new StaffDirectoryUserResponse(parts[0], parts[1], parts[2], parts[3], parts[4]);
    }

    public async Task<IActionResult> ManageUserRoles(CancellationToken cancellationToken) =>
        View(await BuildManageUserRolesViewModelAsync(message: null, messageIsError: false, cancellationToken));

    // Whole-grid submit: every user's role set shown on screen is (re)saved, not just the ones
    // actually changed - idempotent for unchanged rows, mirrors the AdministrationCharge screen's
    // save-the-whole-grid convention. Unlike legacy's UserRoles.aspx (every checkbox click is its
    // own immediate save), this only persists on an explicit Save - matches the story's "When I
    // click Save... Then... persisted" acceptance criteria.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ManageUserRoles(ManageUserRolesViewModel model, CancellationToken cancellationToken)
    {
        var blockedMessages = new List<string>();

        foreach (var row in model.Rows)
        {
            var result = await userApiClient.SetUserRolesAsync(row.UserId, new SetUserRolesRequest(row.SelectedRoleIds), cancellationToken);
            if (!result.Success && result.Message is not null)
            {
                blockedMessages.Add($"{row.FriendlyName}: {result.Message}");
            }
        }

        var message = blockedMessages.Count > 0
            ? string.Join(" ", blockedMessages)
            : "Role assignments updated successfully.";

        return View(await BuildManageUserRolesViewModelAsync(message, messageIsError: blockedMessages.Count > 0, cancellationToken));
    }

    private async Task<ManageUserRolesViewModel> BuildManageUserRolesViewModelAsync(string? message, bool messageIsError, CancellationToken cancellationToken)
    {
        var roles = await roleApiClient.GetRolesAsync(cancellationToken);
        var rows = await userApiClient.GetUserRoleGridAsync(cancellationToken);

        return new ManageUserRolesViewModel
        {
            Roles = roles.Select(r => new RoleColumnViewModel { RoleId = r.RoleId, Name = r.Name }).ToList(),
            Rows = rows.Select(r => new UserRoleRowViewModel
            {
                UserId = r.UserId,
                Username = r.Username,
                FriendlyName = r.FriendlyName,
                SelectedRoleIds = r.RoleIds.ToList()
            }).ToList(),
            Message = message,
            MessageIsError = messageIsError
        };
    }

    private async Task<AdministrationChargeListViewModel> BuildAdministrationChargeViewModelAsync(CancellationToken cancellationToken)
    {
        var currencies = await lookupApiClient.GetCurrenciesAsync(cancellationToken);
        var charges = await administrationChargeApiClient.GetAdministrationChargesAsync(cancellationToken);

        var rows = charges.Select(charge => new AdministrationChargeRowViewModel
        {
            AdministrationChargeId = charge.AdministrationChargeId,
            Name = charge.Name,
            Prices = currencies.Select(currency => new AdministrationChargePriceCellViewModel
            {
                CurrencyId = currency.CurrencyId,
                CurrencyLabel = currency.LongName,
                Price = charge.Prices.FirstOrDefault(p => p.CurrencyId == currency.CurrencyId)?.Price ?? 0m
            }).ToList()
        }).ToList();

        return new AdministrationChargeListViewModel { Charges = rows };
    }

    private async Task<WeightedPricingPlanViewModel> BuildWeightedPricingPlanViewModelAsync(int? yearId, string? message, bool messageIsError, CancellationToken cancellationToken)
    {
        var years = await weightedPricingPlanApiClient.GetYearsAsync(cancellationToken);
        if (years.AvailableYears.Count == 0)
        {
            return new WeightedPricingPlanViewModel
            {
                HasNoYears = true,
                Message = "No weighted pricing percentages have been entered. Please contact SFW.",
                MessageIsError = true
            };
        }

        // Mirrors legacy Page_Load: default to the current financial year if it has a plan,
        // otherwise fall back to the earliest available year - only overridden by an explicit
        // yearId (the year dropdown's own selection, or Renew's posted-back hidden field).
        var currentYears = await lookupApiClient.GetCurrentYearsAsync(cancellationToken);
        var currentYearId = currentYears.Count > 0 ? currentYears[0].YearId : (int?)null;

        int selectedYearId;
        if (yearId is not null && years.AvailableYears.Any(y => y.YearId == yearId))
        {
            selectedYearId = yearId.Value;
        }
        else if (currentYearId is not null && years.AvailableYears.Any(y => y.YearId == currentYearId))
        {
            selectedYearId = currentYearId.Value;
        }
        else
        {
            selectedYearId = years.AvailableYears[0].YearId;
        }

        var percentages = await weightedPricingPlanApiClient.GetPercentagesForYearAsync(selectedYearId, cancellationToken);
        var rowKeys = percentages.Select(p => p.NumberOfDistributionsOnScheme).Distinct().OrderBy(n => n).ToList();
        var columnKeys = percentages.Select(p => p.NumberOfDistributionsChosen).Distinct().OrderBy(n => n).ToList();

        var rows = rowKeys.Select(numberOnScheme => new WeightedPricingPlanRowViewModel
        {
            NumberOfDistributionsOnScheme = numberOnScheme,
            WeightByDistributionsChosen = columnKeys.ToDictionary(
                numberChosen => numberChosen,
                numberChosen => percentages.FirstOrDefault(p => p.NumberOfDistributionsOnScheme == numberOnScheme && p.NumberOfDistributionsChosen == numberChosen)?.Weight)
        }).ToList();

        return new WeightedPricingPlanViewModel
        {
            SelectedYearId = selectedYearId,
            YearOptions = years.AvailableYears.Select(y => new SelectListItem(y.Year, y.YearId.ToString(CultureInfo.InvariantCulture))),
            DistributionsChosenColumns = columnKeys,
            Rows = rows,
            CanRenew = years.CanRenew,
            NextYearLabel = years.NextYearLabel,
            Message = message,
            MessageIsError = messageIsError
        };
    }

    private async Task<PostagePricingPlanViewModel> BuildPostagePricingPlanViewModelAsync(int? yearId, Guid? editId, string? message, bool messageIsError, PostagePricingPlanEditViewModel? editValues, CancellationToken cancellationToken)
    {
        var years = await postagePricingPlanApiClient.GetYearsAsync(cancellationToken);
        if (years.AvailableYears.Count == 0)
        {
            return new PostagePricingPlanViewModel
            {
                HasNoYears = true,
                Message = "No postage pricing plan has been entered. Please contact SFW.",
                MessageIsError = true
            };
        }

        // Mirrors legacy Page_Load: default to the current financial year if it has a plan,
        // otherwise fall back to the earliest available year - only overridden by an explicit
        // yearId (the year dropdown's own selection, or Renew/Save's posted-back hidden field).
        var currentYears = await lookupApiClient.GetCurrentYearsAsync(cancellationToken);
        var currentYearId = currentYears.Count > 0 ? currentYears[0].YearId : (int?)null;
        var selectedYearId = ResolveSelectedYearId(yearId, years.AvailableYears, currentYearId);

        var plans = await lookupApiClient.GetPostagePricingPlansForYearAsync(selectedYearId, cancellationToken);

        var rows = plans
            .Select(plan => BuildPostagePricingPlanRow(plan, editId, editValues))
            .OrderBy(row => row.Name)
            .ToList();

        return new PostagePricingPlanViewModel
        {
            SelectedYearId = selectedYearId,
            YearOptions = years.AvailableYears.Select(y => new SelectListItem(y.Year, y.YearId.ToString(CultureInfo.InvariantCulture))),
            Rows = rows,
            CanRenew = years.CanRenew,
            NextYearLabel = years.NextYearLabel,
            Message = message,
            MessageIsError = messageIsError
        };
    }

    private static int ResolveSelectedYearId(int? requestedYearId, IReadOnlyList<YearResponse> availableYears, int? currentYearId)
    {
        if (requestedYearId is not null && availableYears.Any(y => y.YearId == requestedYearId))
        {
            return requestedYearId.Value;
        }

        if (currentYearId is not null && availableYears.Any(y => y.YearId == currentYearId))
        {
            return currentYearId.Value;
        }

        return availableYears[0].YearId;
    }

    private static PostagePricingPlanRowViewModel BuildPostagePricingPlanRow(PostagePricingPlanResponse plan, Guid? editId, PostagePricingPlanEditViewModel? editValues)
    {
        var isEditing = editId == plan.PostageId;
        var overlay = isEditing ? editValues : null;

        return new PostagePricingPlanRowViewModel
        {
            PostageId = plan.PostageId,
            YearId = plan.YearId,
            Name = plan.Name,
            IsEditing = isEditing,
            UKPrice = overlay?.UKPrice ?? plan.UKPrice ?? 0m,
            EUPrice = overlay?.EUPrice ?? plan.EUPrice ?? 0m,
            NonEUPrice = overlay?.NonEUPrice ?? plan.NonEUPrice ?? 0m
        };
    }

    private async Task<CountryManagementViewModel> BuildCountryManagementViewModelAsync(
        Guid? editId,
        string? message,
        bool messageIsError,
        CountryManagementAddViewModel? addValues,
        CountryManagementEditViewModel? editValues,
        CancellationToken cancellationToken)
    {
        var countries = await countryApiClient.GetCountriesAsync(cancellationToken);
        var countryTypes = await countryApiClient.GetCountryTypesAsync(cancellationToken);

        var rows = countries
            .OrderBy(c => c.Country)
            .Select(c => new CountryManagementRowViewModel
            {
                CountryId = c.CountryId,
                Country = editId == c.CountryId && editValues is not null ? editValues.Country : c.Country,
                CountryType = c.CountryType,
                CountryTypeId = editId == c.CountryId && editValues?.CountryTypeId is not null ? editValues.CountryTypeId.Value : c.CountryTypeId,
                AllocationCount = c.AllocationCount,
                IsEditing = editId == c.CountryId
            })
            .ToList();

        return new CountryManagementViewModel
        {
            Rows = rows,
            CountryTypeOptions = countryTypes
                .OrderBy(t => t.CountryType)
                .Select(t => new SelectListItem(t.CountryType, t.CountryTypeId.ToString()))
                .ToList(),
            Add = addValues ?? new CountryManagementAddViewModel(),
            Message = message,
            MessageIsError = messageIsError
        };
    }
}
