namespace PTL.Contracts.Participant;

public sealed record ViewerResponse(Guid ViewerId, string Name, string Email);

// Legacy ParticipantViewers.aspx: Available Viewers + Participant Viewers dual-list screen.
public sealed record ParticipantViewerAssignmentResponse(
    Guid CustomerId,
    string LabCode,
    string LabName,
    bool IsActive,
    IReadOnlyList<ViewerResponse> AvailableViewers,
    IReadOnlyList<ViewerResponse> AssignedViewers);

public sealed record UpdateParticipantViewersRequest(IReadOnlyList<Guid> ViewerIds);
