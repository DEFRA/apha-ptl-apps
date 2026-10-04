using PTL.Core.Participant;

namespace PTL.Api.Tests.Participant;

internal sealed class FakeParticipantViewerRepository : IParticipantViewerRepository
{
    private readonly List<ParticipantViewerEntity> _assignments = [];

    public IReadOnlyList<ParticipantViewerEntity> Assignments => _assignments;

    public Task<IReadOnlyList<ParticipantViewerEntity>> GetByParticipantIdAsync(Guid participantId, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<ParticipantViewerEntity> result = _assignments.Where(a => a.ParticipantId == participantId).ToList();
        return Task.FromResult(result);
    }

    public Task AddAsync(Guid viewerParticipantId, Guid viewerId, Guid participantId, CancellationToken cancellationToken = default)
    {
        _assignments.Add(new ParticipantViewerEntity
        {
            ViewerParticipantId = viewerParticipantId,
            ViewerId = viewerId,
            ParticipantId = participantId
        });
        return Task.CompletedTask;
    }

    public Task RemoveAsync(Guid viewerParticipantId, CancellationToken cancellationToken = default)
    {
        _assignments.RemoveAll(a => a.ViewerParticipantId == viewerParticipantId);
        return Task.CompletedTask;
    }
}
