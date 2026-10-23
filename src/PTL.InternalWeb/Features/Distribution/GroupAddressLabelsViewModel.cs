using PTL.Contracts.Lookup;

namespace PTL.InternalWeb.Features.Distribution;

// Legacy Distributions/GroupAddress.aspx - lists every configured Group Address (no search/filter,
// no paging in legacy). Always reflects the latest saved data - no caching/snapshotting.
public sealed class GroupAddressLabelsViewModel
{
    public IReadOnlyList<GroupAddressResponse> GroupAddresses { get; init; } = [];
}
