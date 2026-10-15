using System.Data;
using PTL.Core.Distribution;
using PTL.Data.Distribution;
using PTL.Data.Infrastructure;
using PTL.Data.Tests.Fakes;

namespace PTL.Data.Tests.Distribution;

// spgTestConsultantComments / spgMonthlyDistributionSchemeAttachments and the legacy
// TestConsultantComments.DataPortal_Update chain (audit, update, attachment removals, new files).
public class DistributionRepositoryCommentsTests
{
    public DistributionRepositoryCommentsTests() => DapperColumnMappings.Register();

    private const string HeaderSql = "EXEC dbo.spgTestConsultantComments @MonthlyDistributionSchemeId = @MonthlyDistributionSchemeId";
    private const string AttachmentsSql = "EXEC dbo.spgMonthlyDistributionSchemeAttachments @MonthlyDistributionSchemeId = @MonthlyDistributionSchemeId";

    private const string AuditSql = """
        EXEC dbo.spiAuditMonthlyDistributionScheme
            @MonthlyDistributionSchemeId = @MonthlyDistributionSchemeId,
            @AuditWho = @AuditWho,
            @AuditWhy = @AuditWhy,
            @TestConsultantComments = @TestConsultantComments,
            @TestConsultantCommentsUpdate = @TestConsultantCommentsUpdate,
            @FileNames = @FileNames,
            @FileNamesUpdate = @FileNamesUpdate
        """;
    private const string UpdateSql = """
        EXEC dbo.spuMonthlyDistributionSchemeForTabulations
            @MonthlyDistributionSchemeId = @MonthlyDistributionSchemeId,
            @TestConsultantComments = @TestConsultantComments,
            @TestConsultantCommentsAuthorId = @TestConsultantCommentsAuthorId,
            @IsCommentSubmitted = @IsCommentSubmitted
        """;
    private const string SetDeletedSql = "EXEC dbo.spuMonthlyDistributionSchemeAttachmentSetDeleted @AttachmentId = @AttachmentId";
    private const string InsertAttachmentSql = "dbo.spiMonthlyDistributionSchemeAttachment";

    private static readonly Guid SchemeId = Guid.Parse("11111111-0000-0000-0000-000000000001");
    private static readonly Guid AuthorId = Guid.Parse("22222222-0000-0000-0000-000000000001");
    private static readonly Guid AttachmentId = Guid.Parse("33333333-0000-0000-0000-000000000001");
    private static readonly Guid OtherAttachmentId = Guid.Parse("33333333-0000-0000-0000-000000000002");
    private static readonly DateTime UploadDate = new(2026, 5, 10);
    private static readonly CommentsAudit Audit = new("DOMAIN\\tc", "Typo", "New text", true, "a.pdf", false);

    private static (DistributionRepository Repository, FakeDbConnection Connection) CreateRepository()
    {
        var connection = new FakeDbConnection();
        return (new DistributionRepository(new FakeDbConnectionFactory(connection)), connection);
    }

    private static string Normalise(string sql) => sql.ReplaceLineEndings("\n");

    private static DataTable Table(params (string Name, Type Type)[] columns)
    {
        var table = new DataTable();
        foreach (var (name, type) in columns)
        {
            table.Columns.Add(name, type);
        }

        return table;
    }

    private static DataTable Rows(DataTable table, params object[][] rows)
    {
        foreach (var row in rows)
        {
            table.Rows.Add(row);
        }

        return table;
    }

    private static DataTable Header(params object[][] rows) => Rows(
        Table(("fldMonthlyDistributionSchemeId", typeof(Guid)), ("fldSchemeName", typeof(string)), ("fldSchemeIdentifier", typeof(string)),
            ("fldDistributionReference", typeof(string)), ("fldDistributionReferenceSuffix", typeof(string)), ("fldScheduleCode", typeof(string)),
            ("fldTestConsultantComments", typeof(string)), ("fldIsCommentSubmitted", typeof(bool)), ("fldTestConsultantCommentsAuthorId", typeof(Guid)),
            ("fldTestConsultantCommentsAuthor", typeof(string)), ("fldTestConsultantCommentsDept", typeof(string)), ("fldRequiresAssessment", typeof(bool))),
        rows);

    private static DataTable Attachments(params object[][] rows) => Rows(
        Table(("fldMonthlyDistributionAttachmentId", typeof(Guid)), ("fldFileName", typeof(string)), ("fldContentType", typeof(string)), ("fldFileData", typeof(byte[]))),
        rows);

    // ---- Get ----------------------------------------------------------------------------------

    [Fact]
    public async Task GetTestConsultantCommentsAsync_NoHeader_ReturnsNullWithoutReadingAttachments()
    {
        var (repository, connection) = CreateRepository();
        connection.RespondToQuery(HeaderSql, Header());

        Assert.Null(await repository.GetTestConsultantCommentsAsync(SchemeId));
        Assert.Equal(HeaderSql, Assert.Single(connection.ExecutedCommands).CommandText);
    }

    [Fact]
    public async Task GetTestConsultantCommentsAsync_MapsTheHeaderAndAttachments()
    {
        var (repository, connection) = CreateRepository();
        connection.RespondToQuery(HeaderSql, Header([SchemeId, "Brucella", "BT", "D1", "a", "S1", "Hi", true, AuthorId, "A Author", "Dept", true]));
        connection.RespondToQuery(AttachmentsSql, Attachments(
            [AttachmentId, "notes.pdf", "application/pdf", new byte[] { 1, 2 }],
            [OtherAttachmentId, DBNull.Value, DBNull.Value, DBNull.Value]));

        var entity = await repository.GetTestConsultantCommentsAsync(SchemeId);

        Assert.NotNull(entity);
        Assert.Equal((SchemeId, "Brucella", "BT", "D1", "a", "S1"),
            (entity.MonthlyDistributionSchemeId, entity.SchemeName, entity.SchemeIdentifier, entity.DistributionReference, entity.DistributionReferenceSuffix, entity.ScheduleCode));
        Assert.Equal(("Hi", true, AuthorId, "A Author", "Dept", true),
            (entity.TestConsultantComments, entity.IsCommentSubmitted, entity.TestConsultantCommentsAuthorId, entity.TestConsultantCommentsAuthor,
                entity.TestConsultantCommentsDept, entity.RequiresAssessment));
        Assert.Equal(2, entity.Attachments.Count);
        Assert.Equal((AttachmentId, "notes.pdf", "application/pdf"), (entity.Attachments[0].AttachmentId, entity.Attachments[0].FileName, entity.Attachments[0].ContentType));
        Assert.Equal([1, 2], entity.Attachments[0].FileData);
        Assert.Equal((OtherAttachmentId, string.Empty, string.Empty), (entity.Attachments[1].AttachmentId, entity.Attachments[1].FileName, entity.Attachments[1].ContentType));
        Assert.Empty(entity.Attachments[1].FileData);
        Assert.All(connection.ExecutedCommands, c => Assert.Equal(SchemeId, c.ParameterValue("MonthlyDistributionSchemeId")));
    }

    [Fact]
    public async Task GetTestConsultantCommentsAsync_NullColumns_MapToDefaults()
    {
        var (repository, connection) = CreateRepository();
        connection.RespondToQuery(HeaderSql, Header([SchemeId, .. Enumerable.Repeat<object>(DBNull.Value, 11)]));
        connection.RespondToQuery(AttachmentsSql, Attachments());

        var entity = await repository.GetTestConsultantCommentsAsync(SchemeId);

        Assert.NotNull(entity);
        Assert.All(
            [entity.SchemeName, entity.SchemeIdentifier, entity.DistributionReference, entity.DistributionReferenceSuffix, entity.ScheduleCode,
                entity.TestConsultantComments, entity.TestConsultantCommentsAuthor, entity.TestConsultantCommentsDept],
            value => Assert.Equal(string.Empty, value));
        Assert.Equal((false, Guid.Empty, false), (entity.IsCommentSubmitted, entity.TestConsultantCommentsAuthorId, entity.RequiresAssessment));
        Assert.Empty(entity.Attachments);
    }

    // ---- Update -------------------------------------------------------------------------------

    private static FakeDbConnection RespondToEveryWrite(FakeDbConnection connection)
    {
        foreach (var sql in new[] { AuditSql, UpdateSql, SetDeletedSql, InsertAttachmentSql })
        {
            // The SQL is matched exactly, so register both line-ending forms of the multi-line text.
            connection.RespondToNonQuery(sql.ReplaceLineEndings("\n"), 1);
            connection.RespondToNonQuery(sql.ReplaceLineEndings("\r\n"), 1);
        }

        return connection;
    }

    [Fact]
    public async Task UpdateTestConsultantCommentsAsync_NullArguments_Throw()
    {
        var (repository, _) = CreateRepository();

        await Assert.ThrowsAsync<ArgumentNullException>(() => repository.UpdateTestConsultantCommentsAsync(SchemeId, "c", AuthorId, false, null!, [], []));
        await Assert.ThrowsAsync<ArgumentNullException>(() => repository.UpdateTestConsultantCommentsAsync(SchemeId, "c", AuthorId, false, Audit, null!, []));
        await Assert.ThrowsAsync<ArgumentNullException>(() => repository.UpdateTestConsultantCommentsAsync(SchemeId, "c", AuthorId, false, Audit, [], null!));
    }

    [Fact]
    public async Task UpdateTestConsultantCommentsAsync_AuditsUpdatesRemovesThenInsertsInOneTransaction()
    {
        var (repository, connection) = CreateRepository();
        RespondToEveryWrite(connection);
        var file = new NewCommentsAttachment("new.pdf", "application/pdf", [7, 8], UploadDate);

        await repository.UpdateTestConsultantCommentsAsync(SchemeId, "New text", AuthorId, true, Audit, [AttachmentId, OtherAttachmentId], [file]);

        var transaction = connection.LastTransaction!;
        Assert.True(transaction.Committed);
        Assert.False(transaction.RolledBack);
        Assert.Equal(
            [Normalise(AuditSql), Normalise(UpdateSql), SetDeletedSql, SetDeletedSql, InsertAttachmentSql],
            connection.ExecutedCommands.Select(c => Normalise(c.CommandText)));
        Assert.All(connection.ExecutedCommands, c => Assert.Same(transaction, c.Transaction));

        var audit = connection.ExecutedCommands[0];
        Assert.Equal((SchemeId, "DOMAIN\\tc", "Typo", "New text", true, "a.pdf", false),
            (audit.ParameterValue("MonthlyDistributionSchemeId"), audit.ParameterValue("AuditWho"), audit.ParameterValue("AuditWhy"),
                audit.ParameterValue("TestConsultantComments"), audit.ParameterValue("TestConsultantCommentsUpdate"),
                audit.ParameterValue("FileNames"), audit.ParameterValue("FileNamesUpdate")));

        var update = connection.ExecutedCommands[1];
        Assert.Equal((SchemeId, "New text", AuthorId, true),
            (update.ParameterValue("MonthlyDistributionSchemeId"), update.ParameterValue("TestConsultantComments"),
                update.ParameterValue("TestConsultantCommentsAuthorId"), update.ParameterValue("IsCommentSubmitted")));

        Assert.Equal(AttachmentId, connection.ExecutedCommands[2].ParameterValue("AttachmentId"));
        Assert.Equal(OtherAttachmentId, connection.ExecutedCommands[3].ParameterValue("AttachmentId"));

        var insert = connection.ExecutedCommands[4];
        Assert.Equal(CommandType.StoredProcedure, insert.CommandType);
        Assert.Equal((SchemeId, "new.pdf", "application/pdf", UploadDate),
            (insert.ParameterValue("MonthlyDistributionSchemeId"), insert.ParameterValue("FileName"), insert.ParameterValue("ContentType"), insert.ParameterValue("UploadDate")));
        Assert.Equal(new byte[] { 7, 8 }, insert.ParameterValue("FileData"));
        var output = insert.Parameters.Cast<IDataParameter>().Single(p => p.ParameterName.TrimStart('@') == "AttachmentId");
        Assert.Equal((ParameterDirection.Output, DbType.Guid), (output.Direction, output.DbType));
    }

    [Fact]
    public async Task UpdateTestConsultantCommentsAsync_NoAttachmentChanges_RunsOnlyAuditAndUpdate()
    {
        var (repository, connection) = CreateRepository();
        RespondToEveryWrite(connection);

        await repository.UpdateTestConsultantCommentsAsync(SchemeId, "c", Guid.Empty, false, Audit, [], []);

        Assert.True(connection.LastTransaction!.Committed);
        Assert.Equal([Normalise(AuditSql), Normalise(UpdateSql)], connection.ExecutedCommands.Select(c => Normalise(c.CommandText)));
    }
}
