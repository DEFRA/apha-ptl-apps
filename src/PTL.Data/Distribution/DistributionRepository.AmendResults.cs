using System.Data;
using Dapper;
using PTL.Core.Distribution;
using Rows = PTL.Data.Distribution.TabulationReportRows;

namespace PTL.Data.Distribution;

// Amend Results: legacy AmendedResults.MonthlyDistributionScheme DataPortal_Fetch / DataPortal_Update.
public sealed partial class DistributionRepository
{
    public async Task<AmendResultsSource?> GetAmendResultsAsync(Guid monthlyDistributionSchemeId, CancellationToken cancellationToken = default)
    {
        // @ReferenceDate is left to its NULL default - the procedure then uses today's date.
        using var connection = connectionFactory.CreateConnection();
        using var results = await connection.QueryMultipleAsync(
            "EXEC dbo.spgMonthlyDistributionSchemeForAmendResults @MonthlyDistributionSchemeId = @MonthlyDistributionSchemeId",
            new { MonthlyDistributionSchemeId = monthlyDistributionSchemeId });

        var header = await results.ReadFirstOrDefaultAsync<Rows.AmendHeader>();
        if (header is null)
        {
            return null;
        }

        var source = new AmendResultsSource
        {
            MonthlyDistributionSchemeId = header.MonthlyDistributionSchemeId,
            SchemeName = header.SchemeName ?? string.Empty,
            SchemeIdentifier = header.SchemeIdentifier ?? string.Empty,
            DistributionReference = header.DistributionReference ?? string.Empty,
            DistributionReferenceSuffix = header.DistributionReferenceSuffix ?? string.Empty,
            ScheduleCode = header.ScheduleCode ?? string.Empty,
            StoreRatings = header.StoreRatings ?? false,
            RequiresAssessment = header.RequiresAssessment ?? false,
        };

        var tests = new Dictionary<Guid, AmendSourceTest>();
        foreach (var row in await results.ReadAsync<Rows.AmendTest>())
        {
            var test = new AmendSourceTest { TestId = row.TestId, Name = row.TestType ?? string.Empty, IsAssessmentSubmitted = row.AssessmentSubmitted is not null };
            tests[row.TestId] = test;
            source.Tests.Add(test);
        }

        foreach (var row in await results.ReadAsync<Rows.AmendMethodItem>())
        {
            tests.GetValueOrDefault(row.TestId)?.MethodItems.Add(new(
                row.TestMethodItemId, row.TestMethodItemType ?? string.Empty, row.FormatString ?? string.Empty, row.Message ?? string.Empty));
        }

        foreach (var row in await results.ReadAsync<Rows.AmendResultItem>())
        {
            tests.GetValueOrDefault(row.TestId)?.ResultItems.Add(new(
                row.TestResultItemId, row.TestResultItemType ?? string.Empty, row.FormatString ?? string.Empty, row.Message ?? string.Empty,
                row.IsRange ?? false, row.TestResultItemTypeId, row.Order ?? 0));
        }

        foreach (var row in await results.ReadAsync<Rows.DraftCategoryItem>())
        {
            tests.GetValueOrDefault(row.TestId)?.CategoryItems.Add(new(row.CategoryItemId, row.Name ?? string.Empty));
        }

        foreach (var row in await results.ReadAsync<Rows.AmendSample>())
        {
            tests.GetValueOrDefault(row.TestId)?.Samples.Add(new(row.SampleId, row.SampleNumber ?? string.Empty));
        }

        var participants = new Dictionary<Guid, AmendSourceParticipant>();
        foreach (var row in await results.ReadAsync<Rows.AmendParticipant>())
        {
            var participant = new AmendSourceParticipant
            {
                DistributionParticipantId = row.DistributionParticipantId,
                LabCode = row.LabCode ?? string.Empty,
                NilReturnReason = row.NilReturnReason ?? string.Empty,
            };
            participants[row.DistributionParticipantId] = participant;
            source.Participants.Add(participant);
        }

        foreach (var row in await results.ReadAsync<Rows.AmendSampleRating>())
        {
            participants.GetValueOrDefault(row.ParticipantId)?.Ratings.TryAdd(row.SampleId, (row.SampleRatingId, row.RatingId));
        }

        foreach (var row in await results.ReadAsync<Rows.AmendParticipantMethod>())
        {
            participants.GetValueOrDefault(row.ParticipantId)?.MethodResults.TryAdd(row.TestMethodItemId, row.AmendedResult ?? string.Empty);
        }

        foreach (var row in await results.ReadAsync<Rows.AmendParticipantResult>())
        {
            participants.GetValueOrDefault(row.ParticipantId)?.ResultItemResults.TryAdd((row.SampleId, row.TestResultItemId), row.AmendedResult ?? string.Empty);
        }

        foreach (var row in await results.ReadAsync<Rows.AmendAssessment>())
        {
            participants.GetValueOrDefault(row.ParticipantId)?.Assessments.TryAdd(
                (row.TestId, row.SampleId), (row.SampleReceiptId, row.AssessmentComment ?? string.Empty, row.Score ?? 0));
        }

        foreach (var row in await results.ReadAsync<Rows.AmendParticipantCategory>())
        {
            participants.GetValueOrDefault(row.ParticipantId)?.CategoryResults.TryAdd(
                (row.SampleId, row.CategoryItemId), (row.AmendedCategoryScore ?? 0, row.SampleTestId));
        }

        foreach (var row in await results.ReadAsync<Rows.AmendIntendedMethod>())
        {
            source.IntendedMethodResults.TryAdd(row.TestMethodItemId, row.IntendedResult ?? string.Empty);
        }

        foreach (var row in await results.ReadAsync<Rows.AmendIntendedResult>())
        {
            source.IntendedResultItemResults.TryAdd((row.SampleId, row.TestResultItemId), (row.IntendedResult ?? string.Empty, row.IntendedResult2 ?? string.Empty));
        }

        return source;
    }

    public async Task<IReadOnlyList<AmendRatingOption>> GetRatingOptionsAsync(CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        var rows = await connection.QueryAsync<Rows.RatingRow>("EXEC dbo.spgaRating");
        return [.. rows.Select(r => new AmendRatingOption(r.RatingId, r.Rating ?? string.Empty))];
    }

    public async Task SaveAmendResultsAsync(AmendResultsWrite write, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(write);

        using var connection = connectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();
        var audit = new AmendAudit(write.AuditWho, write.AuditWhy);

        // mTests.Update: only IsRange is editable on a test result item.
        foreach (var range in write.Ranges)
        {
            await connection.ExecuteAsync(
                "EXEC dbo.spuDistributionTestResultItem @DistributionTestResultItemId = @ResultItemId, @TestId = @TestId, @TestResultItemTypeId = @ResultItemTypeId, @Order = @Order, @IsRange = @IsRange",
                range, transaction);
        }

        // mParticipants.Update: each changed participant, then its children in legacy order.
        foreach (var participant in write.Participants)
        {
            await WriteParticipantAsync(connection, transaction, audit, participant);
        }

        foreach (var item in write.IntendedResults)
        {
            var parameters = new { item.TestId, item.SampleId, item.ResultItemId, item.Value, item.ValueChanged, item.Value2, item.Value2Changed, audit.AuditWho, audit.AuditWhy };
            await WriteWithAuditAsync(
                connection,
                transaction,
                audit,
                "EXEC dbo.spiAuditAmendSampleResultItem @TestResultItemId = @ResultItemId, @TestId = @TestId, @SampleId = @SampleId, @AuditWho = @AuditWho, @AuditWhy = @AuditWhy, @IntendedResult = @Value, @IntendedResultUpdate = @ValueChanged, @IntendedResult2 = @Value2, @IntendedResult2Update = @Value2Changed",
                "EXEC dbo.spuDistributionTestResultItemForAmendResults @TestResultItemId = @ResultItemId, @SampleId = @SampleId, @TestId = @TestId, @IntendedResult = @Value, @IntendedResult2 = @Value2",
                parameters);
        }

        foreach (var item in write.IntendedMethods)
        {
            var parameters = new { item.TestId, item.MethodItemId, item.Value, audit.AuditWho, audit.AuditWhy };
            await WriteWithAuditAsync(
                connection,
                transaction,
                audit,
                "EXEC dbo.spiAuditAmendTestMethodItem @DistributionTestMethodItemId = @MethodItemId, @AuditWho = @AuditWho, @AuditWhy = @AuditWhy, @IntendedResult = @Value, @IntendedResultUpdate = 1",
                "EXEC dbo.spuDistributionTestMethodItemForAmendResults @TestMethodItemId = @MethodItemId, @TestId = @TestId, @IntendedResult = @Value",
                parameters);
        }

        transaction.Commit();
    }

    // mParticipants.Update: the participant, then its children in legacy order.
    private static async Task WriteParticipantAsync(
        IDbConnection connection, IDbTransaction transaction, AmendAudit audit, AmendParticipantWrite participant)
    {
        await connection.ExecuteAsync(
            "EXEC dbo.spuDistributionParticipantForAmendResults @DistributionParticipantId = @DistributionParticipantId, @NilReturnReason = @NilReturnReason",
            new { participant.DistributionParticipantId, participant.NilReturnReason }, transaction);

        foreach (var item in participant.MethodResults)
        {
            var parameters = new { item.TestId, item.MethodItemId, ParticipantId = participant.DistributionParticipantId, item.Value, audit.AuditWho, audit.AuditWhy };
            await WriteWithAuditAsync(
                connection,
                transaction,
                audit,
                "EXEC dbo.spiAuditAmendedTestMethodItem @ActualResultsId = @ParticipantId, @TestMethodItemId = @MethodItemId, @AuditWho = @AuditWho, @AuditWhy = @AuditWhy, @Result = @Value, @ResultUpdate = 1",
                "EXEC dbo.spuAmendedMethodItem @TestMethodItemId = @MethodItemId, @TestId = @TestId, @ParticipantId = @ParticipantId, @AmendedResult = @Value",
                parameters);
        }

        foreach (var item in participant.ResultItemResults)
        {
            var parameters = new { item.TestId, item.SampleId, item.ResultItemId, ParticipantId = participant.DistributionParticipantId, item.Value, audit.AuditWho, audit.AuditWhy };
            await WriteWithAuditAsync(
                connection,
                transaction,
                audit,
                "EXEC dbo.spiAuditAmendedSampleResultItem @ActualResultsId = @ParticipantId, @TestResultItemId = @ResultItemId, @TestId = @TestId, @SampleId = @SampleId, @AuditWho = @AuditWho, @AuditWhy = @AuditWhy, @Result = @Value, @ResultUpdate = 1",
                "EXEC dbo.spuAmendedResultItem @TestResultItemId = @ResultItemId, @SampleId = @SampleId, @TestId = @TestId, @ParticipantId = @ParticipantId, @AmendedResult = @Value",
                parameters);
        }

        foreach (var item in participant.Assessments)
        {
            var parameters = new { item.SampleReceiptId, item.Comment, item.CommentChanged, item.Score, item.ScoreChanged, audit.AuditWho, audit.AuditWhy };
            await WriteWithAuditAsync(
                connection,
                transaction,
                audit,
                "EXEC dbo.spiAuditAssessmentItem @SampleReceiptId = @SampleReceiptId, @AuditWho = @AuditWho, @AuditWhy = @AuditWhy, @AssessmentComment = @Comment, @AssessmentCommentUpdate = @CommentChanged, @Score = @Score, @ScoreUpdate = @ScoreChanged",
                "EXEC dbo.spuAmendedAssessmentItem @SampleReceiptId = @SampleReceiptId, @AssessmentComment = @Comment, @AssessmentScore = @Score",
                parameters);
        }

        foreach (var rating in participant.Ratings)
        {
            await connection.ExecuteAsync(
                "EXEC dbo.spuSampleRating @SampleRatingId = @SampleRatingId, @RatingId = @RatingId", rating, transaction);
        }

        foreach (var item in participant.CategoryResults)
        {
            var parameters = new { item.TestId, item.SampleId, item.CategoryItemId, item.SampleTestId, item.Score, ParticipantId = participant.DistributionParticipantId, audit.AuditWho, audit.AuditWhy };
            await WriteWithAuditAsync(
                connection,
                transaction,
                audit,
                "EXEC dbo.spiAuditAmendedCategoryItem @ActualResultsId = @ParticipantId, @CategoryItemId = @CategoryItemId, @SampleTestId = @SampleTestId, @TestId = @TestId, @SampleId = @SampleId, @AuditWho = @AuditWho, @AuditWhy = @AuditWhy, @Result = @Score, @ResultUpdate = 1",
                "EXEC dbo.spuAmendedCategoryItem @CategoryItemId = @CategoryItemId, @SampleId = @SampleId, @TestId = @TestId, @ParticipantId = @ParticipantId, @AmendedCategoryScore = @Score, @SampleTestId = @SampleTestId",
                parameters);
        }
    }

    // The audit row (only when a reason was captured) is written before the update it describes.
    private static async Task WriteWithAuditAsync(
        IDbConnection connection, IDbTransaction transaction, AmendAudit audit, string auditSql, string updateSql, object parameters)
    {
        if (audit.Audited)
        {
            await connection.ExecuteAsync(auditSql, parameters, transaction);
        }

        await connection.ExecuteAsync(updateSql, parameters, transaction);
    }

    private sealed record AmendAudit(string AuditWho, string AuditWhy)
    {
        public bool Audited => AuditWhy.Length > 0;
    }
}
