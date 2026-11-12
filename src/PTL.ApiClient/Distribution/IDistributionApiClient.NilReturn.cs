using PTL.Contracts.Distribution;

namespace PTL.ApiClient.Distribution;

public partial interface IDistributionApiClient
{
    Task<ParticipantNilReturnResponse?> GetParticipantNilReturnAsync(
        Guid monthlyDistributionSchemeId,
        Guid distributionParticipantId,
        CancellationToken cancellationToken = default);

    Task<ParticipantNilReturnSaveResult?> SaveParticipantNilReturnAsync(
        Guid monthlyDistributionSchemeId,
        Guid distributionParticipantId,
        ParticipantNilReturnSaveRequest request,
        CancellationToken cancellationToken = default);
}
