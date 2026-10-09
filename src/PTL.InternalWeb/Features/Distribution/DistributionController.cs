using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using PTL.ApiClient.Distribution;
using PTL.Contracts.Distribution;

namespace PTL.InternalWeb.Features.Distribution;

// Distribution Dashboard (legacy Distributions/MenuDistributions.aspx) - see
// docs/analysis/distribution-analysis.md and docs/migration/distribution-migration.md.
//
// SCOPE: only the dashboard itself (year selection, month rows, progress indicators, the
// "Initialise this Month" action's visibility/confirmation, and navigation) is implemented. The
// five "View" links (Scheduling/Preparation/Packaging/Results/Tabulations) and the Initialise
// action itself are placeholders - see ViewStage/InitialiseMonth below - because those workflows
// (and the exact legacy persisted-save chain behind "Initialise this Month") are out of scope for
// this story and are flagged [NEEDS INVESTIGATION] in docs/migration/distribution-migration.md.
public sealed class DistributionController(IDistributionApiClient distributionApiClient) : Controller
{
    private static readonly string[] MonthOrder = ["Scheduling", "Preparation", "Packaging", "Results", "Tabulations"];

    [HttpGet]
    public async Task<IActionResult> Index(int? year, CancellationToken cancellationToken)
    {
        var yearOptions = await distributionApiClient.GetYearsAsync(cancellationToken);

        // Legacy default (MenuDistributions.aspx.vb cboYear_DataBound, no prior session state):
        // the financial year runs April -> March, so before April the "current" year is last year.
        var now = DateTime.UtcNow;
        var defaultYearId = now.Month < 4 ? now.Year - 1 : now.Year;
        var selectedYearId = year ?? defaultYearId;

        var months = await distributionApiClient.GetDashboardAsync(selectedYearId, cancellationToken);

        var model = new DistributionDashboardViewModel
        {
            SelectedYearId = selectedYearId,
            YearOptions = BuildYearOptions(yearOptions, selectedYearId),
            Months = [.. months.Select(ToRow)],
        };

        return View(model);
    }

    // Placeholder for every "View" link (Scheduling/Preparation/Packaging/Results/Tabulations) -
    // these workflows are not yet migrated; see class remarks.
    [HttpGet]
    public IActionResult ViewStage(string stage)
    {
        var title = MonthOrder.Contains(stage, StringComparer.OrdinalIgnoreCase) ? stage : "Distribution";
        return View("FeatureNotAvailable", title);
    }

    // Placeholder for "Initialise this Month" - the confirmation text is preserved and the button
    // is shown exactly where legacy shows it, but the write path (sppSetupMonthlyDistribution's
    // full persisted-save chain) is unconfirmed - see docs/migration/distribution-migration.md's
    // "Initialise Month" NEEDS INVESTIGATION items. Do not wire this to a real write until that is
    // resolved.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult InitialiseMonth(int year, int monthId)
    {
        return View("FeatureNotAvailable", "Initialise This Month");
    }

    private static DistributionDashboardMonthRowViewModel ToRow(DistributionDashboardMonthResponse month) =>
        new(
            month.YearId,
            month.MonthId,
            month.MonthDescription,
            IsInitialised: month.MonthlyDistributionId is not null,
            month.IsInitialisable,
            (int)Math.Round(month.SchedulingCompletePercentage),
            (int)Math.Round(month.PreparationCompletePercentage),
            (int)Math.Round(month.PackagingCompletePercentage),
            (int)Math.Round(month.ResultsCompletePercentage),
            (int)Math.Round(month.TabulationsCompletePercentage));

    private static List<SelectListItem> BuildYearOptions(IReadOnlyList<DistributionYearOptionResponse> yearOptions, int selectedYearId) =>
        [.. yearOptions.Select(y => new SelectListItem(y.Label, y.YearId.ToString(System.Globalization.CultureInfo.InvariantCulture), y.YearId == selectedYearId))];
}
