using PTL.Contracts.Distribution;

namespace PTL.ExternalWeb.Features.Distribution;

/// <summary>View model for the participant-facing Previous Distributions screen (legacy
/// DistributionsPrevious.aspx). Search/sort/pagination are all applied in-memory by the controller
/// over one full fetch per request, the same pattern as Features/Scheme/SchemeController.</summary>
public sealed record DistributionPreviousListViewModel(
    IReadOnlyList<ParticipantPastDistributionResponse> Distributions,
    int TotalCount,
    int Page,
    int PageSize,
    string SortBy,
    string SortDirection,
    int? Year,
    int? Month,
    string? DistributionReference,
    string? SchemeCode,
    string? SchemeName)
{
    public bool IsFiltered =>
        Year is not null || Month is not null
        || !string.IsNullOrWhiteSpace(DistributionReference)
        || !string.IsNullOrWhiteSpace(SchemeCode)
        || !string.IsNullOrWhiteSpace(SchemeName);
}
