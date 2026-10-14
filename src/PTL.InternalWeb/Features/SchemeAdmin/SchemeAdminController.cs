using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using PTL.ApiClient;
using PTL.Contracts.CategoryItemType;
using PTL.Contracts.CriterionItemType;
using PTL.Contracts.Lookup;
using PTL.Contracts.TestType;

namespace PTL.InternalWeb.Features.SchemeAdmin;

// Authentication/authorization are out of scope for this phase - same convention as
// Features/Scheme/SchemeController.cs, which this "Manage Schemes" sub-area sits alongside.
public class SchemeAdminController(ICategoryItemTypeApiClient categoryItemTypeApiClient, ITestTypeApiClient testTypeApiClient, ICriterionItemTypeApiClient criterionItemTypeApiClient, ILookupApiClient lookupApiClient, ILogger<SchemeAdminController> logger) : Controller
{
    private static readonly Action<ILogger, Guid, Exception?> LogCategoryItemTypeRemoveBlockedMessage =
        LoggerMessage.Define<Guid>(
            LogLevel.Information,
            new EventId(1, nameof(LogCategoryItemTypeRemoveBlockedMessage)),
            "Category item type {CategoryItemTypeId} removal was blocked: still in use");

    private static readonly Action<ILogger, Guid, Exception?> LogTestTypeRemoveBlockedMessage =
        LoggerMessage.Define<Guid>(
            LogLevel.Information,
            new EventId(2, nameof(LogTestTypeRemoveBlockedMessage)),
            "Test type {TestTypeId} removal was blocked: still in use");

    private static readonly Action<ILogger, Guid, Exception?> LogCriterionItemTypeRemoveBlockedMessage =
        LoggerMessage.Define<Guid>(
            LogLevel.Information,
            new EventId(3, nameof(LogCriterionItemTypeRemoveBlockedMessage)),
            "Criterion item type {CriterionItemTypeId} removal was blocked: still in use");

    private const string NoLongerInUseSaveFailedMessage = "Could not save all 'No Longer in Use' changes because the service did not respond. Please try again.";

    // Distinguishes a genuine downstream API failure (e.g. the API process stopped mid-request,
    // surfacing as HttpRequestException/TaskCanceledException/IOException) from the caller's own
    // request being aborted (requestAborted already cancelled), which should propagate normally
    // rather than trying to render a now-pointless response.
    private static bool IsTransientApiFailure(Exception ex, CancellationToken requestAborted) =>
        !requestAborted.IsCancellationRequested && ex is HttpRequestException or TaskCanceledException or IOException;

    public async Task<IActionResult> CategoryItemType(int? yearId, Guid? editId, Guid? removeConfirmId, int page = 1, CancellationToken cancellationToken = default) =>
        View(await BuildCategoryItemTypeViewModelAsync(
            new CategoryItemTypeViewModelRequest(yearId, editId, page, Message: null, MessageIsError: false, AddValues: null, EditValues: null, removeConfirmId),
            cancellationToken));

    // Matches legacy ButtonAddCategory_Click - Name is required; the new item always starts in
    // whichever year is currently selected in the dropdown.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CategoryItemTypeAdd([Bind(Prefix = "Add")] CategoryItemTypeAddViewModel model, int yearId, int page, CancellationToken cancellationToken)
    {
        if (ModelState.IsValid)
        {
            var result = await categoryItemTypeApiClient.CreateAsync(new CategoryItemTypeSaveRequest(model.Name, model.NoLongerInUse, yearId), cancellationToken);
            if (result.Success)
            {
                return RedirectToAction(nameof(CategoryItemType), new { yearId, page });
            }

            foreach (var (field, messages) in result.FieldErrors)
            {
                foreach (var errorMessage in messages)
                {
                    ModelState.AddModelError($"Add.{field}", errorMessage);
                }
            }
        }

        return View(nameof(CategoryItemType), await BuildCategoryItemTypeViewModelAsync(
            new CategoryItemTypeViewModelRequest(yearId, EditId: null, page, Message: null, MessageIsError: false, model, EditValues: null),
            cancellationToken));
    }

    // Only the row matching model.CategoryItemTypeId is ever in edit mode when this posts -
    // matches legacy GridViewCategoryItemType_RowUpdating, which only acts on the row with
    // EditIndex set. Blocked (in-use) edits surface the legacy warning message as a notification
    // banner rather than a JS alert.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CategoryItemTypeSave([Bind(Prefix = "Edit")] CategoryItemTypeEditViewModel model, int yearId, int page, CancellationToken cancellationToken)
    {
        if (ModelState.IsValid)
        {
            var result = await categoryItemTypeApiClient.UpdateAsync(model.CategoryItemTypeId!.Value, new CategoryItemTypeSaveRequest(model.Name, model.NoLongerInUse, yearId), cancellationToken);
            if (result.Success)
            {
                return RedirectToAction(nameof(CategoryItemType), new { yearId, page });
            }

            if (result.FieldErrors.TryGetValue(string.Empty, out var conflictMessages))
            {
                return View(nameof(CategoryItemType), await BuildCategoryItemTypeViewModelAsync(
                    new CategoryItemTypeViewModelRequest(yearId, model.CategoryItemTypeId, page, conflictMessages[0], MessageIsError: true, AddValues: null, EditValues: null),
                    cancellationToken));
            }

            foreach (var (field, messages) in result.FieldErrors)
            {
                foreach (var errorMessage in messages)
                {
                    ModelState.AddModelError($"Edit.{field}", errorMessage);
                }
            }
        }

        return View(nameof(CategoryItemType), await BuildCategoryItemTypeViewModelAsync(
            new CategoryItemTypeViewModelRequest(yearId, model.CategoryItemTypeId, page, Message: null, MessageIsError: false, AddValues: null, model),
            cancellationToken));
    }

    // "No Longer in Use" is saved for every row on the current page in one combined submit -
    // matches legacy ButtonSave_Click's explicit comment that this flag can be changed on any
    // item, even one otherwise blocked from editing because it is still in use, and matches the
    // standard GOV.UK Design System checkboxes pattern (one group of checkboxes, one submit)
    // rather than firing a separate request per row. pageCategoryItemTypeIds lists every row on
    // the page unconditionally (so an unticked row is still set back to false); only the ticked
    // rows appear in checkedCategoryItemTypeIds (plain HTML never submits an unchecked checkbox).
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CategoryItemTypeSaveAllNoLongerInUse(List<Guid>? pageCategoryItemTypeIds, List<Guid>? checkedCategoryItemTypeIds, int yearId, int page, CancellationToken cancellationToken)
    {
        var checkedIds = checkedCategoryItemTypeIds ?? [];
        try
        {
            foreach (var id in pageCategoryItemTypeIds ?? [])
            {
                await categoryItemTypeApiClient.SetNoLongerInUseAsync(id, new CategoryItemTypeNoLongerInUseRequest(checkedIds.Contains(id), yearId), cancellationToken);
            }
        }
        catch (Exception ex) when (IsTransientApiFailure(ex, cancellationToken))
        {
            return View(nameof(CategoryItemType), await BuildCategoryItemTypeViewModelAsync(
                new CategoryItemTypeViewModelRequest(yearId, EditId: null, page, NoLongerInUseSaveFailedMessage, MessageIsError: true, AddValues: null, EditValues: null),
                cancellationToken));
        }

        return RedirectToAction(nameof(CategoryItemType), new { yearId, page });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CategoryItemTypeRemove(Guid categoryItemTypeId, int yearId, int page, CancellationToken cancellationToken)
    {
        var result = await categoryItemTypeApiClient.DeleteAsync(categoryItemTypeId, cancellationToken);
        if (!result.Success)
        {
            LogCategoryItemTypeRemoveBlockedMessage(logger, categoryItemTypeId, null);
        }

        return View(nameof(CategoryItemType), await BuildCategoryItemTypeViewModelAsync(
            new CategoryItemTypeViewModelRequest(yearId, EditId: null, page, result.Message, !result.Success, AddValues: null, EditValues: null),
            cancellationToken));
    }

    // Bundles BuildCategoryItemTypeViewModelAsync's render-state inputs into one object - the
    // method itself was flagged for exceeding Sonar's 7-parameter limit (S107).
    private sealed record CategoryItemTypeViewModelRequest(
        int? YearId,
        Guid? EditId,
        int Page,
        string? Message,
        bool MessageIsError,
        CategoryItemTypeAddViewModel? AddValues,
        CategoryItemTypeEditViewModel? EditValues,
        Guid? RemoveConfirmId = null);

    private async Task<CategoryItemTypeViewModel> BuildCategoryItemTypeViewModelAsync(
        CategoryItemTypeViewModelRequest request,
        CancellationToken cancellationToken)
    {
        var yearId = request.YearId;
        var editId = request.EditId;
        var page = request.Page;
        var message = request.Message;
        var messageIsError = request.MessageIsError;
        var addValues = request.AddValues;
        var editValues = request.EditValues;
        var removeConfirmId = request.RemoveConfirmId;

        var years = await lookupApiClient.GetCurrentYearsAsync(cancellationToken);
        var currentYearId = years.Count > 0 ? years[0].YearId : (int?)null;
        var selectedYearId = ResolveSelectedYearId(yearId, years, currentYearId);

        var items = await categoryItemTypeApiClient.GetByYearAsync(selectedYearId, cancellationToken);

        // Defence in depth against a forced ?editId= for an in-use item (the Edit link itself is
        // never rendered for such a row) - refuses to open edit mode and surfaces the same
        // dependency message Save/Remove would, rather than silently rendering an editable row.
        var editTarget = editId is null ? null : items.FirstOrDefault(i => i.CategoryItemTypeId == editId);
        if (editTarget is not null && editTarget.ForeignInstances != 0 && message is null)
        {
            message = BuildInUseMessage(editTarget, "edited");
            messageIsError = true;
            editId = null;
        }

        // Matches legacy CategoryItemTypeCollection.GetSortedFilteredList's ApplySort("Name",
        // Ascending) - the grid's one and only default sort order.
        var sortedItems = items.OrderBy(i => i.Name, StringComparer.OrdinalIgnoreCase).ToList();

        // Matches legacy GridViewCategoryItemType.AllowPaging/PageSize = 15 - the grid paginates
        // the full year's in-memory collection client-side, not via a separate server round-trip
        // per page, so the same client-side Skip/Take here is a faithful reproduction, not a gap.
        var currentPage = page < 1 ? 1 : page;
        var pagedItems = sortedItems
            .Skip((currentPage - 1) * CategoryItemTypeViewModel.PageSize)
            .Take(CategoryItemTypeViewModel.PageSize);

        var rows = pagedItems
            .Select(i => new CategoryItemTypeRowViewModel
            {
                CategoryItemTypeId = i.CategoryItemTypeId,
                Name = editId == i.CategoryItemTypeId && editValues is not null ? editValues.Name : i.Name,
                NoLongerInUse = editId == i.CategoryItemTypeId && editValues is not null ? editValues.NoLongerInUse : i.NoLongerInUse,
                ForeignInstances = i.ForeignInstances,
                ForeignInstancesString = i.ForeignInstancesString,
                IsEditing = editId == i.CategoryItemTypeId,
                IsConfirmingRemove = removeConfirmId == i.CategoryItemTypeId
            })
            .ToList();

        return new CategoryItemTypeViewModel
        {
            SelectedYearId = selectedYearId,
            // Legacy's DropDownListYear.Items.Add(CurrentYearId) shows the plain year ("2026"),
            // not the shared Year lookup's financial-year label ("2026/27") used elsewhere (e.g.
            // Contract/PostagePricingPlan) - this dropdown only ever needs the YearId anyway.
            YearOptions = years.Select(y => new SelectListItem(y.YearId.ToString(System.Globalization.CultureInfo.InvariantCulture), y.YearId.ToString(System.Globalization.CultureInfo.InvariantCulture))),
            Rows = rows,
            Page = currentPage,
            TotalRecords = sortedItems.Count,
            Add = addValues ?? new CategoryItemTypeAddViewModel(),
            Message = message,
            MessageIsError = messageIsError
        };
    }

    // Matches the exact wording CategoryItemTypeService uses server-side, so a blocked Edit
    // attempt (forced ?editId=) reads identically to a blocked Save/Remove.
    private static string BuildInUseMessage(CategoryItemTypeResponse item, string action) =>
        item.ForeignInstances == -1
            ? $"This Category Item is being used on a distribution and cannot be {action}. It is also being used on the following Scheme(s): {item.ForeignInstancesString}"
            : $"This Category Item is being used on {item.ForeignInstances} Scheme(s): {item.ForeignInstancesString} and cannot be {action}.";

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

        return availableYears.Count > 0 ? availableYears[0].YearId : 0;
    }

    public async Task<IActionResult> TestType(int? yearId, Guid? editId, Guid? removeConfirmId, int page = 1, CancellationToken cancellationToken = default) =>
        View(await BuildTestTypeViewModelAsync(
            new TestTypeViewModelRequest(yearId, editId, page, Message: null, MessageIsError: false, AddValues: null, EditValues: null, removeConfirmId),
            cancellationToken));

    // Matches legacy ButtonAdd_Click - Name is required; the new item always starts in whichever
    // year is currently selected in the dropdown.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TestTypeAdd([Bind(Prefix = "Add")] TestTypeAddViewModel model, int yearId, int page, CancellationToken cancellationToken)
    {
        if (ModelState.IsValid)
        {
            var result = await testTypeApiClient.CreateAsync(new TestTypeSaveRequest(model.Name, model.NoLongerInUse, yearId), cancellationToken);
            if (result.Success)
            {
                return RedirectToAction(nameof(TestType), new { yearId, page });
            }

            foreach (var (field, messages) in result.FieldErrors)
            {
                foreach (var errorMessage in messages)
                {
                    ModelState.AddModelError($"Add.{field}", errorMessage);
                }
            }
        }

        return View(nameof(TestType), await BuildTestTypeViewModelAsync(
            new TestTypeViewModelRequest(yearId, EditId: null, page, Message: null, MessageIsError: false, model, EditValues: null),
            cancellationToken));
    }

    // Only the row matching model.TestTypeId is ever in edit mode when this posts - matches
    // legacy GridViewTestType_RowUpdating, which only acts on the row with EditIndex set. Blocked
    // (in-use) edits surface the legacy warning message as a notification banner rather than a JS
    // alert.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TestTypeSave([Bind(Prefix = "Edit")] TestTypeEditViewModel model, int yearId, int page, CancellationToken cancellationToken)
    {
        if (ModelState.IsValid)
        {
            var result = await testTypeApiClient.UpdateAsync(model.TestTypeId!.Value, new TestTypeSaveRequest(model.Name, model.NoLongerInUse, yearId), cancellationToken);
            if (result.Success)
            {
                return RedirectToAction(nameof(TestType), new { yearId, page });
            }

            if (result.FieldErrors.TryGetValue(string.Empty, out var conflictMessages))
            {
                return View(nameof(TestType), await BuildTestTypeViewModelAsync(
                    new TestTypeViewModelRequest(yearId, model.TestTypeId, page, conflictMessages[0], MessageIsError: true, AddValues: null, EditValues: null),
                    cancellationToken));
            }

            foreach (var (field, messages) in result.FieldErrors)
            {
                foreach (var errorMessage in messages)
                {
                    ModelState.AddModelError($"Edit.{field}", errorMessage);
                }
            }
        }

        return View(nameof(TestType), await BuildTestTypeViewModelAsync(
            new TestTypeViewModelRequest(yearId, model.TestTypeId, page, Message: null, MessageIsError: false, AddValues: null, model),
            cancellationToken));
    }

    // "No Longer in Use" is saved for every row on the current page in one combined submit - see
    // CategoryItemTypeSaveAllNoLongerInUse remarks for the full rationale (bulk GOV.UK checkboxes
    // pattern, flat dual-list binding to avoid the edit-mode index-gap bug).
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TestTypeSaveAllNoLongerInUse(List<Guid>? pageTestTypeIds, List<Guid>? checkedTestTypeIds, int yearId, int page, CancellationToken cancellationToken)
    {
        var checkedIds = checkedTestTypeIds ?? [];
        try
        {
            foreach (var id in pageTestTypeIds ?? [])
            {
                await testTypeApiClient.SetNoLongerInUseAsync(id, new TestTypeNoLongerInUseRequest(checkedIds.Contains(id), yearId), cancellationToken);
            }
        }
        catch (Exception ex) when (IsTransientApiFailure(ex, cancellationToken))
        {
            return View(nameof(TestType), await BuildTestTypeViewModelAsync(
                new TestTypeViewModelRequest(yearId, EditId: null, page, NoLongerInUseSaveFailedMessage, MessageIsError: true, AddValues: null, EditValues: null),
                cancellationToken));
        }

        return RedirectToAction(nameof(TestType), new { yearId, page });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TestTypeRemove(Guid testTypeId, int yearId, int page, CancellationToken cancellationToken)
    {
        var result = await testTypeApiClient.DeleteAsync(testTypeId, yearId, cancellationToken);
        if (!result.Success)
        {
            LogTestTypeRemoveBlockedMessage(logger, testTypeId, null);
        }

        return View(nameof(TestType), await BuildTestTypeViewModelAsync(
            new TestTypeViewModelRequest(yearId, EditId: null, page, result.Message, !result.Success, AddValues: null, EditValues: null),
            cancellationToken));
    }

    // Bundles BuildTestTypeViewModelAsync's render-state inputs into one object - see
    // CategoryItemTypeViewModelRequest remarks (Sonar S107, 7-parameter limit).
    private sealed record TestTypeViewModelRequest(
        int? YearId,
        Guid? EditId,
        int Page,
        string? Message,
        bool MessageIsError,
        TestTypeAddViewModel? AddValues,
        TestTypeEditViewModel? EditValues,
        Guid? RemoveConfirmId = null);

    private async Task<TestTypeViewModel> BuildTestTypeViewModelAsync(
        TestTypeViewModelRequest request,
        CancellationToken cancellationToken)
    {
        var yearId = request.YearId;
        var editId = request.EditId;
        var page = request.Page;
        var message = request.Message;
        var messageIsError = request.MessageIsError;
        var addValues = request.AddValues;
        var editValues = request.EditValues;
        var removeConfirmId = request.RemoveConfirmId;

        var years = await lookupApiClient.GetCurrentYearsAsync(cancellationToken);
        var currentYearId = years.Count > 0 ? years[0].YearId : (int?)null;
        var selectedYearId = ResolveSelectedYearId(yearId, years, currentYearId);

        var items = await testTypeApiClient.GetByYearAsync(selectedYearId, cancellationToken);

        // Defence in depth against a forced ?editId= for an in-use item (the Edit link itself is
        // never rendered for such a row) - refuses to open edit mode and surfaces the same
        // dependency message Save/Remove would, rather than silently rendering an editable row.
        var editTarget = editId is null ? null : items.FirstOrDefault(i => i.TestTypeId == editId);
        if (editTarget is not null && editTarget.ForeignInstances != 0 && message is null)
        {
            message = BuildTestTypeInUseMessage(editTarget, "edited");
            messageIsError = true;
            editId = null;
        }

        // Matches legacy TestTypeCollection.GetSortedFilteredList's ApplySort("Name", Ascending).
        var sortedItems = items.OrderBy(i => i.Name, StringComparer.OrdinalIgnoreCase).ToList();

        // Matches legacy GridViewTestType.AllowPaging/PageSize = 15.
        var currentPage = page < 1 ? 1 : page;
        var pagedItems = sortedItems
            .Skip((currentPage - 1) * TestTypeViewModel.PageSize)
            .Take(TestTypeViewModel.PageSize);

        var rows = pagedItems
            .Select(i => new TestTypeRowViewModel
            {
                TestTypeId = i.TestTypeId,
                Name = editId == i.TestTypeId && editValues is not null ? editValues.Name : i.Name,
                NoLongerInUse = editId == i.TestTypeId && editValues is not null ? editValues.NoLongerInUse : i.NoLongerInUse,
                ForeignInstances = i.ForeignInstances,
                ForeignInstancesString = i.ForeignInstancesString,
                IsEditing = editId == i.TestTypeId,
                IsConfirmingRemove = removeConfirmId == i.TestTypeId
            })
            .ToList();

        return new TestTypeViewModel
        {
            SelectedYearId = selectedYearId,
            // See CategoryItemTypeViewModel's YearOptions remarks: plain year, not the shared
            // financial-year label.
            YearOptions = years.Select(y => new SelectListItem(y.YearId.ToString(System.Globalization.CultureInfo.InvariantCulture), y.YearId.ToString(System.Globalization.CultureInfo.InvariantCulture))),
            Rows = rows,
            Page = currentPage,
            TotalRecords = sortedItems.Count,
            Add = addValues ?? new TestTypeAddViewModel(),
            Message = message,
            MessageIsError = messageIsError
        };
    }

    // Matches the exact wording TestTypeService uses server-side, so a blocked Edit attempt
    // (forced ?editId=) reads identically to a blocked Save/Remove.
    private static string BuildTestTypeInUseMessage(TestTypeResponse item, string action) =>
        item.ForeignInstances == -1
            ? $"This Test is being used on a distribution and cannot be {action}. It is also being used on the following Scheme(s): {item.ForeignInstancesString}"
            : $"This Test is being used on {item.ForeignInstances} Scheme(s): {item.ForeignInstancesString} and cannot be {action}.";

    public async Task<IActionResult> CriterionItemType(int? yearId, Guid? editId, Guid? removeConfirmId, int page = 1, CancellationToken cancellationToken = default) =>
        View(await BuildCriterionItemTypeViewModelAsync(
            new CriterionItemTypeViewModelRequest(yearId, editId, page, Message: null, MessageIsError: false, AddValues: null, EditValues: null, removeConfirmId),
            cancellationToken));

    // Matches legacy ButtonAdd_Click - Name is required; the new item always starts in whichever
    // year is currently selected in the dropdown.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CriterionItemTypeAdd([Bind(Prefix = "Add")] CriterionItemTypeAddViewModel model, int yearId, int page, CancellationToken cancellationToken)
    {
        if (ModelState.IsValid)
        {
            var result = await criterionItemTypeApiClient.CreateAsync(new CriterionItemTypeSaveRequest(model.Name, model.NoLongerInUse, yearId), cancellationToken);
            if (result.Success)
            {
                return RedirectToAction(nameof(CriterionItemType), new { yearId, page });
            }

            foreach (var (field, messages) in result.FieldErrors)
            {
                foreach (var errorMessage in messages)
                {
                    ModelState.AddModelError($"Add.{field}", errorMessage);
                }
            }
        }

        return View(nameof(CriterionItemType), await BuildCriterionItemTypeViewModelAsync(
            new CriterionItemTypeViewModelRequest(yearId, EditId: null, page, Message: null, MessageIsError: false, model, EditValues: null),
            cancellationToken));
    }

    // Only the row matching model.CriterionItemTypeId is ever in edit mode when this posts -
    // matches legacy GridViewCriterionItemType_RowUpdating, which only acts on the row with
    // EditIndex set. Blocked (in-use) edits surface the legacy warning message as a notification
    // banner rather than a JS alert.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CriterionItemTypeSave([Bind(Prefix = "Edit")] CriterionItemTypeEditViewModel model, int yearId, int page, CancellationToken cancellationToken)
    {
        if (ModelState.IsValid)
        {
            var result = await criterionItemTypeApiClient.UpdateAsync(model.CriterionItemTypeId!.Value, new CriterionItemTypeSaveRequest(model.Name, model.NoLongerInUse, yearId), cancellationToken);
            if (result.Success)
            {
                return RedirectToAction(nameof(CriterionItemType), new { yearId, page });
            }

            if (result.FieldErrors.TryGetValue(string.Empty, out var conflictMessages))
            {
                return View(nameof(CriterionItemType), await BuildCriterionItemTypeViewModelAsync(
                    new CriterionItemTypeViewModelRequest(yearId, model.CriterionItemTypeId, page, conflictMessages[0], MessageIsError: true, AddValues: null, EditValues: null),
                    cancellationToken));
            }

            foreach (var (field, messages) in result.FieldErrors)
            {
                foreach (var errorMessage in messages)
                {
                    ModelState.AddModelError($"Edit.{field}", errorMessage);
                }
            }
        }

        return View(nameof(CriterionItemType), await BuildCriterionItemTypeViewModelAsync(
            new CriterionItemTypeViewModelRequest(yearId, model.CriterionItemTypeId, page, Message: null, MessageIsError: false, AddValues: null, model),
            cancellationToken));
    }

    // "No Longer in Use" is saved for every row on the current page in one combined submit - see
    // CategoryItemTypeSaveAllNoLongerInUse remarks for the full rationale.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CriterionItemTypeSaveAllNoLongerInUse(List<Guid>? pageCriterionItemTypeIds, List<Guid>? checkedCriterionItemTypeIds, int yearId, int page, CancellationToken cancellationToken)
    {
        var checkedIds = checkedCriterionItemTypeIds ?? [];
        try
        {
            foreach (var id in pageCriterionItemTypeIds ?? [])
            {
                await criterionItemTypeApiClient.SetNoLongerInUseAsync(id, new CriterionItemTypeNoLongerInUseRequest(checkedIds.Contains(id), yearId), cancellationToken);
            }
        }
        catch (Exception ex) when (IsTransientApiFailure(ex, cancellationToken))
        {
            return View(nameof(CriterionItemType), await BuildCriterionItemTypeViewModelAsync(
                new CriterionItemTypeViewModelRequest(yearId, EditId: null, page, NoLongerInUseSaveFailedMessage, MessageIsError: true, AddValues: null, EditValues: null),
                cancellationToken));
        }

        return RedirectToAction(nameof(CriterionItemType), new { yearId, page });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CriterionItemTypeRemove(Guid criterionItemTypeId, int yearId, int page, CancellationToken cancellationToken)
    {
        var result = await criterionItemTypeApiClient.DeleteAsync(criterionItemTypeId, yearId, cancellationToken);
        if (!result.Success)
        {
            LogCriterionItemTypeRemoveBlockedMessage(logger, criterionItemTypeId, null);
        }

        return View(nameof(CriterionItemType), await BuildCriterionItemTypeViewModelAsync(
            new CriterionItemTypeViewModelRequest(yearId, EditId: null, page, result.Message, !result.Success, AddValues: null, EditValues: null),
            cancellationToken));
    }

    // Bundles BuildCriterionItemTypeViewModelAsync's render-state inputs into one object - see
    // CategoryItemTypeViewModelRequest remarks (Sonar S107, 7-parameter limit).
    private sealed record CriterionItemTypeViewModelRequest(
        int? YearId,
        Guid? EditId,
        int Page,
        string? Message,
        bool MessageIsError,
        CriterionItemTypeAddViewModel? AddValues,
        CriterionItemTypeEditViewModel? EditValues,
        Guid? RemoveConfirmId = null);

    private async Task<CriterionItemTypeViewModel> BuildCriterionItemTypeViewModelAsync(
        CriterionItemTypeViewModelRequest request,
        CancellationToken cancellationToken)
    {
        var yearId = request.YearId;
        var editId = request.EditId;
        var page = request.Page;
        var message = request.Message;
        var messageIsError = request.MessageIsError;
        var addValues = request.AddValues;
        var editValues = request.EditValues;
        var removeConfirmId = request.RemoveConfirmId;

        var years = await lookupApiClient.GetCurrentYearsAsync(cancellationToken);
        var currentYearId = years.Count > 0 ? years[0].YearId : (int?)null;
        var selectedYearId = ResolveSelectedYearId(yearId, years, currentYearId);

        var items = await criterionItemTypeApiClient.GetByYearAsync(selectedYearId, cancellationToken);

        // Defence in depth against a forced ?editId= for an in-use item (the Edit link itself is
        // never rendered for such a row) - refuses to open edit mode and surfaces the same
        // dependency message Save/Remove would, rather than silently rendering an editable row.
        var editTarget = editId is null ? null : items.FirstOrDefault(i => i.CriterionItemTypeId == editId);
        if (editTarget is not null && editTarget.ForeignInstances != 0 && message is null)
        {
            message = BuildCriterionItemTypeInUseMessage(editTarget, "edited");
            messageIsError = true;
            editId = null;
        }

        // Matches legacy CriterionItemTypeCollection.GetSortedFilteredList's ApplySort("Name",
        // Ascending).
        var sortedItems = items.OrderBy(i => i.Name, StringComparer.OrdinalIgnoreCase).ToList();

        // Matches legacy GridViewCriterionItemType.AllowPaging/PageSize = 15.
        var currentPage = page < 1 ? 1 : page;
        var pagedItems = sortedItems
            .Skip((currentPage - 1) * CriterionItemTypeViewModel.PageSize)
            .Take(CriterionItemTypeViewModel.PageSize);

        var rows = pagedItems
            .Select(i => new CriterionItemTypeRowViewModel
            {
                CriterionItemTypeId = i.CriterionItemTypeId,
                Name = editId == i.CriterionItemTypeId && editValues is not null ? editValues.Name : i.Name,
                NoLongerInUse = editId == i.CriterionItemTypeId && editValues is not null ? editValues.NoLongerInUse : i.NoLongerInUse,
                ForeignInstances = i.ForeignInstances,
                ForeignInstancesString = i.ForeignInstancesString,
                IsEditing = editId == i.CriterionItemTypeId,
                IsConfirmingRemove = removeConfirmId == i.CriterionItemTypeId
            })
            .ToList();

        return new CriterionItemTypeViewModel
        {
            SelectedYearId = selectedYearId,
            // See CategoryItemTypeViewModel's YearOptions remarks: plain year, not the shared
            // financial-year label.
            YearOptions = years.Select(y => new SelectListItem(y.YearId.ToString(System.Globalization.CultureInfo.InvariantCulture), y.YearId.ToString(System.Globalization.CultureInfo.InvariantCulture))),
            Rows = rows,
            Page = currentPage,
            TotalRecords = sortedItems.Count,
            Add = addValues ?? new CriterionItemTypeAddViewModel(),
            Message = message,
            MessageIsError = messageIsError
        };
    }

    // Matches the exact wording CriterionItemTypeService uses server-side, so a blocked Edit
    // attempt (forced ?editId=) reads identically to a blocked Save/Remove.
    private static string BuildCriterionItemTypeInUseMessage(CriterionItemTypeResponse item, string action) =>
        item.ForeignInstances == -1
            ? $"This Criterion Item is being used on a distribution and cannot be {action}. It is also being used on the following Scheme(s): {item.ForeignInstancesString}"
            : $"This Criterion Item is being used on {item.ForeignInstances} Scheme(s): {item.ForeignInstancesString} and cannot be {action}.";
}
