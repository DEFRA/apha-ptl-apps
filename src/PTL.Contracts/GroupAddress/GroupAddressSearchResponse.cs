namespace PTL.Contracts.GroupAddress;

public sealed record GroupAddressSearchResponse(IReadOnlyList<GroupAddressResponse> Items, int TotalCount, int Page, int PageSize);
