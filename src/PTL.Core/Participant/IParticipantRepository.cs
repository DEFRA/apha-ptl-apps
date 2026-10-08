namespace PTL.Core.Participant;

public interface IParticipantRepository
{
    Task<Participant?> GetByIdAsync(Guid participantId, CancellationToken cancellationToken = default);
    Task<Participant?> GetBySsoIdAsync(Guid ssoId, CancellationToken cancellationToken = default);

    /// <summary>Reads the participant row matching a CIDM contact id via <c>spgParticipantBySsoId</c>.</summary>
    Task<Participant?> GetBySsoIdExtAsync(Guid ssoIdExt, CancellationToken cancellationToken = default);

    /// <summary>Reads the participant row matching an email address via <c>spgParticipantByEmail</c>, used only as a CIDM fallback.</summary>
    Task<Participant?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ParticipantSummaryEntity>> GetSummariesAsync(Guid customerId, bool includeInactive = false, CancellationToken cancellationToken = default);
    Task<Participant> CreateAsync(Participant participant, CancellationToken cancellationToken = default);
    Task<Participant?> UpdateAsync(Participant participant, CancellationToken cancellationToken = default);
}
