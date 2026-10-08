using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using PTL.ApiClient;
using PTL.Contracts.GroupAddress;
using PTL.SharedUI.Notifications;

namespace PTL.InternalWeb.Features.GroupAddress;

public sealed class GroupAddressController(IGroupAddressApiClient groupAddressApiClient, ILookupApiClient lookupApiClient) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(int page = 1, int pageSize = PTL.SharedUI.Pagination.PaginationModel.DefaultPageSize, CancellationToken cancellationToken = default)
    {
        var result = await groupAddressApiClient.SearchGroupAddressesAsync(new GroupAddressSearchRequest(page, pageSize), cancellationToken);

        var pagedItems = result.Items
            .Select(g => new GroupAddressSummaryViewModel(g.GroupAddressId, g.Identifier, g.Address1))
            .ToList();

        var model = new GroupAddressListViewModel(result.Page, result.PageSize, result.TotalCount, pagedItems);
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        var item = await groupAddressApiClient.GetGroupAddressAsync(id, cancellationToken);
        if (item is null)
        {
            return NotFound();
        }

        var countryName = (await lookupApiClient.GetCountriesAsync(cancellationToken))
            .FirstOrDefault(c => c.CountryId == item.CountryId)?.Country ?? string.Empty;

        return View(new GroupAddressDetailsViewModel(item, countryName));
    }

    [HttpGet]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        var model = new GroupAddressFormViewModel();
        await PopulateCountryOptionsAsync(model, cancellationToken);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(GroupAddressFormViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            await PopulateCountryOptionsAsync(model, cancellationToken);
            return View(model);
        }

        var result = await groupAddressApiClient.CreateGroupAddressAsync(ToRequest(model), cancellationToken);
        if (!result.Success)
        {
            AddErrors(result.FieldErrors);
            await PopulateCountryOptionsAsync(model, cancellationToken);
            return View(model);
        }

        TempData.SetNotification(NotificationType.Success, "Group Address created successfully.");
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        var item = await groupAddressApiClient.GetGroupAddressAsync(id, cancellationToken);
        if (item is null)
        {
            return NotFound();
        }

        var model = ToFormViewModel(item);
        await PopulateCountryOptionsAsync(model, cancellationToken);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, GroupAddressFormViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            await PopulateCountryOptionsAsync(model, cancellationToken);
            return View(model);
        }

        var result = await groupAddressApiClient.UpdateGroupAddressAsync(id, ToRequest(model), cancellationToken);
        if (!result.Success)
        {
            AddErrors(result.FieldErrors);
            await PopulateCountryOptionsAsync(model, cancellationToken);
            return View(model);
        }

        TempData.SetNotification(NotificationType.Success, "Group Address updated successfully.");
        return RedirectToAction(nameof(Index));
    }

    private async Task PopulateCountryOptionsAsync(GroupAddressFormViewModel model, CancellationToken cancellationToken)
    {
        var countries = await lookupApiClient.GetCountriesAsync(cancellationToken);
        model.CountryOptions = countries
            .Select(c => new SelectListItem(c.Country, c.CountryId.ToString()))
            .Prepend(new SelectListItem("- Please Select -", Guid.Empty.ToString()))
            .ToList();
    }

    private void AddErrors(IReadOnlyDictionary<string, string[]> fieldErrors)
    {
        foreach (var (field, messages) in fieldErrors)
        {
            foreach (var message in messages)
            {
                if (string.IsNullOrWhiteSpace(field))
                {
                    ModelState.AddModelError(string.Empty, message);
                }
                else
                {
                    ModelState.AddModelError(field, message);
                }
            }
        }
    }

    private static GroupAddressSaveRequest ToRequest(GroupAddressFormViewModel model) => new(
        model.Identifier ?? string.Empty,
        model.Address1 ?? string.Empty,
        model.Address2 ?? string.Empty,
        model.Address3 ?? string.Empty,
        model.Address4 ?? string.Empty,
        model.Address5 ?? string.Empty,
        model.CountryId.GetValueOrDefault(),
        model.Telephone ?? string.Empty,
        model.PackingInstructions ?? string.Empty);

    private static GroupAddressFormViewModel ToFormViewModel(GroupAddressResponse response) => new()
    {
        GroupAddressId = response.GroupAddressId,
        Identifier = response.Identifier,
        Address1 = response.Address1,
        Address2 = response.Address2,
        Address3 = response.Address3,
        Address4 = response.Address4,
        Address5 = response.Address5,
        CountryId = response.CountryId,
        Telephone = response.Telephone,
        PackingInstructions = response.PackingInstructions
    };
}
