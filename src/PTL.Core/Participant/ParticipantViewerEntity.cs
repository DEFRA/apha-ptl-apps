namespace PTL.Core.Participant;

// tlnkViewerParticipant - one row per viewer assigned to a participant.
public sealed class ParticipantViewerEntity
{
    public Guid ViewerParticipantId { get; set; }
    public Guid ViewerId { get; set; }
    public Guid ParticipantId { get; set; }
    public string Name { get; set; } = string.Empty;
}

public interface IParticipantViewerRepository
{
    // spgViewerParticipants @ParticipantId - result set 2 (the first is the participant header,
    // already covered by IParticipantRepository.GetByIdAsync).
    Task<IReadOnlyList<ParticipantViewerEntity>> GetByParticipantIdAsync(Guid participantId, CancellationToken cancellationToken = default);

    // spiViewerParticipant @ViewerParticipantId, @ViewerId, @ParticipantId
    Task AddAsync(Guid viewerParticipantId, Guid viewerId, Guid participantId, CancellationToken cancellationToken = default);

    // spdViewerParticipant @ViewerParticipantId
    Task RemoveAsync(Guid viewerParticipantId, CancellationToken cancellationToken = default);
}
