using Microsoft.AspNetCore.Mvc;
using PTL.Contracts.TestConsultant;
using PTL.Core.TestConsultant;
using PTL.SharedUI.Notifications;

namespace PTL.InternalWeb.Features.SystemAdministration;

// Manage External Test Consultants - legacy ManageExternalTCs.aspx. Edits, adds, removals and Active/Inactive
// toggles all stay pending (round-tripped through the form, where legacy used ViewState) until the page-level
// Save/Apply writes them. Only Save and Cancel leave the page.
public partial class SystemAdministrationController
{
    public async Task<IActionResult> ExternalTestConsultantManagement(CancellationToken cancellationToken)
    {
        var consultants = await externalTestConsultantApiClient.GetAllAsync(cancellationToken);

        return ExternalTestConsultantPage(new ExternalTestConsultantManagementViewModel
        {
            Rows = [.. consultants
                .OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
                .Select(c => new ExternalTestConsultantRowViewModel
                {
                    ExternalTestConsultantId = c.ExternalTestConsultantId,
                    Name = c.Name,
                    Department = c.Department,
                    Email = c.Email,
                    IsInactive = c.IsInactive,
                    InactiveDate = c.InactiveDate,
                })]
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ExternalTestConsultantManagement(ExternalTestConsultantManagementViewModel model, string? command, int? rowIndex, CancellationToken cancellationToken)
    {
        // Each command validates only the part of the page it acts on (legacy ValidationGroups).
        ModelState.Clear();

        if (PendingListCommands.Is(command, PendingListCommands.Submit))
        {
            command = model.EditIndex is null ? PendingListCommands.Add : PendingListCommands.Update;
            rowIndex = model.EditIndex;
        }

        if (PendingListCommands.Is(command, PendingListCommands.Cancel))
        {
            return RedirectToAction(nameof(Index));
        }

        if (PendingListCommands.Is(command, PendingListCommands.Save) || PendingListCommands.Is(command, PendingListCommands.Apply))
        {
            return await SaveExternalTestConsultantsAsync(model, leavePage: PendingListCommands.Is(command, PendingListCommands.Save), cancellationToken);
        }

        if (PendingListCommands.Is(command, PendingListCommands.Remove) || PendingListCommands.Is(command, PendingListCommands.Toggle))
        {
            await ApplyExternalRowChangeAsync(model, PendingListCommands.Is(command, PendingListCommands.Remove), rowIndex, cancellationToken);
            return ExternalTestConsultantPage(model);
        }

        ApplyExternalTestConsultantRowCommand(model, command, rowIndex);
        return ExternalTestConsultantPage(model);
    }

    private ViewResult ExternalTestConsultantPage(ExternalTestConsultantManagementViewModel model)
    {
        if (!PendingList.IsRow(model.Rows, model.EditIndex) || model.Rows[model.EditIndex!.Value].IsRemoved)
        {
            model.EditIndex = null;
        }

        for (var i = 0; i < model.Rows.Count; i++)
        {
            model.Rows[i].IsEditing = model.EditIndex == i;
        }

        return View(nameof(ExternalTestConsultantManagement), model);
    }

    // edit/update/cancelEdit/add change the pending list only - nothing is written and no notification shown.
    private void ApplyExternalTestConsultantRowCommand(ExternalTestConsultantManagementViewModel model, string? command, int? rowIndex)
    {
        var isRow = PendingList.IsRow(model.Rows, rowIndex);

        if (PendingListCommands.Is(command, PendingListCommands.Edit) && isRow)
        {
            var row = model.Rows[rowIndex!.Value];
            model.EditIndex = rowIndex;
            model.Edit = new ExternalTestConsultantEditViewModel { Name = row.Name, Department = row.Department, Email = row.Email };
        }
        else if (PendingListCommands.Is(command, PendingListCommands.Update) && isRow)
        {
            UpdatePendingExternalTestConsultantRow(model, rowIndex!.Value);
        }
        else if (PendingListCommands.Is(command, PendingListCommands.CancelEdit))
        {
            model.EditIndex = null;
        }
        else if (PendingListCommands.Is(command, PendingListCommands.Add) && TryValidateModel(model.Add, nameof(model.Add)))
        {
            var name = model.Add.Name.Trim();
            if (IsDuplicateExternalTestConsultantName(model, name, excludingIndex: null))
            {
                ModelState.AddModelError($"{nameof(model.Add)}.{nameof(model.Add.Name)}", ExternalTestConsultantValidator.DuplicateNameMessage);
                return;
            }

            model.Rows.Add(new ExternalTestConsultantRowViewModel
            {
                Name = name,
                Department = model.Add.Department.Trim(),
                Email = model.Add.Email.Trim(),
            });
            model.Add = new ExternalTestConsultantAddViewModel();
        }
    }

    // Case-insensitive on the trimmed name, against every row still on the page (removed rows no longer count).
    private static bool IsDuplicateExternalTestConsultantName(ExternalTestConsultantManagementViewModel model, string name, int? excludingIndex) =>
        model.Rows.Where((row, index) => index != excludingIndex && !row.IsRemoved)
            .Any(row => string.Equals(row.Name.Trim(), name, StringComparison.OrdinalIgnoreCase));

    // Matches legacy GridViewTC_Updating: applies the edited row to the in-memory list only.
    private void UpdatePendingExternalTestConsultantRow(ExternalTestConsultantManagementViewModel model, int rowIndex)
    {
        if (!TryValidateModel(model.Edit, nameof(model.Edit)))
        {
            model.EditIndex = rowIndex;
            return;
        }

        var row = model.Rows[rowIndex];
        var name = model.Edit.Name.Trim();
        if (IsDuplicateExternalTestConsultantName(model, name, excludingIndex: rowIndex))
        {
            ModelState.AddModelError($"{nameof(model.Edit)}.{nameof(model.Edit.Name)}", ExternalTestConsultantValidator.DuplicateNameMessage);
            model.EditIndex = rowIndex;
            return;
        }

        row.Name = name;
        row.Department = model.Edit.Department.Trim();
        row.Email = model.Edit.Email.Trim();
        model.Edit = new ExternalTestConsultantEditViewModel();
        model.EditIndex = null;
    }

    // Remove / Status link of one row. Both are refused, with the schemes listed, while the consultant is still
    // allocated to schemes (legacy RowDeleting / ToggleInactive alerts). Otherwise the change is only made to the
    // pending list. A consultant added on this page (not yet saved) has no allocations and, if removed, is just dropped.
    private async Task ApplyExternalRowChangeAsync(ExternalTestConsultantManagementViewModel model, bool remove, int? rowIndex, CancellationToken cancellationToken)
    {
        if (!PendingList.IsRow(model.Rows, rowIndex))
        {
            return;
        }

        var index = rowIndex!.Value;
        var row = model.Rows[index];

        if (row.ExternalTestConsultantId is { } id)
        {
            var stored = (await externalTestConsultantApiClient.GetAllAsync(cancellationToken)).FirstOrDefault(c => c.ExternalTestConsultantId == id);
            if (stored is { AllocationCount: > 0 })
            {
                model.Message = TestConsultantInUseMessage("external", remove ? "be removed" : "have its Active/Inactive status changed", stored.AllocationCount, stored.AllocationString);
                model.MessageIsError = true;
                return;
            }
        }

        if (!remove)
        {
            row.IsInactive = !row.IsInactive;
            row.InactiveDate = row.IsInactive ? DateTime.UtcNow : null;
        }
        else if (row.IsNew)
        {
            model.Rows.RemoveAt(index);
            model.EditIndex = PendingList.EditIndexAfterRemoval(model.EditIndex, index);
        }
        else
        {
            row.IsRemoved = true;
            model.EditIndex = model.EditIndex == index ? null : model.EditIndex;
        }
    }

    // Matches legacy ButtonSave_Click/ButtonApply: deletes the removed consultants, then writes every added or
    // changed one. Save then leaves the page for System Administration; Apply stays on the page. Any row the API
    // rejects stays pending, with its message in the error summary. Like legacy, text typed into a row still in
    // edit mode is not applied.
    private async Task<IActionResult> SaveExternalTestConsultantsAsync(ExternalTestConsultantManagementViewModel model, bool leavePage, CancellationToken cancellationToken)
    {
        model.EditIndex = null;
        await DeleteRemovedExternalTestConsultantsAsync(model, cancellationToken);
        await WriteChangedExternalTestConsultantsAsync(model, cancellationToken);

        if (!ModelState.IsValid || model.MessageIsError)
        {
            return ExternalTestConsultantPage(model);
        }

        TempData.SetNotification(NotificationType.Success, "External test consultants saved successfully.");
        return leavePage ? RedirectToAction(nameof(Index)) : ExternalTestConsultantPage(model);
    }

    private async Task DeleteRemovedExternalTestConsultantsAsync(ExternalTestConsultantManagementViewModel model, CancellationToken cancellationToken)
    {
        foreach (var row in model.Rows.Where(r => r.IsRemoved).ToList())
        {
            var result = row.ExternalTestConsultantId is { } id
                ? await externalTestConsultantApiClient.DeleteAsync(id, cancellationToken)
                : new ExternalTestConsultantDeleteResponse(true, null);

            if (result.Success)
            {
                model.Rows.Remove(row);
                continue;
            }

            PendingList.AddOutcomeError(model, $"{row.Name}: {result.Message ?? "The external test consultant could not be removed."}");
        }
    }

    private async Task WriteChangedExternalTestConsultantsAsync(ExternalTestConsultantManagementViewModel model, CancellationToken cancellationToken)
    {
        var persisted = (await externalTestConsultantApiClient.GetAllAsync(cancellationToken)).ToDictionary(c => c.ExternalTestConsultantId);

        foreach (var row in model.Rows.Where(r => !r.IsRemoved))
        {
            ExternalTestConsultantResponse? stored = null;
            if (row.ExternalTestConsultantId is { } id)
            {
                persisted.TryGetValue(id, out stored);
            }

            if (!await SaveExternalTestConsultantDetailsAsync(model, row, stored, cancellationToken))
            {
                continue;
            }

            if (row.ExternalTestConsultantId is { } savedId && row.IsInactive != (stored?.IsInactive ?? false))
            {
                var status = await externalTestConsultantApiClient.SetStatusAsync(savedId, row.IsInactive, cancellationToken);
                AddExternalTestConsultantErrors(model, row, status);
            }
        }
    }

    // Creates a new consultant or updates one whose name, department or email changed. False if the API rejected it.
    private async Task<bool> SaveExternalTestConsultantDetailsAsync(ExternalTestConsultantManagementViewModel model, ExternalTestConsultantRowViewModel row, ExternalTestConsultantResponse? stored, CancellationToken cancellationToken)
    {
        if (stored is not null && stored.Name == row.Name && stored.Department == row.Department && stored.Email == row.Email)
        {
            return true;
        }

        var request = new ExternalTestConsultantSaveRequest(row.Name, row.Department, row.Email);
        var result = row.ExternalTestConsultantId is { } id
            ? await externalTestConsultantApiClient.UpdateAsync(id, request, cancellationToken)
            : await externalTestConsultantApiClient.CreateAsync(request, cancellationToken);

        if (result.Success)
        {
            row.ExternalTestConsultantId ??= result.TestConsultant?.ExternalTestConsultantId;
            return true;
        }

        AddExternalTestConsultantErrors(model, row, result);
        return false;
    }

    // Problems with a field (name, email) go in the validation summary; a refusal that belongs to no field (still
    // allocated to schemes) or an unspecific failure goes in the banner at the top of the page.
    private void AddExternalTestConsultantErrors(ExternalTestConsultantManagementViewModel model, ExternalTestConsultantRowViewModel row, ExternalTestConsultantSaveResult result)
    {
        if (result.Success)
        {
            return;
        }

        var reported = false;
        foreach (var (field, messages) in result.FieldErrors)
        {
            foreach (var message in messages)
            {
                reported = true;
                if (string.IsNullOrEmpty(field))
                {
                    PendingList.AddOutcomeError(model, $"{row.Name}: {message}");
                }
                else
                {
                    ModelState.AddModelError("Rows", $"{row.Name}: {message}");
                }
            }
        }

        if (!reported)
        {
            PendingList.AddOutcomeError(model, $"{row.Name}: The external test consultant could not be saved.");
        }
    }

    private static string TestConsultantInUseMessage(string kind, string action, int allocationCount, string? schemes) =>
        $"This {kind} test consultant cannot {action} because it is being used on {allocationCount} scheme(s)"
        + (string.IsNullOrWhiteSpace(schemes) ? "." : $": {schemes}. Remove it from those schemes first.");
}
