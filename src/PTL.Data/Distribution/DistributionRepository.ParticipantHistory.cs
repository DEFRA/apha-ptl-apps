using Dapper;
using PTL.Core.Distribution;
using PTL.Data.Infrastructure;

namespace PTL.Data.Distribution;

// Legacy ExternalWeb DistributionsPrevious.aspx / ResultsView.aspx - reuses the existing
// spgPastDistributionsBySsoId and spgResultsEntry stored procedures exactly, same pattern as
// SchemeRepository's spgCommercialSchemesBySsoId.
public sealed partial class DistributionRepository
{
    public async Task<IReadOnlyList<ParticipantPastDistributionEntity>> GetParticipantDistributionHistoryAsync(
        Guid ssoId, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        return (await connection.QueryAsync<ParticipantPastDistributionEntity>(
            "EXEC dbo.spgPastDistributionsBySsoId @SsoId",
            new { SsoId = ssoId })).ToList();
    }

    // spgResultsEntry returns 4 result sets (header, Tests, TestResultItems, TestMethodItems) -
    // only its header result set is used here, read via a plain (non-grid-reader) query, which
    // reads the first result set only and leaves the rest unread.
    public async Task<Guid?> ResolveDistributionParticipantIdAsync(
        Guid ssoId, Guid monthlyDistributionSchemeId, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        var header = await connection.QuerySingleOrDefaultAsync<ResultsEntryHeaderEntity>(
            "EXEC dbo.spgResultsEntry @MonthlyDistributionSchemeId = @MonthlyDistributionSchemeId, @SsoId = @SsoId",
            new { MonthlyDistributionSchemeId = monthlyDistributionSchemeId, SsoId = ssoId });
        return header?.ActualResultsId;
    }
}

// spgResultsEntry result set 1 - fldActualResultsId is NULL (and the row absent, since the
// procedure's own header SELECT filters on it) when this SsoId has no row for the distribution.
internal sealed class ResultsEntryHeaderEntity
{
    public Guid ActualResultsId { get; set; }
}
