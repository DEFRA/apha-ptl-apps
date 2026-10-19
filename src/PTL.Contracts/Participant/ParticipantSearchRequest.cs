namespace PTL.Contracts.Participant;

public sealed record ParticipantSearchRequest(
    Guid CustomerId,
    bool IncludeInactive = false,
    int Page = 1,
    int PageSize = 20);
