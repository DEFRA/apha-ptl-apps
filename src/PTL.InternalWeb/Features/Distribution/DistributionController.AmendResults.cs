using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PTL.Contracts.Distribution;
using PTL.InternalWeb.Authorization;
using PTL.InternalWeb.Confirmation;
using PTL.InternalWeb.Navigation;
using PTL.SharedUI.Notifications;
using Keys = PTL.Contracts.Distribution.AmendResultsFieldKeys;

namespace PTL.InternalWeb.Features.Distribution;

// Amend Results - legacy Scheduling/AmendResults.aspx, reached from the Tabulations page's
// "Amend Results" action. Save and Cancel return to Tabulations; Apply saves and stays, re-reading
// the page (legacy BtnSave / BtnApply / BtnCancel).
public sealed partial class DistributionController
{
    private const string AuditWhyRequiredMessage = "The reason for these changes must be specified";

    // Legacy OnChangeFunc armed a navigate-away dialog once a cell had been edited. Cancel is the one
    // exit the server sees, and the form posts every value, so the warning is raised only when the
    // posted values actually differ from the stored ones.
    public const string DiscardTitle = "You have changes that have not been saved";
    public const string DiscardMessage = "If you continue, your amendments will be discarded.";

    private static readonly string[] AmendInputPrefixes = ["im_", "ir1_", "ir2_", "pm_", "pr_", "ac_", "as_", "pc_", "rr_", "rg_", "nr_"];

    [HttpGet]
    [Authorize(Policy = PtlPolicies.Scheduling)]
    public async Task<IActionResult> AmendResults(int year, int monthId, Guid schemeId, CancellationToken cancellationToken)
    {
        var page = await distributionApiClient.GetAmendResultsAsync(schemeId, cancellationToken);
        return page is null
            ? NotFound()
            : View(new AmendResultsViewModel { YearId = year, MonthId = monthId, MonthlyDistributionSchemeId = schemeId, Page = page });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestFormLimits(ValueCountLimit = 20000)]
    [Authorize(Policy = PtlPolicies.Scheduling)]
    public async Task<IActionResult> AmendResults(
        int year,
        int monthId,
        Guid schemeId,
        string? command,
        AmendResultsAuditForm audit,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(audit);

        if (string.Equals(command, "cancelAudit", StringComparison.OrdinalIgnoreCase))
        {
            return RedirectToAction(nameof(Tabulations), new { year, monthId });
        }

        var page = await distributionApiClient.GetAmendResultsAsync(schemeId, cancellationToken);
        if (page is null)
        {
            return NotFound();
        }

        var values = Request.Form.Keys
            .Where(k => AmendInputPrefixes.Any(p => k.StartsWith(p, StringComparison.Ordinal)))
            .ToDictionary(k => k, k => Request.Form[k].LastOrDefault() ?? string.Empty, StringComparer.Ordinal);

        foreach (var (name, changed) in ChangedNilReturns(page))
        {
            values[name] = changed;
        }

        if (string.Equals(command, CancelCommand, StringComparison.OrdinalIgnoreCase))
        {
            return CancelAmendResults(year, monthId, schemeId, page, values, audit);
        }

        var isAuditStep = audit.IsAuditStep is true;
        var model = new AmendResultsViewModel
        {
            YearId = year,
            MonthId = monthId,
            MonthlyDistributionSchemeId = schemeId,
            Page = page,
            Values = values,
            IsAuditStep = isAuditStep,
            AuditObjectName = audit.AuditObjectName,
            PendingCommand = audit.PendingCommand,
            AuditWhy = audit.AuditWhy,
        };

        if (isAuditStep && string.IsNullOrWhiteSpace(audit.AuditWhy))
        {
            ModelState.AddModelError(nameof(AmendResultsViewModel.AuditWhy), AuditWhyRequiredMessage);
            return View(model);
        }

        var effectiveCommand = isAuditStep ? audit.PendingCommand : command;
        var result = await distributionApiClient.SaveAmendResultsAsync(
            schemeId, ToAmendResultsRequest(page, model, isAuditStep ? audit.AuditWhy : null), cancellationToken);
        if (result is null)
        {
            return NotFound();
        }

        if (result.AuditReasonRequired)
        {
            model.IsAuditStep = true;
            model.AuditObjectName = result.AuditObjectName;
            model.PendingCommand = effectiveCommand;
            return View(model);
        }

        if (!result.Success)
        {
            foreach (var (key, message) in result.FieldErrors)
            {
                ModelState.AddModelError(key, message);
            }

            return View(model);
        }

        TempData.SetNotification(NotificationType.Success, "Amended Results updated successfully.");

        // Apply keeps the user on the page with the saved values re-read; the audit step carries the
        // original command forward, so an audited Apply stays here too.
        return string.Equals(effectiveCommand, ApplyCommand, StringComparison.OrdinalIgnoreCase)
            ? RedirectToAction(nameof(AmendResults), new { year, monthId, schemeId })
            : RedirectToAction(nameof(Tabulations), new { year, monthId });
    }

    // The Nil Return box repeats on every test; as in legacy the changed one wins.
    private IEnumerable<(string Name, string Value)> ChangedNilReturns(AmendResultsResponse page)
    {
        foreach (var participant in page.Participants)
        {
            var name = Keys.NilReturn(participant.DistributionParticipantId);
            if (Request.Form[name].LastOrDefault(v => v != participant.NilReturnReason) is { } changed)
            {
                yield return (name, changed);
            }
        }
    }

    private IActionResult CancelAmendResults(
        int year, int monthId, Guid schemeId, AmendResultsResponse page, IReadOnlyDictionary<string, string> values, AmendResultsAuditForm audit)
    {
        if (!HasAmendments(page, values) || ConfirmationFlow.IsConfirmed(audit.Confirmation))
        {
            return RedirectToAction(nameof(Tabulations), new { year, monthId });
        }

        // Declined: back to the page with the edits intact, and the banner saying so.
        if (ConfirmationFlow.IsCancelled(audit.Confirmation))
        {
            return View(new AmendResultsViewModel
            {
                YearId = year,
                MonthId = monthId,
                MonthlyDistributionSchemeId = schemeId,
                Page = page,
                Values = values,
                HasUnsavedChanges = true,
            });
        }

        ViewData[BreadcrumbRouteValues.Action] = nameof(AmendResults);
        return this.ConfirmationPage(
            new ConfirmationPageViewModel
            {
                Title = DiscardTitle,
                PageTitle = "Leave Amend Results",
                Caption = page.DistributionTitle,
                Message = DiscardMessage,
                ConfirmText = "Discard changes",
                IsDestructive = true,
                CancelText = "Go back to Amend Results",
            },
            nameof(AmendResults));
    }

    // True when any posted input differs from the value the page was drawn with. Only inputs the page
    // actually rendered are posted, so anything absent is by definition unchanged.
    private static bool HasAmendments(AmendResultsResponse page, IReadOnlyDictionary<string, string> values)
    {
        bool Changed(string name, string stored) =>
            values.TryGetValue(name, out var posted) && !string.Equals(posted, stored, StringComparison.Ordinal);

        bool ScoreChanged(string name, int stored) =>
            values.TryGetValue(name, out var posted)
            && !string.Equals(posted, stored.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal);

        return page.Tests.Any(test =>
                test.IntendedMethodResults.Any(i => Changed(Keys.IntendedMethod(i.ItemId), i.Value))
                || test.IntendedResultItemResults.Any(i =>
                    Changed(Keys.IntendedResult(i.SampleId, i.ItemId, 1), i.Value)
                    || Changed(Keys.IntendedResult(i.SampleId, i.ItemId, 2), i.Value2))
                || test.ResultItems.Any(r => values.ContainsKey(Keys.Range(r.ItemId) + "_shown") && values.ContainsKey(Keys.Range(r.ItemId)) != r.IsRange))
            || page.Participants.Any(participant =>
                participant.MethodResults.Any(m => Changed(Keys.ParticipantMethod(participant.DistributionParticipantId, m.ItemId), m.Value))
                || participant.ResultItemResults.Any(r => Changed(Keys.ParticipantResult(participant.DistributionParticipantId, r.SampleId, r.ItemId), r.Value))
                || participant.CategoryResults.Any(c => ScoreChanged(Keys.CategoryScore(participant.DistributionParticipantId, c.SampleId, c.CategoryItemId), c.Score))
                || participant.Assessments.Any(a =>
                    Changed(Keys.AssessmentComment(participant.DistributionParticipantId, a.TestId, a.SampleId), a.Comment)
                    || ScoreChanged(Keys.AssessmentScore(participant.DistributionParticipantId, a.TestId, a.SampleId), a.Score))
                || Changed(Keys.NilReturn(participant.DistributionParticipantId), participant.NilReturnReason)
                || participant.Ratings.Any(r => Changed(Keys.Rating(participant.DistributionParticipantId, r.SampleId), r.RatingId.ToString())));
    }

    // Reads back every input the page rendered; anything not posted is left out (and so unchanged).
    private static AmendResultsSaveRequest ToAmendResultsRequest(AmendResultsResponse page, AmendResultsViewModel model, string? auditWhy)
    {
        string? Posted(string name) => model.Values.TryGetValue(name, out var value) ? value : null;

        int Score(string name, int stored) =>
            int.TryParse(Posted(name), NumberStyles.Integer, CultureInfo.InvariantCulture, out var score) ? score : stored;

        var intendedMethods = new List<AmendIntendedValueRequest>();
        var intendedResults = new List<AmendIntendedValueRequest>();
        var ranges = new List<AmendRangeRequest>();
        var methodResults = new List<AmendParticipantValueRequest>();
        var resultItemResults = new List<AmendParticipantValueRequest>();
        var categoryResults = new List<AmendParticipantScoreRequest>();
        var assessments = new List<AmendAssessmentRequest>();
        var nilReturns = new List<AmendNilReturnRequest>();
        var ratings = new List<AmendRatingRequest>();

        foreach (var test in page.Tests)
        {
            intendedMethods.AddRange(test.IntendedMethodResults
                .Where(i => Posted(Keys.IntendedMethod(i.ItemId)) is not null)
                .Select(i => new AmendIntendedValueRequest(i.ItemId, Guid.Empty, Posted(Keys.IntendedMethod(i.ItemId)), null)));

            intendedResults.AddRange(test.IntendedResultItemResults
                .Where(i => Posted(Keys.IntendedResult(i.SampleId, i.ItemId, 1)) is not null)
                .Select(i => new AmendIntendedValueRequest(
                    i.ItemId, i.SampleId, Posted(Keys.IntendedResult(i.SampleId, i.ItemId, 1)), Posted(Keys.IntendedResult(i.SampleId, i.ItemId, 2)))));

            ranges.AddRange(test.ResultItems
                .Where(r => Posted(Keys.Range(r.ItemId) + "_shown") is not null)
                .Select(r => new AmendRangeRequest(r.ItemId, model.IsRange(r))));
        }

        foreach (var participant in page.Participants)
        {
            var id = participant.DistributionParticipantId;

            methodResults.AddRange(participant.MethodResults
                .Where(m => Posted(Keys.ParticipantMethod(id, m.ItemId)) is not null)
                .Select(m => new AmendParticipantValueRequest(id, m.ItemId, Guid.Empty, Posted(Keys.ParticipantMethod(id, m.ItemId)))));

            resultItemResults.AddRange(participant.ResultItemResults
                .Where(r => Posted(Keys.ParticipantResult(id, r.SampleId, r.ItemId)) is not null)
                .Select(r => new AmendParticipantValueRequest(id, r.ItemId, r.SampleId, Posted(Keys.ParticipantResult(id, r.SampleId, r.ItemId)))));

            categoryResults.AddRange(participant.CategoryResults
                .Select(c => new AmendParticipantScoreRequest(id, c.CategoryItemId, c.SampleId, Score(Keys.CategoryScore(id, c.SampleId, c.CategoryItemId), c.Score))));

            assessments.AddRange(participant.Assessments
                .Where(a => Posted(Keys.AssessmentComment(id, a.TestId, a.SampleId)) is not null)
                .Select(a => new AmendAssessmentRequest(
                    id, a.TestId, a.SampleId, Posted(Keys.AssessmentComment(id, a.TestId, a.SampleId)), Score(Keys.AssessmentScore(id, a.TestId, a.SampleId), a.Score))));

            if (Posted(Keys.NilReturn(id)) is { } reason)
            {
                nilReturns.Add(new AmendNilReturnRequest(id, reason));
            }

            ratings.AddRange(participant.Ratings
                .Where(r => Guid.TryParse(Posted(Keys.Rating(id, r.SampleId)), out _))
                .Select(r => new AmendRatingRequest(id, r.SampleId, Guid.Parse(Posted(Keys.Rating(id, r.SampleId))!))));
        }

        return new AmendResultsSaveRequest(
            intendedMethods, intendedResults, ranges, methodResults, resultItemResults, categoryResults, assessments, nilReturns, ratings, auditWhy);
    }
}
