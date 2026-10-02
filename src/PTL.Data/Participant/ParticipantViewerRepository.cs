using Dapper;
using PTL.Core.Participant;
using PTL.Data.Infrastructure;

namespace PTL.Data.Participant;

public sealed class ParticipantViewerRepository(IDbConnectionFactory connectionFactory) : IParticipantViewerRepository
{
    public async Task<IReadOnlyList<ParticipantViewerEntity>> GetByParticipantIdAsync(Guid participantId, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();

        // Result set 1 is the participant header (already covered by IParticipantRepository); only
        // the assigned-viewers result set is needed here.
        using var results = await connection.QueryMultipleAsync(
            "EXEC dbo.spgViewerParticipants @ParticipantId",
            new { ParticipantId = participantId });

        await results.ReadAsync();
        return (await results.ReadAsync<ParticipantViewerEntity>()).ToList();
    }

    public async Task AddAsync(Guid viewerParticipantId, Guid viewerId, Guid participantId, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();

        await connection.ExecuteAsync(
            "EXEC dbo.spiViewerParticipant @ViewerParticipantId = @ViewerParticipantId, @ViewerId = @ViewerId, @ParticipantId = @ParticipantId",
            new { ViewerParticipantId = viewerParticipantId, ViewerId = viewerId, ParticipantId = participantId });
    }

    public async Task RemoveAsync(Guid viewerParticipantId, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();

        await connection.ExecuteAsync(
            "EXEC dbo.spdViewerParticipant @ViewerParticipantId",
            new { ViewerParticipantId = viewerParticipantId });
    }
}
