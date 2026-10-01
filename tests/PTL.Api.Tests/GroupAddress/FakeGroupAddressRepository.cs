using PTL.Core.GroupAddress;

namespace PTL.Api.Tests.GroupAddress;

internal sealed class FakeGroupAddressRepository : IGroupAddressRepository
{
    public List<PTL.Core.GroupAddress.GroupAddress> GroupAddresses { get; } = [];

    public PTL.Core.GroupAddress.GroupAddress? UpdateReturns { get; set; } = new();

    public bool UpdateReturnsNull { get; set; }

    public Task<IReadOnlyList<PTL.Core.GroupAddress.GroupAddress>> GetAllAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<PTL.Core.GroupAddress.GroupAddress>>(GroupAddresses);

    public Task<PTL.Core.GroupAddress.GroupAddress?> GetByIdAsync(Guid groupAddressId, CancellationToken cancellationToken = default) =>
        Task.FromResult(GroupAddresses.FirstOrDefault(g => g.GroupAddressId == groupAddressId));

    public Task<PTL.Core.GroupAddress.GroupAddress> CreateAsync(PTL.Core.GroupAddress.GroupAddress groupAddress, CancellationToken cancellationToken = default)
    {
        GroupAddresses.Add(groupAddress);
        return Task.FromResult(groupAddress);
    }

    public Task<PTL.Core.GroupAddress.GroupAddress?> UpdateAsync(Guid groupAddressId, PTL.Core.GroupAddress.GroupAddress groupAddress, CancellationToken cancellationToken = default) =>
        Task.FromResult(UpdateReturnsNull ? null : (UpdateReturns ?? groupAddress));
}
