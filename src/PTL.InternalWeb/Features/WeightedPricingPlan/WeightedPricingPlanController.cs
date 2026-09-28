using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using PTL.ApiClient;

namespace PTL.InternalWeb.Features.WeightedPricingPlan;

// Authentication/authorization are out of scope for this phase - assume the current user is
// already authenticated with full access, same as every other InternalWeb controller.
public class WeightedPricingPlanController(IWeightedPricingPlanApiClient weightedPricingPlanApiClient, ILookupApiClient lookupApiClient, ILogger<WeightedPricingPlanController> logger) : Controller
{
    private static readonly Action<ILogger, Exception?> LogRenewBlockedMessage =
        LoggerMessage.Define(
            LogLevel.Information,
            new EventId(1, nameof(LogRenewBlockedMessage)),
            "Weighted pricing plan renewal was blocked: no percentages configured for the current financial year");

    private static readonly Action<ILogger, int, Exception?> LogRenewedMessage =
        LoggerMessage.Define<int>(
            LogLevel.Information,
            new EventId(2, nameof(LogRenewedMessage)),
            "Renewed weighted pricing plan for financial year {YearId}");

    public async Task<IActionResult> Index(int? yearId, CancellationToken cancellationToken) =>
        View(await BuildViewModelAsync(yearId, message: null, messageIsError: false, cancellationToken));

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

        return View("Index", await BuildViewModelAsync(yearId, result.Message, messageIsError: !result.Success, cancellationToken));
    }

    private async Task<WeightedPricingPlanViewModel> BuildViewModelAsync(int? yearId, string? message, bool messageIsError, CancellationToken cancellationToken)
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

        var selectedYearId = yearId is not null && years.AvailableYears.Any(y => y.YearId == yearId)
            ? yearId.Value
            : currentYearId is not null && years.AvailableYears.Any(y => y.YearId == currentYearId)
                ? currentYearId.Value
                : years.AvailableYears[0].YearId;

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
}
