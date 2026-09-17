namespace PTL.Contracts.Participant;

public sealed record ParticipantSearchResponse(
    IReadOnlyList<ParticipantSummaryResponse> Items,
    int TotalCount,
    int Page,
    int PageSize);
