using PTL.ApiClient;
using PTL.Contracts.Contract;

namespace PTL.InternalWeb.Tests.TestSupport;

public sealed class FakeContractExportApiClient : IContractExportApiClient
{
    public IReadOnlyList<SampleAddressResponse> SampleAddresses { get; set; } = [];

    public ContractRenewalResponse? Renewal { get; set; }

    public Task<IReadOnlyList<SampleAddressResponse>> GetSampleAddressesAsync(Guid contractId, CancellationToken cancellationToken = default) =>
        Task.FromResult(SampleAddresses);

    public Task<ContractRenewalResponse?> GetRenewalAsync(Guid contractId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Renewal);
}
