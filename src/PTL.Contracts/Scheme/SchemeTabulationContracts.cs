namespace PTL.Contracts.Scheme;

// The Results Tabulations tab. Availability is two flags in the database but one three-way
// choice in the UI, matching legacy's dropdown.
public sealed record SchemeTabulationRequest(
    Guid TabulationId,
    string Name,
    bool IntendedResultsOnly,
    bool SingleParticipantTabulation,
    bool ShowRatings,
    bool AvailableToParticipants,
    bool AvailableToViewers,
    IReadOnlyList<Guid> ResultItemIds,
    IReadOnlyList<Guid> MethodItemIds);

public sealed record SchemeTabulationResponse(
    Guid TabulationId,
    string Name,
    bool IntendedResultsOnly,
    bool SingleParticipantTabulation,
    bool ShowRatings,
    bool AvailableToParticipants,
    bool AvailableToViewers,
    IReadOnlyList<Guid> ResultItemIds,
    IReadOnlyList<Guid> MethodItemIds);
