using Dapper;
using PTL.Core.Distribution;
using PTL.Data.Infrastructure;
using Rows = PTL.Data.Distribution.TabulationReportRows;

namespace PTL.Data.Distribution;

// Legacy Results Sign-off MonthlyDistributionInfoCollection.FetchCollection. @ReferenceDate is left to its NULL default -
// the procedure then uses today's date (legacy only overrides it from the ResultsTestDate test-harness appSetting).
// The procedure does not return who the Test Consultants and Assessors are, so their names are read from the schemes with
// the legacy name functions - an inline query, as no stored procedure is added for it.
public sealed partial class ResultsSignOffRepository(IDbConnectionFactory connectionFactory) : IResultsSignOffRepository
{
    private const string QueueSql = "EXEC dbo.spgaMonthlyDistributionInfoForResultsSignOff";

    // Locks the row so the check and the update below see the same state.
    private const string StateSql = "SELECT fldPublishState FROM tblDistributionTabulation WITH (UPDLOCK, HOLDLOCK) WHERE fldDistributionTabulationId = @DistributionTabulationId";

    private const string SetStateSql = "EXEC dbo.spuTabulationForSignOff @DistributionTabulationId = @DistributionTabulationId, @PublishState = @PublishState";

    // The same readiness conditions as the procedure, without its seven-year window (the names are matched to the
    // schemes the procedure returned, so older rows are simply not used).
    private const string NamesSql = """
        SELECT
            mds.fldMonthlyDistributionSchemeId AS fldMonthlyDistributionSchemeId,
            dbo.fnGetTestConsultantNameById(s.fldTestConsultant1) AS fldTestConsultant1,
            dbo.fnGetTestConsultantNameById(s.fldTestConsultant2) AS fldTestConsultant2,
            dbo.fnGetTestConsultantNameById(s.fldTestConsultant3) AS fldTestConsultant3,
            dbo.fnGetAssessorNameById(s.fldAssessor1) AS fldAssessor1,
            dbo.fnGetAssessorNameById(s.fldAssessor2) AS fldAssessor2,
            dbo.fnGetAssessorNameById(s.fldAssessor3) AS fldAssessor3,
            dbo.fnGetAssessorNameById(s.fldAssessor4) AS fldAssessor4
        FROM tblMonthlyDistributionScheme mds
        INNER JOIN tblScheme s ON mds.fldSchemeId = s.fldSchemeId
        WHERE mds.fldIsReadyForResultsSignOff IS NOT NULL
            AND mds.fldIsCancelled = 0
            AND EXISTS (
                SELECT 1 FROM tblDistributionTabulation t
                WHERE t.fldMonthlyDistributionSchemeId = mds.fldMonthlyDistributionSchemeId
                    AND t.fldPublishState = 0)
        """;

    public async Task<IReadOnlyList<ResultsSignOffMonthEntity>> GetQueueAsync(CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();

        List<Rows.ResultsSignOffMonth> months;
        List<Rows.ResultsSignOffScheme> schemes;
        List<Rows.ResultsSignOffTabulation> tabulations;
        using (var results = await connection.QueryMultipleAsync(QueueSql))
        {
            months = (await results.ReadAsync<Rows.ResultsSignOffMonth>()).ToList();
            schemes = (await results.ReadAsync<Rows.ResultsSignOffScheme>()).ToList();
            tabulations = (await results.ReadAsync<Rows.ResultsSignOffTabulation>()).ToList();
        }

        IEnumerable<Rows.ResultsSignOffNames> nameRows = schemes.Count == 0 ? [] : await connection.QueryAsync<Rows.ResultsSignOffNames>(NamesSql);
        var names = nameRows.ToDictionary(n => n.MonthlyDistributionSchemeId);

        var tabulationsByScheme = tabulations.ToLookup(t => t.MonthlyDistributionSchemeId);
        var schemesByMonth = schemes.ToLookup(s => s.MonthlyDistributionId);

        return [.. months.Select(month =>
        {
            var entity = new ResultsSignOffMonthEntity { YearId = month.YearId, MonthId = month.MonthId };
            foreach (var scheme in schemesByMonth[month.MonthlyDistributionId])
            {
                names.TryGetValue(scheme.MonthlyDistributionSchemeId, out var allocated);
                var schemeEntity = new ResultsSignOffSchemeEntity
                {
                    MonthlyDistributionSchemeId = scheme.MonthlyDistributionSchemeId,
                    SchemeName = scheme.SchemeName ?? string.Empty,
                    SchemeIdentifier = scheme.SchemeIdentifier ?? string.Empty,
                    DistributionReference = scheme.DistributionReference ?? string.Empty,
                    DistributionReferenceSuffix = scheme.DistributionReferenceSuffix ?? string.Empty,
                    ScheduleCode = scheme.ScheduleCode ?? string.Empty,
                    TargetDate = scheme.ResultsIssueTargetDate ?? default,
                    TestConsultants = Join(allocated?.TestConsultant1, allocated?.TestConsultant2, allocated?.TestConsultant3),
                    Assessors = Join(allocated?.Assessor1, allocated?.Assessor2, allocated?.Assessor3, allocated?.Assessor4),
                };

                schemeEntity.Tabulations.AddRange(tabulationsByScheme[scheme.MonthlyDistributionSchemeId].Select(t => new ResultsSignOffTabulationEntity
                {
                    DistributionTabulationId = t.DistributionTabulationId,
                    Name = t.Name ?? string.Empty,
                    PublishCount = t.PublishCount ?? 0,
                    SingleParticipantTabulation = t.SingleParticipantTabulation ?? false,
                    AvailableToParticipants = t.AvailableToParticipants ?? false,
                    AvailableToViewers = t.AvailableToViewers ?? false,
                }));

                entity.Schemes.Add(schemeEntity);
            }

            return entity;
        })];
    }

    // spuTabulationForSignOff also clears the scheme's ready-for-sign-off flag once none of its tabulations is left awaiting.
    public async Task<ResultsSignOffOutcome> RejectAsync(Guid tabulationId, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        try
        {
            var state = await connection.ExecuteScalarAsync<int?>(StateSql, new { DistributionTabulationId = tabulationId }, transaction);
            if (state is null)
            {
                transaction.Rollback();
                return ResultsSignOffOutcome.NotFound;
            }

            if (state != ResultsSignOffState.AwaitingSignOff)
            {
                transaction.Rollback();
                return ResultsSignOffOutcome.NotAwaiting;
            }

            await connection.ExecuteAsync(
                SetStateSql,
                new { DistributionTabulationId = tabulationId, PublishState = ResultsSignOffState.NotAuthorized },
                transaction);
            transaction.Commit();
            return ResultsSignOffOutcome.Done;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    private static string Join(params string?[] values) =>
        string.Join(", ", values.Where(v => !string.IsNullOrWhiteSpace(v)));
}
