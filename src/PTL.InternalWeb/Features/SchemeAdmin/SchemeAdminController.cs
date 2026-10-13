using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using PTL.ApiClient;
using PTL.Contracts.CategoryItemType;
using PTL.Contracts.Lookup;

namespace PTL.InternalWeb.Features.SchemeAdmin;

// Authentication/authorization are out of scope for this phase - same convention as
// Features/Scheme/SchemeController.cs, which this "Manage Schemes" sub-area sits alongside.
public class SchemeAdminController(ICategoryItemTypeApiClient categoryItemTypeApiClient, ILookupApiClient lookupApiClient, ILogger<SchemeAdminController> logger) : Controller
{
    private static readonly Action<ILogger, Guid, Exception?> LogCategoryItemTypeRemoveBlockedMessage =
        LoggerMessage.Define<Guid>(
            LogLevel.Information,
            new EventId(1, nameof(LogCategoryItemTypeRemoveBlockedMessage)),
            "Category item type {CategoryItemTypeId} removal was blocked: still in use");

    public async Task<IActionResult> CategoryItemType(int? yearId, Guid? editId, Guid? removeConfirmId, int page = 1, CancellationToken cancellationToken = default) =>
        View(await BuildCategoryItemTypeViewModelAsync(yearId, editId, page, message: null, messageIsError: false, addValues: null, editValues: null, cancellationToken, removeConfirmId));

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

        return View(nameof(CategoryItemType), await BuildCategoryItemTypeViewModelAsync(yearId, editId: null, page, message: null, messageIsError: false, model, editValues: null, cancellationToken));
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
                return View(nameof(CategoryItemType), await BuildCategoryItemTypeViewModelAsync(yearId, model.CategoryItemTypeId, page, conflictMessages[0], messageIsError: true, addValues: null, editValues: null, cancellationToken));
            }

            foreach (var (field, messages) in result.FieldErrors)
            {
                foreach (var errorMessage in messages)
                {
                    ModelState.AddModelError($"Edit.{field}", errorMessage);
                }
            }
        }

        return View(nameof(CategoryItemType), await BuildCategoryItemTypeViewModelAsync(yearId, model.CategoryItemTypeId, page, message: null, messageIsError: false, addValues: null, model, cancellationToken));
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
        foreach (var id in pageCategoryItemTypeIds ?? [])
        {
            await categoryItemTypeApiClient.SetNoLongerInUseAsync(id, new CategoryItemTypeNoLongerInUseRequest(checkedIds.Contains(id), yearId), cancellationToken);
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

        return View(nameof(CategoryItemType), await BuildCategoryItemTypeViewModelAsync(yearId, editId: null, page, result.Message, messageIsError: !result.Success, addValues: null, editValues: null, cancellationToken));
    }

    private async Task<CategoryItemTypeViewModel> BuildCategoryItemTypeViewModelAsync(
        int? yearId,
        Guid? editId,
        int page,
        string? message,
        bool messageIsError,
        CategoryItemTypeAddViewModel? addValues,
        CategoryItemTypeEditViewModel? editValues,
        CancellationToken cancellationToken,
        Guid? removeConfirmId = null)
    {
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
            YearOptions = years.Select(y => new SelectListItem(y.Year, y.YearId.ToString())),
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
}
