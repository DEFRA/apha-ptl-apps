using PTL.Contracts.Participant;

namespace PTL.InternalWeb.Features.Participant;

// Legacy ParticipantViewers.aspx dual-list screen.
public sealed record ParticipantViewerFormViewModel(
    Guid ParticipantId,
    Guid CustomerId,
    string LabCode,
    string LabName,
    bool IsActive,
    IReadOnlyList<ViewerResponse> AvailableViewers,
    IReadOnlyList<ViewerResponse> AssignedViewers);
