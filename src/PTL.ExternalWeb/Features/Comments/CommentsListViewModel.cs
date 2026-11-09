using PTL.Contracts.Distribution;

namespace PTL.ExternalWeb.Features.Comments;

public sealed record CommentsListViewModel(
    IReadOnlyList<TestConsultantDistributionResponse> Distributions,
    string? Search)
{
    public bool IsFiltered => !string.IsNullOrWhiteSpace(Search);
}
