using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using PTL.ApiClient.Distribution;
using PTL.Contracts.Distribution;

namespace PTL.InternalWeb.Features.Distribution;

// Distribution Dashboard and Monthly Distributions (Scheduling) - legacy
// Distributions/MenuDistributions.aspx and Scheduling/monthlys.aspx - see
// docs/analysis/distribution-analysis.md and docs/migration/distribution-migration.md.
//
// SCOPE: the Dashboard (year selection, month rows, progress indicators, "Initialise this Month"
// visibility/confirmation) and the Monthly Distributions scheduling screen (dates, Cancel/
// Commence, Save/Apply/Cancel) are implemented. The Preparation/Packaging/Results/Tabulations
// "View" links, the Sample Numbers/Intended Results/Participants links reached FROM Scheduling,
// and "Initialise this Month" itself all remain placeholders (ViewStage/InitialiseMonth below) -
// those workflows (and the exact legacy persisted-save chain behind Initialise) are out of scope
// and flagged [NEEDS INVESTIGATION] in docs/migration/distribution-migration.md.
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

    // Placeholder for every "View" link not yet migrated (Preparation/Packaging/Results/
    // Tabulations, plus the Sample Numbers/Intended Results/Participants links on the Scheduling
    // screen below, which all open workflows out of scope for this story) - see class remarks.
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

    // Monthly Distributions (Scheduling) - legacy Scheduling/monthlys.aspx. Reached from the
    // Dashboard's "Scheduling: View" link once a month is initialised.
    [HttpGet]
    public async Task<IActionResult> Schedule(int year, int monthId, CancellationToken cancellationToken)
    {
        var schedule = await distributionApiClient.GetScheduleAsync(year, monthId, cancellationToken);
        if (schedule is null || schedule.MonthlyDistributionId is null)
        {
            return NotFound();
        }

        return View(ToScheduleViewModel(year, monthId, schedule));
    }

    // Save = persist then leave (legacy ButtonSave_Click -> Save() -> LeavePage()).
    // Apply = persist then stay, re-reading fresh data from the database (legacy
    // ButtonApply_Click -> Save() -> LoadMonth(True)).
    // Cancel = discard then leave, no save at all (legacy ButtonCancel_Click -> LeavePage()).
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Schedule(int year, int monthId, MonthlyDistributionScheduleViewModel model, string command, CancellationToken cancellationToken)
    {
        if (string.Equals(command, "cancel", StringComparison.OrdinalIgnoreCase))
        {
            return RedirectToAction(nameof(Index));
        }

        if (!ModelState.IsValid)
        {
            model.YearId = year;
            model.MonthId = monthId;
            return View(model);
        }

        var rows = model.Schemes.Select(ToRowRequest).ToList();
        var result = await distributionApiClient.SaveScheduleAsync(year, monthId, rows, cancellationToken);

        if (result is null || !result.Success)
        {
            foreach (var (schemeId, messages) in result?.FieldErrorsBySchemeId ?? new Dictionary<Guid, string[]>())
            {
                var index = model.Schemes.FindIndex(s => s.MonthlyDistributionSchemeId == schemeId);
                if (index < 0)
                {
                    continue;
                }

                foreach (var message in messages)
                {
                    ModelState.AddModelError($"{nameof(model.Schemes)}[{index}]", message);
                }
            }

            model.YearId = year;
            model.MonthId = monthId;
            return View(model);
        }

        if (string.Equals(command, "apply", StringComparison.OrdinalIgnoreCase))
        {
            return RedirectToAction(nameof(Schedule), new { year, monthId });
        }

        return RedirectToAction(nameof(Index));
    }

    // Preserves the legacy Cancel/Commence toggle exactly: dlMonth_RowCommand only flips the
    // in-memory IsCancelled flag and redisplays the page from ViewState - it does NOT save to the
    // database and does NOT discard any other unsaved date edits already typed into the form. The
    // posted model already carries every other row's current (possibly unsaved) values, so
    // re-rendering it after flipping just the one row reproduces that behaviour without any
    // server-side state beyond this one request.
    // S6967 (ModelState.IsValid not checked) suppressed deliberately: legacy dlMonth_RowCommand
    // runs with CausesValidation=False, so a half-filled form can still be toggled and redisplayed.
    [HttpPost]
    [ValidateAntiForgeryToken]
#pragma warning disable S6967
    public IActionResult ToggleCancelled(int year, int monthId, Guid schemeId, MonthlyDistributionScheduleViewModel model)
#pragma warning restore S6967
    {
        ModelState.Clear();
        var row = model.Schemes.FirstOrDefault(s => s.MonthlyDistributionSchemeId == schemeId);
        if (row is not null)
        {
            row.IsCancelled = !row.IsCancelled;
        }

        model.YearId = year;
        model.MonthId = monthId;
        return View(nameof(Schedule), model);
    }

    private static MonthlyDistributionScheduleRowRequest ToRowRequest(MonthlyDistributionScheduleRowViewModel row) =>
        new(row.MonthlyDistributionSchemeId, row.DistributionDate!.Value, row.OverseasPostingDate!.Value, row.DeadlineDate!.Value, row.ResultsIssueTargetDate!.Value, row.IsCancelled);

    private static MonthlyDistributionScheduleViewModel ToScheduleViewModel(int year, int monthId, MonthlyDistributionResponse schedule) =>
        new()
        {
            YearId = year,
            MonthId = monthId,
            MonthYearLabel = new DateTime(year, monthId, 1, 0, 0, 0, DateTimeKind.Utc).ToString("MMM yyyy", System.Globalization.CultureInfo.InvariantCulture),
            Schemes = [.. schedule.Schemes.Select(ToRowViewModel)],
        };

    private static MonthlyDistributionScheduleRowViewModel ToRowViewModel(MonthlyDistributionSchemeResponse scheme) =>
        new()
        {
            MonthlyDistributionSchemeId = scheme.MonthlyDistributionSchemeId,
            SchemeIdentifier = scheme.SchemeIdentifier,
            SchemeName = scheme.SchemeName,
            DistributionReferenceFull = scheme.DistributionReferenceFull,
            DistributionDate = scheme.DistributionDate,
            OverseasPostingDate = scheme.OverseasPostingDate,
            DeadlineDate = scheme.DeadlineDate,
            ResultsIssueTargetDate = scheme.ResultsIssueTargetDate,
            IsCancelled = scheme.IsCancelled,
            IsAsAvailable = scheme.IsAsAvailable,
            ParticipantCount = scheme.ParticipantCount,
            TotalSetsOfSamplesRequired = scheme.TotalSetsOfSamplesRequired,
            HasSampleNumbersDefined = scheme.HasSampleNumbersDefined,
            HasIntendedResults = scheme.HasIntendedResults,
        };

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
