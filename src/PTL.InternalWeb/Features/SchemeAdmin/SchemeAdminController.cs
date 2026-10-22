using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using PTL.ApiClient;
using PTL.Contracts.CategoryItemType;
using PTL.Contracts.CriterionItemType;
using PTL.Contracts.Lookup;
using PTL.Contracts.TestType;
using PTL.InternalWeb.Authorization;

namespace PTL.InternalWeb.Features.SchemeAdmin;

// Legacy "Scheme Admin" folder - requires the Scheme Admin role (PtlPolicies.SchemeAdmin), same as
// Features/Scheme/SchemeController.cs, which this "Manage Schemes" sub-area sits alongside.
[Authorize(Policy = PtlPolicies.SchemeAdmin)]
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
            new(yearId, editId, page, Message: null, MessageIsError: false, AddValues: null, EditValues: null, removeConfirmId),
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
            new(yearId, EditId: null, page, Message: null, MessageIsError: false, model, EditValues: null),
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
                    new(yearId, model.CategoryItemTypeId, page, conflictMessages[0], MessageIsError: true, AddValues: null, EditValues: null),
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
            new(yearId, model.CategoryItemTypeId, page, Message: null, MessageIsError: false, AddValues: null, model),
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
                new(yearId, EditId: null, page, NoLongerInUseSaveFailedMessage, MessageIsError: true, AddValues: null, EditValues: null),
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
            new(yearId, EditId: null, page, result.Message, !result.Success, AddValues: null, EditValues: null),
            cancellationToken));
    }

    // Bundles each BuildXViewModelAsync's render-state inputs into one object - the method itself
    // was flagged for exceeding Sonar's 7-parameter limit (S107). Generic over the entity's own
    // Add/Edit view-model types so CategoryItemType/TestType/CriterionItemType share one record
    // instead of 3 near-identical copies.
    private sealed record ItemTypeViewModelRequest<TAdd, TEdit>(
        int? YearId,
        Guid? EditId,
        int Page,
        string? Message,
        bool MessageIsError,
        TAdd? AddValues,
        TEdit? EditValues,
        Guid? RemoveConfirmId = null);

    // Shared body of BuildXViewModelAsync: years fetch -> selected year -> fetch items for year ->
    // edit-blocked check (forced ?editId= on an in-use item) -> sort by name. Row mapping/paging
    // stays in each entity's own BuildXViewModelAsync since the row view-model/edit-values types
    // differ per entity.
    private async Task<ItemTypeYearData<TResponse>> ResolveItemTypeYearDataAsync<TResponse>(
        int? requestedYearId,
        Guid? editId,
        string? message,
        bool messageIsError,
        string entityDisplayName,
        ItemTypeResponseAccessors<TResponse> accessors,
        CancellationToken cancellationToken)
        where TResponse : class
    {
        var years = await lookupApiClient.GetCurrentYearsAsync(cancellationToken);
        var currentYearId = years.Count > 0 ? years[0].YearId : (int?)null;
        var selectedYearId = ResolveSelectedYearId(requestedYearId, years, currentYearId);

        var items = await accessors.FetchItemsAsync(selectedYearId, cancellationToken);

        // Defence in depth against a forced ?editId= for an in-use item (the Edit link itself is
        // never rendered for such a row) - refuses to open edit mode and surfaces the same
        // dependency message Save/Remove would, rather than silently rendering an editable row.
        var editTarget = editId is null ? null : items.FirstOrDefault(i => accessors.IdSelector(i) == editId);
        if (editTarget is not null && accessors.ForeignInstancesSelector(editTarget) != 0 && message is null)
        {
            message = BuildInUseMessage(entityDisplayName, accessors.ForeignInstancesSelector(editTarget), accessors.ForeignInstancesStringSelector(editTarget), "edited");
            messageIsError = true;
            editId = null;
        }

        // Matches legacy *Collection.GetSortedFilteredList's ApplySort("Name", Ascending) - the
        // grid's one and only default sort order.
        var sortedItems = items.OrderBy(accessors.NameSelector, StringComparer.OrdinalIgnoreCase).ToList();

        return new ItemTypeYearData<TResponse>(
            selectedYearId,
            // Legacy's DropDownListYear.Items.Add(CurrentYearId) shows the plain year ("2026"),
            // not the shared Year lookup's financial-year label ("2026/27") used elsewhere (e.g.
            // Contract/PostagePricingPlan) - this dropdown only ever needs the YearId anyway.
            years.Select(y => new SelectListItem(y.YearId.ToString(System.Globalization.CultureInfo.InvariantCulture), y.YearId.ToString(System.Globalization.CultureInfo.InvariantCulture))),
            sortedItems,
            sortedItems.Count,
            editId,
            message,
            messageIsError);
    }

    // Bundles the entity-specific response accessors passed to ResolveItemTypeYearDataAsync into
    // one object - the method itself was flagged for exceeding Sonar's 7-parameter limit (S107).
    private sealed record ItemTypeResponseAccessors<TResponse>(
        Func<int, CancellationToken, Task<IReadOnlyList<TResponse>>> FetchItemsAsync,
        Func<TResponse, Guid> IdSelector,
        Func<TResponse, string> NameSelector,
        Func<TResponse, int> ForeignInstancesSelector,
        Func<TResponse, string> ForeignInstancesStringSelector);

    private readonly record struct ItemTypeYearData<TResponse>(
        int SelectedYearId,
        IEnumerable<SelectListItem> YearOptions,
        IReadOnlyList<TResponse> SortedItems,
        int TotalRecords,
        Guid? EditId,
        string? Message,
        bool MessageIsError);

    // Matches the exact wording each entity's *Service used server-side, so a blocked Edit
    // attempt (forced ?editId=) reads identically to a blocked Save/Remove.
    private static string BuildInUseMessage(string entityDisplayName, int foreignInstances, string foreignInstancesString, string action) =>
        foreignInstances == -1
            ? $"This {entityDisplayName} is being used on a distribution and cannot be {action}. It is also being used on the following Scheme(s): {foreignInstancesString}"
            : $"This {entityDisplayName} is being used on {foreignInstances} Scheme(s): {foreignInstancesString} and cannot be {action}.";

    private async Task<CategoryItemTypeViewModel> BuildCategoryItemTypeViewModelAsync(
        ItemTypeViewModelRequest<CategoryItemTypeAddViewModel, CategoryItemTypeEditViewModel> request,
        CancellationToken cancellationToken)
    {
        var yearData = await ResolveItemTypeYearDataAsync(
            request.YearId,
            request.EditId,
            request.Message,
            request.MessageIsError,
            "Category Item",
            new ItemTypeResponseAccessors<CategoryItemTypeResponse>(
                categoryItemTypeApiClient.GetByYearAsync,
                i => i.CategoryItemTypeId,
                i => i.Name,
                i => i.ForeignInstances,
                i => i.ForeignInstancesString),
            cancellationToken);

        // Matches legacy GridViewCategoryItemType.AllowPaging/PageSize = 15 - the grid paginates
        // the full year's in-memory collection client-side, not via a separate server round-trip
        // per page, so the same client-side Skip/Take here is a faithful reproduction, not a gap.
        var currentPage = request.Page < 1 ? 1 : request.Page;
        var pagedItems = yearData.SortedItems
            .Skip((currentPage - 1) * CategoryItemTypeViewModel.PageSize)
            .Take(CategoryItemTypeViewModel.PageSize);

        var rows = pagedItems
            .Select(i => new CategoryItemTypeRowViewModel
            {
                CategoryItemTypeId = i.CategoryItemTypeId,
                Name = yearData.EditId == i.CategoryItemTypeId && request.EditValues is not null ? request.EditValues.Name : i.Name,
                NoLongerInUse = yearData.EditId == i.CategoryItemTypeId && request.EditValues is not null ? request.EditValues.NoLongerInUse : i.NoLongerInUse,
                ForeignInstances = i.ForeignInstances,
                ForeignInstancesString = i.ForeignInstancesString,
                IsEditing = yearData.EditId == i.CategoryItemTypeId,
                IsConfirmingRemove = request.RemoveConfirmId == i.CategoryItemTypeId
            })
            .ToList();

        return new CategoryItemTypeViewModel
        {
            SelectedYearId = yearData.SelectedYearId,
            YearOptions = yearData.YearOptions,
            Rows = rows,
            Page = currentPage,
            TotalRecords = yearData.TotalRecords,
            Add = request.AddValues ?? new CategoryItemTypeAddViewModel(),
            Message = yearData.Message,
            MessageIsError = yearData.MessageIsError
        };
    }

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
            new(yearId, editId, page, Message: null, MessageIsError: false, AddValues: null, EditValues: null, removeConfirmId),
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
            new(yearId, EditId: null, page, Message: null, MessageIsError: false, model, EditValues: null),
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
                    new(yearId, model.TestTypeId, page, conflictMessages[0], MessageIsError: true, AddValues: null, EditValues: null),
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
            new(yearId, model.TestTypeId, page, Message: null, MessageIsError: false, AddValues: null, model),
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
                new(yearId, EditId: null, page, NoLongerInUseSaveFailedMessage, MessageIsError: true, AddValues: null, EditValues: null),
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
            new(yearId, EditId: null, page, result.Message, !result.Success, AddValues: null, EditValues: null),
            cancellationToken));
    }

    private async Task<TestTypeViewModel> BuildTestTypeViewModelAsync(
        ItemTypeViewModelRequest<TestTypeAddViewModel, TestTypeEditViewModel> request,
        CancellationToken cancellationToken)
    {
        var yearData = await ResolveItemTypeYearDataAsync(
            request.YearId,
            request.EditId,
            request.Message,
            request.MessageIsError,
            "Test",
            new ItemTypeResponseAccessors<TestTypeResponse>(
                testTypeApiClient.GetByYearAsync,
                i => i.TestTypeId,
                i => i.Name,
                i => i.ForeignInstances,
                i => i.ForeignInstancesString),
            cancellationToken);

        // Matches legacy GridViewTestType.AllowPaging/PageSize = 15.
        var currentPage = request.Page < 1 ? 1 : request.Page;
        var pagedItems = yearData.SortedItems
            .Skip((currentPage - 1) * TestTypeViewModel.PageSize)
            .Take(TestTypeViewModel.PageSize);

        var rows = pagedItems
            .Select(i => new TestTypeRowViewModel
            {
                TestTypeId = i.TestTypeId,
                Name = yearData.EditId == i.TestTypeId && request.EditValues is not null ? request.EditValues.Name : i.Name,
                NoLongerInUse = yearData.EditId == i.TestTypeId && request.EditValues is not null ? request.EditValues.NoLongerInUse : i.NoLongerInUse,
                ForeignInstances = i.ForeignInstances,
                ForeignInstancesString = i.ForeignInstancesString,
                IsEditing = yearData.EditId == i.TestTypeId,
                IsConfirmingRemove = request.RemoveConfirmId == i.TestTypeId
            })
            .ToList();

        return new TestTypeViewModel
        {
            SelectedYearId = yearData.SelectedYearId,
            YearOptions = yearData.YearOptions,
            Rows = rows,
            Page = currentPage,
            TotalRecords = yearData.TotalRecords,
            Add = request.AddValues ?? new TestTypeAddViewModel(),
            Message = yearData.Message,
            MessageIsError = yearData.MessageIsError
        };
    }

    public async Task<IActionResult> CriterionItemType(int? yearId, Guid? editId, Guid? removeConfirmId, int page = 1, CancellationToken cancellationToken = default) =>
        View(await BuildCriterionItemTypeViewModelAsync(
            new(yearId, editId, page, Message: null, MessageIsError: false, AddValues: null, EditValues: null, removeConfirmId),
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
            new(yearId, EditId: null, page, Message: null, MessageIsError: false, model, EditValues: null),
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
                    new(yearId, model.CriterionItemTypeId, page, conflictMessages[0], MessageIsError: true, AddValues: null, EditValues: null),
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
            new(yearId, model.CriterionItemTypeId, page, Message: null, MessageIsError: false, AddValues: null, model),
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
                new(yearId, EditId: null, page, NoLongerInUseSaveFailedMessage, MessageIsError: true, AddValues: null, EditValues: null),
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
            new(yearId, EditId: null, page, result.Message, !result.Success, AddValues: null, EditValues: null),
            cancellationToken));
    }

    private async Task<CriterionItemTypeViewModel> BuildCriterionItemTypeViewModelAsync(
        ItemTypeViewModelRequest<CriterionItemTypeAddViewModel, CriterionItemTypeEditViewModel> request,
        CancellationToken cancellationToken)
    {
        var yearData = await ResolveItemTypeYearDataAsync(
            request.YearId,
            request.EditId,
            request.Message,
            request.MessageIsError,
            "Criterion Item",
            new ItemTypeResponseAccessors<CriterionItemTypeResponse>(
                criterionItemTypeApiClient.GetByYearAsync,
                i => i.CriterionItemTypeId,
                i => i.Name,
                i => i.ForeignInstances,
                i => i.ForeignInstancesString),
            cancellationToken);

        // Matches legacy GridViewCriterionItemType.AllowPaging/PageSize = 15.
        var currentPage = request.Page < 1 ? 1 : request.Page;
        var pagedItems = yearData.SortedItems
            .Skip((currentPage - 1) * CriterionItemTypeViewModel.PageSize)
            .Take(CriterionItemTypeViewModel.PageSize);

        var rows = pagedItems
            .Select(i => new CriterionItemTypeRowViewModel
            {
                CriterionItemTypeId = i.CriterionItemTypeId,
                Name = yearData.EditId == i.CriterionItemTypeId && request.EditValues is not null ? request.EditValues.Name : i.Name,
                NoLongerInUse = yearData.EditId == i.CriterionItemTypeId && request.EditValues is not null ? request.EditValues.NoLongerInUse : i.NoLongerInUse,
                ForeignInstances = i.ForeignInstances,
                ForeignInstancesString = i.ForeignInstancesString,
                IsEditing = yearData.EditId == i.CriterionItemTypeId,
                IsConfirmingRemove = request.RemoveConfirmId == i.CriterionItemTypeId
            })
            .ToList();

        return new CriterionItemTypeViewModel
        {
            SelectedYearId = yearData.SelectedYearId,
            YearOptions = yearData.YearOptions,
            Rows = rows,
            Page = currentPage,
            TotalRecords = yearData.TotalRecords,
            Add = request.AddValues ?? new CriterionItemTypeAddViewModel(),
            Message = yearData.Message,
            MessageIsError = yearData.MessageIsError
        };
    }
}
