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

// ExpectedLength is only meaningful for ResultItems/MethodItems (drives the Printable Scheme
// worksheet's column-width packing); Categories/Criteria always pass the default 0.
public sealed record SchemeTestItemResponse(Guid ItemId, Guid ItemTypeId, string Name, int Order, int ExpectedLength = 0);

public sealed record SchemeCategoryItemResponse(
    Guid CategoryItemId,
    Guid CategoryItemTypeId,
    string Name,
    int Order,
    IReadOnlyList<SchemeTestItemResponse> Criteria);
