using PTL.Contracts.Participant;

namespace PTL.ApiClient;

public interface IParticipantApiClient
{
    Task<IReadOnlyList<ParticipantSummaryResponse>> GetParticipantsAsync(Guid customerId, bool includeInactive = false, CancellationToken cancellationToken = default);
    Task<ParticipantResponse?> GetParticipantAsync(Guid participantId, CancellationToken cancellationToken = default);
    Task<ParticipantSearchResponse> SearchParticipantsAsync(ParticipantSearchRequest request, CancellationToken cancellationToken = default);
    Task<ParticipantResponse> CreateParticipantAsync(CreateParticipantRequest request, CancellationToken cancellationToken = default);
    Task<ParticipantResponse?> UpdateParticipantAsync(Guid participantId, UpdateParticipantRequest request, CancellationToken cancellationToken = default);
    Task<ParticipantResponse?> DeactivateParticipantAsync(Guid participantId, CancellationToken cancellationToken = default);
    Task<ParticipantResponse?> ReactivateParticipantAsync(Guid participantId, CancellationToken cancellationToken = default);
}
