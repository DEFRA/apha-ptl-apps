using Dapper;
using PTL.Core.Distribution;

namespace PTL.Data.Distribution;

public sealed partial class DistributionRepository
{
    public async Task<IReadOnlyList<CurrentDistributionData>> GetCurrentDistributionsAsync(Guid participantId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                mds.fldMonthlyDistributionSchemeId AS MonthlyDistributionSchemeId,
                dp.fldDistributionParticipantId AS DistributionParticipantId,
                mds.fldDistributionReference + mds.fldDistributionReferenceSuffix + '/' + mds.fldScheduleCode AS DistributionReference,
                s.fldIdentifier AS SchemeCode,
                s.fldName AS SchemeName,
                mds.fldDistributionDate AS DistributionDate,
                ISNULL(dp.fldDeadlineDate, mds.fldDeadlineDate) AS DeadlineDate,
                CASE
                    WHEN ar.fldFirstEditDate IS NULL THEN CAST(0 AS bit)
                    ELSE CAST(1 AS bit)
                END AS HasResults,
                ISNULL(ar.fldSubmitted, CAST(0 AS bit)) AS Submitted
            FROM dbo.tblMonthlyDistributionScheme mds
            INNER JOIN dbo.tblScheme s ON mds.fldSchemeId = s.fldSchemeId
            INNER JOIN dbo.tlnkDistributionParticipant dp ON mds.fldMonthlyDistributionSchemeId = dp.fldDistributionSchemeId
            LEFT JOIN dbo.tblActualResults ar ON dp.fldDistributionParticipantId = ar.fldActualResultsId
            WHERE
                dp.fldParticipantId = @ParticipantId
                AND dp.fldPostedDate IS NOT NULL
                AND mds.fldIsCancelled = 0
                AND ISNULL(dp.fldDeadlineDate, mds.fldDeadlineDate) >= CAST(CONVERT(varchar, GETDATE(), 112) AS datetime)
                AND CAST(mds.fldResultsIssueTargetDate AS date) > CAST(dbo.fnGetSevenYearsAgo(NULL) AS date)
            ORDER BY
                mds.fldDistributionReference,
                mds.fldDistributionReferenceSuffix
            """;

        using var connection = connectionFactory.CreateConnection();
        var rows = await connection.QueryAsync<CurrentDistributionData>(sql, new { ParticipantId = participantId });
        return rows.ToList();
    }
}
