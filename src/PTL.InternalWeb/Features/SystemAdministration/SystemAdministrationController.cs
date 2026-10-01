using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using PTL.ApiClient;
using PTL.Contracts.AdministrationCharge;
using PTL.Contracts.Lookup;
using PTL.Contracts.PostagePricingPlan;

namespace PTL.InternalWeb.Features.SystemAdministration;

// Authentication/authorization are out of scope for this phase - assume the current user is
// already authenticated with full access, same as every other InternalWeb controller.
public class SystemAdministrationController(
    IAdministrationChargeApiClient administrationChargeApiClient,
    IWeightedPricingPlanApiClient weightedPricingPlanApiClient,
    IPostagePricingPlanApiClient postagePricingPlanApiClient,
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
