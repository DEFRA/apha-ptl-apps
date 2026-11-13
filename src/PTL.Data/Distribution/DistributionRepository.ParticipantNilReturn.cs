using Dapper;
using PTL.Core.Distribution;

namespace PTL.Data.Distribution;

public sealed partial class DistributionRepository
{
    public async Task<ParticipantNilReturnData?> GetParticipantNilReturnAsync(
        Guid distributionParticipantId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                dp.fldDistributionParticipantId AS DistributionParticipantId,
                dp.fldNilReturnReason AS NilReturnReason
            FROM dbo.tlnkDistributionParticipant dp
            WHERE dp.fldDistributionParticipantId = @DistributionParticipantId
            """;

        using var connection = connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<ParticipantNilReturnData>(sql, new { DistributionParticipantId = distributionParticipantId });
    }

    public async Task SaveParticipantNilReturnReasonAsync(
        Guid distributionParticipantId, string nilReturnReason, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        await connection.ExecuteAsync(
            "EXEC dbo.spuDistributionParticipantForAmendResults @DistributionParticipantId = @DistributionParticipantId, @NilReturnReason = @NilReturnReason",
            new { DistributionParticipantId = distributionParticipantId, NilReturnReason = nilReturnReason });
    }
}
