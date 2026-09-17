namespace PTL.Core.Participant;

public sealed record ParticipantSearchResult(IReadOnlyList<ParticipantSummaryEntity> Items, int TotalCount);
