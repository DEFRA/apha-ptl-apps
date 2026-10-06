namespace PTL.Contracts.Scheme;

// The Tests tab tree. Sent whole on save - legacy likewise writes the entire collection from
// ViewState rather than issuing per-item commands.
public sealed record SchemeTestRequest(
    Guid TestId,
    Guid TestTypeId,
    IReadOnlyList<SchemeTestItemRequest> ResultItems,
    IReadOnlyList<SchemeTestItemRequest> MethodItems,
    IReadOnlyList<SchemeCategoryItemRequest> Categories);

public sealed record SchemeTestItemRequest(Guid ItemId, Guid ItemTypeId);

public sealed record SchemeCategoryItemRequest(
    Guid CategoryItemId,
    Guid CategoryItemTypeId,
    IReadOnlyList<SchemeTestItemRequest> Criteria);

public sealed record SchemeTestResponse(
    Guid TestId,
    Guid TestTypeId,
    string TestType,
    int Order,
    IReadOnlyList<SchemeTestItemResponse> ResultItems,
    IReadOnlyList<SchemeTestItemResponse> MethodItems,
    IReadOnlyList<SchemeCategoryItemResponse> Categories);

public sealed record SchemeTestItemResponse(Guid ItemId, Guid ItemTypeId, string Name, int Order);

public sealed record SchemeCategoryItemResponse(
    Guid CategoryItemId,
    Guid CategoryItemTypeId,
    string Name,
    int Order,
    IReadOnlyList<SchemeTestItemResponse> Criteria);
