using Dapper;
using PTL.Core.Distribution;

namespace PTL.Data.Distribution;

public sealed partial class DistributionRepository
{
    public async Task SendTabulationSchemesAsync(IReadOnlyList<TabulationSendWrite> writes, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(writes);

        using var connection = connectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        foreach (var write in writes)
        {
            await connection.ExecuteAsync(
                """
                EXEC dbo.spuMonthlyDistributionSchemeForResultsReady
                    @MonthlyDistributionSchemeId = @MonthlyDistributionSchemeId,
                    @IsReadyForTestConsultant = @IsReadyForTestConsultant,
                    @IsReadyForResultsSignOff = @IsReadyForResultsSignOff,
                    @IsReadyForAssessor = @IsReadyForAssessor
                """,
                new { write.MonthlyDistributionSchemeId, write.IsReadyForTestConsultant, write.IsReadyForResultsSignOff, write.IsReadyForAssessor },
                transaction);

            if (write.SubmitComments)
            {
                await connection.ExecuteAsync(
                    "EXEC dbo.sppSubmitComment @MonthlyDistributionSchemeId = @MonthlyDistributionSchemeId",
                    new { write.MonthlyDistributionSchemeId },
                    transaction);
            }

            foreach (var (tabulationId, publishState) in write.PublishStates)
            {
                await connection.ExecuteAsync(
                    "EXEC dbo.spuTabulationForSignOff @DistributionTabulationId = @TabulationId, @PublishState = @PublishState",
                    new { TabulationId = tabulationId, PublishState = publishState },
                    transaction);
            }
        }

        transaction.Commit();
    }
}
