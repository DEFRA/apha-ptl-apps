using Microsoft.AspNetCore.Mvc;
using PTL.Contracts.Viewer;
using PTL.InternalWeb.Confirmation;
using PTL.SharedUI.Notifications;

namespace PTL.InternalWeb.Features.SystemAdministration;

// Manage Viewers - legacy ManageViewers.aspx. Edits, adds and removals all stay pending (round-tripped
// through the form, where legacy used ViewState) until the page-level Save/Apply writes them. Only Save and
// Cancel leave the page.
public partial class SystemAdministrationController
{
    private static readonly Action<ILogger, Guid, Exception?> LogViewerRemoveBlockedMessage =
        LoggerMessage.Define<Guid>(
            LogLevel.Warning,
            new EventId(101, nameof(LogViewerRemoveBlockedMessage)),
            "Viewer {ViewerId} could not be removed");

    public async Task<IActionResult> ViewerManagement(CancellationToken cancellationToken)
    {
        var viewers = await viewerApiClient.GetAllAsync(cancellationToken);

        return ViewerPage(new ViewerManagementViewModel
        {
            Rows = [.. viewers
                .OrderBy(v => v.Name, StringComparer.OrdinalIgnoreCase)
                .Select(v => new ViewerRowViewModel { ViewerId = v.ViewerId, Name = v.Name, Email = v.Email })]
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ViewerManagement(ViewerManagementViewModel model, string? command, int? rowIndex, string? confirmation, CancellationToken cancellationToken)
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
            return await SaveViewersAsync(model, leavePage: PendingListCommands.Is(command, PendingListCommands.Save), cancellationToken);
        }

        if (PendingListCommands.Is(command, PendingListCommands.Remove))
        {
            return await RemoveViewerAsync(model, rowIndex, confirmation, cancellationToken);
        }

        ApplyViewerRowCommand(model, command, rowIndex);
        return ViewerPage(model);
    }

    private ViewResult ViewerPage(ViewerManagementViewModel model)
    {
        if (!PendingList.IsRow(model.Rows, model.EditIndex) || model.Rows[model.EditIndex!.Value].IsRemoved)
        {
            model.EditIndex = null;
        }

        for (var i = 0; i < model.Rows.Count; i++)
        {
            model.Rows[i].IsEditing = model.EditIndex == i;
        }

        return View(nameof(ViewerManagement), model);
    }

    // edit/update/cancelEdit/add change the pending list only - nothing is written and no notification shown.
    private void ApplyViewerRowCommand(ViewerManagementViewModel model, string? command, int? rowIndex)
    {
        var isRow = PendingList.IsRow(model.Rows, rowIndex);

        if (PendingListCommands.Is(command, PendingListCommands.Edit) && isRow)
        {
            var row = model.Rows[rowIndex!.Value];
            model.EditIndex = rowIndex;
            model.Edit = new ViewerEditViewModel { Name = row.Name, Email = row.Email };
        }
        else if (PendingListCommands.Is(command, PendingListCommands.Update) && isRow)
        {
            UpdatePendingViewerRow(model, rowIndex!.Value);
        }
        else if (PendingListCommands.Is(command, PendingListCommands.CancelEdit))
        {
            model.EditIndex = null;
        }
        else if (PendingListCommands.Is(command, PendingListCommands.Add) && TryValidateModel(model.Add, nameof(model.Add)))
        {
            model.Rows.Add(new ViewerRowViewModel { Name = model.Add.Name.Trim(), Email = model.Add.Email.Trim() });
            model.Add = new ViewerAddViewModel();
        }
    }

    // Matches legacy GridViewViewer_Updating: applies the edited row to the in-memory list only.
    private void UpdatePendingViewerRow(ViewerManagementViewModel model, int rowIndex)
    {
        if (!TryValidateModel(model.Edit, nameof(model.Edit)))
        {
            model.EditIndex = rowIndex;
            return;
        }

        var row = model.Rows[rowIndex];
        row.Name = model.Edit.Name.Trim();
        row.Email = model.Edit.Email.Trim();
        model.Edit = new ViewerEditViewModel();
        model.EditIndex = null;
    }

    // Matches legacy ButtonSave_Click/ButtonApply: deletes the removed viewers, then writes every added or
    // changed one. Save then leaves the page for System Administration; Apply stays on the page. Any row
    // the API rejects stays pending, with its message in the error summary. Like legacy, text typed into
    // a row still in edit mode is not applied.
    private async Task<IActionResult> SaveViewersAsync(ViewerManagementViewModel model, bool leavePage, CancellationToken cancellationToken)
    {
        model.EditIndex = null;
        await DeleteRemovedViewersAsync(model, cancellationToken);
        await WriteChangedViewersAsync(model, cancellationToken);

        if (!ModelState.IsValid)
        {
            return ViewerPage(model);
        }

        TempData.SetNotification(NotificationType.Success, "Viewers saved successfully.");
        return leavePage ? RedirectToAction(nameof(Index)) : ViewerPage(model);
    }

    private async Task DeleteRemovedViewersAsync(ViewerManagementViewModel model, CancellationToken cancellationToken)
    {
        foreach (var row in model.Rows.Where(r => r.IsRemoved).ToList())
        {
            var result = row.ViewerId is { } id
                ? await viewerApiClient.DeleteAsync(id, cancellationToken)
                : new ViewerDeleteResponse(true, null);

            if (result.Success)
            {
                model.Rows.Remove(row);
                continue;
            }

            LogViewerRemoveBlockedMessage(logger, row.ViewerId.GetValueOrDefault(), null);
            ModelState.AddModelError(nameof(model.Rows), $"{row.Name}: {result.Message ?? "The viewer could not be removed."}");
        }
    }

    private async Task WriteChangedViewersAsync(ViewerManagementViewModel model, CancellationToken cancellationToken)
    {
        var persisted = (await viewerApiClient.GetAllAsync(cancellationToken)).ToDictionary(v => v.ViewerId);

        foreach (var row in model.Rows.Where(r => !r.IsRemoved))
        {
            if (row.ViewerId is { } id && persisted.TryGetValue(id, out var existing) && existing.Name == row.Name && existing.Email == row.Email)
            {
                continue;
            }

            var result = row.ViewerId is { } existingId
                ? await viewerApiClient.UpdateAsync(existingId, new ViewerSaveRequest(row.Name, row.Email), cancellationToken)
                : await viewerApiClient.CreateAsync(new ViewerSaveRequest(row.Name, row.Email), cancellationToken);

            if (result.Success)
            {
                row.ViewerId ??= result.Viewer?.ViewerId;
                continue;
            }

            foreach (var message in result.FieldErrors.SelectMany(e => e.Value))
            {
                ModelState.AddModelError(nameof(model.Rows), $"{row.Name}: {message}");
            }
        }
    }

    // Remove -> confirmation page -> the row is marked removed on the page. Nothing is deleted until
    // Save/Apply, and the user stays on the page. A viewer that is still assigned to schemes or
    // participants cannot be removed; the assignments are listed instead. Removing a viewer added on this
    // page (not yet saved) just drops it from the pending list.
    private async Task<IActionResult> RemoveViewerAsync(ViewerManagementViewModel model, int? rowIndex, string? confirmation, CancellationToken cancellationToken)
    {
        if (!PendingList.IsRow(model.Rows, rowIndex) || ConfirmationFlow.IsCancelled(confirmation))
        {
            return ViewerPage(model);
        }

        var index = rowIndex!.Value;
        var row = model.Rows[index];

        if (row.ViewerId is not { } viewerId)
        {
            model.Rows.RemoveAt(index);
            model.EditIndex = PendingList.EditIndexAfterRemoval(model.EditIndex, index);
            return ViewerPage(model);
        }

        var viewer = (await viewerApiClient.GetAllAsync(cancellationToken)).FirstOrDefault(v => v.ViewerId == viewerId);
        if (viewer is not null && (viewer.Schemes.Count > 0 || viewer.Participants.Count > 0))
        {
            LogViewerRemoveBlockedMessage(logger, viewerId, null);
            model.Message = "This viewer cannot be removed because it is assigned to one or more schemes or participants. Remove those assignments before removing the viewer.";
            model.MessageIsError = true;
            model.MessageDetails =
            [
                .. viewer.Schemes.Select(s => $"Scheme {s.Identifier}: {s.Name}"),
                .. viewer.Participants.Select(p => $"Participant {p.LabCode}: {p.LabName}")
            ];
            return ViewerPage(model);
        }

        if (!ConfirmationFlow.IsConfirmed(confirmation))
        {
            return this.ConfirmationPage(
                new ConfirmationPageViewModel
                {
                    Title = "Remove viewer",
                    Caption = "Manage Viewers",
                    Message = $"You are about to remove the viewer {row.Name} ({row.Email}).",
                    WarningText = "The viewer is removed from this list now and deleted when you select Save or Apply. Select Cancel on the page to keep it.",
                    ConfirmText = "Remove viewer",
                    IsDestructive = true,
                },
                nameof(ViewerManagement));
        }

        row.IsRemoved = true;
        model.EditIndex = model.EditIndex == index ? null : model.EditIndex;
        return ViewerPage(model);
    }
}
