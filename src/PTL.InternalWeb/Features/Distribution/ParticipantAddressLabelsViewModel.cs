using PTL.Contracts.Search;
using PTL.InternalWeb.Features.Search;

namespace PTL.InternalWeb.Features.Distribution;

// Legacy Distributions/ParticipantAddress.aspx ("This page can be used to print single
// Participant Address labels."). Results is null until Search has been clicked (legacy's results
// panel starts hidden); an empty list means a search ran and found nothing ("There are no Results
// to Display", legacy GVparticipants EmptyDataTemplate).
public sealed class ParticipantAddressLabelsViewModel
{
    public ParticipantSearchForm Search { get; init; } = new();

    public IReadOnlyList<ParticipantSearchResultResponse>? Results { get; init; }
}
