using Dapper;
using PTL.Core.Distribution;
using PTL.Data.Infrastructure;

namespace PTL.Data.Distribution;

public sealed class DistributionRepository(IDbConnectionFactory connectionFactory) : IDistributionRepository
{
    public async Task<IReadOnlyList<DistributionMonthSummaryEntity>> GetMonthlyDistributionSummariesAsync(CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        return (await connection.QueryAsync<DistributionMonthSummaryEntity>("EXEC dbo.spgaMonthlyDistributionInfo")).ToList();
    }

    public async Task<IReadOnlyList<DistributionMonthYearEntity>> GetDistributionYearsAsync(CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        return (await connection.QueryAsync<DistributionMonthYearEntity>("EXEC dbo.spgaDistributionYearInfo")).ToList();
    }

    public async Task<(Guid MonthlyDistributionId, IReadOnlyList<MonthlyDistributionSchemeEntity> Schemes)?> GetMonthlyDistributionSchedulesAsync(
        int yearId, int monthId, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        using var results = await connection.QueryMultipleAsync(
            "EXEC dbo.spgMonthlyDistribution @YearId = @YearId, @MonthId = @MonthId",
            new { YearId = yearId, MonthId = monthId });

        var header = await results.ReadFirstOrDefaultAsync<MonthlyDistributionHeaderEntity>();
        var schemes = (await results.ReadAsync<MonthlyDistributionSchemeEntity>()).ToList();
        var participants = (await results.ReadAsync<MonthlyDistributionParticipantAggregateEntity>()).ToList();

        if (header is null || header.MonthlyDistributionId == Guid.Empty)
        {
            return null;
        }

        var participantsBySchemeId = participants
            .GroupBy(p => p.DistributionSchemeId)
            .ToDictionary(g => g.Key, g => (Count: g.Count(), Total: g.Sum(p => p.NumberOfSetsRequired)));

        foreach (var scheme in schemes)
        {
            if (participantsBySchemeId.TryGetValue(scheme.MonthlyDistributionSchemeId, out var aggregate))
            {
                scheme.ParticipantCount = aggregate.Count;
                scheme.TotalSetsOfSamplesRequired = aggregate.Total;
            }
        }

        return (header.MonthlyDistributionId, schemes);
    }

    public async Task UpdateMonthlyDistributionSchemesAsync(IReadOnlyList<MonthlyDistributionSchemeEntity> schemes, CancellationToken cancellationToken = default)
    {
        if (schemes.Count == 0)
        {
            return;
        }

        using var connection = connectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        const string sql = """
            EXEC dbo.spuMonthlyDistributionScheme
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

        foreach (var scheme in schemes)
        {
            await connection.ExecuteAsync(sql, scheme, transaction);
        }

        transaction.Commit();
    }
}

// spgMonthlyDistribution result set 1 - only present when the month has been initialised.
internal sealed class MonthlyDistributionHeaderEntity
{
    public Guid MonthlyDistributionId { get; set; }
    public int YearId { get; set; }
    public int MonthId { get; set; }
}
