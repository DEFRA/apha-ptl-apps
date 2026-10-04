using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using PTL.Contracts.AdministrationCharge;

namespace PTL.ApiClient;

public interface IAdministrationChargeApiClient
{
    Task<IReadOnlyList<AdministrationChargeResponse>> GetAdministrationChargesAsync(CancellationToken cancellationToken = default);
    Task<AdministrationChargeSaveResult> SetPriceAsync(UpdateAdministrationChargePriceRequest request, CancellationToken cancellationToken = default);
}

// Thin typed HttpClient wrapper around PTL.Api's administration charge endpoints, shared by every
// web front-end (PTL.InternalWeb, PTL.ExternalWeb, ...).
public sealed class AdministrationChargeApiClient(HttpClient httpClient) : IAdministrationChargeApiClient
{
    public async Task<IReadOnlyList<AdministrationChargeResponse>> GetAdministrationChargesAsync(CancellationToken cancellationToken = default)
    {
        var charges = await httpClient.GetFromJsonAsync<IReadOnlyList<AdministrationChargeResponse>>("/api/administration-charges", cancellationToken);
        return charges ?? [];
    }

    public async Task<AdministrationChargeSaveResult> SetPriceAsync(UpdateAdministrationChargePriceRequest request, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PutAsJsonAsync("/api/administration-charges/price", request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(cancellationToken);
            var errors = problem?.Errors is { Count: > 0 }
                ? problem.Errors.ToDictionary(e => e.Key, e => e.Value)
                : new Dictionary<string, string[]> { [string.Empty] = ["The request was invalid."] };
            return new AdministrationChargeSaveResult(false, null, errors);
        }

        response.EnsureSuccessStatusCode();
        var price = await response.Content.ReadFromJsonAsync<AdministrationChargeCurrencyPriceResponse>(cancellationToken);
        return new AdministrationChargeSaveResult(true, price, new Dictionary<string, string[]>());
    }
}
