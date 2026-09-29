namespace PTL.Core.GroupAddress;

public interface IGroupAddressService
{
    Task<IReadOnlyList<GroupAddress>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<GroupAddress?> GetByIdAsync(Guid groupAddressId, CancellationToken cancellationToken = default);
    Task<GroupAddress> CreateAsync(GroupAddress groupAddress, CancellationToken cancellationToken = default);
    Task<GroupAddress?> UpdateAsync(Guid groupAddressId, GroupAddress groupAddress, CancellationToken cancellationToken = default);
}
