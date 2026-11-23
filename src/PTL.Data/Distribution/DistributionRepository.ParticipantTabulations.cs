using Dapper;
using PTL.Core.Distribution;

namespace PTL.Data.Distribution;

// Legacy ExternalWeb Tabulations.aspx - spgTabulations' @SsoIdExt branch (PTL.Database script 0017).
public sealed partial class DistributionRepository
{
    public async Task<IReadOnlyList<PublishedTabulationSummaryEntity>> GetTabulationsForExternalUserAsync(
        Guid ssoIdExt, Guid? participantId, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        return (await connection.QueryAsync<PublishedTabulationSummaryEntity>(
            "EXEC dbo.spgTabulations @SsoIdExt = @SsoIdExt, @ParticipantId = @ParticipantId",
            new { SsoIdExt = ssoIdExt, ParticipantId = participantId })).ToList();
    }
}
