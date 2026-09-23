using PTL.Contracts.Participant;

namespace PTL.ApiClient;

public interface IParticipantApiClient
{
    Task<IReadOnlyList<ParticipantSummaryResponse>> GetParticipantsAsync(Guid customerId, bool includeInactive = false, CancellationToken cancellationToken = default);
    Task<ParticipantResponse?> GetParticipantAsync(Guid participantId, CancellationToken cancellationToken = default);
    Task<ParticipantSearchResponse> SearchParticipantsAsync(ParticipantSearchRequest request, CancellationToken cancellationToken = default);
    Task<ParticipantSaveResult> CreateParticipantAsync(ParticipantRequest request, CancellationToken cancellationToken = default);
    Task<ParticipantSaveResult> UpdateParticipantAsync(Guid participantId, ParticipantRequest request, CancellationToken cancellationToken = default);
}
