using Microsoft.AspNetCore.Mvc;
using PTL.ApiClient;
using PTL.Contracts.AdministrationCharge;

namespace PTL.InternalWeb.Features.AdministrationCharge;

// Authentication/authorization are out of scope for this phase - assume the current user is
// already authenticated with full access, same as every other InternalWeb controller.
public class AdministrationChargeController(IAdministrationChargeApiClient administrationChargeApiClient, ILookupApiClient lookupApiClient, ILogger<AdministrationChargeController> logger) : Controller
{
    private static readonly Action<ILogger, Guid, Guid, Exception?> LogSetPriceFailedMessage =
        LoggerMessage.Define<Guid, Guid>(
            LogLevel.Warning,
            new EventId(1, nameof(LogSetPriceFailedMessage)),
            "Failed to save administration charge {AdministrationChargeId} price for currency {CurrencyId}");

    public async Task<IActionResult> Index(CancellationToken cancellationToken) =>
        View(await BuildViewModelAsync(cancellationToken));

    // Whole-grid submit: every cell is (re)saved, not just the ones the user actually changed -
    // idempotent for unchanged cells, and far simpler than diffing against the previously loaded
    // values. Mirrors the legacy page's end result (Save persists every price shown on screen).
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(AdministrationChargeListViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        foreach (var charge in model.Charges)
        {
            foreach (var price in charge.Prices)
            {
                var result = await administrationChargeApiClient.SetPriceAsync(
                    new UpdateAdministrationChargePriceRequest(charge.AdministrationChargeId, price.CurrencyId, price.Price),
                    cancellationToken);
                if (!result.Success)
                {
                    LogSetPriceFailedMessage(logger, charge.AdministrationChargeId, price.CurrencyId, null);
                    ModelState.AddModelError(string.Empty, "One or more prices could not be saved. Please try again.");
                }
            }
        }

        return ModelState.IsValid ? RedirectToAction(nameof(Index)) : View(model);
    }

    private async Task<AdministrationChargeListViewModel> BuildViewModelAsync(CancellationToken cancellationToken)
    {
        var currencies = await lookupApiClient.GetCurrenciesAsync(cancellationToken);
        var charges = await administrationChargeApiClient.GetAdministrationChargesAsync(cancellationToken);

        var rows = charges.Select(charge => new AdministrationChargeRowViewModel
        {
            AdministrationChargeId = charge.AdministrationChargeId,
            Name = charge.Name,
            Prices = currencies.Select(currency => new AdministrationChargePriceCellViewModel
            {
                CurrencyId = currency.CurrencyId,
                CurrencyLabel = currency.LongName,
                Price = charge.Prices.FirstOrDefault(p => p.CurrencyId == currency.CurrencyId)?.Price ?? 0m
            }).ToList()
        }).ToList();

        return new AdministrationChargeListViewModel { Charges = rows };
    }
}
