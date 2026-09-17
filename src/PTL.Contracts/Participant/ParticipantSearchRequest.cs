namespace PTL.Contracts.Participant;

public sealed record ParticipantSearchRequest(
    Guid CustomerId,
    string? SearchTerm = null,
    bool IncludeInactive = false,
    int Page = 1,
    int PageSize = 20);
