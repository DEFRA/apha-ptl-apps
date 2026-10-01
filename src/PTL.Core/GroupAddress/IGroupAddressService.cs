namespace PTL.Core.GroupAddress;

public sealed record GroupAddressSearchResult(IReadOnlyList<GroupAddress> Items, int TotalCount);

public interface IGroupAddressService
{
    Task<IReadOnlyList<GroupAddress>> GetAllAsync(CancellationToken cancellationToken = default);

    // In-memory paging over GetAllAsync's result set, ordered by Identifier - see GroupAddressService for rationale.
    Task<GroupAddressSearchResult> SearchAsync(int page, int pageSize, CancellationToken cancellationToken = default);
    Task<GroupAddress?> GetByIdAsync(Guid groupAddressId, CancellationToken cancellationToken = default);
    Task<GroupAddress> CreateAsync(GroupAddress groupAddress, CancellationToken cancellationToken = default);
    Task<GroupAddress?> UpdateAsync(Guid groupAddressId, GroupAddress groupAddress, CancellationToken cancellationToken = default);
}
