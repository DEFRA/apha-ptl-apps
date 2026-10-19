using System.Data;
using Dapper;
using PTL.Core.Distribution;

namespace PTL.Data.Distribution;

// Legacy SetupMonthlyDistribution (MenuDistributions.aspx "Initialise this Month").
public sealed partial class DistributionRepository
{
    private const string InsertSchemeSql = """
        EXEC dbo.spiMonthlyDistributionScheme
            @MonthlyDistributionSchemeId = @MonthlyDistributionSchemeId,
            @MonthlyDistributionId = @MonthlyDistributionId,
            @SchemeId = @SchemeId,
            @MonthlyDistributionReference = @DistributionReference,
            @MonthlyDistributionReferenceSuffix = @DistributionReferenceSuffix,
            @MonthlyDistributionDate = @DistributionDate,
            @DeadlineDate = @DeadlineDate,
            @OverseasPostingDate = @OverseasPostingDate,
            @ResultsIssueTargetDate = @ResultsIssueTargetDate,
            @Comments = @Comments,
            @HasIntendedResults = @HasIntendedResults,
            @HasSampleNumbersDefined = @HasSampleNumbersDefined,
            @IsCancelled = @IsCancelled,
            @StoreRatings = @StoreRatings,
            @SchemeVersionDate = @SchemeVersionDate
        """;

    private const string InsertParticipantSql = """
        EXEC dbo.spiDistributionParticipant
            @DistributionParticipantId = @DistributionParticipantId,
            @ParticipantId = @ParticipantId,
            @MonthlyDistributionSchemeId = @MonthlyDistributionSchemeId,
            @DeadlineDate = @DeadlineDate,
            @DeadlineChangeReason = @DeadlineChangeReason,
            @AdditionalParticipant = @AdditionalParticipant
        """;

    public async Task<bool> InitialiseMonthAsync(
        int yearId, int monthId, MonthSetupPlanner planner, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(planner);

        using var connection = connectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        // Legacy FetchMonthlyDistribution: an existing month is left exactly as it is.
        using (var existing = await connection.QueryMultipleAsync(
            "EXEC dbo.spgMonthlyDistribution @YearId = @YearId, @MonthId = @MonthId",
            new { YearId = yearId, MonthId = monthId },
            transaction))
        {
            var header = await existing.ReadFirstOrDefaultAsync<MonthlyDistributionHeaderEntity>();
            if (header is not null && header.MonthlyDistributionId != Guid.Empty)
            {
                transaction.Rollback();
                return false;
            }
        }

        List<MonthSetupSchemeRow> schemeRows;
        List<MonthSetupParticipantRow> participantRows;
        using (var setup = await connection.QueryMultipleAsync(
            "EXEC dbo.sppSetupMonthlyDistribution @YearId = @YearId, @MonthId = @MonthId",
            new { YearId = yearId, MonthId = monthId },
            transaction))
        {
            schemeRows = (await setup.ReadAsync<MonthSetupSchemeRow>()).ToList();
            participantRows = (await setup.ReadAsync<MonthSetupParticipantRow>()).ToList();
        }

        var plan = planner(schemeRows, participantRows);

        await connection.ExecuteAsync(
            "EXEC dbo.spiMonthlyDistribution @MonthlyDistributionId = @MonthlyDistributionId, @YearId = @YearId, @MonthId = @MonthId",
            new { plan.MonthlyDistributionId, plan.YearId, plan.MonthId },
            transaction);

        await InsertSchemesAndParticipantsAsync(connection, transaction, plan.Schemes, plan.Participants);

        transaction.Commit();
        return true;
    }

    // Each scheme is inserted with its own participants, in the order legacy saved them.
    private static async Task InsertSchemesAndParticipantsAsync(
        IDbConnection connection,
        IDbTransaction transaction,
        IReadOnlyList<MonthlyDistributionSchemeEntity> schemes,
        IReadOnlyList<DistributionParticipantEntity> participants)
    {
        foreach (var scheme in schemes)
        {
            await connection.ExecuteAsync(InsertSchemeSql, scheme, transaction);

            foreach (var participant in participants.Where(p => p.MonthlyDistributionSchemeId == scheme.MonthlyDistributionSchemeId))
            {
                await connection.ExecuteAsync(InsertParticipantSql, participant, transaction);
            }
        }
    }
}
