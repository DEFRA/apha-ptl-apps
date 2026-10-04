using PTL.Contracts.Participant;

namespace PTL.ApiClient;

public interface IParticipantApiClient
{
    Task<IReadOnlyList<ParticipantSummaryResponse>> GetParticipantsAsync(Guid customerId, bool includeInactive = false, CancellationToken cancellationToken = default);
    Task<ParticipantResponse?> GetParticipantAsync(Guid participantId, CancellationToken cancellationToken = default);
    Task<ParticipantSearchResponse> SearchParticipantsAsync(ParticipantSearchRequest request, CancellationToken cancellationToken = default);
    Task<ParticipantSaveResult> CreateParticipantAsync(ParticipantRequest request, CancellationToken cancellationToken = default);
    Task<ParticipantSaveResult> UpdateParticipantAsync(Guid participantId, ParticipantRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PendingParticipantUpdateSummaryResponse>> GetPendingParticipantUpdatesAsync(CancellationToken cancellationToken = default);
    Task<PendingParticipantUpdateComparisonResponse?> GetPendingParticipantUpdateAsync(Guid participantId, CancellationToken cancellationToken = default);
    Task<PendingParticipantUpdateDecisionResult> ApprovePendingParticipantUpdateAsync(Guid participantId, PendingParticipantUpdateSaveRequest? request = null, CancellationToken cancellationToken = default);
    Task<bool> DeclinePendingParticipantUpdateAsync(Guid participantId, CancellationToken cancellationToken = default);

    // Legacy ParticipantViewers.aspx. Returns null when the participant does not exist.
    Task<ParticipantViewerAssignmentResponse?> GetParticipantViewersAsync(Guid participantId, CancellationToken cancellationToken = default);

    // Returns false when the participant does not exist.
    Task<bool> UpdateParticipantViewersAsync(Guid participantId, IReadOnlyList<Guid> viewerIds, CancellationToken cancellationToken = default);
}
