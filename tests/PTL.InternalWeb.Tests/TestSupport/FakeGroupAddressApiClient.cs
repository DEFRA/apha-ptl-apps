using PTL.ApiClient;
using PTL.Contracts.GroupAddress;

namespace PTL.InternalWeb.Tests.TestSupport;

internal sealed class FakeGroupAddressApiClient : IGroupAddressApiClient
{
    public IReadOnlyList<GroupAddressResponse> GroupAddresses { get; set; } = [];
    public GroupAddressResponse? GroupAddress { get; set; }
    public GroupAddressSaveResult SaveResult { get; set; } = new(true, null, new Dictionary<string, string[]>());

    public Task<IReadOnlyList<GroupAddressResponse>> GetGroupAddressesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(GroupAddresses);

    public Task<GroupAddressResponse?> GetGroupAddressAsync(Guid groupAddressId, CancellationToken cancellationToken = default) =>
        Task.FromResult(GroupAddress);

    public Task<GroupAddressSaveResult> CreateGroupAddressAsync(GroupAddressSaveRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult(SaveResult);

    public Task<GroupAddressSaveResult> UpdateGroupAddressAsync(Guid groupAddressId, GroupAddressSaveRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult(SaveResult);
}
