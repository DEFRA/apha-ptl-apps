using PTL.Core.Viewer;

namespace PTL.Core.Participant;

// Legacy ParticipantViewers.aspx screen: Available Viewers (every viewer not yet assigned) and
// Participant Viewers (current assignments) for one participant.
public sealed record ParticipantViewerAssignment(
    Guid CustomerId,
    string LabCode,
    string LabName,
    bool IsActive,
    IReadOnlyList<ViewerEntity> AvailableViewers,
    IReadOnlyList<ParticipantViewerEntity> AssignedViewers);
