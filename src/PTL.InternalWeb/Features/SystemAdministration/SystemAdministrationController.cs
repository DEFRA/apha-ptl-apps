using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using PTL.ApiClient;
using PTL.Contracts.AdministrationCharge;
using PTL.Contracts.Country;
using PTL.Contracts.ExternalSiteMessage;
using PTL.Contracts.Lookup;
using PTL.Contracts.PostagePricingPlan;
using PTL.Contracts.TestConsultant;
using PTL.Contracts.User;
using PTL.Contracts.Viewer;
using PTL.InternalWeb.Features.Account;

namespace PTL.InternalWeb.Features.SystemAdministration;

// Gated behind the "Admin" role (resolved from our own database, not an Entra ID group - see
// SystemAdministrationPolicy remarks). PTL.Api itself remains unauthenticated for this phase
// (tracked separately) - this is the one enforcement point today.
[Authorize(Policy = SystemAdministrationPolicy.Name)]
public class SystemAdministrationController : Controller
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
    private readonly ILogger<SystemAdministrationController> logger;

    // Takes the bundled SystemAdministrationApiClients rather than 8 separate parameters, to stay
    // under the analyzer's constructor-parameter-count threshold - see that type's remarks.
    public SystemAdministrationController(SystemAdministrationApiClients apiClients, ILogger<SystemAdministrationController> logger)
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
                new UpdatePostagePricingPlanPriceRequest(model.PostageId!.Value, model.UKPrice, model.EUPrice, model.NonEUPrice),
                cancellationToken);

            if (result.Success)
            {
                return RedirectToAction(nameof(PostagePricingPlan), new { yearId = model.YearId!.Value });
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
            var result = await countryApiClient.UpdateCountryAsync(model.CountryId!.Value, new CountrySaveRequest(model.Country, model.CountryTypeId!.Value), cancellationToken);
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

    public async Task<IActionResult> InternalTestConsultantDepartment(Guid? editId, CancellationToken cancellationToken) =>
        View(await BuildInternalTestConsultantDepartmentViewModelAsync(editId, message: null, messageIsError: false, editValues: null, cancellationToken));

    // Only the row matching model.UserId is ever in edit mode when this posts - matches
    // CountryManagementSave. IsInactive/InactiveDate are round-tripped unchanged (see
    // InternalTestConsultantEditViewModel remarks) since spuUserDept updates all three together.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> InternalTestConsultantDepartmentSave([Bind(Prefix = "Edit")] InternalTestConsultantEditViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(nameof(InternalTestConsultantDepartment), await BuildInternalTestConsultantDepartmentViewModelAsync(model.UserId, message: null, messageIsError: false, model, cancellationToken));
        }

        await userApiClient.UpdateTestConsultantAsync(
            model.UserId!.Value,
            new UpdateTestConsultantRequest(model.Department, model.IsInactive!.Value, model.InactiveDate),
            cancellationToken);

        return RedirectToAction(nameof(InternalTestConsultantDepartment));
    }

    // Toggles the current Status (Active/Inactive). Department is round-tripped unchanged for the
    // same reason as the Save action above. Deactivating stamps InactiveDate with now; activating
    // clears it - matches the story's "Inactive Date is populated" acceptance criterion. The
    // activation confirmation ("Are you sure?") is a client-side confirm() on the view's button,
    // matching the story's wording exactly - no separate server-side confirmation step exists.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> InternalTestConsultantDepartmentToggleStatus(Guid userId, string department, bool isInactive, CancellationToken cancellationToken)
    {
        var newIsInactive = !isInactive;
        var inactiveDate = newIsInactive ? DateTime.UtcNow : (DateTime?)null;

        await userApiClient.UpdateTestConsultantAsync(
            userId,
            new UpdateTestConsultantRequest(department, newIsInactive, inactiveDate),
            cancellationToken);

        return RedirectToAction(nameof(InternalTestConsultantDepartment));
    }

    private async Task<InternalTestConsultantDepartmentViewModel> BuildInternalTestConsultantDepartmentViewModelAsync(
        Guid? editId,
        string? message,
        bool messageIsError,
        InternalTestConsultantEditViewModel? editValues,
        CancellationToken cancellationToken)
    {
        var consultants = await userApiClient.GetTestConsultantsAsync(cancellationToken);

        var rows = consultants
            .OrderBy(c => c.FriendlyName, StringComparer.OrdinalIgnoreCase)
            .Select(c => new InternalTestConsultantRowViewModel
            {
                UserId = c.UserId,
                Username = c.Username,
                FriendlyName = c.FriendlyName,
                Department = editId == c.UserId && editValues is not null ? editValues.Department : c.Department,
                IsInactive = c.IsInactive,
                InactiveDate = c.InactiveDate,
                IsEditing = editId == c.UserId
            })
            .ToList();

        return new InternalTestConsultantDepartmentViewModel
        {
            Rows = rows,
            Message = message,
            MessageIsError = messageIsError
        };
    }

    public async Task<IActionResult> ExternalTestConsultantManagement(Guid? editId, CancellationToken cancellationToken) =>
        View(await BuildExternalTestConsultantManagementViewModelAsync(editId, message: null, messageIsError: false, addValues: null, editValues: null, cancellationToken));

    // Matches legacy ButtonAdd_Click - Name and Email are required (Department is optional), per
    // the story's Business Rule.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ExternalTestConsultantManagementAdd([Bind(Prefix = "Add")] ExternalTestConsultantAddViewModel model, CancellationToken cancellationToken)
    {
        if (ModelState.IsValid)
        {
            var result = await externalTestConsultantApiClient.CreateAsync(new ExternalTestConsultantSaveRequest(model.Name, model.Department, model.Email), cancellationToken);
            if (result.Success)
            {
                return RedirectToAction(nameof(ExternalTestConsultantManagement));
            }

            foreach (var (field, messages) in result.FieldErrors)
            {
                foreach (var errorMessage in messages)
                {
                    ModelState.AddModelError($"Add.{field}", errorMessage);
                }
            }
        }

        return View(nameof(ExternalTestConsultantManagement), await BuildExternalTestConsultantManagementViewModelAsync(editId: null, message: null, messageIsError: false, model, editValues: null, cancellationToken));
    }

    // Only the row matching model.ExternalTestConsultantId is ever in edit mode when this posts -
    // matches CountryManagementSave/legacy GridViewTC_Updating.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ExternalTestConsultantManagementSave([Bind(Prefix = "Edit")] ExternalTestConsultantEditViewModel model, CancellationToken cancellationToken)
    {
        if (ModelState.IsValid)
        {
            var result = await externalTestConsultantApiClient.UpdateAsync(model.ExternalTestConsultantId!.Value, new ExternalTestConsultantSaveRequest(model.Name, model.Department, model.Email), cancellationToken);
            if (result.Success)
            {
                return RedirectToAction(nameof(ExternalTestConsultantManagement));
            }

            foreach (var (field, messages) in result.FieldErrors)
            {
                foreach (var errorMessage in messages)
                {
                    ModelState.AddModelError($"Edit.{field}", errorMessage);
                }
            }
        }

        return View(nameof(ExternalTestConsultantManagement), await BuildExternalTestConsultantManagementViewModelAsync(model.ExternalTestConsultantId, message: null, messageIsError: false, addValues: null, model, cancellationToken));
    }

    // Toggles Active/Inactive - the API stamps/clears InactiveDate accordingly, matching the
    // story's "Inactive Date is populated" acceptance criterion.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ExternalTestConsultantManagementToggleStatus(Guid externalTestConsultantId, bool isInactive, CancellationToken cancellationToken)
    {
        await externalTestConsultantApiClient.SetStatusAsync(externalTestConsultantId, !isInactive, cancellationToken);
        return RedirectToAction(nameof(ExternalTestConsultantManagement));
    }

    // [NEEDS INVESTIGATION] Generate Login is a stub - see IExternalLoginService remarks. The
    // confirmation ("Generating a login... Are you sure you wish to proceed?") is a client-side
    // confirm() on the view's link, matching the story's wording exactly - Cancel simply never
    // submits the form, so no server-side action runs.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ExternalTestConsultantManagementGenerateLogin(Guid externalTestConsultantId, CancellationToken cancellationToken)
    {
        var result = await externalTestConsultantApiClient.GenerateLoginAsync(externalTestConsultantId, cancellationToken);
        return View(nameof(ExternalTestConsultantManagement), await BuildExternalTestConsultantManagementViewModelAsync(
            editId: null, result.Message ?? (result.Success ? "Login generated successfully." : null), messageIsError: !result.Success, addValues: null, editValues: null, cancellationToken));
    }

    private async Task<ExternalTestConsultantManagementViewModel> BuildExternalTestConsultantManagementViewModelAsync(
        Guid? editId,
        string? message,
        bool messageIsError,
        ExternalTestConsultantAddViewModel? addValues,
        ExternalTestConsultantEditViewModel? editValues,
        CancellationToken cancellationToken)
    {
        var consultants = await externalTestConsultantApiClient.GetAllAsync(cancellationToken);

        var rows = consultants
            .OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .Select(c => new ExternalTestConsultantRowViewModel
            {
                ExternalTestConsultantId = c.ExternalTestConsultantId,
                Name = editId == c.ExternalTestConsultantId && editValues is not null ? editValues.Name : c.Name,
                Department = editId == c.ExternalTestConsultantId && editValues is not null ? editValues.Department : c.Department,
                Email = editId == c.ExternalTestConsultantId && editValues is not null ? editValues.Email : c.Email,
                IsInactive = c.IsInactive,
                InactiveDate = c.InactiveDate,
                HasLogin = c.HasLogin,
                IsEditing = editId == c.ExternalTestConsultantId
            })
            .ToList();

        return new ExternalTestConsultantManagementViewModel
        {
            Rows = rows,
            Add = addValues ?? new ExternalTestConsultantAddViewModel(),
            Message = message,
            MessageIsError = messageIsError
        };
    }

    public async Task<IActionResult> ViewerManagement(Guid? editId, CancellationToken cancellationToken) =>
        View(await BuildViewerManagementViewModelAsync(editId, message: null, messageIsError: false, addValues: null, editValues: null, cancellationToken));

    // Matches legacy ButtonAdd_Click - Name and Email are both required per the story's Business Rule.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ViewerManagementAdd([Bind(Prefix = "Add")] ViewerAddViewModel model, CancellationToken cancellationToken)
    {
        if (ModelState.IsValid)
        {
            var result = await viewerApiClient.CreateAsync(new ViewerSaveRequest(model.Name, model.Email), cancellationToken);
            if (result.Success)
            {
                return RedirectToAction(nameof(ViewerManagement));
            }

            foreach (var (field, messages) in result.FieldErrors)
            {
                foreach (var errorMessage in messages)
                {
                    ModelState.AddModelError($"Add.{field}", errorMessage);
                }
            }
        }

        return View(nameof(ViewerManagement), await BuildViewerManagementViewModelAsync(editId: null, message: null, messageIsError: false, model, editValues: null, cancellationToken));
    }

    // Only the row matching model.ViewerId is ever in edit mode when this posts - matches
    // CountryManagementSave/legacy GridViewViewer_Updating.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ViewerManagementSave([Bind(Prefix = "Edit")] ViewerEditViewModel model, CancellationToken cancellationToken)
    {
        if (ModelState.IsValid)
        {
            var result = await viewerApiClient.UpdateAsync(model.ViewerId!.Value, new ViewerSaveRequest(model.Name, model.Email), cancellationToken);
            if (result.Success)
            {
                return RedirectToAction(nameof(ViewerManagement));
            }

            foreach (var (field, messages) in result.FieldErrors)
            {
                foreach (var errorMessage in messages)
                {
                    ModelState.AddModelError($"Edit.{field}", errorMessage);
                }
            }
        }

        return View(nameof(ViewerManagement), await BuildViewerManagementViewModelAsync(model.ViewerId, message: null, messageIsError: false, addValues: null, model, cancellationToken));
    }

    // Removal is never blocked server-side (spdViewer is unconditional, matching legacy) - the
    // confirmation listing assigned schemes/participants is purely a client-side courtesy
    // (ViewerRowViewModel.RemoveConfirmMessage), matching legacy ManageViewers.aspx.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ViewerManagementRemove(Guid viewerId, CancellationToken cancellationToken)
    {
        var result = await viewerApiClient.DeleteAsync(viewerId, cancellationToken);
        return View(nameof(ViewerManagement), await BuildViewerManagementViewModelAsync(
            editId: null, result.Success ? "Viewer removed successfully." : result.Message, messageIsError: !result.Success, addValues: null, editValues: null, cancellationToken));
    }

    // [NEEDS INVESTIGATION] Generate Login is a stub - see IExternalLoginService remarks. The
    // confirmation ("Generating a login... Are you sure you wish to proceed?") is a client-side
    // confirm() on the view's link, matching the story's wording exactly - Cancel simply never
    // submits the form, so no server-side action runs.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ViewerManagementGenerateLogin(Guid viewerId, CancellationToken cancellationToken)
    {
        var result = await viewerApiClient.GenerateLoginAsync(viewerId, cancellationToken);
        return View(nameof(ViewerManagement), await BuildViewerManagementViewModelAsync(
            editId: null, result.Message ?? (result.Success ? "Login generated successfully." : null), messageIsError: !result.Success, addValues: null, editValues: null, cancellationToken));
    }

    private async Task<ViewerManagementViewModel> BuildViewerManagementViewModelAsync(
        Guid? editId,
        string? message,
        bool messageIsError,
        ViewerAddViewModel? addValues,
        ViewerEditViewModel? editValues,
        CancellationToken cancellationToken)
    {
        var viewers = await viewerApiClient.GetAllAsync(cancellationToken);

        var rows = viewers
            .OrderBy(v => v.Name, StringComparer.OrdinalIgnoreCase)
            .Select(v => new ViewerRowViewModel
            {
                ViewerId = v.ViewerId,
                Name = editId == v.ViewerId && editValues is not null ? editValues.Name : v.Name,
                Email = editId == v.ViewerId && editValues is not null ? editValues.Email : v.Email,
                HasLogin = v.HasLogin,
                IsEditing = editId == v.ViewerId,
                RemoveConfirmMessage = BuildRemoveConfirmMessage(v.Schemes, v.Participants)
            })
            .ToList();

        return new ViewerManagementViewModel
        {
            Rows = rows,
            Add = addValues ?? new ViewerAddViewModel(),
            Message = message,
            MessageIsError = messageIsError
        };
    }

    // Matches legacy GridViewTC_RowDataBound's warning text (Schemes then Participants, each as
    // "Identifier/LabCode: Name/LabName"), always ending with a plain "are you sure" question so
    // every viewer gets a confirmation - unlike legacy, which showed no confirmation at all when
    // nothing was assigned; this codebase's other Remove actions (e.g. Remove User) always
    // confirm, so this keeps that consistent even for a viewer with no assignments.
    private static string BuildRemoveConfirmMessage(IReadOnlyList<ViewerSchemeResponse> schemes, IReadOnlyList<ViewerParticipantResponse> participants)
    {
        var lines = new List<string>();

        if (schemes.Count > 0)
        {
            lines.Add("This viewer is assigned to the following schemes:");
            lines.AddRange(schemes.Select(s => $"{s.Identifier}: {s.Name}"));
            lines.Add(string.Empty);
        }

        if (participants.Count > 0)
        {
            lines.Add("This viewer is assigned to the following participants:");
            lines.AddRange(participants.Select(p => $"{p.LabCode}: {p.LabName}"));
            lines.Add(string.Empty);
        }

        lines.Add("Are you sure you want to remove this viewer?");

        // JS-escape each line's raw content (not HTML-escape) before joining with the literal
        // "\n" line-break sequence, since this is embedded inside a single-quoted JS string
        // literal in an onclick attribute - HTML-encoding alone would not stop an apostrophe in a
        // scheme/participant name from terminating the string early, and escaping after joining
        // would double-escape the "\n" separators themselves.
        return string.Join("\\n", lines.Select(EscapeForJavaScriptString));
    }

    private static string EscapeForJavaScriptString(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("'", "\\'", StringComparison.Ordinal);

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
