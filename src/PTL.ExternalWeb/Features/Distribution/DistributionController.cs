using System.Diagnostics.CodeAnalysis;
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
    private const string SubmitCommand = "submit";
    private const string SaveFailedKey = "SaveFailed";
    private const string ExternalAuditReason = "External participant results updated.";
    private static readonly string[] ValidSortColumns = ["reference", "schemeCode", "schemeName", "distributionDate"];
    private static readonly string[] ValidSortDirections = ["asc", "desc"];
    private static readonly string[] ValidCurrentSortColumns = [.. ValidSortColumns, "deadlineDate", "status"];

    // Legacy DistributionsCurrent has no default sort: rows keep the stored-procedure order until a column is chosen.
    // Legacy pages the grid at 15 rows by default.
    public async Task<IActionResult> Current(
        string? sortBy = null,
        string? sortDir = null,
        int page = 1,
        int pageSize = ReferenceSchemeListQuery.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = PaginationModel.AvailablePageSizes.Contains(pageSize) ? pageSize : ReferenceSchemeListQuery.DefaultPageSize;

        var participantId = User.GetParticipantId();
        if (participantId is null)
        {
            return View(new CurrentDistributionsViewModel { Page = page, PageSize = pageSize });
        }

        var distributions = await distributionApiClient.GetCurrentDistributionsAsync(participantId.Value, cancellationToken);

        var rows = distributions
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
            .ToList();

        var column = sortBy is not null && ValidCurrentSortColumns.Contains(sortBy) ? sortBy : string.Empty;
        var direction = sortDir is not null && ValidSortDirections.Contains(sortDir) ? sortDir : "asc";

        var sorted = column.Length == 0 ? rows : SortCurrent(rows, column, direction);

        return View(new CurrentDistributionsViewModel
        {
            Distributions = [.. sorted.Skip((page - 1) * pageSize).Take(pageSize)],
            TotalCount = sorted.Count,
            Page = page,
            PageSize = pageSize,
            SortBy = column,
            SortDirection = direction,
        });
    }

    // The participant comes from the signed-in user, never from the URL or form, so one participant
    // cannot open or overwrite another's results (same rule as Results).
    [HttpGet]
    public async Task<IActionResult> Entry(Guid schemeId, CancellationToken cancellationToken)
    {
        var page = await GetOwnResultsAsync(schemeId, cancellationToken);
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

        var own = model.MonthlyDistributionSchemeId is { } schemeId
            ? await GetOwnResultsAsync(schemeId, cancellationToken)
            : null;
        if (own is null)
        {
            return RedirectToAction(nameof(Current));
        }

        // Legacy raises the deadline rule on its own, ahead of any format errors.
        if (own.IsReadOnly)
        {
            ViewData[SaveFailedKey] = true;
            return View(RehydrateResultsEntry(model, own));
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        // Legacy's participant rule rejects a drop-down value that is not one of its options; the shared save endpoint does not.
        var unlisted = UnlistedDropdownValues(own, model);
        if (unlisted.Count > 0)
        {
            var invalidOptions = RehydrateResultsEntry(model, own);
            ApplyResultsEntryFieldErrors(invalidOptions, unlisted);
            return View(invalidOptions);
        }

        var request = new ActualResultsSaveRequest(
            [.. model.Tests.SelectMany(t => t.Samples).SelectMany(s => s.Values)
                .Select(v => new ActualResultsSampleValueRequest(v.SampleResultItemId, v.Result ?? string.Empty))],
            [.. model.Tests.SelectMany(t => t.MethodItems).Select(m => new ActualResultsMethodItemRequest(m.TestMethodItemId, m.Result ?? string.Empty))],
            ExternalAuditReason,
            string.Equals(command, SubmitCommand, StringComparison.OrdinalIgnoreCase));

        var result = await distributionApiClient.SaveActualResultsAsync(
            own.MonthlyDistributionSchemeId,
            own.DistributionParticipantId,
            request,
            cancellationToken);

        if (result is null)
        {
            TempData.SetNotification(NotificationType.Error, "Results could not be saved. Please try again later.");
            return View(RehydrateResultsEntry(model, own));
        }

        if (!result.Success)
        {
            var invalid = RehydrateResultsEntry(model, own);
            ViewData[SaveFailedKey] = result.DeadlinePassed;
            ApplyResultsEntryFieldErrors(invalid, result.FieldErrorsByItemId);
            return View(invalid);
        }

        TempData.SetNotification(NotificationType.Success, request.Submitted == true ? "Results submitted successfully." : "Results saved successfully.");
        return RedirectToAction(nameof(Current));
    }

    // Legacy DistributionsPrevious.aspx - Year/Month dropdowns are exact matches, the 3 text
    // criteria are "contains" (Scheme Name matches when every typed word is contained), mirroring
    // DistributionsPast.ExecuteSearch. Default sort is DistributionDate descending.
    public async Task<IActionResult> Previous([FromQuery] ReferenceSchemeListQuery query, CancellationToken cancellationToken = default)
    {
        var sortBy = query.SortBy is not null && ValidSortColumns.Contains(query.SortBy) ? query.SortBy : "distributionDate";
        var sortDir = query.SortDir is not null && ValidSortDirections.Contains(query.SortDir) ? query.SortDir : "desc";
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
            paged, sorted.Count, page, pageSize, sortBy, sortDir, query.Year, query.Month, query.DistributionReference, query.SchemeCode, query.SchemeName, YearOptionsFor(history.Distributions)));
    }

    // Legacy ResultsView.aspx - read-only, no Save/Submit/Edit/Delete. Reuses the same
    // actual-results data as InternalWeb's Actual Results screen, resolved against the signed-in
    // participant server-side.
    public async Task<IActionResult> Results(Guid monthlyDistributionSchemeId, string? returnUrl, CancellationToken cancellationToken)
    {
        var participantId = User.GetParticipantId();
        if (participantId is null)
        {
            return NotFound();
        }

        var page = await distributionApiClient.GetActualResultsForParticipantAsync(monthlyDistributionSchemeId, participantId.Value, cancellationToken);
        if (page is null)
        {
            return NotFound();
        }

        ViewData["ListReturnUrl"] = ListReturnUrl.Validate(returnUrl, ListReturnUrl.DistributionPrevious);
        return View(page);
    }

    private async Task<ActualResultsResponse?> GetOwnResultsAsync(Guid monthlyDistributionSchemeId, CancellationToken cancellationToken)
    {
        var participantId = User.GetParticipantId();
        return participantId is null
            ? null
            : await distributionApiClient.GetActualResultsForParticipantAsync(monthlyDistributionSchemeId, participantId.Value, cancellationToken);
    }

    private static DistributionResultsEntryViewModel RehydrateResultsEntry(DistributionResultsEntryViewModel posted, ActualResultsResponse page)
    {
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

    private static Dictionary<Guid, string> UnlistedDropdownValues(ActualResultsResponse own, DistributionResultsEntryViewModel posted)
    {
        var resultItems = own.Tests.SelectMany(t => t.ResultItems).DistinctBy(i => i.TestResultItemId).ToDictionary(i => i.TestResultItemId);
        var itemOfSampleValue = own.Tests.SelectMany(t => t.Samples).SelectMany(s => s.Values)
            .DistinctBy(v => v.SampleResultItemId).ToDictionary(v => v.SampleResultItemId, v => v.TestResultItemId);
        var methodItems = own.Tests.SelectMany(t => t.MethodItems).DistinctBy(m => m.TestMethodItemId).ToDictionary(m => m.TestMethodItemId);

        var errors = new Dictionary<Guid, string>();

        foreach (var value in posted.Tests.SelectMany(t => t.Samples).SelectMany(s => s.Values))
        {
            if (itemOfSampleValue.TryGetValue(value.SampleResultItemId, out var itemId)
                && resultItems.TryGetValue(itemId, out var item)
                && IsUnlistedOption(item.FormatString, value.Result))
            {
                errors[value.SampleResultItemId] = $"{item.Name} must be one of the listed options";
            }
        }

        foreach (var method in posted.Tests.SelectMany(t => t.MethodItems))
        {
            if (methodItems.TryGetValue(method.TestMethodItemId, out var item) && IsUnlistedOption(item.FormatString, method.Result))
            {
                errors[method.TestMethodItemId] = $"{item.Name} must be one of the listed options";
            }
        }

        return errors;
    }

    private static bool IsUnlistedOption(string formatString, string? value) =>
        !string.IsNullOrEmpty(value)
        && !formatString.StartsWith('^')
        && !formatString.Split('|').Contains(value, StringComparer.Ordinal);

    private void ApplyResultsEntryFieldErrors(
        DistributionResultsEntryViewModel model,
        IReadOnlyDictionary<Guid, string> fieldErrorsByItemId)
    {
        foreach (var (itemId, message) in fieldErrorsByItemId)
        {
            if (TryFindMethodField(model, itemId, out var methodPath, out var methodName))
            {
                ModelState.AddModelError(methodPath, LegacyFormatMessage(methodName, message));
                continue;
            }

            if (TryFindSampleField(model, itemId, out var samplePath, out var itemName))
            {
                ModelState.AddModelError(samplePath, LegacyFormatMessage(itemName, message));
                continue;
            }

            ModelState.AddModelError(string.Empty, message);
        }
    }

    // Legacy ResultsEntry names the item: "{Item} must be in format {format}".
    private static string LegacyFormatMessage(string itemName, string apiMessage)
    {
        const string prefix = "Input must be of format:";
        return apiMessage.StartsWith(prefix, StringComparison.Ordinal)
            ? $"{itemName} must be in format {apiMessage[prefix.Length..].Trim().Replace("dd/MM/yyyy", DistributionResultsEntryFormatMessages.DateFormat, StringComparison.Ordinal)}"
            : apiMessage;
    }

    private static bool TryFindMethodField(
        DistributionResultsEntryViewModel model,
        Guid itemId,
        [NotNullWhen(true)] out string? path,
        [NotNullWhen(true)] out string? name)
    {
        for (var t = 0; t < model.Tests.Count; t++)
        {
            for (var m = 0; m < model.Tests[t].MethodItems.Count; m++)
            {
                if (model.Tests[t].MethodItems[m].TestMethodItemId == itemId)
                {
                    path = $"Tests[{t}].MethodItems[{m}].Result";
                    name = model.Tests[t].MethodItems[m].Name;
                    return true;
                }
            }
        }

        path = null;
        name = null;
        return false;
    }

    private static bool TryFindSampleField(
        DistributionResultsEntryViewModel model,
        Guid itemId,
        [NotNullWhen(true)] out string? path,
        [NotNullWhen(true)] out string? name)
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
                        var testResultItemId = model.Tests[t].Samples[s].Values[v].TestResultItemId;
                        name = model.Tests[t].ResultItems.FirstOrDefault(r => r.TestResultItemId == testResultItemId)?.Name ?? string.Empty;
                        return true;
                    }
                }
            }
        }

        path = null;
        name = null;
        return false;
    }

    private static DistributionResultsEntryViewModel ToResultsEntryViewModel(ActualResultsResponse page) => new()
    {
        MonthlyDistributionSchemeId = page.MonthlyDistributionSchemeId,
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

    private static List<int> YearOptionsFor(IReadOnlyList<ParticipantPastDistributionResponse> distributions) =>
        [.. distributions.SelectMany(d => new[] { d.YearId, d.DistributionDate.Year }).Distinct().Order()];

    private static List<ParticipantPastDistributionResponse> Filter(
        IReadOnlyList<ParticipantPastDistributionResponse> distributions,
        int? year, int? month, string? distributionReference, string? schemeCode, string? schemeName) =>
        [.. distributions.Where(d =>
            (year is null or 0 || d.YearId == year || d.DistributionDate.Year == year)
            && (month is null or 0 || d.MonthId == month || d.DistributionDate.Month == month)
            && Contains(d.DistributionReference, distributionReference)
            && Contains(d.SchemeCode, schemeCode)
            && ContainsAllWords(d.SchemeName, schemeName))];

    private static bool Contains(string value, string? term) =>
        string.IsNullOrWhiteSpace(term) || value.Contains(term, StringComparison.OrdinalIgnoreCase);

    private static bool ContainsAllWords(string value, string? term) =>
        string.IsNullOrWhiteSpace(term)
        || term.Split(' ', StringSplitOptions.RemoveEmptyEntries).All(word => value.Contains(word, StringComparison.OrdinalIgnoreCase));

    private static List<CurrentDistributionRowViewModel> SortCurrent(
        IReadOnlyList<CurrentDistributionRowViewModel> rows, string sortBy, string sortDir)
    {
        IOrderedEnumerable<CurrentDistributionRowViewModel> Order<TKey>(Func<CurrentDistributionRowViewModel, TKey> keySelector) =>
            sortDir == "desc" ? rows.OrderByDescending(keySelector) : rows.OrderBy(keySelector);

        var ordered = sortBy switch
        {
            "reference" => Order(d => d.DistributionReference),
            "schemeCode" => Order(d => d.SchemeCode),
            "schemeName" => Order(d => d.SchemeName),
            "deadlineDate" => Order(d => d.DeadlineDate),
            "status" => Order(d => d.HasResults),
            _ => Order(d => d.DistributionDate),
        };

        return [.. ordered];
    }

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
