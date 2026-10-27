using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using PTL.Contracts.Country;
using PTL.SharedUI.Notifications;

namespace PTL.InternalWeb.Features.SystemAdministration;

// Manage Countries - legacy ManageCountries.aspx. Edits, adds and removals all stay pending (round-tripped
// through the form, where legacy used ViewState) until the page-level Save writes them. Only Save and
// Cancel leave the page.
public partial class SystemAdministrationController
{
    // Legacy ButtonAdd_Click's exact wording for a name already on the page.
    private const string DuplicateCountryMessage = "This country already exists. Please try with different name.";

    public async Task<IActionResult> CountryManagement(CancellationToken cancellationToken)
    {
        var countries = await countryApiClient.GetCountriesAsync(cancellationToken);

        return await CountryPageAsync(
            new CountryManagementViewModel
            {
                Rows = [.. countries
                    .OrderBy(c => c.Country, StringComparer.OrdinalIgnoreCase)
                    .Select(c => new CountryManagementRowViewModel { CountryId = c.CountryId, Country = c.Country, CountryTypeId = c.CountryTypeId })]
            },
            cancellationToken);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CountryManagement(CountryManagementViewModel model, string? command, int? rowIndex, CancellationToken cancellationToken)
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

        if (PendingListCommands.Is(command, PendingListCommands.Save))
        {
            return await SaveCountriesAsync(model, cancellationToken);
        }

        if (PendingListCommands.Is(command, PendingListCommands.Remove))
        {
            return await RemoveCountryAsync(model, rowIndex, cancellationToken);
        }

        ApplyCountryRowCommand(model, command, rowIndex);
        return await CountryPageAsync(model, cancellationToken);
    }

    // Fills in what is never posted back: the country type options and each row's type name.
    private async Task<ViewResult> CountryPageAsync(CountryManagementViewModel model, CancellationToken cancellationToken)
    {
        var countryTypes = await countryApiClient.GetCountryTypesAsync(cancellationToken);
        model.CountryTypeOptions = [.. countryTypes
            .OrderBy(t => t.CountryType, StringComparer.OrdinalIgnoreCase)
            .Select(t => new SelectListItem(t.CountryType, t.CountryTypeId.ToString()))];

        if (!PendingList.IsRow(model.Rows, model.EditIndex) || model.Rows[model.EditIndex!.Value].IsRemoved)
        {
            model.EditIndex = null;
        }

        for (var i = 0; i < model.Rows.Count; i++)
        {
            var row = model.Rows[i];
            row.CountryType = countryTypes.FirstOrDefault(t => t.CountryTypeId == row.CountryTypeId)?.CountryType ?? string.Empty;
            row.IsEditing = model.EditIndex == i;
        }

        return View(nameof(CountryManagement), model);
    }

    // edit/update/cancelEdit/add change the pending list only - nothing is written and no notification shown.
    private void ApplyCountryRowCommand(CountryManagementViewModel model, string? command, int? rowIndex)
    {
        var isRow = PendingList.IsRow(model.Rows, rowIndex);

        if (PendingListCommands.Is(command, PendingListCommands.Edit) && isRow)
        {
            var row = model.Rows[rowIndex!.Value];
            model.EditIndex = rowIndex;
            model.Edit = new CountryManagementEditViewModel { Country = row.Country, CountryTypeId = row.CountryTypeId };
        }
        else if (PendingListCommands.Is(command, PendingListCommands.Update) && isRow)
        {
            UpdatePendingCountryRow(model, rowIndex!.Value);
        }
        else if (PendingListCommands.Is(command, PendingListCommands.CancelEdit))
        {
            model.EditIndex = null;
        }
        else if (PendingListCommands.Is(command, PendingListCommands.Add))
        {
            AddPendingCountryRow(model);
        }
    }

    // Matches legacy GridViewCountry_Updating: applies the edited row to the in-memory list only.
    private void UpdatePendingCountryRow(CountryManagementViewModel model, int rowIndex)
    {
        if (!TryValidateModel(model.Edit, nameof(model.Edit)))
        {
            model.EditIndex = rowIndex;
            return;
        }

        var row = model.Rows[rowIndex];
        row.Country = model.Edit.Country.Trim();
        row.CountryTypeId = model.Edit.CountryTypeId!.Value;
        model.Edit = new CountryManagementEditViewModel();
        model.EditIndex = null;
    }

    // Matches legacy ButtonAdd_Click: Name and Country Type are required and the name must not already
    // be on the page (case-insensitive).
    private void AddPendingCountryRow(CountryManagementViewModel model)
    {
        if (!TryValidateModel(model.Add, nameof(model.Add)))
        {
            return;
        }

        var name = model.Add.Country.Trim();
        if (model.Rows.Any(r => !r.IsRemoved && string.Equals(r.Country, name, StringComparison.OrdinalIgnoreCase)))
        {
            ModelState.AddModelError($"{nameof(model.Add)}.{nameof(model.Add.Country)}", DuplicateCountryMessage);
            return;
        }

        model.Rows.Add(new CountryManagementRowViewModel { Country = name, CountryTypeId = model.Add.CountryTypeId!.Value });
        model.Add = new CountryManagementAddViewModel();
    }

    // Matches legacy ButtonSave_Click: deletes the removed countries, then writes every added or changed
    // one, then leaves the page for System Administration. Any row the API rejects stays pending, with its
    // message in the error summary. Like legacy, text typed into a row still in edit mode is not applied.
    private async Task<IActionResult> SaveCountriesAsync(CountryManagementViewModel model, CancellationToken cancellationToken)
    {
        model.EditIndex = null;
        await DeleteRemovedCountriesAsync(model, cancellationToken);
        await WriteChangedCountriesAsync(model, cancellationToken);

        if (!ModelState.IsValid)
        {
            return await CountryPageAsync(model, cancellationToken);
        }

        TempData.SetNotification(NotificationType.Success, "Countries saved successfully.");
        return RedirectToAction(nameof(Index));
    }

    private async Task DeleteRemovedCountriesAsync(CountryManagementViewModel model, CancellationToken cancellationToken)
    {
        foreach (var row in model.Rows.Where(r => r.IsRemoved).ToList())
        {
            var result = row.CountryId is { } id
                ? await countryApiClient.DeleteCountryAsync(id, cancellationToken)
                : new CountryDeleteResponse(true, null);

            if (result.Success)
            {
                model.Rows.Remove(row);
                continue;
            }

            LogCountryRemoveBlockedMessage(logger, row.CountryId.GetValueOrDefault(), null);
            ModelState.AddModelError(nameof(model.Rows), $"{row.Country}: {result.Message ?? "The country could not be removed."}");
        }
    }

    private async Task WriteChangedCountriesAsync(CountryManagementViewModel model, CancellationToken cancellationToken)
    {
        var persisted = (await countryApiClient.GetCountriesAsync(cancellationToken)).ToDictionary(c => c.CountryId);

        foreach (var row in model.Rows.Where(r => !r.IsRemoved))
        {
            if (row.CountryId is { } id && persisted.TryGetValue(id, out var existing) && existing.Country == row.Country && existing.CountryTypeId == row.CountryTypeId)
            {
                continue;
            }

            var request = new CountrySaveRequest(row.Country, row.CountryTypeId);
            var result = row.CountryId is { } existingId
                ? await countryApiClient.UpdateCountryAsync(existingId, request, cancellationToken)
                : await countryApiClient.CreateCountryAsync(request, cancellationToken);

            if (result.Success)
            {
                row.CountryId ??= result.Country?.CountryId;
                continue;
            }

            foreach (var message in result.FieldErrors.SelectMany(e => e.Value))
            {
                ModelState.AddModelError(nameof(model.Rows), $"{row.Country}: {message}");
            }
        }
    }

    // Remove marks the row removed on the page. Nothing is deleted until Save,
    // and the user stays on the page. A country that is still used by customers, participants or group
    // addresses cannot be removed (legacy's AllocationCount rule). Removing a country added on this page
    // (not yet saved) just drops it from the pending list.
    private async Task<IActionResult> RemoveCountryAsync(CountryManagementViewModel model, int? rowIndex, CancellationToken cancellationToken)
    {
        if (!PendingList.IsRow(model.Rows, rowIndex))
        {
            return await CountryPageAsync(model, cancellationToken);
        }

        var index = rowIndex!.Value;
        var row = model.Rows[index];

        if (row.CountryId is not { } countryId)
        {
            model.Rows.RemoveAt(index);
            model.EditIndex = PendingList.EditIndexAfterRemoval(model.EditIndex, index);
            return await CountryPageAsync(model, cancellationToken);
        }

        var country = (await countryApiClient.GetCountriesAsync(cancellationToken)).FirstOrDefault(c => c.CountryId == countryId);
        if (country is { AllocationCount: > 0 })
        {
            LogCountryRemoveBlockedMessage(logger, countryId, null);
            model.Message = $"This country cannot be removed because it is being used by {country.AllocationCount} customer(s)/participant(s)/Group Addresses.";
            model.MessageIsError = true;
            return await CountryPageAsync(model, cancellationToken);
        }

        row.IsRemoved = true;
        model.EditIndex = model.EditIndex == index ? null : model.EditIndex;
        return await CountryPageAsync(model, cancellationToken);
    }
}
