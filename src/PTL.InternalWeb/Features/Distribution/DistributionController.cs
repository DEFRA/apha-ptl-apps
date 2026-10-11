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
// visibility/confirmation), the Monthly Distributions scheduling screen (dates, Cancel/
// Commence, Save/Apply/Cancel) and the Sample Numbers screen are implemented. The Preparation/
// Packaging/Results/Tabulations "View" links, the Intended Results/Participants links reached FROM
// Scheduling, and "Initialise this Month" itself all remain placeholders (ViewStage/
// InitialiseMonth below) - those workflows (and the exact legacy persisted-save chain behind
// Initialise) are out of scope and flagged [NEEDS INVESTIGATION] in
// docs/migration/distribution-migration.md.
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
    // Tabulations, plus the Intended Results/Participants links on the Scheduling screen below,
    // which all open workflows out of scope for this story) - see class remarks.
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

    // Sample Numbers - legacy Scheduling/SampleNumbers.aspx. Opened from the Scheduling screen's
    // "Define Sample Numbers"/"View Sample Numbers" link; year/monthId are carried through purely
    // so Save and Cancel can return to the Scheduling screen the user came from (legacy redirects
    // to ~/Scheduling/monthlys.aspx, which restores the month from session).
    [HttpGet]
    public async Task<IActionResult> SampleNumbers(int year, int monthId, Guid schemeId, CancellationToken cancellationToken)
    {
        var result = await distributionApiClient.GetSampleNumbersAsync(schemeId, cancellationToken);

        // The distribution structure could not be built from the Scheme (legacy would have thrown
        // on the bare guidDictionary lookup) - show the business reason instead of a 500.
        if (result.Errors.Count > 0)
        {
            return View("SampleNumbersUnavailable", new SampleNumbersUnavailableViewModel
            {
                YearId = year,
                MonthId = monthId,
                Errors = result.Errors,
            });
        }

        if (result.Page is null)
        {
            return NotFound();
        }

        return View(ToSampleNumbersViewModel(year, monthId, result.Page));
    }

    // Save = persist then return to Monthly Distributions (legacy ButtonSave_Click).
    // Save and go to Intended Results = persist then open Intended Results for this distribution
    // (legacy ButtonGoToIR_Click).
    // Cancel = discard, no save at all (legacy ButtonCancel_Click, CausesValidation=False).
    // cancelAudit = Cancel on the audit-reason step, which legacy Audit.aspx handles by returning
    // to the destination the user originally chose WITHOUT saving.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SampleNumbers(int year, int monthId, Guid schemeId, SampleNumbersViewModel model, string command, CancellationToken cancellationToken)
    {
        if (string.Equals(command, "cancel", StringComparison.OrdinalIgnoreCase))
        {
            return RedirectToAction(nameof(Schedule), new { year, monthId });
        }

        if (string.Equals(command, "cancelAudit", StringComparison.OrdinalIgnoreCase))
        {
            return RedirectToDestination(model.PendingCommand, year, monthId);
        }

        model.YearId = year;
        model.MonthId = monthId;
        model.MonthlyDistributionSchemeId = schemeId;

        if (!ModelState.IsValid)
        {
            return View(await RehydrateSampleNumbersAsync(year, monthId, schemeId, model, cancellationToken));
        }

        var request = new SampleNumbersSaveRequest(
            model.Comments ?? string.Empty,
            [.. model.Samples.Select(s => new SampleNumberRowRequest(
                s.SampleId,
                s.SampleNumber,
                s.InternalDescription,
                s.ExternalDescription ?? string.Empty,
                s.TabulationDescription ?? string.Empty,
                s.TestConsultantsDescription ?? string.Empty,
                [.. s.Allocations.Select(a => new SampleAllocationRequest(a.SampleTestId, a.IsApplicable))]))],
            model.AuditWhy);

        var result = await distributionApiClient.SaveSampleNumbersAsync(schemeId, request, cancellationToken);

        // Legacy IsAuditDirty -> Audit.aspx: the edit is valid, but a Tabulation Description
        // changed, so a mandatory reason is captured before the save is allowed to run.
        if (result is not null && result.AuditReasonRequired)
        {
            var auditModel = await RehydrateSampleNumbersAsync(year, monthId, schemeId, model, cancellationToken);
            auditModel.RequiresAuditReason = true;
            auditModel.AuditObjectName = result.AuditObjectName;
            auditModel.PendingCommand = command;
            auditModel.AuditWhy = model.AuditWhy;
            return View(auditModel);
        }

        if (result is null || !result.Success)
        {
            foreach (var message in result?.PageErrors ?? [])
            {
                ModelState.AddModelError(nameof(model.Comments), message);
            }

            foreach (var (sampleId, errors) in result?.FieldErrorsBySampleId ?? new Dictionary<Guid, SampleNumbersFieldError[]>())
            {
                var index = model.Samples.FindIndex(s => s.SampleId == sampleId);
                if (index < 0)
                {
                    continue;
                }

                foreach (var error in errors)
                {
                    ModelState.AddModelError($"{nameof(model.Samples)}[{index}].{error.Field}", error.Message);
                }
            }

            return View(await RehydrateSampleNumbersAsync(year, monthId, schemeId, model, cancellationToken));
        }

        return RedirectToDestination(command, year, monthId);
    }

    private RedirectToActionResult RedirectToDestination(string? command, int year, int monthId) =>
        string.Equals(command, "saveAndGoToIntendedResults", StringComparison.OrdinalIgnoreCase)
            ? RedirectToAction(nameof(ViewStage), new { stage = "Intended Results" })
            : RedirectToAction(nameof(Schedule), new { year, monthId });

    // Legacy keeps the whole IntendedResultsSet in Session, so a failed Save redisplays the page
    // with the server's heading/sample-count/test tabs intact and only the user's typed values
    // overlaid. There is no server-side session here, so re-read the distribution and overlay the
    // posted editable fields onto it - the display-only data is never trusted from the form.
    private async Task<SampleNumbersViewModel> RehydrateSampleNumbersAsync(
        int year, int monthId, Guid schemeId, SampleNumbersViewModel posted, CancellationToken cancellationToken)
    {
        var page = await distributionApiClient.GetSampleNumbersAsync(schemeId, cancellationToken);
        if (page.Page is null)
        {
            return posted;
        }

        var model = ToSampleNumbersViewModel(year, monthId, page.Page);
        model.Comments = posted.Comments;
        model.RequiresAuditReason = posted.RequiresAuditReason;
        model.AuditObjectName = posted.AuditObjectName;
        model.PendingCommand = posted.PendingCommand;
        model.AuditWhy = posted.AuditWhy;

        var postedSamples = posted.Samples.ToDictionary(s => s.SampleId);
        foreach (var sample in model.Samples)
        {
            if (!postedSamples.TryGetValue(sample.SampleId, out var postedSample))
            {
                continue;
            }

            sample.SampleNumber = postedSample.SampleNumber;
            sample.InternalDescription = postedSample.InternalDescription;
            sample.TabulationDescription = postedSample.TabulationDescription;
            sample.ExternalDescription = postedSample.ExternalDescription;
            sample.TestConsultantsDescription = postedSample.TestConsultantsDescription;

            if (!model.AllocationEditable)
            {
                continue;
            }

            var postedAllocations = postedSample.Allocations.ToDictionary(a => a.SampleTestId, a => a.IsApplicable);
            foreach (var allocation in sample.Allocations)
            {
                if (postedAllocations.TryGetValue(allocation.SampleTestId, out var isApplicable))
                {
                    allocation.IsApplicable = isApplicable;
                }
            }
        }

        return model;
    }

    private static SampleNumbersViewModel ToSampleNumbersViewModel(int year, int monthId, SampleNumbersResponse page) =>
        new()
        {
            MonthlyDistributionSchemeId = page.MonthlyDistributionSchemeId,
            YearId = year,
            MonthId = monthId,
            DistributionReferenceFull = page.DistributionReferenceFull,
            SchemeIdentifier = page.SchemeIdentifier,
            SchemeName = page.SchemeName,
            NumberOfSetsRequired = page.NumberOfSetsRequired,
            Comments = page.Comments,
            RequiresAssessment = page.RequiresAssessment,
            IsReadyForAssessment = page.IsReadyForAssessment,
            Samples = [.. page.Samples.Select(s => new SampleNumberRowViewModel
            {
                SampleId = s.SampleId,
                SampleNumber = s.SampleNumber,
                InternalDescription = s.InternalDescription,
                ExternalDescription = s.ExternalDescription,
                TabulationDescription = s.TabulationDescription,
                TestConsultantsDescription = s.TestConsultantsDescription,
                Allocations = [.. s.Allocations.Select(a => new SampleAllocationViewModel
                {
                    SampleTestId = a.SampleTestId,
                    TestId = a.TestId,
                    IsApplicable = a.IsApplicable,
                })],
            })],
            Tests = [.. page.Tests.Select(t => new SampleNumbersTestViewModel
            {
                TestId = t.TestId,
                TestName = t.TestName,
                TestResultItemNames = t.TestResultItemNames,
                TestMethodItemNames = t.TestMethodItemNames,
                CategoryItemNames = t.CategoryItemNames,
            })],
        };

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
