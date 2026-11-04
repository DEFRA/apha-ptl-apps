using Dapper;
using PTL.Core.Distribution;

namespace PTL.Data.Distribution;

// Legacy ExternalWeb Tabulations.aspx - reuses the existing spgTabulations stored procedure
// exactly (already granted to ProficiencyTestingExternalUser, unused until now), same pattern as
// DistributionRepository.ParticipantHistory.cs's spgPastDistributionsBySsoId.
public sealed partial class DistributionRepository
{
    public async Task<IReadOnlyList<PublishedTabulationSummaryEntity>> GetTabulationsForSsoIdAsync(
        Guid ssoId, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        return (await connection.QueryAsync<PublishedTabulationSummaryEntity>(
            "EXEC dbo.spgTabulations @SsoId",
            new { SsoId = ssoId })).ToList();
    }
}
