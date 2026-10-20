using Dapper;
using PTL.Core.Distribution;

namespace PTL.Data.Distribution;

public sealed partial class DistributionRepository
{
    public async Task<IReadOnlyList<TabulationEmailCandidateEntity>> GetTabulationEmailCandidatesAsync(
        DateTime from, DateTime toExclusive, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();

        // The columns of spgaMonthlyDistributionSchemeForEmail with the scheme's Test Consultants; that procedure
        // stops returning a distribution on the ninth day, which would cut the chaser window short.
        return (await connection.QueryAsync<TabulationEmailCandidateEntity>(
            """
            SELECT
                m.fldMonthlyDistributionSchemeId,
                s.fldSchemeId,
                s.fldName AS fldSchemeName,
                m.fldDistributionReference,
                m.fldDistributionReferenceSuffix,
                m.fldScheduleCode,
                m.fldEmailState,
                m.fldIsCommentSubmitted,
                m.fldIsReadyForTestConsultant,
                s.fldTestConsultant1 AS fldTestConsultant1Id,
                s.fldTestConsultant2 AS fldTestConsultant2Id,
                s.fldTestConsultant3 AS fldTestConsultant3Id
            FROM
                tblMonthlyDistributionScheme m
            INNER JOIN tblScheme s ON m.fldSchemeId = s.fldSchemeId
            WHERE
                m.fldIsReadyForTestConsultant >= @From AND
                m.fldIsReadyForTestConsultant < @ToExclusive
            ORDER BY
                m.fldDistributionReference, m.fldDistributionReferenceSuffix
            """,
            new { From = from, ToExclusive = toExclusive })).ToList();
    }

    public async Task<IReadOnlyList<TabulationConsultantEntity>> GetTabulationEmailConsultantsAsync(CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();

        // Result set 1 is internal users with the Test Consultant role, result set 2 external test consultants.
        using var results = await connection.QueryMultipleAsync("EXEC dbo.spgaUserAllTestConsultant");

        var internalConsultants = (await results.ReadAsync<TabulationConsultantEntity>()).ToList();
        var externalConsultants = (await results.ReadAsync<TabulationConsultantEntity>()).ToList();
        foreach (var consultant in externalConsultants)
        {
            consultant.IsExternal = true;
        }

        return [.. internalConsultants, .. externalConsultants];
    }

    public async Task SetTabulationEmailStateAsync(Guid monthlyDistributionSchemeId, int emailState, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        await connection.ExecuteAsync(
            "EXEC dbo.spuMonthlyDistributionSchemeForEmail @MonthlyDistributionSchemeId = @MonthlyDistributionSchemeId, @EmailState = @EmailState",
            new { MonthlyDistributionSchemeId = monthlyDistributionSchemeId, EmailState = emailState });
    }

    public async Task<IReadOnlyList<NotificationEmailEntity>> GetUnsentNotificationEmailsAsync(CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        return (await connection.QueryAsync<NotificationEmailEntity>("EXEC dbo.spgaNotificationEmail")).ToList();
    }

    public async Task MarkNotificationEmailSentAsync(Guid notificationEmailId, DateTime dateSent, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        await connection.ExecuteAsync(
            "EXEC dbo.spuNotificationEmail @NotificationEmailId = @NotificationEmailId, @DateSent = @DateSent",
            new { NotificationEmailId = notificationEmailId, DateSent = dateSent });
    }
}
