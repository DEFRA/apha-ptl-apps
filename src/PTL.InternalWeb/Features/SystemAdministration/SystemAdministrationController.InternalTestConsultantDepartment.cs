using Microsoft.AspNetCore.Mvc;
using PTL.Contracts.User;
using PTL.SharedUI.Notifications;

namespace PTL.InternalWeb.Features.SystemAdministration;

// Internal Test Consultant Department - legacy TCDepartment.aspx. Department edits and Active/Inactive toggles stay
// pending (round-tripped through the form, where legacy used ViewState) until the page-level Save writes them.
// Only Save and Cancel leave the page. The legacy page has no Add, Remove or Apply.
public partial class SystemAdministrationController
{
    public async Task<IActionResult> InternalTestConsultantDepartment(CancellationToken cancellationToken)
    {
        var consultants = await userApiClient.GetTestConsultantsAsync(cancellationToken);

        return InternalTestConsultantPage(new InternalTestConsultantDepartmentViewModel
        {
            Rows = [.. consultants
                .OrderBy(c => c.FriendlyName, StringComparer.OrdinalIgnoreCase)
                .Select(c => new InternalTestConsultantRowViewModel
                {
                    UserId = c.UserId,
                    Username = c.Username,
                    FriendlyName = c.FriendlyName,
                    Department = c.Department,
                    IsInactive = c.IsInactive,
                    InactiveDate = c.InactiveDate,
                })]
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> InternalTestConsultantDepartment(InternalTestConsultantDepartmentViewModel model, string? command, int? rowIndex, CancellationToken cancellationToken)
    {
        // Each command validates only the part of the page it acts on (legacy ValidationGroups).
        ModelState.Clear();

        if (PendingListCommands.Is(command, PendingListCommands.Submit))
        {
            command = model.EditIndex is null ? null : PendingListCommands.Update;
            rowIndex = model.EditIndex;
        }

        if (PendingListCommands.Is(command, PendingListCommands.Cancel))
        {
            return RedirectToAction(nameof(Index));
        }

        if (PendingListCommands.Is(command, PendingListCommands.Save))
        {
            return await SaveInternalTestConsultantsAsync(model, cancellationToken);
        }

        if (PendingListCommands.Is(command, PendingListCommands.Toggle))
        {
            await ToggleInternalTestConsultantStatusAsync(model, rowIndex, cancellationToken);
            return InternalTestConsultantPage(model);
        }

        ApplyInternalTestConsultantRowCommand(model, command, rowIndex);
        return InternalTestConsultantPage(model);
    }

    private ViewResult InternalTestConsultantPage(InternalTestConsultantDepartmentViewModel model)
    {
        if (!PendingList.IsRow(model.Rows, model.EditIndex))
        {
            model.EditIndex = null;
        }

        for (var i = 0; i < model.Rows.Count; i++)
        {
            model.Rows[i].IsEditing = model.EditIndex == i;
        }

        return View(nameof(InternalTestConsultantDepartment), model);
    }

    // edit/update/cancelEdit change the pending list only - nothing is written and no notification shown.
    private void ApplyInternalTestConsultantRowCommand(InternalTestConsultantDepartmentViewModel model, string? command, int? rowIndex)
    {
        var isRow = PendingList.IsRow(model.Rows, rowIndex);

        if (PendingListCommands.Is(command, PendingListCommands.Edit) && isRow)
        {
            model.EditIndex = rowIndex;
            model.Edit = new InternalTestConsultantEditViewModel { Department = model.Rows[rowIndex!.Value].Department };
        }
        else if (PendingListCommands.Is(command, PendingListCommands.Update) && isRow)
        {
            // Matches legacy GridViewTC_Updating: applies the edited row to the in-memory list only.
            if (TryValidateModel(model.Edit, nameof(model.Edit)))
            {
                model.Rows[rowIndex!.Value].Department = model.Edit.Department.Trim();
                model.Edit = new InternalTestConsultantEditViewModel();
                model.EditIndex = null;
            }
            else
            {
                model.EditIndex = rowIndex;
            }
        }
        else if (PendingListCommands.Is(command, PendingListCommands.CancelEdit))
        {
            model.EditIndex = null;
        }
    }

    // Legacy ToggleInactive_Click: flips Active/Inactive in the pending list, stamping the date when it becomes
    // inactive. Refused (with the schemes listed) while the consultant is allocated to schemes. The legacy
    // "You are setting this test consultant to be active. Are you sure?" confirm() is not carried over.
    private async Task ToggleInternalTestConsultantStatusAsync(InternalTestConsultantDepartmentViewModel model, int? rowIndex, CancellationToken cancellationToken)
    {
        if (!PendingList.IsRow(model.Rows, rowIndex) || model.Rows[rowIndex!.Value].UserId is not { } userId)
        {
            return;
        }

        var stored = (await userApiClient.GetTestConsultantsAsync(cancellationToken)).FirstOrDefault(u => u.UserId == userId);
        if (stored is { AllocationCount: > 0 })
        {
            model.Message = TestConsultantInUseMessage("internal", "have its Active/Inactive status changed", stored.AllocationCount, stored.AllocationString);
            model.MessageIsError = true;
            return;
        }

        var row = model.Rows[rowIndex.Value];
        row.IsInactive = !row.IsInactive;
        row.InactiveDate = row.IsInactive ? DateTime.UtcNow : null;
    }

    // Legacy ButtonSubmit_Click: writes every consultant whose department or status changed, then leaves the page for
    // System Administration. The inactive date comes from the stored record (or now when it newly becomes inactive),
    // never from the posted form. Like legacy, text typed into a row still in edit mode is not applied.
    private async Task<IActionResult> SaveInternalTestConsultantsAsync(InternalTestConsultantDepartmentViewModel model, CancellationToken cancellationToken)
    {
        var stored = (await userApiClient.GetTestConsultantsAsync(cancellationToken)).ToDictionary(u => u.UserId);

        foreach (var row in model.Rows)
        {
            if (row.UserId is not { } userId || !stored.TryGetValue(userId, out var existing) || !IsInternalTestConsultantChanged(row, existing))
            {
                continue;
            }

            var inactiveDate = GetInternalTestConsultantInactiveDate(row, existing);
            await userApiClient.UpdateTestConsultantAsync(userId, new UpdateTestConsultantRequest(row.Department, row.IsInactive, inactiveDate), cancellationToken);
        }

        TempData.SetNotification(NotificationType.Success, "Internal test consultant departments saved successfully.");
        return RedirectToAction(nameof(Index));
    }

    private static DateTime? GetInternalTestConsultantInactiveDate(InternalTestConsultantRowViewModel row, UserResponse existing)
    {
        if (!row.IsInactive)
        {
            return null;
        }

        return existing.IsInactive ? existing.InactiveDate : DateTime.UtcNow;
    }

    private static bool IsInternalTestConsultantChanged(InternalTestConsultantRowViewModel row, UserResponse existing) =>
        row.Department != existing.Department || row.IsInactive != existing.IsInactive;
}
