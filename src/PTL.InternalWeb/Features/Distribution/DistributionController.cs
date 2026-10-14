using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using PTL.ApiClient.Distribution;
using PTL.Contracts.Distribution;
using PTL.InternalWeb.Navigation;
using PTL.SharedUI.Notifications;

namespace PTL.InternalWeb.Features.Distribution;

// Distribution Dashboard and Monthly Distributions (Scheduling) - legacy
// Distributions/MenuDistributions.aspx and Scheduling/monthlys.aspx - see
// docs/analysis/distribution-analysis.md and docs/migration/distribution-migration.md.
//
// SCOPE: the Dashboard (year selection, month rows, progress indicators, "Initialise this Month"
// visibility/confirmation), the Monthly Distributions scheduling screen (dates, Cancel/
// Commence, Save/Apply/Cancel), the Sample Numbers screen, the Intended Results screen, the
// Distribution Participants screen (view, Change Deadline Date, Repeat Distribution), the Sample
// Preparation screen (Diary Entry/Prep Started/Prep Completed, Save/Apply/Cancel, Print) and its
// per-scheme Printable Results view, and the Sample Packaging screen (Group Address / Participant
// Combined / Daily, Save/Apply/Cancel, Print, Print Labels, Print Customs) are implemented. The
// Results(dashboard)/Tabulations "View" links, plus "Initialise this Month" itself, all remain
// placeholders (ViewStage/InitialiseMonth below) - those workflows (and the exact legacy
// persisted-save chain behind Initialise) are out of scope and flagged [NEEDS INVESTIGATION] in
// docs/migration/distribution-migration.md.
public sealed class DistributionController(IDistributionApiClient distributionApiClient) : Controller
{
    private static readonly string[] MonthOrder = ["Scheduling", "Preparation", "Packaging", "Results", "Tabulations", "Actual Results", "Sample Receipt"];
    private const string CancelCommand = "cancel";
    private const string ApplyCommand = "apply";

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
    // Tabulations, plus the Participants link on the Scheduling screen below, which all open
    // workflows out of scope for this story) - see class remarks.
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
        if (string.Equals(command, CancelCommand, StringComparison.OrdinalIgnoreCase))
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
            if (result is null)
            {
                TempData.SetNotification(NotificationType.Error, "Monthly Distributions could not be saved. Please try again later.");
            }

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

        TempData.SetNotification(NotificationType.Success, "Monthly Distributions updated successfully.");

        if (string.Equals(command, ApplyCommand, StringComparison.OrdinalIgnoreCase))
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
        ViewData[BreadcrumbRouteValues.Action] = nameof(Schedule);
        var row = model.Schemes.FirstOrDefault(s => s.MonthlyDistributionSchemeId == schemeId);
        if (row is not null)
        {
            row.IsCancelled = !row.IsCancelled;
        }

        model.YearId = year;
        model.MonthId = monthId;
        return View(nameof(Schedule), model);
    }

    // Sample Preparation - legacy Internal User/PreparationSheet.aspx. Reached from the
    // Dashboard's "Preparation: View" link.
    [HttpGet]
    public async Task<IActionResult> Preparation(int year, int monthId, CancellationToken cancellationToken)
    {
        var sheet = await distributionApiClient.GetPreparationAsync(year, monthId, cancellationToken);
        return sheet is null ? NotFound() : View(ToPreparationViewModel(year, monthId, sheet));
    }

    // Save = persist then leave (legacy ButtonSave_Click -> Save() -> LeavePage() ->
    // MenuDistributions.aspx, i.e. the Dashboard). Apply = persist then stay, re-reading fresh
    // data (legacy ButtonApply_Click -> Save() only, redrawing from the already-updated object).
    // Cancel = discard then leave, no save at all (legacy ButtonCancel_Click -> LeavePage()).
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Preparation(int year, int monthId, PreparationViewModel model, string command, CancellationToken cancellationToken)
    {
        if (string.Equals(command, CancelCommand, StringComparison.OrdinalIgnoreCase))
        {
            return RedirectToAction(nameof(Index));
        }

        var updates = model.Schemes
            .Select(s => new PreparationStageUpdateRequest(s.MonthlyDistributionSchemeId, s.DiaryEntryChecked, s.PrepStartChecked, s.PrepCompleteChecked))
            .ToList();

        var result = await distributionApiClient.SavePreparationAsync(year, monthId, new PreparationSaveRequest(updates), cancellationToken);

        if (result is null || !result.Success)
        {
            TempData.SetNotification(NotificationType.Error, "Sample Preparation could not be saved. Please try again later.");
            return RedirectToAction(nameof(Preparation), new { year, monthId });
        }

        TempData.SetNotification(NotificationType.Success, "Sample Preparation updated successfully.");

        if (string.Equals(command, ApplyCommand, StringComparison.OrdinalIgnoreCase))
        {
            return RedirectToAction(nameof(Preparation), new { year, monthId });
        }

        return RedirectToAction(nameof(Index));
    }

    // Results Receipt - legacy Internal User/ResultsReceipt.aspx. Reached from the Dashboard's
    // "Results: View" link. Participant View/Edit links open Actual Results / Sample Receipt, which
    // are separate legacy screens outside this feature (ViewStage placeholder).
    [HttpGet]
    public async Task<IActionResult> ResultsReceipt(int year, int monthId, CancellationToken cancellationToken)
    {
        var receipt = await distributionApiClient.GetResultsReceiptAsync(year, monthId, cancellationToken);
        return receipt is null ? NotFound() : View(ToResultsReceiptViewModel(year, monthId, receipt));
    }

    // Save = persist then leave (legacy ButtonSave_Click -> Save() -> LeavePage() -> MenuDistributions.aspx).
    // Apply = persist then stay, re-reading fresh data (legacy ButtonApply_Click -> Save() -> re-fetch).
    // Cancel = discard then leave, no save at all (legacy ButtonCancel_Click -> LeavePage()).
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResultsReceipt(int year, int monthId, ResultsReceiptViewModel model, string command, CancellationToken cancellationToken)
    {
        if (string.Equals(command, CancelCommand, StringComparison.OrdinalIgnoreCase))
        {
            return RedirectToAction(nameof(Index));
        }

        var updates = model.Schemes
            .SelectMany(s => s.Participants)
            .Select(p => new ResultsReceiptParticipantUpdateRequest(p.DistributionParticipantId, p.ExtraDocumentsReturned, p.ReturnedChecked))
            .ToList();

        var result = await distributionApiClient.SaveResultsReceiptAsync(year, monthId, new ResultsReceiptSaveRequest(updates), cancellationToken);

        if (result is null || !result.Success)
        {
            TempData.SetNotification(NotificationType.Error, "Results Receipt could not be saved. Please try again later.");
            return RedirectToAction(nameof(ResultsReceipt), new { year, monthId });
        }

        TempData.SetNotification(NotificationType.Success, "Results Receipt updated successfully.");

        return string.Equals(command, ApplyCommand, StringComparison.OrdinalIgnoreCase)
            ? RedirectToAction(nameof(ResultsReceipt), new { year, monthId })
            : RedirectToAction(nameof(Index));
    }

    // Actual Results - legacy Internal User/ActualResults.aspx. Reached from the Results Receipt
    // screen's per-participant Results "View/Edit" link; year/monthId are carried through purely so
    // Save and Cancel can return to the Results Receipt screen the user came from.
    [HttpGet]
    public async Task<IActionResult> ActualResults(int year, int monthId, Guid schemeId, Guid participantId, CancellationToken cancellationToken)
    {
        var page = await distributionApiClient.GetActualResultsAsync(schemeId, participantId, cancellationToken);
        return page is null ? NotFound() : View(ToActualResultsViewModel(year, monthId, page));
    }

    // Save = persist then return to Results Receipt (legacy ButtonSave_Click -> LeavePage()).
    // Apply = persist then stay on Actual Results (legacy ButtonApply_Click, and Audit.aspx's
    // AuditLeavePage). Cancel = discard, no save (legacy ButtonCancel_Click, CausesValidation=False).
    // cancelAudit = Cancel on the audit-reason step: return to where the user was heading, unsaved.
    // Every result value posts several fields, so a large grid exceeds the 1,024-value default.
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestFormLimits(ValueCountLimit = 20000)]
    public async Task<IActionResult> ActualResults(
        int year, int monthId, Guid schemeId, Guid participantId, ActualResultsViewModel model, string command, CancellationToken cancellationToken)
    {
        if (string.Equals(command, CancelCommand, StringComparison.OrdinalIgnoreCase))
        {
            return RedirectToAction(nameof(ResultsReceipt), new { year, monthId });
        }

        if (string.Equals(command, "cancelAudit", StringComparison.OrdinalIgnoreCase))
        {
            return string.Equals(model.PendingCommand, ApplyCommand, StringComparison.OrdinalIgnoreCase)
                ? RedirectToAction(nameof(ActualResults), new { year, monthId, schemeId, participantId })
                : RedirectToAction(nameof(ResultsReceipt), new { year, monthId });
        }

        model.YearId = year;
        model.MonthId = monthId;
        model.MonthlyDistributionSchemeId = schemeId;
        model.DistributionParticipantId = participantId;

        if (!ModelState.IsValid)
        {
            return View(await RehydrateActualResultsAsync(year, monthId, schemeId, participantId, model, cancellationToken));
        }

        var request = new ActualResultsSaveRequest(
            [.. model.Tests.SelectMany(t => t.Samples).SelectMany(s => s.Values)
                .Select(v => new ActualResultsSampleValueRequest(v.SampleResultItemId, v.Result ?? string.Empty))],
            [.. model.Tests.SelectMany(t => t.MethodItems).Select(m => new ActualResultsMethodItemRequest(m.TestMethodItemId, m.Result ?? string.Empty))],
            model.AuditWhy);

        var result = await distributionApiClient.SaveActualResultsAsync(schemeId, participantId, request, cancellationToken);

        if (result is null)
        {
            TempData.SetNotification(NotificationType.Error, "Results could not be saved. Please try again later.");
            return View(await RehydrateActualResultsAsync(year, monthId, schemeId, participantId, model, cancellationToken));
        }

        // Legacy: results already exist, so the save is diverted through Audit.aspx for a reason.
        if (result.AuditReasonRequired)
        {
            var auditModel = await RehydrateActualResultsAsync(year, monthId, schemeId, participantId, model, cancellationToken);
            auditModel.IsAuditStep = true;
            auditModel.AuditObjectName = result.AuditObjectName;
            auditModel.PendingCommand = command;
            return View(auditModel);
        }

        if (!result.Success)
        {
            var invalid = await RehydrateActualResultsAsync(year, monthId, schemeId, participantId, model, cancellationToken);
            invalid.SaveFailed = result.DeadlinePassed;
            ApplyActualResultsFieldErrors(invalid, result.FieldErrorsByItemId);
            return View(invalid);
        }

        var applying = string.Equals(command, ApplyCommand, StringComparison.OrdinalIgnoreCase)
            || string.Equals(model.PendingCommand, ApplyCommand, StringComparison.OrdinalIgnoreCase);

        TempData.SetNotification(NotificationType.Success, "Results updated successfully.");

        return applying
            ? RedirectToAction(nameof(ActualResults), new { year, monthId, schemeId, participantId })
            : RedirectToAction(nameof(ResultsReceipt), new { year, monthId });
    }

    // Legacy keeps the whole ActualResultsSet in Session, so a failed Save redisplays the page with
    // the server's structure intact and only the typed values overlaid. There is no server-side
    // session here, so re-read the page and overlay the posted editable values onto it.
    private async Task<ActualResultsViewModel> RehydrateActualResultsAsync(
        int year, int monthId, Guid schemeId, Guid participantId, ActualResultsViewModel posted, CancellationToken cancellationToken)
    {
        var page = await distributionApiClient.GetActualResultsAsync(schemeId, participantId, cancellationToken);
        if (page is null)
        {
            return posted;
        }

        var model = ToActualResultsViewModel(year, monthId, page);
        model.IsAuditStep = posted.IsAuditStep;
        model.AuditObjectName = posted.AuditObjectName;
        model.PendingCommand = posted.PendingCommand;
        model.AuditWhy = posted.AuditWhy;

        var postedSampleValues = posted.Tests.SelectMany(t => t.Samples).SelectMany(s => s.Values)
            .GroupBy(v => v.SampleResultItemId)
            .ToDictionary(g => g.Key, g => g.Last().Result);
        var postedMethodItems = posted.Tests.SelectMany(t => t.MethodItems)
            .GroupBy(m => m.TestMethodItemId)
            .ToDictionary(g => g.Key, g => g.Last().Result);

        foreach (var test in model.Tests)
        {
            foreach (var value in test.Samples.SelectMany(s => s.Values))
            {
                if (postedSampleValues.TryGetValue(value.SampleResultItemId, out var result))
                {
                    value.Result = result;
                }
            }

            foreach (var methodItem in test.MethodItems)
            {
                if (postedMethodItems.TryGetValue(methodItem.TestMethodItemId, out var result))
                {
                    methodItem.Result = result;
                }
            }
        }

        return model;
    }

    private void ApplyActualResultsFieldErrors(ActualResultsViewModel model, IReadOnlyDictionary<Guid, string> errorsByItemId)
    {
        for (var t = 0; t < model.Tests.Count; t++)
        {
            var test = model.Tests[t];

            for (var s = 0; s < test.Samples.Count; s++)
            {
                for (var v = 0; v < test.Samples[s].Values.Count; v++)
                {
                    if (errorsByItemId.TryGetValue(test.Samples[s].Values[v].SampleResultItemId, out var message))
                    {
                        ModelState.AddModelError($"Tests[{t}].Samples[{s}].Values[{v}].Result", message);
                    }
                }
            }

            for (var m = 0; m < test.MethodItems.Count; m++)
            {
                if (errorsByItemId.TryGetValue(test.MethodItems[m].TestMethodItemId, out var message))
                {
                    ModelState.AddModelError($"Tests[{t}].MethodItems[{m}].Result", message);
                }
            }
        }
    }

    private static ActualResultsViewModel ToActualResultsViewModel(int year, int monthId, ActualResultsResponse page) =>
        new()
        {
            YearId = year,
            MonthId = monthId,
            MonthlyDistributionSchemeId = page.MonthlyDistributionSchemeId,
            DistributionParticipantId = page.DistributionParticipantId,
            Heading = page.Heading,
            IsReadOnly = page.IsReadOnly,
            Tests = [.. page.Tests.Select(t => new ActualResultsTestViewModel
            {
                TestId = t.TestId,
                TestName = t.TestName,
                ResultItems = [.. t.ResultItems.Select(r => new ActualResultsResultItemViewModel
                {
                    TestResultItemId = r.TestResultItemId,
                    Name = r.Name,
                    FormatString = r.FormatString,
                    ReturnValueType = r.ReturnValueType,
                })],
                MethodItems = [.. t.MethodItems.Select(m => new ActualResultsMethodItemViewModel
                {
                    TestMethodItemId = m.TestMethodItemId,
                    Name = m.Name,
                    FormatString = m.FormatString,
                    ReturnValueType = m.ReturnValueType,
                    Result = m.Result,
                })],
                Samples = [.. t.Samples.Select(s => new ActualResultsSampleRowViewModel
                {
                    SampleId = s.SampleId,
                    SampleNumber = s.SampleNumber,
                    Values = [.. s.Values.Select(v => new ActualResultsSampleValueViewModel
                    {
                        SampleResultItemId = v.SampleResultItemId,
                        TestResultItemId = v.TestResultItemId,
                        Result = v.Result,
                    })],
                })],
            })],
        };

    private static ResultsReceiptViewModel ToResultsReceiptViewModel(int year, int monthId, ResultsReceiptResponse receipt) =>
        new()
        {
            YearId = year,
            MonthId = monthId,
            MonthDescription = PackagingMonthDescription(year, monthId),
            Schemes =
            [
                .. receipt.Schemes.Select(s => new ResultsReceiptSchemeRowViewModel
                {
                    MonthlyDistributionSchemeId = s.MonthlyDistributionSchemeId,
                    Description = s.Description,
                    RequiresAssessment = s.RequiresAssessment,
                    ProgressPercentage = s.ProgressPercentage,
                    Participants =
                    [
                        .. s.Participants.Select(p => new ResultsReceiptParticipantRowViewModel
                        {
                            DistributionParticipantId = p.DistributionParticipantId,
                            FullName = p.FullName,
                            DeadlineDate = p.DeadlineDate,
                            FirstEditDate = p.FirstEditDate,
                            LastEditDate = p.LastEditDate,
                            ExtraDocumentsReturned = p.ExtraDocumentsReturned,
                            CanViewResults = p.CanViewResults,
                            HasUnaccountedSamples = p.HasUnaccountedSamples,
                            ReturnedDate = p.ReturnedDate,
                        }),
                    ],
                }),
            ],
        };

    // Sample Packaging - legacy Internal User/PackagingSheet.aspx. Reached from the Dashboard's
    // "Packaging: View" link. Print is the browser's own print of this page (legacy window.print()
    // with the Print column choosing which rows appear), so it has no server action.
    [HttpGet]
    public async Task<IActionResult> Packaging(int year, int monthId, int? mode, int week, CancellationToken cancellationToken)
    {
        var model = await BuildPackagingViewModelAsync(year, monthId, new PackagingViewModel { Mode = mode, Week = week }, cancellationToken);
        return model is null ? NotFound() : View(model);
    }

    // select = explicit View button after choosing the Packaging/Week values: redisplay, keeping unsaved ticks.
    // Save = persist then leave (legacy ButtonSave_Click -> LeavePage() -> MenuDistributions.aspx).
    // Apply = persist then stay (legacy ButtonApply_Click redraws from the saved data).
    // Cancel = discard then leave. labels/customs = Print Labels / Print Customs.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Packaging(int year, int monthId, PackagingViewModel model, string command, CancellationToken cancellationToken)
    {
        if (string.Equals(command, CancelCommand, StringComparison.OrdinalIgnoreCase))
        {
            return RedirectToAction(nameof(Index));
        }

        if (!ValidatePackagingSelection(model))
        {
            var invalid = await BuildPackagingViewModelAsync(year, monthId, model, cancellationToken);
            return invalid is null ? NotFound() : View(invalid);
        }

        if (string.Equals(command, "save", StringComparison.OrdinalIgnoreCase) || string.Equals(command, ApplyCommand, StringComparison.OrdinalIgnoreCase))
        {
            return await SavePackagingAsync(year, monthId, model, command, cancellationToken);
        }

        if (string.Equals(command, "labels", StringComparison.OrdinalIgnoreCase) && model.Mode is { } labelMode)
        {
            var labels = await distributionApiClient.GetPackagingLabelsAsync(
                year, monthId, new PackagingSelectionRequest(labelMode, model.Week, model.PrintKeys), cancellationToken);

            if (labels is { Labels.Count: > 0 })
            {
                return View("PackagingLabels", new PackagingLabelsViewModel
                {
                    YearId = year,
                    MonthId = monthId,
                    Mode = labelMode,
                    Week = model.Week,
                    MonthDescription = PackagingMonthDescription(year, monthId),
                    Labels = labels.Labels,
                });
            }

            model.Message = "No Labels to Print";
        }
        else if (string.Equals(command, "customs", StringComparison.OrdinalIgnoreCase) && model.Mode is { } customsMode)
        {
            var customs = await distributionApiClient.GetPackagingCustomsAsync(
                year, monthId, new PackagingSelectionRequest(customsMode, model.Week, model.PrintKeys), cancellationToken);

            if (customs.Content is not null)
            {
                return File(customs.Content, "application/vnd.openxmlformats-officedocument.wordprocessingml.document", customs.FileName);
            }

            model.Message = "There was a problem with the Mail Merge";
            model.MessageDetail = customs.Error;
        }
        else if (string.Equals(command, "labels", StringComparison.OrdinalIgnoreCase))
        {
            model.Message = "No Labels to Print";
        }

        var rebuilt = await BuildPackagingViewModelAsync(year, monthId, model, cancellationToken);
        return rebuilt is null ? NotFound() : View(rebuilt);
    }

    private static bool ValidatePackagingSelection(PackagingViewModel model)
    {
        if (model.Mode is not (0 or 1 or 2))
        {
            model.Mode = null;
            model.ModeError = "Select a packaging option";
        }
        else if (model.Mode is 0 or 1 && model.Week is < 1 or > 4)
        {
            model.WeekError = "Select a week";
        }

        return !model.HasSelectionError;
    }

    private async Task<IActionResult> SavePackagingAsync(int year, int monthId, PackagingViewModel model, string command, CancellationToken cancellationToken)
    {
        var result = await distributionApiClient.SavePackagingAsync(year, monthId, new PackagingSaveRequest(ToPackagingUpdates(model)), cancellationToken);

        if (result is null || !result.Success)
        {
            TempData.SetNotification(NotificationType.Error, "Sample Packaging could not be saved. Please try again later.");
            return RedirectToAction(nameof(Packaging), new { year, monthId, mode = model.Mode, week = model.Week });
        }

        if (result.EmailFailures is { Count: > 0 } emailFailures)
        {
            TempData.SetNotification(
                NotificationType.Warning,
                $"Sample Packaging updated, but the Distribution Posted email could not be sent to: {string.Join(", ", emailFailures)}. Please notify the participant another way.");
        }
        else
        {
            TempData.SetNotification(NotificationType.Success, "Sample Packaging updated successfully.");
        }

        return string.Equals(command, ApplyCommand, StringComparison.OrdinalIgnoreCase)
            ? RedirectToAction(nameof(Packaging), new { year, monthId, mode = model.Mode, week = model.Week })
            : RedirectToAction(nameof(Index));
    }

    private async Task<PackagingViewModel?> BuildPackagingViewModelAsync(int year, int monthId, PackagingViewModel model, CancellationToken cancellationToken)
    {
        model.YearId = year;
        model.MonthId = monthId;
        model.MonthDescription = PackagingMonthDescription(year, monthId);
        model.PrintTitle = PackagingPrintTitle(model.Mode, model.Week, model.MonthDescription);
        model.Sheet = null;

        var weekNeeded = model.Mode is 0 or 1;
        if (model.Mode is not (0 or 1 or 2) || (weekNeeded && model.Week is < 1 or > 4))
        {
            return model;
        }

        model.Sheet = await distributionApiClient.GetPackagingAsync(year, monthId, model.Mode.Value, weekNeeded ? model.Week : 0, cancellationToken);
        return model.Sheet is null ? null : model;
    }

    private static string PackagingMonthDescription(int year, int monthId) =>
        $"{System.Globalization.CultureInfo.GetCultureInfo("en-GB").DateTimeFormat.GetMonthName(monthId)} {year}";

    // Legacy LblPrintTitle2 - printed beneath "Sample Packaging - ".
    private static string PackagingPrintTitle(int? mode, int week, string monthDescription) =>
        mode switch
        {
            0 when week > 0 => $"Week {week} - {monthDescription} - Group Packaging",
            1 when week > 0 => $"Week {week} - {monthDescription} - Participant Combined Packaging",
            2 => $"{monthDescription} - Weekly",
            _ => string.Empty,
        };

    // Ticks are "{mode}:{week}:{distributionParticipantId}"; ticking Posted alone is enough, the API
    // stamps Packed as well.
    private static List<PackagingStageUpdateRequest> ToPackagingUpdates(PackagingViewModel model)
    {
        var updates = new Dictionary<(int Mode, int Week, Guid Id), (bool Packed, bool Posted)>();

        foreach (var tick in model.PackedTicks)
        {
            if (TryParsePackagingTick(tick, out var key))
            {
                updates[key] = (true, updates.TryGetValue(key, out var current) && current.Posted);
            }
        }

        foreach (var tick in model.PostedTicks)
        {
            if (TryParsePackagingTick(tick, out var key))
            {
                updates[key] = (updates.TryGetValue(key, out var current) && current.Packed, true);
            }
        }

        return [.. updates.Select(u => new PackagingStageUpdateRequest(u.Key.Mode, u.Key.Week, u.Key.Id, u.Value.Packed, u.Value.Posted))];
    }

    private static bool TryParsePackagingTick(string tick, out (int Mode, int Week, Guid Id) key)
    {
        key = default;
        var parts = tick.Split(':');
        if (parts.Length != 3
            || !int.TryParse(parts[0], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var mode)
            || !int.TryParse(parts[1], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var week)
            || !Guid.TryParse(parts[2], out var id))
        {
            return false;
        }

        key = (mode, week, id);
        return true;
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
        if (string.Equals(command, CancelCommand, StringComparison.OrdinalIgnoreCase))
        {
            return RedirectToAction(nameof(Schedule), new { year, monthId });
        }

        if (string.Equals(command, "cancelAudit", StringComparison.OrdinalIgnoreCase))
        {
            return RedirectToDestination(model.PendingCommand, year, monthId, schemeId);
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
            if (result is null)
            {
                TempData.SetNotification(NotificationType.Error, "Sample Numbers could not be saved. Please try again later.");
            }

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

        TempData.SetNotification(NotificationType.Success, "Sample Numbers updated successfully.");

        return RedirectToDestination(command, year, monthId, schemeId);
    }

    private RedirectToActionResult RedirectToDestination(string? command, int year, int monthId, Guid schemeId) =>
        string.Equals(command, "saveAndGoToIntendedResults", StringComparison.OrdinalIgnoreCase)
            ? RedirectToAction(nameof(IntendedResults), new { year, monthId, schemeId })
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

    // Intended Results - legacy Scheduling/IntendedResults.aspx. Opened from the Scheduling
    // screen's "View Intended Results" link; year/monthId are carried through purely so Save and
    // Cancel can return to the Scheduling screen the user came from.
    [HttpGet]
    public async Task<IActionResult> IntendedResults(int year, int monthId, Guid schemeId, CancellationToken cancellationToken)
    {
        var page = await distributionApiClient.GetIntendedResultsAsync(schemeId, cancellationToken);

        // Legacy Page_Load: "Samples numbers must be defined before setting the Intended
        // Results" - If Not mds.HasSampleNumbersDefined Then LeavePage(). No error shown, just a
        // silent return to Scheduling.
        if (page is null)
        {
            return RedirectToAction(nameof(Schedule), new { year, monthId });
        }

        return View(ToIntendedResultsViewModel(year, monthId, page));
    }

    // Save = persist then return to Monthly Distributions (legacy ButtonSave_Click -> LeavePage()).
    // Cancel = discard, no save at all (legacy ButtonCancel_Click, CausesValidation=False).
    // cancelAudit = Cancel on the audit-reason step - legacy Audit.aspx always returns to
    // ~/Scheduling/monthlys.aspx for this screen, so there is only one destination.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> IntendedResults(int year, int monthId, Guid schemeId, IntendedResultsViewModel model, string command, CancellationToken cancellationToken)
    {
        if (string.Equals(command, CancelCommand, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(command, "cancelAudit", StringComparison.OrdinalIgnoreCase))
        {
            return RedirectToAction(nameof(Schedule), new { year, monthId });
        }

        model.YearId = year;
        model.MonthId = monthId;
        model.MonthlyDistributionSchemeId = schemeId;

        if (!ModelState.IsValid)
        {
            return View(await RehydrateIntendedResultsAsync(year, monthId, schemeId, model, cancellationToken));
        }

        var request = new IntendedResultsSaveRequest(
            [.. model.Tests.Select(t => new IntendedResultsTestRequest(
                t.TestId,
                [.. t.ResultItems.Select(r => new IntendedResultsResultItemRequest(r.TestResultItemId, r.IsRange))],
                [.. t.MethodItems.Select(m => new IntendedResultsMethodItemRequest(m.TestMethodItemId, m.IntendedResult ?? string.Empty))],
                [.. t.Samples.Select(s => new IntendedResultsSampleRowRequest(
                    s.SampleId,
                    s.IsApplicable,
                    [.. s.Values.Select(v => new IntendedResultsSampleResultValueRequest(v.SampleResultItemId, v.IntendedResult ?? string.Empty, v.IntendedResult2 ?? string.Empty))]))]))],
            model.RequiresAuditReason,
            model.AuditWhy);

        var result = await distributionApiClient.SaveIntendedResultsAsync(schemeId, request, cancellationToken);

        // Legacy RequireAudit -> Audit.aspx: diverted BEFORE anything is written, even when
        // nothing in this particular save is dirty.
        if (result is not null && result.AuditReasonRequired)
        {
            var auditModel = await RehydrateIntendedResultsAsync(year, monthId, schemeId, model, cancellationToken);
            auditModel.RequiresAuditReason = true;
            auditModel.IsAuditStep = true;
            auditModel.AuditObjectName = result.AuditObjectName;
            auditModel.PendingCommand = command;
            auditModel.AuditWhy = model.AuditWhy;
            return View(auditModel);
        }

        if (result is null || !result.Success)
        {
            if (result is null)
            {
                TempData.SetNotification(NotificationType.Error, "Intended Results could not be saved. Please try again later.");
            }

            foreach (var (itemId, errors) in result?.FieldErrorsByItemId ?? new Dictionary<Guid, IntendedResultsFieldError[]>())
            {
                ApplyIntendedResultsFieldErrors(model, itemId, errors);
            }

            return View(await RehydrateIntendedResultsAsync(year, monthId, schemeId, model, cancellationToken));
        }

        TempData.SetNotification(NotificationType.Success, "Intended Results updated successfully.");

        return RedirectToAction(nameof(Schedule), new { year, monthId });
    }

    // Printable Results - legacy Internal User/PrintableResults.aspx, reached from the Sample
    // Preparation screen's per-scheme Results "View" link. Reuses the same Intended Results data
    // (legacy IntendedResultsSet.FetchIntendedResults) already fetched for the Intended Results
    // screen above, but shaped by PrintableResultsWorksheetBuilder into legacy's dynamic,
    // ExpectedLength-packed multi-table-per-test worksheet rather than the interactive edit
    // screen's own view model.
    // showValues mirrors legacy's "Select Version to Print" dropdown, whose default selected item
    // is always "Blank" (DropDownParticipant.SelectedIndex = 0) regardless of whether intended
    // results exist - only an explicit ?showValues=true request shows them, and only when
    // HasIntendedResults is true (legacy never even offers the "Show Intended Results" option
    // otherwise).
    [HttpGet]
    public async Task<IActionResult> PrintableResults(int year, int monthId, Guid schemeId, bool showValues, CancellationToken cancellationToken)
    {
        var page = await distributionApiClient.GetIntendedResultsAsync(schemeId, cancellationToken);
        if (page is null)
        {
            return NotFound();
        }

        // DATE OF DISTRIBUTION/the deadline message/the UKAS accreditation mark only exist on the
        // Scheduling-level scheme row (legacy's separate MonthlyDistributionScheme.
        // FetchMonthlyDistributionSchemeChild call) - already-available via the same schedule
        // lookup the Scheduling screen itself uses.
        var schedule = await distributionApiClient.GetScheduleAsync(year, monthId, cancellationToken);
        var scheduleScheme = schedule?.Schemes.FirstOrDefault(s => s.MonthlyDistributionSchemeId == schemeId);

        var worksheet = PrintableResultsWorksheetBuilder.Build(
            year,
            monthId,
            page,
            page.HasIntendedResults && showValues,
            scheduleScheme?.DistributionDate ?? default,
            scheduleScheme?.DeadlineDate ?? default,
            scheduleScheme?.IsAccredited ?? false);

        return View(worksheet);
    }

    // Field errors are keyed by TestResultItemId/TestMethodItemId/SampleResultItemId (globally
    // unique, so no collision risk across the three id spaces) rather than by a flat row index,
    // since this screen has no single list to index into.
    private void ApplyIntendedResultsFieldErrors(IntendedResultsViewModel model, Guid itemId, IntendedResultsFieldError[] errors)
    {
        for (var testIndex = 0; testIndex < model.Tests.Count; testIndex++)
        {
            var test = model.Tests[testIndex];

            if (TryApplyMethodItemErrors(test, testIndex, itemId, errors))
            {
                return;
            }

            if (TryApplySampleValueErrors(test, testIndex, itemId, errors))
            {
                return;
            }
        }
    }

    private bool TryApplyMethodItemErrors(IntendedResultsTestViewModel test, int testIndex, Guid itemId, IntendedResultsFieldError[] errors)
    {
        var methodItemIndex = test.MethodItems.FindIndex(m => m.TestMethodItemId == itemId);
        if (methodItemIndex < 0)
        {
            return false;
        }

        foreach (var error in errors)
        {
            ModelState.AddModelError(
                $"{nameof(IntendedResultsViewModel.Tests)}[{testIndex}].{nameof(test.MethodItems)}[{methodItemIndex}].{nameof(IntendedResultsMethodItemViewModel.IntendedResult)}",
                error.Message);
        }

        return true;
    }

    private bool TryApplySampleValueErrors(IntendedResultsTestViewModel test, int testIndex, Guid itemId, IntendedResultsFieldError[] errors)
    {
        for (var sampleIndex = 0; sampleIndex < test.Samples.Count; sampleIndex++)
        {
            var valueIndex = test.Samples[sampleIndex].Values.FindIndex(v => v.SampleResultItemId == itemId);
            if (valueIndex < 0)
            {
                continue;
            }

            foreach (var error in errors)
            {
                var propertyName = string.Equals(error.Field, "IntendedResult2", StringComparison.Ordinal)
                    ? nameof(IntendedResultsSampleValueViewModel.IntendedResult2)
                    : nameof(IntendedResultsSampleValueViewModel.IntendedResult);

                ModelState.AddModelError(
                    $"{nameof(IntendedResultsViewModel.Tests)}[{testIndex}].{nameof(test.Samples)}[{sampleIndex}].{nameof(IntendedResultsSampleRowViewModel.Values)}[{valueIndex}].{propertyName}",
                    error.Message);
            }

            return true;
        }

        return false;
    }

    // Legacy keeps the whole IntendedResultsSet in Session, so a failed Save redisplays the page
    // with the server's heading/structure intact and only the user's typed edits overlaid. There
    // is no server-side session here, so re-read the distribution and overlay the posted editable
    // fields onto it - the display-only data is never trusted from the form.
    private async Task<IntendedResultsViewModel> RehydrateIntendedResultsAsync(
        int year, int monthId, Guid schemeId, IntendedResultsViewModel posted, CancellationToken cancellationToken)
    {
        var page = await distributionApiClient.GetIntendedResultsAsync(schemeId, cancellationToken);
        if (page is null)
        {
            return posted;
        }

        var model = ToIntendedResultsViewModel(year, monthId, page);
        model.RequiresAuditReason = posted.RequiresAuditReason;
        model.IsAuditStep = posted.IsAuditStep;
        model.AuditObjectName = posted.AuditObjectName;
        model.PendingCommand = posted.PendingCommand;
        model.AuditWhy = posted.AuditWhy;

        var postedTestsById = posted.Tests.ToDictionary(t => t.TestId);
        foreach (var test in model.Tests)
        {
            if (!postedTestsById.TryGetValue(test.TestId, out var postedTest))
            {
                continue;
            }

            OverlayPostedTest(test, postedTest);
        }

        return model;
    }

    private static void OverlayPostedTest(IntendedResultsTestViewModel test, IntendedResultsTestViewModel postedTest)
    {
        var postedResultItemsById = postedTest.ResultItems.ToDictionary(r => r.TestResultItemId);
        foreach (var resultItem in test.ResultItems)
        {
            if (postedResultItemsById.TryGetValue(resultItem.TestResultItemId, out var postedResultItem))
            {
                resultItem.IsRange = postedResultItem.IsRange;
            }
        }

        var postedMethodItemsById = postedTest.MethodItems.ToDictionary(m => m.TestMethodItemId);
        foreach (var methodItem in test.MethodItems)
        {
            if (postedMethodItemsById.TryGetValue(methodItem.TestMethodItemId, out var postedMethodItem))
            {
                methodItem.IntendedResult = postedMethodItem.IntendedResult;
            }
        }

        var postedSamplesById = postedTest.Samples.ToDictionary(s => s.SampleId);
        foreach (var sample in test.Samples)
        {
            // Mirrors legacy's disabled controls: a non-applicable sample/test pair is never
            // posted back by a real browser, so its server-fetched (stored) values stand as-is.
            if (!sample.IsApplicable || !postedSamplesById.TryGetValue(sample.SampleId, out var postedSample))
            {
                continue;
            }

            var postedValuesById = postedSample.Values.ToDictionary(v => v.SampleResultItemId);
            foreach (var value in sample.Values)
            {
                if (postedValuesById.TryGetValue(value.SampleResultItemId, out var postedValue))
                {
                    value.IntendedResult = postedValue.IntendedResult;
                    value.IntendedResult2 = postedValue.IntendedResult2;
                }
            }
        }
    }

    private static IntendedResultsViewModel ToIntendedResultsViewModel(int year, int monthId, IntendedResultsResponse page) =>
        new()
        {
            MonthlyDistributionSchemeId = page.MonthlyDistributionSchemeId,
            YearId = year,
            MonthId = monthId,
            DistributionReferenceFull = page.DistributionReferenceFull,
            SchemeIdentifier = page.SchemeIdentifier,
            SchemeName = page.SchemeName,
            RequiresAuditReason = page.RequiresAuditReason,
            Tests = [.. page.Tests.Select(t => new IntendedResultsTestViewModel
            {
                TestId = t.TestId,
                TestName = t.TestName,
                ResultItems = [.. t.ResultItems.Select(r => new IntendedResultsResultItemViewModel
                {
                    TestResultItemId = r.TestResultItemId,
                    Name = r.Name,
                    FormatString = r.FormatString,
                    ReturnValueType = r.ReturnValueType,
                    IsRange = r.IsRange,
                })],
                MethodItems = [.. t.MethodItems.Select(m => new IntendedResultsMethodItemViewModel
                {
                    TestMethodItemId = m.TestMethodItemId,
                    Name = m.Name,
                    FormatString = m.FormatString,
                    ReturnValueType = m.ReturnValueType,
                    IntendedResult = m.IntendedResult,
                })],
                Samples = [.. t.Samples.Select(s => new IntendedResultsSampleRowViewModel
                {
                    SampleId = s.SampleId,
                    SampleNumber = s.SampleNumber,
                    InternalDescription = s.InternalDescription,
                    IsApplicable = s.IsApplicable,
                    Values = [.. s.Values.Select(v => new IntendedResultsSampleValueViewModel
                    {
                        SampleResultItemId = v.SampleResultItemId,
                        TestResultItemId = v.TestResultItemId,
                        IntendedResult = v.IntendedResult,
                        IntendedResult2 = v.IntendedResult2,
                    })],
                })],
            })],
        };

    private static PreparationViewModel ToPreparationViewModel(int year, int monthId, PreparationSheetResponse sheet) =>
        new()
        {
            YearId = year,
            MonthId = monthId,
            MonthDescription = new DateTime(2000, monthId, 1, 0, 0, 0, DateTimeKind.Utc).ToString("MMM", System.Globalization.CultureInfo.InvariantCulture) + " " + year,
            Schemes = [.. sheet.Schemes.Select(s => new PreparationSchemeRowViewModel
            {
                MonthlyDistributionSchemeId = s.MonthlyDistributionSchemeId,
                SchemeIdentifier = s.SchemeIdentifier,
                SchemeName = s.SchemeName,
                DistributionReferenceFull = s.DistributionReferenceFull,
                NumberOfSetsRequired = s.NumberOfSetsRequired,
                SamplePackingInstructions = s.SamplePackingInstructions,
                DiaryEntry = s.DiaryEntry,
                PrepStartDate = s.PrepStartDate,
                PrepCompleteDate = s.PrepCompleteDate,
                Samples = [.. s.Samples.Select(sa => new PreparationSampleRowViewModel
                {
                    SampleNumber = sa.SampleNumber,
                    InternalDescription = sa.InternalDescription,
                })],
            })],
        };

    // Distribution Participants - legacy Scheduling/DistributionParticipantList.aspx. Reached from
    // the Scheduling screen's "View Participants" link.
    [HttpGet]
    public async Task<IActionResult> Participants(int year, int monthId, Guid schemeId, CancellationToken cancellationToken)
    {
        var data = await distributionApiClient.GetParticipantsAsync(schemeId, cancellationToken);
        return data is null ? NotFound() : View(ToParticipantsViewModel(year, monthId, data));
    }

    // Change Deadline Date - legacy gridParticipants_RowCommand (lnkSaveNewDeadline). Each row has
    // its own ValidationGroup in legacy, so only the row being saved is validated/applied here;
    // every other row's posted values are ignored and redisplay uses fresh server data.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeDeadline(
        int year, int monthId, Guid schemeId, Guid participantId, DistributionParticipantsViewModel model, CancellationToken cancellationToken)
    {
        ViewData[BreadcrumbRouteValues.Action] = nameof(Participants);
        if (!ModelState.IsValid)
        {
            return View(nameof(Participants), await RehydrateParticipantsAsync(year, monthId, schemeId, model, cancellationToken));
        }

        var row = model.Participants.FirstOrDefault(p => p.DistributionParticipantId == participantId);
        var result = await distributionApiClient.ChangeDeadlineAsync(
            schemeId, participantId, new ChangeDeadlineRequest(row?.NewDeadlineDate, row?.NewDeadlineReason), cancellationToken);

        if (result is null || !result.Success)
        {
            if (result is null)
            {
                TempData.SetNotification(NotificationType.Error, "The deadline date could not be saved. Please try again later.");
            }
            else
            {
                var rowIndex = model.Participants.FindIndex(p => p.DistributionParticipantId == participantId);
                if (result.NewDeadlineDateError is not null)
                {
                    ModelState.AddModelError($"{nameof(model.Participants)}[{rowIndex}].{nameof(DistributionParticipantRowViewModel.NewDeadlineDate)}", result.NewDeadlineDateError);
                }

                if (result.ReasonError is not null)
                {
                    ModelState.AddModelError($"{nameof(model.Participants)}[{rowIndex}].{nameof(DistributionParticipantRowViewModel.NewDeadlineReason)}", result.ReasonError);
                }
            }

            return View(nameof(Participants), await RehydrateParticipantsAsync(year, monthId, schemeId, model, cancellationToken));
        }

        TempData.SetNotification(NotificationType.Success, "Deadline Date updated successfully.");
        return RedirectToAction(nameof(Participants), new { year, monthId, schemeId });
    }

    // Repeat this Distribution - legacy btnRepeatDistribution_Click. "Please tick at least one
    // participant..." mirrors legacy's client-side checkifchecked() guard, re-enforced server-side.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RepeatDistribution(int year, int monthId, Guid schemeId, DistributionParticipantsViewModel model, CancellationToken cancellationToken)
    {
        ViewData[BreadcrumbRouteValues.Action] = nameof(Participants);
        if (!ModelState.IsValid)
        {
            return View(nameof(Participants), await RehydrateParticipantsAsync(year, monthId, schemeId, model, cancellationToken));
        }

        var selections = model.Participants.Where(p => p.Selected).Select(p => new ParticipantSelectionRequest(p.DistributionParticipantId, false))
            .Concat(model.AdditionalParticipants.Where(p => p.Selected).Select(p => new ParticipantSelectionRequest(p.ParticipantSchemeId, true)))
            .ToList();

        var result = await distributionApiClient.RepeatDistributionAsync(schemeId, new RepeatDistributionRequest(selections), cancellationToken);

        if (result is null || !result.Success)
        {
            TempData.SetNotification(
                NotificationType.Error,
                result?.Error ?? "The distribution could not be repeated. Please try again later.");
            return View(nameof(Participants), await RehydrateParticipantsAsync(year, monthId, schemeId, model, cancellationToken));
        }

        TempData.SetNotification(NotificationType.Success, "Distribution repeated successfully.");
        return RedirectToAction(nameof(Schedule), new { year, monthId });
    }

    private async Task<DistributionParticipantsViewModel> RehydrateParticipantsAsync(
        int year, int monthId, Guid schemeId, DistributionParticipantsViewModel posted, CancellationToken cancellationToken)
    {
        var data = await distributionApiClient.GetParticipantsAsync(schemeId, cancellationToken);
        return data is null ? posted : ToParticipantsViewModel(year, monthId, data);
    }

    private static DistributionParticipantsViewModel ToParticipantsViewModel(int year, int monthId, DistributionParticipantsResponse data) =>
        new()
        {
            MonthlyDistributionSchemeId = data.MonthlyDistributionSchemeId,
            YearId = year,
            MonthId = monthId,
            SchemeIdentifier = data.SchemeIdentifier,
            SchemeName = data.SchemeName,
            DistributionReferenceFull = data.DistributionReferenceFull,
            IsRepeatDistribution = data.IsRepeatDistribution,
            Participants = [.. data.Participants.Select(p => new DistributionParticipantRowViewModel
            {
                DistributionParticipantId = p.DistributionParticipantId,
                FullName = p.FullName,
                DeadlineDate = p.DeadlineDate,
                DeadlineChangeReason = p.DeadlineChangeReason,
                AdditionalParticipant = p.AdditionalParticipant,
            })],
            AdditionalParticipants = [.. data.AdditionalParticipants.Select(p => new DistributionAdditionalParticipantRowViewModel
            {
                ParticipantSchemeId = p.ParticipantSchemeId,
                FullName = p.FullName,
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
