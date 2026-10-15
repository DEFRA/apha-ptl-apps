using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using PTL.Contracts.Distribution;
using Keys = PTL.Contracts.Distribution.AmendResultsFieldKeys;

namespace PTL.InternalWeb.Features.Distribution;

// Amend Results - legacy Scheduling/AmendResults.aspx, reached from the Tabulations page's
// "Amend Results" action. Save and Cancel return to Tabulations (legacy Apply is not carried over).
public sealed partial class DistributionController
{
    private const string AuditWhyRequiredMessage = "The reason for these changes must be specified";

    private static readonly string[] AmendInputPrefixes = ["im_", "ir1_", "ir2_", "pm_", "pr_", "ac_", "as_", "pc_", "rr_", "rg_", "nr_"];

    [HttpGet]
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
    public async Task<IActionResult> AmendResults(
        int year,
        int monthId,
        Guid schemeId,
        string? command,
        bool isAuditStep,
        string? pendingCommand,
        string? auditObjectName,
        string? auditWhy,
        CancellationToken cancellationToken)
    {
        if (string.Equals(command, CancelCommand, StringComparison.OrdinalIgnoreCase))
        {
            return RedirectToAction(nameof(Tabulations), new { year, monthId });
        }

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

        // The Nil Return box repeats on every test; as in legacy the changed one wins.
        foreach (var participant in page.Participants)
        {
            var name = Keys.NilReturn(participant.DistributionParticipantId);
            if (Request.Form[name].LastOrDefault(v => v != participant.NilReturnReason) is { } changed)
            {
                values[name] = changed;
            }
        }

        var model = new AmendResultsViewModel
        {
            YearId = year,
            MonthId = monthId,
            MonthlyDistributionSchemeId = schemeId,
            Page = page,
            Values = values,
            IsAuditStep = isAuditStep,
            AuditObjectName = auditObjectName,
            PendingCommand = pendingCommand,
            AuditWhy = auditWhy,
        };

        if (isAuditStep && string.IsNullOrWhiteSpace(auditWhy))
        {
            ModelState.AddModelError(nameof(AmendResultsViewModel.AuditWhy), AuditWhyRequiredMessage);
            return View(model);
        }

        var effectiveCommand = isAuditStep ? pendingCommand : command;
        var result = await distributionApiClient.SaveAmendResultsAsync(
            schemeId, ToAmendResultsRequest(page, model, isAuditStep ? auditWhy : null), cancellationToken);
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

        return RedirectToAction(nameof(Tabulations), new { year, monthId });
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
