using PTL.ApiClient;
using PTL.Contracts.AdministrationCharge;

namespace PTL.InternalWeb.Tests.TestSupport;

// Test double for IAdministrationChargeApiClient so AdministrationChargeController tests don't
// need a real HTTP call to PTL.Api.
internal sealed class FakeAdministrationChargeApiClient : IAdministrationChargeApiClient
{
    public IReadOnlyList<AdministrationChargeResponse> Charges { get; set; } = [];
    public bool SetPriceSucceeds { get; set; } = true;
    public List<UpdateAdministrationChargePriceRequest> SetPriceRequests { get; } = [];

    public Task<IReadOnlyList<AdministrationChargeResponse>> GetAdministrationChargesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(Charges);

    public Task<AdministrationChargeSaveResult> SetPriceAsync(UpdateAdministrationChargePriceRequest request, CancellationToken cancellationToken = default)
    {
        SetPriceRequests.Add(request);
        return Task.FromResult(SetPriceSucceeds
            ? new AdministrationChargeSaveResult(true, new AdministrationChargeCurrencyPriceResponse(request.CurrencyId, request.Price), new Dictionary<string, string[]>())
            : new AdministrationChargeSaveResult(false, null, new Dictionary<string, string[]> { [string.Empty] = ["Save failed."] }));
    }
}
