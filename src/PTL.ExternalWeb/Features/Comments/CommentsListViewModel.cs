namespace PTL.ExternalWeb.Features.Comments;

public sealed record CommentsListViewModel(
    IReadOnlyList<CommentsDistributionListRow> Distributions,
    string? DistributionReference,
    string? SchemeCode,
    string? SchemeName,
    string SortBy,
    string SortDirection)
{
    public bool IsFiltered =>
        !string.IsNullOrWhiteSpace(DistributionReference)
        || !string.IsNullOrWhiteSpace(SchemeCode)
        || !string.IsNullOrWhiteSpace(SchemeName);
}

public sealed record CommentsDistributionListRow(
    Guid MonthlyDistributionSchemeId,
    string DistributionReference,
    string SchemeCode,
    string SchemeName,
    DateTime TargetDate,
    string CommentStatus,
    string CommentsProvidedBy,
    bool IsSubmitted);
