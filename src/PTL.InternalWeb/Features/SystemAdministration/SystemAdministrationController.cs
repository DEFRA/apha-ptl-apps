using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Polly;
using PTL.ApiClient;
using PTL.Common.Graph;
using PTL.Contracts.AdministrationCharge;
using PTL.Contracts.Country;
using PTL.Contracts.ExternalSiteMessage;
using PTL.Contracts.Lookup;
using PTL.Contracts.PostagePricingPlan;
using PTL.Contracts.TestConsultant;
using PTL.Contracts.User;
using PTL.Contracts.Viewer;
using PTL.Core.User;
using PTL.InternalWeb.Authorization;
using PTL.InternalWeb.Confirmation;
using PTL.InternalWeb.Features.Account;
using PTL.SharedUI.Notifications;

namespace PTL.InternalWeb.Features.SystemAdministration;

// Gated behind the "Admin" role (resolved from our own database, not an Entra ID group). PTL.Api itself
// remains unauthenticated for this phase (tracked separately) - this is the one enforcement point today.
[Authorize(Policy = PtlPolicies.Admin)]
public partial class SystemAdministrationController : Controller
{
    private readonly IAdministrationChargeApiClient administrationChargeApiClient;
    private readonly IWeightedPricingPlanApiClient weightedPricingPlanApiClient;
    private readonly IPostagePricingPlanApiClient postagePricingPlanApiClient;
    private readonly ICountryApiClient countryApiClient;
    private readonly IExternalSiteMessageApiClient externalSiteMessageApiClient;
    private readonly IUserApiClient userApiClient;
    private readonly IRoleApiClient roleApiClient;
    private readonly ILookupApiClient lookupApiClient;
    private readonly IExternalTestConsultantApiClient externalTestConsultantApiClient;
    private readonly IViewerApiClient viewerApiClient;
    private readonly IStaffDirectoryService staffDirectoryService;
    private readonly ILogger<SystemAdministrationController> logger;

    // Takes the bundled SystemAdministrationApiClients rather than one parameter per client, to stay
    // under the analyzer's constructor-parameter-count threshold - see that type's remarks.
    public SystemAdministrationController(SystemAdministrationApiClients apiClients, IStaffDirectoryService staffDirectoryService, ILogger<SystemAdministrationController> logger)
    {
        administrationChargeApiClient = apiClients.AdministrationCharge;
        weightedPricingPlanApiClient = apiClients.WeightedPricingPlan;
        postagePricingPlanApiClient = apiClients.PostagePricingPlan;
        countryApiClient = apiClients.Country;
        externalSiteMessageApiClient = apiClients.ExternalSiteMessage;
        userApiClient = apiClients.User;
        roleApiClient = apiClients.Role;
        lookupApiClient = apiClients.Lookup;
        externalTestConsultantApiClient = apiClients.ExternalTestConsultant;
        viewerApiClient = apiClients.Viewer;
        this.staffDirectoryService = staffDirectoryService;
        this.logger = logger;
    }

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

    private static readonly Action<ILogger, Exception?> LogDirectorySearchFailedMessage =
        LoggerMessage.Define(
            LogLevel.Warning,
            new EventId(8, nameof(LogDirectorySearchFailedMessage)),
            "Staff directory search failed while creating a user");

    // Landing page for the System Administration section (moved from the removed Menu feature) -
    // shows only a heading and a short description, relying on the left nav (SideNavigationProvider)
    // for the actual section contents (Administration Charges, Weighted Pricing Plan, etc.).
    public IActionResult Index() => View();

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

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        TempData.SetNotification(NotificationType.Success, "Administration charges saved successfully.");
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> WeightedPricingPlan(int? yearId, CancellationToken cancellationToken) =>
        View(await BuildWeightedPricingPlanViewModelAsync(yearId, message: null, messageIsError: false, cancellationToken));

    // yearId is whatever year was selected in the dropdown when Renew was clicked (posted via a
    // hidden field) - matches legacy btnRenew_Click, which rebuilds the same page around the
    // already-selected Session("YearId") rather than switching to the newly renewed year. The
    // browser confirm() is replaced by the shared two-step confirmation page.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Renew(int? yearId, string? confirmation, CancellationToken cancellationToken)
    {
        if (ConfirmationFlow.IsCancelled(confirmation))
        {
            return RedirectToAction(nameof(WeightedPricingPlan), new { yearId });
        }

        if (!ConfirmationFlow.IsConfirmed(confirmation))
        {
            var years = await weightedPricingPlanApiClient.GetYearsAsync(cancellationToken);
            if (years is { CanRenew: true, NextYearLabel: { } nextYearLabel })
            {
                return this.ConfirmationPage(RenewPricingPlanConfirmation("weighted", "Weighted Pricing Plan", nextYearLabel), nameof(WeightedPricingPlan));
            }
        }

        var result = await weightedPricingPlanApiClient.RenewAsync(cancellationToken);
        if (result.Success)
        {
            LogRenewedMessage(logger, yearId.GetValueOrDefault(), null);
            TempData.SetNotification(NotificationType.Success, result.Message);
            return RedirectToAction(nameof(Index));
        }

        LogRenewBlockedMessage(logger, null);
        return View("WeightedPricingPlan", await BuildWeightedPricingPlanViewModelAsync(yearId, result.Message, messageIsError: true, cancellationToken));
    }

    private static ConfirmationPageViewModel RenewPricingPlanConfirmation(string planName, string caption, string nextYearLabel) =>
        new()
        {
            Title = $"Renew pricing plan for financial year {nextYearLabel}",
            Caption = caption,
            Message = $"You are about to renew the {planName} pricing plan for the financial year {nextYearLabel}. The plan for the current financial year will be used to create the plan for {nextYearLabel}.",
            WarningText = "A pricing plan can only be renewed once for each financial year, and this cannot be undone here.",
            ConfirmText = "Renew pricing plan",
        };

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
                new UpdatePostagePricingPlanPriceRequest(model.PostageId!.Value, model.UKPrice, model.EUPrice, model.NonEUPrice),
                cancellationToken);

            if (result.Success)
            {
                TempData.SetNotification(NotificationType.Success, "Postage pricing plan saved successfully.");
                return RedirectToAction(nameof(Index));
            }

            LogSetPostagePriceFailedMessage(logger, model.PostageId.Value, null);
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
    public async Task<IActionResult> PostagePricingPlanRenew(int? yearId, string? confirmation, CancellationToken cancellationToken)
    {
        if (ConfirmationFlow.IsCancelled(confirmation))
        {
            return RedirectToAction(nameof(PostagePricingPlan), new { yearId });
        }

        if (!ConfirmationFlow.IsConfirmed(confirmation))
        {
            var years = await postagePricingPlanApiClient.GetYearsAsync(cancellationToken);
            if (years is { CanRenew: true, NextYearLabel: { } nextYearLabel })
            {
                return this.ConfirmationPage(RenewPricingPlanConfirmation("postage", "Postage Pricing Plan", nextYearLabel), nameof(PostagePricingPlan));
            }
        }

        var result = await postagePricingPlanApiClient.RenewAsync(cancellationToken);
        if (result.Success)
        {
            LogPostageRenewedMessage(logger, yearId.GetValueOrDefault(), null);
            TempData.SetNotification(NotificationType.Success, result.Message);
            return RedirectToAction(nameof(Index));
        }

        LogPostageRenewBlockedMessage(logger, null);
        return View(nameof(PostagePricingPlan), await BuildPostagePricingPlanViewModelAsync(yearId, editId: null, result.Message, messageIsError: true, editValues: null, cancellationToken));
    }

    public async Task<IActionResult> ExternalSiteManagement(CancellationToken cancellationToken)
    {
        var message = await externalSiteMessageApiClient.GetExternalSiteMessageAsync(cancellationToken);
        return View(BuildExternalSiteManagementViewModel(message));
    }

    // Save publishes all three areas together in one request - matches legacy ButtonSave_Click,
    // which writes Message/ImportantMessage/SupportEmailAddress in a single spuMainPageMessage
    // call. Cancel needs no action method of its own - the view's Cancel button is a plain GET to
    // the System Administration landing page, so unsaved edits are discarded.
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
                TempData.SetNotification(NotificationType.Success, "External site management saved successfully.");
                return RedirectToAction(nameof(Index));
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

    // Matches legacy GetADList - searches the staff directory (Microsoft Graph, called from this app) and shows
    // "No valid search results found" whether nobody matched at all, or everybody who matched already
    // has a PT-LIMS account (both collapse to an empty result set, same as legacy).
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateUserSearch(CreateUserViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(nameof(CreateUser), model);
        }

        var search = await TrySearchStaffDirectoryAsync(model.SearchTerm, cancellationToken);
        if (search is not null)
        {
            ApplySearchResult(model, search);
        }

        return View(nameof(CreateUser), model);
    }

    // Only the candidate matching model.SelectedCandidate (a username) is ever created - matches legacy
    // Button_CreateUser_Click, which only acts on DropDownList_Users.SelectedValue. The posted username is
    // never trusted on its own: the directory is searched again (SearchTerm is round-tripped via a hidden
    // field) and the account details come from that fresh result, so a tampered form post cannot create
    // an arbitrary account - the same protection WebForms event validation gave legacy's dropdown.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateUserConfirm(CreateUserViewModel model, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(model.SelectedCandidate))
        {
            ModelState.AddModelError(nameof(model.SelectedCandidate), "Select a user");
        }

        if (string.IsNullOrWhiteSpace(model.SearchTerm))
        {
            return View(nameof(CreateUser), model);
        }

        var search = await TrySearchStaffDirectoryAsync(model.SearchTerm, cancellationToken);
        if (search is null)
        {
            return View(nameof(CreateUser), model);
        }

        if (ModelState.IsValid)
        {
            var created = await TryCreateSelectedUserAsync(model, search, cancellationToken);
            if (created)
            {
                return View(nameof(CreateUser), new CreateUserViewModel { IsCreated = true, Message = "User added successfully" });
            }
        }

        ApplySearchResult(model, search);
        return View(nameof(CreateUser), model);
    }

    // Creates the account for model.SelectedCandidate using the directory's own details for that username.
    // Returns false (with the reason in ModelState) when the user is not in the search results or the API
    // rejects the request.
    private async Task<bool> TryCreateSelectedUserAsync(CreateUserViewModel model, StaffDirectorySearchResponse search, CancellationToken cancellationToken)
    {
        var candidate = search.Users.FirstOrDefault(u => string.Equals(u.Username, model.SelectedCandidate, StringComparison.OrdinalIgnoreCase));
        if (candidate is null)
        {
            ModelState.AddModelError(nameof(model.SelectedCandidate), "The selected user is no longer available. Search again");
            return false;
        }

        var result = await userApiClient.CreateUserAsync(
            new CreateUserRequest(candidate.Username, candidate.Email, candidate.FriendlyName, candidate.FirstName, candidate.LastName, model.Department, GetCurrentUserId()),
            cancellationToken);

        foreach (var (field, messages) in result.FieldErrors)
        {
            // Only Department is a field on this form; the other validated values come from the
            // directory, so their errors are shown without a link to a field that is not there.
            var key = field == nameof(model.Department) ? field : string.Empty;
            foreach (var errorMessage in messages)
            {
                ModelState.AddModelError(key, errorMessage);
            }
        }

        return result.Success;
    }

    // The directory is searched from this app (Microsoft Graph, credentials held here), then anyone who already
    // has a PT-LIMS account is dropped - legacy GetADList's mUserList.ContainsByName check, and what makes a
    // search where every match already has an account collapse to an empty result set.
    // A directory failure shows a problem message at the top of the page (no field to link to) instead of
    // the generic error page; legacy showed "An Error has occured whilst Loading the User List".
    private async Task<StaffDirectorySearchResponse?> TrySearchStaffDirectoryAsync(string searchTerm, CancellationToken cancellationToken)
    {
        try
        {
            var candidates = await staffDirectoryService.SearchAsync(searchTerm, cancellationToken);
            var existingUsernames = (await userApiClient.GetUsersAsync(cancellationToken))
                .Select(u => u.Username)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            return new StaffDirectorySearchResponse(
                [.. candidates.Users
                    .Where(c => !existingUsernames.Contains(c.Username))
                    .Select(c => new StaffDirectoryUserResponse(c.Username, c.Email, c.FriendlyName, c.FirstName, c.LastName))],
                candidates.IsTruncated);
        }
        catch (Exception ex) when (ex is HttpRequestException or GraphException or ExecutionRejectedException
            || (ex is OperationCanceledException && !cancellationToken.IsCancellationRequested))
        {
            LogDirectorySearchFailedMessage(logger, ex);
            ModelState.AddModelError(string.Empty, "There was a problem searching the staff directory. Try again, and contact support if it keeps happening");
            return null;
        }
    }

    private static void ApplySearchResult(CreateUserViewModel model, StaffDirectorySearchResponse search)
    {
        model.HasSearched = true;
        model.ResultOptions = BuildResultOptions(search.Users);

        if (search.IsTruncated)
        {
            model.Message = "Only some of the matching users are shown. Enter more of the name or the employee number to narrow your search";
        }
        else if (model.ResultOptions.Count == 0)
        {
            model.Message = "No valid search results found";
        }
    }

    private static List<SelectListItem> BuildResultOptions(IReadOnlyList<StaffDirectoryUserResponse> results) =>
        results.Select(r => new SelectListItem($"{r.FriendlyName} : {r.Username}", r.Username)).ToList();

    public async Task<IActionResult> ManageUserRoles(CancellationToken cancellationToken) =>
        View(await BuildManageUserRolesViewModelAsync(cancellationToken));

    // Whole-grid submit: every user's role set shown on screen is (re)saved, not just the ones
    // actually changed - idempotent for unchanged rows, mirrors the AdministrationCharge screen's
    // save-the-whole-grid convention. Unlike legacy's UserRoles.aspx (every checkbox click is its
    // own immediate save), this only persists on an explicit Save - matches the story's "When I
    // click Save... Then... persisted" acceptance criteria. Redirects back to the System
    // Administration landing page (matches legacy ButtonSubmit_Click's Response.Redirect).
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ManageUserRoles(ManageUserRolesViewModel model, CancellationToken cancellationToken)
    {
        var actingUserId = GetCurrentUserId();
        var blockedMessages = new List<string>();

        foreach (var row in model.Rows)
        {
            var result = await userApiClient.SetUserRolesAsync(row.UserId, new SetUserRolesRequest(row.SelectedRoleIds, actingUserId), cancellationToken);
            if (!result.Success && result.Message is not null)
            {
                blockedMessages.Add($"{row.FriendlyName}: {result.Message}");
            }
        }

        if (blockedMessages.Count > 0)
        {
            TempData.SetNotification(NotificationType.Error, string.Join(" ", blockedMessages));
        }
        else
        {
            TempData.SetNotification(NotificationType.Success, "Role assignments updated successfully.");
        }

        return RedirectToAction(nameof(Index));
    }

    private async Task<ManageUserRolesViewModel> BuildManageUserRolesViewModelAsync(CancellationToken cancellationToken)
    {
        var roles = await roleApiClient.GetRolesAsync(cancellationToken);
        var rows = await userApiClient.GetUserRoleGridAsync(cancellationToken);

        return new ManageUserRolesViewModel
        {
            // spgaRole returns the roles by fldOrder DESC. Legacy UserRoles.aspx inserted each one at column 2 as it
            // looped, so its columns ran the other way (fldOrder ascending): do the same here.
            Roles = roles.Reverse().Select(r => new RoleColumnViewModel { RoleId = r.RoleId, Name = r.Name }).ToList(),
            Rows = rows.Select(r => new UserRoleRowViewModel
            {
                UserId = r.UserId,
                Username = r.Username,
                FriendlyName = r.FriendlyName,
                SelectedRoleIds = r.RoleIds.ToList()
            }).ToList()
        };
    }

    public async Task<IActionResult> RemoveUser(CancellationToken cancellationToken) =>
        View(await BuildRemoveUserViewModelAsync(message: null, messageIsError: false, cancellationToken));

    // Matches legacy Button_DeleteUser_Click - revokes every role then removes the user record.
    // Blocked (Success = false) if the selection is the current user, same reasoning as the
    // self-Admin-removal guard in ManageUserRoles.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveUser(RemoveUserViewModel model, CancellationToken cancellationToken)
    {
        if (model.SelectedUserId is not Guid userId)
        {
            return View(await BuildRemoveUserViewModelAsync("Select a user to remove.", messageIsError: true, cancellationToken));
        }

        var result = await userApiClient.RemoveUserAsync(userId, GetCurrentUserId(), cancellationToken);
        if (result.Success)
        {
            return View(new RemoveUserViewModel { IsRemoved = true, Message = "User Removed Successfully" });
        }

        return View(await BuildRemoveUserViewModelAsync(result.Message, messageIsError: true, cancellationToken));
    }

    // Reads the InternalUserId claim InternalUserResolver added at sign-in - null if absent
    // (e.g. no HttpContext in a unit test), which the API layer treats as "unknown caller".
    private Guid? GetCurrentUserId() =>
        Guid.TryParse(HttpContext?.User?.FindFirst(InternalUserClaimTypes.InternalUserId)?.Value, out var id) ? id : null;

    private async Task<RemoveUserViewModel> BuildRemoveUserViewModelAsync(string? message, bool messageIsError, CancellationToken cancellationToken)
    {
        var users = await userApiClient.GetUsersAsync(cancellationToken);
        var options = new List<SelectListItem> { new("Select an existing user...", string.Empty) };
        options.AddRange(users
            .OrderBy(u => u.FriendlyName, StringComparer.OrdinalIgnoreCase)
            .Select(u => new SelectListItem($"{u.Username}: {u.FriendlyName}", u.UserId.ToString())));

        return new RemoveUserViewModel
        {
            UserOptions = options,
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
}
