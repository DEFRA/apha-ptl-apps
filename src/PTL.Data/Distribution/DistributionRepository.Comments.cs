using System.Data;
using Dapper;
using PTL.Core.Distribution;
using Rows = PTL.Data.Distribution.TabulationReportRows;

namespace PTL.Data.Distribution;

// View Comments: legacy ResultsTabulation.TestConsultantComments (DataPortal_Fetch / DataPortal_Update
// with AuditableBusinessBase.Audit) and TestConsultants.MonthlyDistributionSchemeAttachment(List).
public sealed partial class DistributionRepository
{
    public async Task<TestConsultantCommentsEntity?> GetTestConsultantCommentsAsync(Guid monthlyDistributionSchemeId, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();

        // @ReferenceDate is left to its NULL default - the procedure then uses today's date.
        var header = await connection.QueryFirstOrDefaultAsync<Rows.CommentsHeader>(
            "EXEC dbo.spgTestConsultantComments @MonthlyDistributionSchemeId = @MonthlyDistributionSchemeId",
            new { MonthlyDistributionSchemeId = monthlyDistributionSchemeId });
        if (header is null)
        {
            return null;
        }

        var entity = new TestConsultantCommentsEntity
        {
            MonthlyDistributionSchemeId = header.MonthlyDistributionSchemeId,
            SchemeName = header.SchemeName ?? string.Empty,
            SchemeIdentifier = header.SchemeIdentifier ?? string.Empty,
            DistributionReference = header.DistributionReference ?? string.Empty,
            DistributionReferenceSuffix = header.DistributionReferenceSuffix ?? string.Empty,
            ScheduleCode = header.ScheduleCode ?? string.Empty,
            TestConsultantComments = header.TestConsultantComments ?? string.Empty,
            IsCommentSubmitted = header.IsCommentSubmitted ?? false,
            TestConsultantCommentsAuthorId = header.TestConsultantCommentsAuthorId ?? Guid.Empty,
            TestConsultantCommentsAuthor = header.TestConsultantCommentsAuthor ?? string.Empty,
            TestConsultantCommentsDept = header.TestConsultantCommentsDept ?? string.Empty,
            RequiresAssessment = header.RequiresAssessment ?? false,
        };

        var attachments = await connection.QueryAsync<Rows.DraftAttachment>(
            "EXEC dbo.spgMonthlyDistributionSchemeAttachments @MonthlyDistributionSchemeId = @MonthlyDistributionSchemeId",
            new { MonthlyDistributionSchemeId = monthlyDistributionSchemeId });
        entity.Attachments.AddRange(attachments.Select(a =>
            new TabulationSourceAttachment(a.MonthlyDistributionAttachmentId, a.FileName ?? string.Empty, a.ContentType ?? string.Empty, a.FileData ?? [])));

        return entity;
    }

    public async Task UpdateTestConsultantCommentsAsync(
        Guid monthlyDistributionSchemeId,
        string comments,
        Guid authorId,
        bool isCommentSubmitted,
        CommentsAudit audit,
        CommentsAttachmentChanges attachments,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(audit);
        ArgumentNullException.ThrowIfNull(attachments);
        ArgumentNullException.ThrowIfNull(attachments.RemovedAttachmentIds);
        ArgumentNullException.ThrowIfNull(attachments.NewAttachments);

        using var connection = connectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        await AuditMonthlyDistributionSchemeAsync(connection, transaction, monthlyDistributionSchemeId, audit);

        await connection.ExecuteAsync(
            """
            EXEC dbo.spuMonthlyDistributionSchemeForTabulations
                @MonthlyDistributionSchemeId = @MonthlyDistributionSchemeId,
                @TestConsultantComments = @TestConsultantComments,
                @TestConsultantCommentsAuthorId = @TestConsultantCommentsAuthorId,
                @IsCommentSubmitted = @IsCommentSubmitted
            """,
            new
            {
                MonthlyDistributionSchemeId = monthlyDistributionSchemeId,
                TestConsultantComments = comments,
                TestConsultantCommentsAuthorId = authorId,
                IsCommentSubmitted = isCommentSubmitted,
            },
            transaction);

        // Legacy MonthlyDistributionSchemeAttachmentList.Update: removals first, then new files.
        foreach (var attachmentId in attachments.RemovedAttachmentIds)
        {
            await connection.ExecuteAsync(
                "EXEC dbo.spuMonthlyDistributionSchemeAttachmentSetDeleted @AttachmentId = @AttachmentId",
                new { AttachmentId = attachmentId },
                transaction);
        }

        foreach (var attachment in attachments.NewAttachments)
        {
            var parameters = new DynamicParameters(new
            {
                MonthlyDistributionSchemeId = monthlyDistributionSchemeId,
                attachment.FileName,
                attachment.ContentType,
                attachment.FileData,
                attachment.UploadDate,
            });
            parameters.Add("AttachmentId", dbType: DbType.Guid, direction: ParameterDirection.Output);

            await connection.ExecuteAsync(
                "dbo.spiMonthlyDistributionSchemeAttachment", parameters, transaction, commandType: CommandType.StoredProcedure);
        }

        transaction.Commit();
    }

    // Runs before the change so the procedure can record the attachment names as they were.
    private static Task<int> AuditMonthlyDistributionSchemeAsync(IDbConnection connection, IDbTransaction transaction, Guid monthlyDistributionSchemeId, CommentsAudit audit) =>
        connection.ExecuteAsync(
            """
            EXEC dbo.spiAuditMonthlyDistributionScheme
                @MonthlyDistributionSchemeId = @MonthlyDistributionSchemeId,
                @AuditWho = @AuditWho,
                @AuditWhy = @AuditWhy,
                @TestConsultantComments = @TestConsultantComments,
                @TestConsultantCommentsUpdate = @TestConsultantCommentsUpdate,
                @FileNames = @FileNames,
                @FileNamesUpdate = @FileNamesUpdate
            """,
            new
            {
                MonthlyDistributionSchemeId = monthlyDistributionSchemeId,
                audit.AuditWho,
                audit.AuditWhy,
                audit.TestConsultantComments,
                audit.TestConsultantCommentsUpdate,
                audit.FileNames,
                audit.FileNamesUpdate,
            },
            transaction);
}
