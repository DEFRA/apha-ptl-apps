namespace PTL.Core.Contract.SampleAddress;

public interface ISampleAddressRepository
{
    Task<IReadOnlyList<SampleAddressEntity>> GetByContractIdAsync(Guid contractId, CancellationToken cancellationToken = default);
}

public interface ISampleAddressService
{
    Task<IReadOnlyList<SampleAddressEntity>> GetByContractIdAsync(Guid contractId, CancellationToken cancellationToken = default);
}

public sealed class SampleAddressService(ISampleAddressRepository sampleAddressRepository) : ISampleAddressService
{
    public Task<IReadOnlyList<SampleAddressEntity>> GetByContractIdAsync(Guid contractId, CancellationToken cancellationToken = default) =>
        sampleAddressRepository.GetByContractIdAsync(contractId, cancellationToken);
}
