using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PTL.ApiClient.Distribution;
using PTL.Contracts.Distribution;
using PTL.ExternalWeb.Authorization;
using PTL.ExternalWeb.Features.Account;
using PTL.ExternalWeb.ViewSupport;
using PTL.SharedUI.Notifications;
using PTL.SharedUI.Pagination;

namespace PTL.ExternalWeb.Features.Distribution;

// Legacy DistributionsCurrent.aspx/ResultsEntry.aspx and DistributionsPrevious.aspx.
[Authorize(Policy = ExternalPolicies.Participant)]
public sealed class DistributionController(IDistributionApiClient distributionApiClient) : Controller
{
    private const string CancelCommand = "cancel";
    private const string ExternalAuditReason = "External participant results updated.";
    private static readonly string[] ValidSortColumns = ["reference", "schemeCode", "schemeName", "distributionDate"];
    private static readonly string[] ValidSortDirections = ["asc", "desc"];

    public async Task<IActionResult> Current(CancellationToken cancellationToken)
    {
        var participantId = User.GetParticipantId();
        if (participantId is null)
        {
            return View(new CurrentDistributionsViewModel());
        }

        var distributions = await distributionApiClient.GetCurrentDistributionsAsync(participantId.Value, cancellationToken);

        return View(new CurrentDistributionsViewModel
        {
            Distributions = distributions
                .Select(d => new CurrentDistributionRowViewModel
                {
                    MonthlyDistributionSchemeId = d.MonthlyDistributionSchemeId,
                    DistributionParticipantId = d.DistributionParticipantId,
                    DistributionReference = d.DistributionReference,
                    SchemeCode = d.SchemeCode,
                    SchemeName = d.SchemeName,
                    DistributionDate = d.DistributionDate,
                    DeadlineDate = d.DeadlineDate,
                    HasResults = d.HasResults,
                    Submitted = d.Submitted,
                })
                .ToList(),
        });
    }

    [HttpGet]
    public async Task<IActionResult> Entry(
        Guid schemeId,
        Guid participantId,
        CancellationToken cancellationToken)
    {
        var page = await distributionApiClient.GetActualResultsAsync(schemeId, participantId, cancellationToken);
        return page is null ? RedirectToAction(nameof(Current)) : View(ToResultsEntryViewModel(page));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestFormLimits(ValueCountLimit = 20000)]
    public async Task<IActionResult> Entry(
        DistributionResultsEntryViewModel model,
        string? command,
        CancellationToken cancellationToken)
    {
        if (string.Equals(command, CancelCommand, StringComparison.OrdinalIgnoreCase))
        {
            return RedirectToAction(nameof(Current));
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var request = new ActualResultsSaveRequest(
            [.. model.Tests.SelectMany(t => t.Samples).SelectMany(s => s.Values)
                .Select(v => new ActualResultsSampleValueRequest(v.SampleResultItemId, v.Result ?? string.Empty))],
            [.. model.Tests.SelectMany(t => t.MethodItems).Select(m => new ActualResultsMethodItemRequest(m.TestMethodItemId, m.Result ?? string.Empty))],
            ExternalAuditReason);

        var result = await distributionApiClient.SaveActualResultsAsync(
            model.MonthlyDistributionSchemeId,
            model.DistributionParticipantId,
            request,
            cancellationToken);

        if (result is null)
        {
            TempData.SetNotification(NotificationType.Error, "Results could not be saved. Please try again later.");
            return View(await RehydrateResultsEntryAsync(distributionApiClient, model, cancellationToken));
        }

        if (!result.Success)
        {
            var invalid = await RehydrateResultsEntryAsync(distributionApiClient, model, cancellationToken);
            invalid.SaveFailed = result.DeadlinePassed;
            ApplyResultsEntryFieldErrors(invalid, result.FieldErrorsByItemId);
            return View(invalid);
        }

        TempData.SetNotification(NotificationType.Success, "Results saved successfully.");
        return RedirectToAction(nameof(Current));
    }

    // Legacy DistributionsPrevious.aspx - Year/Month dropdowns are exact matches, the 3 text
    // criteria are "contains" (Scheme Name matches when every typed word is contained), mirroring
    // DistributionsPast.ExecuteSearch. Default sort is DistributionDate descending.
    public async Task<IActionResult> Previous([FromQuery] ReferenceSchemeListQuery query, CancellationToken cancellationToken = default)
    {
        var sortBy = ValidSortColumns.Contains(query.SortBy) ? query.SortBy : "distributionDate";
        var sortDir = ValidSortDirections.Contains(query.SortDir) ? query.SortDir : "desc";
        var page = Math.Max(query.Page, 1);
        var pageSize = PaginationModel.AvailablePageSizes.Contains(query.PageSize) ? query.PageSize : ReferenceSchemeListQuery.DefaultPageSize;

        var participantId = User.GetParticipantId();
        if (participantId is null)
        {
            return View(new DistributionPreviousListViewModel([], 0, page, pageSize, sortBy, sortDir, query.Year, query.Month, query.DistributionReference, query.SchemeCode, query.SchemeName));
        }

        var history = await distributionApiClient.GetParticipantDistributionHistoryAsync(participantId.Value, cancellationToken);

        var filtered = Filter(history.Distributions, query.Year, query.Month, query.DistributionReference, query.SchemeCode, query.SchemeName);
        var sorted = Sort(filtered, sortBy, sortDir);
        var paged = sorted.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return View(new DistributionPreviousListViewModel(
            paged, sorted.Count, page, pageSize, sortBy, sortDir, query.Year, query.Month, query.DistributionReference, query.SchemeCode, query.SchemeName));
    }

    // Legacy ResultsView.aspx - read-only, no Save/Submit/Edit/Delete. Reuses the same
    // actual-results data as InternalWeb's Actual Results screen, resolved against the signed-in
    // participant server-side.
    public async Task<IActionResult> Results(Guid monthlyDistributionSchemeId, CancellationToken cancellationToken)
    {
        var participantId = User.GetParticipantId();
        if (participantId is null)
        {
            return NotFound();
        }

        var page = await distributionApiClient.GetActualResultsForParticipantAsync(monthlyDistributionSchemeId, participantId.Value, cancellationToken);
        return page is null ? NotFound() : View(page);
    }

    private static async Task<DistributionResultsEntryViewModel> RehydrateResultsEntryAsync(
        IDistributionApiClient distributionApiClient,
        DistributionResultsEntryViewModel posted,
        CancellationToken cancellationToken)
    {
        var page = await distributionApiClient.GetActualResultsAsync(posted.MonthlyDistributionSchemeId, posted.DistributionParticipantId, cancellationToken);
        if (page is null)
        {
            return posted;
        }

        var model = ToResultsEntryViewModel(page);

        var postedSampleValues = posted.Tests.SelectMany(t => t.Samples).SelectMany(s => s.Values)
            .GroupBy(v => v.SampleResultItemId)
            .ToDictionary(g => g.Key, g => g.Last().Result);

        var postedMethodValues = posted.Tests.SelectMany(t => t.MethodItems)
            .GroupBy(m => m.TestMethodItemId)
            .ToDictionary(g => g.Key, g => g.Last().Result);

        foreach (var sampleValue in model.Tests.SelectMany(t => t.Samples).SelectMany(s => s.Values))
        {
            if (postedSampleValues.TryGetValue(sampleValue.SampleResultItemId, out var postedValue))
            {
                sampleValue.Result = postedValue;
            }
        }

        foreach (var methodItem in model.Tests.SelectMany(t => t.MethodItems))
        {
            if (postedMethodValues.TryGetValue(methodItem.TestMethodItemId, out var postedValue))
            {
                methodItem.Result = postedValue;
            }
        }

        return model;
    }

    private void ApplyResultsEntryFieldErrors(
        DistributionResultsEntryViewModel model,
        IReadOnlyDictionary<Guid, string> fieldErrorsByItemId)
    {
        foreach (var (itemId, message) in fieldErrorsByItemId)
        {
            if (TryFindMethodField(model, itemId, out var methodPath))
            {
                ModelState.AddModelError(methodPath, message);
                continue;
            }

            if (TryFindSampleField(model, itemId, out var samplePath))
            {
                ModelState.AddModelError(samplePath, message);
                continue;
            }

            ModelState.AddModelError(string.Empty, message);
        }
    }

    private static bool TryFindMethodField(
        DistributionResultsEntryViewModel model,
        Guid itemId,
        out string? path)
    {
        for (var t = 0; t < model.Tests.Count; t++)
        {
            for (var m = 0; m < model.Tests[t].MethodItems.Count; m++)
            {
                if (model.Tests[t].MethodItems[m].TestMethodItemId == itemId)
                {
                    path = $"Tests[{t}].MethodItems[{m}].Result";
                    return true;
                }
            }
        }

        path = null;
        return false;
    }

    private static bool TryFindSampleField(
        DistributionResultsEntryViewModel model,
        Guid itemId,
        out string? path)
    {
        for (var t = 0; t < model.Tests.Count; t++)
        {
            for (var s = 0; s < model.Tests[t].Samples.Count; s++)
            {
                for (var v = 0; v < model.Tests[t].Samples[s].Values.Count; v++)
                {
                    if (model.Tests[t].Samples[s].Values[v].SampleResultItemId == itemId)
                    {
                        path = $"Tests[{t}].Samples[{s}].Values[{v}].Result";
                        return true;
                    }
                }
            }
        }

        path = null;
        return false;
    }

    private static DistributionResultsEntryViewModel ToResultsEntryViewModel(ActualResultsResponse page) => new()
    {
        MonthlyDistributionSchemeId = page.MonthlyDistributionSchemeId,
        DistributionParticipantId = page.DistributionParticipantId,
        Heading = page.Heading,
        Tests =
        [
            .. page.Tests.Select(test => new DistributionResultsEntryTestViewModel
            {
                TestId = test.TestId,
                TestName = test.TestName,
                ResultItems =
                [
                    .. test.ResultItems.Select(i => new DistributionResultsEntryResultItemViewModel
                    {
                        TestResultItemId = i.TestResultItemId,
                        Name = i.Name,
                        FormatString = i.FormatString,
                        ReturnValueType = i.ReturnValueType,
                    })
                ],
                MethodItems =
                [
                    .. test.MethodItems.Select(i => new DistributionResultsEntryMethodItemViewModel
                    {
                        TestMethodItemId = i.TestMethodItemId,
                        Name = i.Name,
                        FormatString = i.FormatString,
                        ReturnValueType = i.ReturnValueType,
                        Result = i.Result,
                    })
                ],
                Samples =
                [
                    .. test.Samples.Select(s => new DistributionResultsEntrySampleRowViewModel
                    {
                        SampleId = s.SampleId,
                        SampleNumber = s.SampleNumber,
                        Values =
                        [
                            .. s.Values.Select(v => new DistributionResultsEntrySampleValueViewModel
                            {
                                SampleResultItemId = v.SampleResultItemId,
                                TestResultItemId = v.TestResultItemId,
                                Result = v.Result,
                            })
                        ],
                    })
                ],
            })
        ],
    };

    private static List<ParticipantPastDistributionResponse> Filter(
        IReadOnlyList<ParticipantPastDistributionResponse> distributions,
        int? year, int? month, string? distributionReference, string? schemeCode, string? schemeName) =>
        [.. distributions.Where(d =>
            (year is null or 0 || d.YearId == year)
            && (month is null or 0 || d.MonthId == month)
            && Contains(d.DistributionReference, distributionReference)
            && Contains(d.SchemeCode, schemeCode)
            && ContainsAllWords(d.SchemeName, schemeName))];

    private static bool Contains(string value, string? term) =>
        string.IsNullOrWhiteSpace(term) || value.Contains(term, StringComparison.OrdinalIgnoreCase);

    private static bool ContainsAllWords(string value, string? term) =>
        string.IsNullOrWhiteSpace(term)
        || term.Split(' ', StringSplitOptions.RemoveEmptyEntries).All(word => value.Contains(word, StringComparison.OrdinalIgnoreCase));

    private static List<ParticipantPastDistributionResponse> Sort(
        IReadOnlyList<ParticipantPastDistributionResponse> distributions, string sortBy, string sortDir)
    {
        IOrderedEnumerable<ParticipantPastDistributionResponse> Order<TKey>(Func<ParticipantPastDistributionResponse, TKey> keySelector) =>
            sortDir == "desc" ? distributions.OrderByDescending(keySelector) : distributions.OrderBy(keySelector);

        var ordered = sortBy switch
        {
            "reference" => Order(d => d.DistributionReference),
            "schemeCode" => Order(d => d.SchemeCode),
            "schemeName" => Order(d => d.SchemeName),
            _ => Order(d => d.DistributionDate),
        };

        return [.. ordered];
    }
}
