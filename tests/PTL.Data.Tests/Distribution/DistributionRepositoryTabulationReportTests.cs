using System.Data;
using PTL.Data.Distribution;
using PTL.Data.Infrastructure;
using PTL.Data.Tests.Fakes;

namespace PTL.Data.Tests.Distribution;

// spgDistributionTabulation, spgPublishedTabulation, spgPublishedTabulations and the two assessment
// score procedures - legacy ResultsTabulation.Tabulation / Published.Tabulation / AssessmentScoreInfoCollection fetches.
public class DistributionRepositoryTabulationReportTests
{
    public DistributionRepositoryTabulationReportTests() => DapperColumnMappings.Register();

    private const string DraftSql = "EXEC dbo.spgDistributionTabulation @DistributionTabulationId = @TabulationId";
    private const string PublishedSql = "EXEC dbo.spgPublishedTabulation @PublishedTabulationId = @PublishedTabulationId";
    private const string PublishedListSql = "EXEC dbo.spgPublishedTabulations @DistributionTabulationId = @TabulationId";
    private const string DraftScoresSql = "EXEC dbo.spgAssessmentScoreByTestId @TestId = @TestId, @TabulationId = @TabulationId";
    private const string PublishedScoresSql = "EXEC dbo.spgPublishedAssessmentScoreByTestId @TestId = @TestId, @TabulationId = @TabulationId";

    private static readonly Guid TabulationId = Guid.Parse("11111111-0000-0000-0000-000000000001");
    private static readonly Guid TestId = Guid.Parse("22222222-0000-0000-0000-000000000001");
    private static readonly Guid MethodId = Guid.Parse("33333333-0000-0000-0000-000000000001");
    private static readonly Guid ResultId = Guid.Parse("44444444-0000-0000-0000-000000000001");
    private static readonly Guid CategoryId = Guid.Parse("55555555-0000-0000-0000-000000000001");
    private static readonly Guid SampleId = Guid.Parse("66666666-0000-0000-0000-000000000001");
    private static readonly Guid SampleTestId = Guid.Parse("66666666-1111-0000-0000-000000000001");
    private static readonly Guid ParticipantId = Guid.Parse("77777777-0000-0000-0000-000000000001");
    private static readonly Guid ViewerId = Guid.Parse("88888888-0000-0000-0000-000000000001");
    private static readonly Guid AttachmentId = Guid.Parse("99999999-0000-0000-0000-000000000001");
    private static readonly Guid Unknown = Guid.Parse("00000000-0000-0000-0000-0000000000ff");
    private static readonly DateTime Day = new(2026, 5, 10);

    private static (DistributionRepository Repository, FakeDbConnection Connection) CreateRepository()
    {
        var connection = new FakeDbConnection();
        return (new DistributionRepository(new FakeDbConnectionFactory(connection)), connection);
    }

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

    private static DataSet Sets(params DataTable[] tables)
    {
        var dataSet = new DataSet();
        dataSet.Tables.AddRange(tables);
        return dataSet;
    }

    private static readonly (string, Type)[] DraftHeaderColumns =
    [
        ("fldDistributionTabulationId", typeof(Guid)), ("fldMonthlyDistributionSchemeId", typeof(Guid)), ("fldTabulationName", typeof(string)),
        ("fldSchemeName", typeof(string)), ("fldSchemeIdentifier", typeof(string)), ("fldRequiresAssessment", typeof(bool)),
        ("fldDistributionReference", typeof(string)), ("fldDistributionReferenceSuffix", typeof(string)), ("fldScheduleCode", typeof(string)),
        ("fldComments", typeof(string)), ("fldTestConsultantComments", typeof(string)), ("fldTestConsultantCommentsAuthor", typeof(string)),
        ("fldTestConsultantCommentsDept", typeof(string)), ("fldDistributionDate", typeof(DateTime)), ("fldShowUkasLogo", typeof(bool)),
        ("fldSchemeVersionDate", typeof(DateTime)), ("fldHasRatings", typeof(bool)), ("fldIntendedResultsOnly", typeof(bool)),
        ("fldSingleParticipantTabulation", typeof(bool)), ("fldAvailableToViewers", typeof(bool)), ("fldStandardTabulationText", typeof(string)),
    ];

    private static readonly (string, Type)[] PublishedHeaderColumns =
    [
        ("fldPublishedTabulationId", typeof(Guid)), ("fldName", typeof(string)), ("fldSignatory", typeof(string)), ("fldCreationDate", typeof(DateTime)),
        ("fldDistribution", typeof(string)), ("fldDistributionDate", typeof(DateTime)), ("fldSchemeIdentifier", typeof(string)),
        ("fldSchemeName", typeof(string)), ("fldRequiresAssessment", typeof(bool)), ("fldDistributionTabulationId", typeof(Guid)),
        ("fldSampleComments", typeof(string)), ("fldTestConsultantComments", typeof(string)), ("fldTestConsultantCommentsAuthor", typeof(string)),
        ("fldTestConsultantCommentsDept", typeof(string)), ("fldIsAccredited", typeof(bool)), ("fldSchemeVersionDate", typeof(DateTime)),
        ("fldHasRatings", typeof(bool)), ("fldSingleParticipantTabulation", typeof(bool)), ("fldAvailableToViewers", typeof(bool)),
        ("fldIntendedResultsOnly", typeof(bool)), ("fldStandardTabulationText", typeof(string)),
    ];

    private static DataTable Viewers(bool withRows) => withRows
        ? Rows(Table(("fldViewerId", typeof(Guid)), ("fldName", typeof(string))),
            [ViewerId, "Viewer One"], [ViewerId, "Viewer One"], [Unknown, DBNull.Value])
        : Table(("fldViewerId", typeof(Guid)), ("fldName", typeof(string)));

    private static DataTable ViewerParticipants(bool withRows) => withRows
        ? Rows(Table(("fldViewerId", typeof(Guid)), ("fldParticipantId", typeof(Guid))),
            [ViewerId, ParticipantId], [Guid.NewGuid(), ParticipantId])
        : Table(("fldViewerId", typeof(Guid)), ("fldParticipantId", typeof(Guid)));

    private static DataTable CriterionItems() => Table(("fldCriterionItemId", typeof(Guid)));

    private static DataSet DraftSets(DataTable header, bool withRows)
    {
        object[][] When(params object[][] rows) => withRows ? rows : [];

        return Sets(
            header,
            Rows(Table(("fldTestId", typeof(Guid)), ("fldTestType", typeof(string)), ("fldAssessmentSubmitted", typeof(DateTime))),
                When([TestId, "Culture", Day], [Guid.NewGuid(), DBNull.Value, DBNull.Value])),
            Rows(Table(("fldTestId", typeof(Guid)), ("fldTestMethodItemId", typeof(Guid)), ("fldTestMethodItemType", typeof(string))),
                When([TestId, MethodId, "Method"], [Unknown, Guid.NewGuid(), "Orphan"])),
            Rows(Table(("fldTestId", typeof(Guid)), ("fldTestResultItemId", typeof(Guid)), ("fldTestResultItemType", typeof(string))),
                When([TestId, ResultId, "Identity"], [Unknown, Guid.NewGuid(), "Orphan"])),
            Rows(Table(("fldCategoryItemId", typeof(Guid)), ("fldTestId", typeof(Guid)), ("fldName", typeof(string))),
                When([CategoryId, TestId, "Clinical"], [Guid.NewGuid(), Unknown, "Orphan"])),
            CriterionItems(),
            Rows(Table(("fldTestId", typeof(Guid)), ("fldSampleId", typeof(Guid)), ("fldSampleNumber", typeof(string)), ("fldTabulationDescription", typeof(string)), ("fldSampleTestId", typeof(Guid))),
                When([TestId, SampleId, "S1", "Abortus", SampleTestId], [Unknown, Guid.NewGuid(), "S9", "Orphan", Guid.NewGuid()])),
            Rows(Table(("fldParticipantId", typeof(Guid)), ("fldLabCode", typeof(string)), ("fldNilReturnReason", typeof(string))),
                When([ParticipantId, "L001", "Late"])),
            Rows(Table(("fldSampleId", typeof(Guid)), ("fldParticipantId", typeof(Guid)), ("fldRating", typeof(string))),
                When([SampleId, ParticipantId, "2"], [SampleId, ParticipantId, "1"], [SampleId, Unknown, "0"])),
            Rows(Table(("fldParticipantId", typeof(Guid)), ("fldTestMethodItemId", typeof(Guid)), ("fldAmendedResult", typeof(string))),
                When([ParticipantId, MethodId, "PCR"], [Unknown, MethodId, "X"])),
            Rows(Table(("fldSampleId", typeof(Guid)), ("fldParticipantId", typeof(Guid)), ("fldTestResultItemId", typeof(Guid)), ("fldAmendedResult", typeof(string))),
                When([SampleId, ParticipantId, ResultId, "Abortus"], [SampleId, Unknown, ResultId, "X"])),
            Rows(Table(("fldParticipantId", typeof(Guid)), ("fldCategoryItemId", typeof(Guid)), ("fldSampleTestId", typeof(Guid)), ("fldCategoryScore", typeof(int)), ("fldSampleId", typeof(Guid)), ("fldAssessmentSubmitted", typeof(DateTime))),
                When([ParticipantId, CategoryId, SampleTestId, 4, SampleId, Day], [Unknown, CategoryId, SampleTestId, 1, SampleId, Day])),
            Rows(Table(("fldParticipantId", typeof(Guid)), ("fldCategoryItemId", typeof(Guid)), ("fldSampleId", typeof(Guid)), ("fldName", typeof(string))),
                When([ParticipantId, CategoryId, SampleId, "Correct"], [ParticipantId, CategoryId, SampleId, "Neat"], [ParticipantId, Unknown, SampleId, "Lost"], [Unknown, CategoryId, SampleId, "Lost"])),
            Rows(Table(("fldTestMethodItemId", typeof(Guid)), ("fldIntendedResult", typeof(string))),
                When([MethodId, "ELISA"])),
            Rows(Table(("fldSampleId", typeof(Guid)), ("fldTestResultItemId", typeof(Guid)), ("fldIntendedResult", typeof(string)), ("fldIntendedResult2", typeof(string))),
                When([SampleId, ResultId, "1", "5"])),
            Viewers(withRows),
            ViewerParticipants(withRows),
            Rows(Table(("fldTestId", typeof(Guid)), ("fldSampleId", typeof(Guid)), ("fldParticipantId", typeof(Guid)), ("fldScore", typeof(int)), ("fldAssessmentComment", typeof(string)), ("fldSubmissionDate", typeof(DateTime)), ("fldNonSubmissionDate", typeof(DateTime)), ("fldNonCapableDate", typeof(DateTime)), ("fldAssessmentSubmitted", typeof(DateTime))),
                When([TestId, SampleId, ParticipantId, 4, "Good", Day, Day.AddDays(1), Day.AddDays(2), Day.AddDays(3)], [TestId, SampleId, Unknown, 1, "X", DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value])),
            Rows(Table(("fldMonthlyDistributionAttachmentId", typeof(Guid)), ("fldFileName", typeof(string)), ("fldContentType", typeof(string)), ("fldFileData", typeof(byte[]))),
                When([AttachmentId, "notes.pdf", "application/pdf", new byte[] { 1, 2 }])));
    }

    private static DataSet PublishedSets(DataTable header, bool withRows)
    {
        object[][] When(params object[][] rows) => withRows ? rows : [];

        return Sets(
            header,
            Rows(Table(("fldPublishedTestId", typeof(Guid)), ("fldName", typeof(string))),
                When([TestId, "Culture"])),
            Rows(Table(("fldPublishedSampleId", typeof(Guid)), ("fldPublishedTestId", typeof(Guid)), ("fldSampleNumber", typeof(string)), ("fldTabulationDescription", typeof(string))),
                When([SampleId, TestId, "S1", "Abortus"], [Guid.NewGuid(), Unknown, "S9", "Orphan"])),
            Rows(Table(("fldPublishedTestMethodItemId", typeof(Guid)), ("fldPublishedTestId", typeof(Guid)), ("fldName", typeof(string))),
                When([MethodId, TestId, "Method"], [Guid.NewGuid(), Unknown, "Orphan"])),
            Rows(Table(("fldPublishedTestResultItemId", typeof(Guid)), ("fldPublishedTestId", typeof(Guid)), ("fldName", typeof(string))),
                When([ResultId, TestId, "Identity"], [Guid.NewGuid(), Unknown, "Orphan"])),
            Rows(Table(("fldPublishedCategoryItemId", typeof(Guid)), ("fldPublishedTestId", typeof(Guid)), ("fldName", typeof(string))),
                When([CategoryId, TestId, "Clinical"], [Guid.NewGuid(), Unknown, "Orphan"])),
            CriterionItems(),
            Rows(Table(("fldPublishedParticipantId", typeof(Guid)), ("fldLabCode", typeof(string)), ("fldNilReturnReason", typeof(string))),
                When([ParticipantId, "L001", "Late"])),
            Rows(Table(("fldPublishedParticipantId", typeof(Guid)), ("fldPublishedSampleId", typeof(Guid)), ("fldRating", typeof(string))),
                When([ParticipantId, SampleId, "2"], [Unknown, SampleId, "1"])),
            Rows(Table(("fldPublishedParticipantId", typeof(Guid)), ("fldPublishedTestMethodItemId", typeof(Guid)), ("fldResult", typeof(string))),
                When([ParticipantId, MethodId, "PCR"], [Unknown, MethodId, "X"])),
            Rows(Table(("fldPublishedParticipantId", typeof(Guid)), ("fldPublishedTestResultItemId", typeof(Guid)), ("fldPublishedSampleId", typeof(Guid)), ("fldResult", typeof(string))),
                When([ParticipantId, ResultId, SampleId, "Abortus"], [Unknown, ResultId, SampleId, "X"])),
            Rows(Table(("fldPublishedParticipantId", typeof(Guid)), ("fldPublishedCategoryItemId", typeof(Guid)), ("fldScore", typeof(int)), ("fldPublishedSampleId", typeof(Guid))),
                When([ParticipantId, CategoryId, 5, SampleId], [Unknown, CategoryId, 1, SampleId])),
            Rows(Table(("fldPublishedParticipantId", typeof(Guid)), ("fldPublishedSampleId", typeof(Guid)), ("fldPublishedCategoryItemId", typeof(Guid)), ("fldName", typeof(string))),
                When([ParticipantId, SampleId, CategoryId, "Complete"], [ParticipantId, SampleId, Unknown, "Lost"], [Unknown, SampleId, CategoryId, "Lost"])),
            Rows(Table(("fldPublishedTestMethodItemId", typeof(Guid)), ("fldResult", typeof(string))),
                When([MethodId, "ELISA"])),
            Rows(Table(("fldPublishedTestResultItemId", typeof(Guid)), ("fldPublishedSampleId", typeof(Guid)), ("fldResult", typeof(string)), ("fldResult2", typeof(string))),
                When([ResultId, SampleId, "Pos", DBNull.Value])),
            Viewers(withRows),
            ViewerParticipants(withRows),
            Rows(Table(("fldPublishedTestId", typeof(Guid)), ("fldPublishedSampleId", typeof(Guid)), ("fldPublishedParticipantId", typeof(Guid)), ("fldAssessmentScore", typeof(int)), ("fldAssessmentComment", typeof(string)), ("fldSubmissionDate", typeof(DateTime)), ("fldNonSubmissionDate", typeof(DateTime)), ("fldNonCapableDate", typeof(DateTime))),
                When([TestId, SampleId, ParticipantId, 3, "Issued comment", Day, Day.AddDays(1), Day.AddDays(2)], [TestId, SampleId, Unknown, 1, "X", DBNull.Value, DBNull.Value, DBNull.Value])),
            Rows(Table(("fldPublishedAttachmentId", typeof(Guid)), ("fldFileName", typeof(string)), ("fldContentType", typeof(string)), ("fldFileData", typeof(byte[]))),
                When([AttachmentId, "issued.pdf", "application/pdf", new byte[] { 9 }])));
    }

    private static DataTable FullDraftHeader() =>
        Rows(Table(DraftHeaderColumns),
            [TabulationId, Guid.NewGuid(), "Summary", "Brucella", "BT", true, "D1", "a", "S1", "<p>Comments</p>", "<p>Consultant</p>", "Dr A", "QAU",
             new DateTime(2026, 5, 3), true, new DateTime(2025, 4, 1), true, true, true, true, "<p>Standard</p>"]);

    private static DataTable NullDraftHeader() =>
        Rows(Table(DraftHeaderColumns),
            [TabulationId, DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value,
             DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value]);

    private static DataTable FullPublishedHeader() =>
        Rows(Table(PublishedHeaderColumns),
            [Guid.Parse("12121212-0000-0000-0000-000000000001"), "Summary", "Dr Sign", new DateTime(2026, 6, 10), "D1a/S1", new DateTime(2026, 5, 3), "BT", "Brucella", true, TabulationId,
             "<p>Sample comments</p>", "<p>Consultant</p>", "Dr A", "QAU", true, new DateTime(2025, 4, 1), true, true, true, true, "<p>Standard</p>"]);

    private static DataTable NullPublishedHeader() =>
        Rows(Table(PublishedHeaderColumns),
            [Guid.Parse("12121212-0000-0000-0000-000000000001"), DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, TabulationId,
             DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value]);

    [Fact]
    public async Task GetTabulationSourceAsync_NoHeaderRow_ReturnsNull()
    {
        var (repository, connection) = CreateRepository();
        connection.RespondToQuery(DraftSql, Sets(Table(DraftHeaderColumns)));

        Assert.Null(await repository.GetTabulationSourceAsync(TabulationId));

        var command = Assert.Single(connection.ExecutedCommands);
        Assert.Equal(TabulationId, command.ParameterValue("TabulationId"));
    }

    [Fact]
    public async Task GetTabulationSourceAsync_MapsTheHeader()
    {
        var (repository, connection) = CreateRepository();
        connection.RespondToQuery(DraftSql, DraftSets(FullDraftHeader(), withRows: false));

        var source = (await repository.GetTabulationSourceAsync(TabulationId))!;

        Assert.Equal((TabulationId, TabulationId, false), (source.TabulationId, source.DistributionTabulationId, source.IsPublished));
        Assert.Equal(("D1a/S1", "BT", "Brucella", "Summary"), (source.Distribution, source.SchemeIdentifier, source.SchemeName, source.TabulationName));
        Assert.True(source.RequiresAssessment);
        Assert.Equal(("<p>Comments</p>", "<p>Consultant</p>", "Dr A", "QAU"), (source.Comments, source.TestConsultantComments, source.TestConsultantCommentsAuthor, source.TestConsultantCommentsDept));
        Assert.Equal((new DateTime(2026, 5, 3), new DateTime(2025, 4, 1)), (source.DistributionDate, source.SchemeVersionDate));
        Assert.True(source.ShowUkasLogo && source.HasRatings && source.IntendedResultsOnly && source.SingleParticipantTabulation && source.AvailableToViewers);
        Assert.Equal("<p>Standard</p>", source.StandardTabulationText);
        Assert.Equal((string.Empty, (DateTime?)null), (source.Signatory, source.CreationDate));
        Assert.Empty(source.Tests);
        Assert.Empty(source.Participants);
        Assert.Empty(source.Viewers);
        Assert.Empty(source.Attachments);
    }

    [Fact]
    public async Task GetTabulationSourceAsync_NullHeaderColumns_MapToDefaults()
    {
        var (repository, connection) = CreateRepository();
        connection.RespondToQuery(DraftSql, DraftSets(NullDraftHeader(), withRows: false));

        var source = (await repository.GetTabulationSourceAsync(TabulationId))!;

        Assert.Equal("/", source.Distribution);
        Assert.Equal(string.Empty, source.SchemeIdentifier + source.SchemeName + source.TabulationName + source.Comments
            + source.TestConsultantComments + source.TestConsultantCommentsAuthor + source.TestConsultantCommentsDept + source.StandardTabulationText);
        Assert.False(source.RequiresAssessment || source.ShowUkasLogo || source.HasRatings || source.IntendedResultsOnly
            || source.SingleParticipantTabulation || source.AvailableToViewers);
        Assert.Equal(default, source.DistributionDate);
        Assert.Equal(default, source.SchemeVersionDate);
    }

    [Fact]
    public async Task GetTabulationSourceAsync_MapsEveryResultSetAndIgnoresRowsForUnknownParents()
    {
        var (repository, connection) = CreateRepository();
        connection.RespondToQuery(DraftSql, DraftSets(FullDraftHeader(), withRows: true));

        var source = (await repository.GetTabulationSourceAsync(TabulationId))!;

        Assert.Equal(2, source.Tests.Count);
        var test = source.Tests[0];
        Assert.Equal((TestId, "Culture", true), (test.TestId, test.Name, test.IsAssessmentSubmitted));
        Assert.Equal((string.Empty, false), (source.Tests[1].Name, source.Tests[1].IsAssessmentSubmitted));
        Assert.Equal([new(MethodId, "Method")], test.MethodItems);
        Assert.Equal([new(ResultId, "Identity")], test.ResultItems);
        Assert.Equal([new(CategoryId, "Clinical")], test.CategoryItems);
        Assert.Equal([new(SampleId, SampleTestId, "S1", "Abortus")], test.Samples);
        Assert.Empty(source.Tests[1].MethodItems);

        var participant = Assert.Single(source.Participants);
        Assert.Equal((ParticipantId, "L001", "Late"), (participant.ParticipantId, participant.LabCode, participant.NilReturnReason));
        // The first row wins for duplicate keys.
        Assert.Equal("2", Assert.Single(participant.SampleRatings).Value);
        Assert.Equal("PCR", participant.MethodResults[MethodId]);
        Assert.Equal("Abortus", participant.ResultItemResults[(SampleId, ResultId)]);
        var category = participant.CategoryResults[(SampleId, CategoryId)];
        Assert.Equal((SampleTestId, 4, true), (category.SampleTestId, category.Score, category.IsAssessmentSubmitted));
        Assert.Equal(["Correct", "Neat"], category.Criteria);
        var assessment = participant.Assessments[(SampleId, TestId)];
        Assert.Equal(new(4, "Good", Day, Day.AddDays(1), Day.AddDays(2), Day.AddDays(3)), assessment);

        Assert.Equal("ELISA", source.IntendedMethodResults[MethodId]);
        Assert.Equal(("1", "5"), source.IntendedResultItemResults[(SampleId, ResultId)]);

        Assert.Equal(2, source.Viewers.Count);
        Assert.Equal((ViewerId, "Viewer One"), (source.Viewers[0].ViewerId, source.Viewers[0].Name));
        Assert.Equal([ParticipantId], source.Viewers[0].ParticipantIds);
        Assert.Equal((Unknown, string.Empty), (source.Viewers[1].ViewerId, source.Viewers[1].Name));
        Assert.Empty(source.Viewers[1].ParticipantIds);

        var attachment = Assert.Single(source.Attachments);
        Assert.Equal((AttachmentId, "notes.pdf", "application/pdf"), (attachment.AttachmentId, attachment.FileName, attachment.ContentType));
        Assert.Equal([1, 2], attachment.FileData);
    }

    [Fact]
    public async Task GetTabulationSourceAsync_NullDetailColumns_MapToDefaults()
    {
        var (repository, connection) = CreateRepository();
        var data = DraftSets(FullDraftHeader(), withRows: false);
        data.Tables[1].Rows.Add(TestId, DBNull.Value, DBNull.Value);
        data.Tables[2].Rows.Add(TestId, MethodId, DBNull.Value);
        data.Tables[3].Rows.Add(TestId, ResultId, DBNull.Value);
        data.Tables[4].Rows.Add(CategoryId, TestId, DBNull.Value);
        data.Tables[6].Rows.Add(TestId, SampleId, DBNull.Value, DBNull.Value, SampleTestId);
        data.Tables[7].Rows.Add(ParticipantId, DBNull.Value, DBNull.Value);
        data.Tables[8].Rows.Add(SampleId, ParticipantId, DBNull.Value);
        data.Tables[9].Rows.Add(ParticipantId, MethodId, DBNull.Value);
        data.Tables[10].Rows.Add(SampleId, ParticipantId, ResultId, DBNull.Value);
        data.Tables[11].Rows.Add(ParticipantId, CategoryId, SampleTestId, DBNull.Value, SampleId, DBNull.Value);
        data.Tables[12].Rows.Add(ParticipantId, CategoryId, SampleId, DBNull.Value);
        data.Tables[13].Rows.Add(MethodId, DBNull.Value);
        data.Tables[14].Rows.Add(SampleId, ResultId, DBNull.Value, DBNull.Value);
        data.Tables[17].Rows.Add(TestId, SampleId, ParticipantId, DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value);
        data.Tables[18].Rows.Add(AttachmentId, DBNull.Value, DBNull.Value, DBNull.Value);
        connection.RespondToQuery(DraftSql, data);

        var source = (await repository.GetTabulationSourceAsync(TabulationId))!;

        var test = Assert.Single(source.Tests);
        Assert.Equal((string.Empty, false), (test.Name, test.IsAssessmentSubmitted));
        Assert.Equal(string.Empty, test.MethodItems[0].Name + test.ResultItems[0].Name + test.CategoryItems[0].Name);
        Assert.Equal(("", ""), (test.Samples[0].SampleNumber, test.Samples[0].TabulationDescription));
        var participant = Assert.Single(source.Participants);
        Assert.Equal(("", ""), (participant.LabCode, participant.NilReturnReason));
        Assert.Equal("", participant.SampleRatings[SampleId]);
        Assert.Equal("", participant.MethodResults[MethodId]);
        Assert.Equal("", participant.ResultItemResults[(SampleId, ResultId)]);
        var category = participant.CategoryResults[(SampleId, CategoryId)];
        Assert.Equal((0, false), (category.Score, category.IsAssessmentSubmitted));
        Assert.Equal([""], category.Criteria);
        Assert.Equal(new(0, "", null, null, null, null), participant.Assessments[(SampleId, TestId)]);
        Assert.Equal("", source.IntendedMethodResults[MethodId]);
        Assert.Equal(("", ""), source.IntendedResultItemResults[(SampleId, ResultId)]);
        var attachment = Assert.Single(source.Attachments);
        Assert.Equal(("", ""), (attachment.FileName, attachment.ContentType));
        Assert.Empty(attachment.FileData);
    }

    [Fact]
    public async Task GetPublishedTabulationSourceAsync_EmptyResult_ReturnsNull()
    {
        var (repository, connection) = CreateRepository();
        connection.RespondToQuery(PublishedSql, Sets(Table(PublishedHeaderColumns)));
        var publishedId = Guid.NewGuid();

        Assert.Null(await repository.GetPublishedTabulationSourceAsync(publishedId));

        Assert.Equal(publishedId, Assert.Single(connection.ExecutedCommands).ParameterValue("PublishedTabulationId"));
    }

    [Fact]
    public async Task GetPublishedTabulationSourceAsync_MapsTheHeader()
    {
        var (repository, connection) = CreateRepository();
        connection.RespondToQuery(PublishedSql, PublishedSets(FullPublishedHeader(), withRows: false));

        var source = (await repository.GetPublishedTabulationSourceAsync(Guid.NewGuid()))!;

        Assert.True(source.IsPublished);
        Assert.Equal((Guid.Parse("12121212-0000-0000-0000-000000000001"), TabulationId), (source.TabulationId, source.DistributionTabulationId));
        Assert.Equal(("D1a/S1", "BT", "Brucella", "Summary"), (source.Distribution, source.SchemeIdentifier, source.SchemeName, source.TabulationName));
        Assert.True(source.RequiresAssessment);
        Assert.Equal(("<p>Sample comments</p>", "<p>Consultant</p>", "Dr A", "QAU"), (source.Comments, source.TestConsultantComments, source.TestConsultantCommentsAuthor, source.TestConsultantCommentsDept));
        Assert.Equal((new DateTime(2026, 5, 3), new DateTime(2025, 4, 1)), (source.DistributionDate, source.SchemeVersionDate));
        Assert.True(source.ShowUkasLogo && source.HasRatings && source.IntendedResultsOnly && source.SingleParticipantTabulation && source.AvailableToViewers);
        Assert.Equal("<p>Standard</p>", source.StandardTabulationText);
        Assert.Equal(("Dr Sign", (DateTime?)new DateTime(2026, 6, 10)), (source.Signatory, source.CreationDate));
    }

    [Fact]
    public async Task GetPublishedTabulationSourceAsync_NullHeaderColumns_MapToDefaults()
    {
        var (repository, connection) = CreateRepository();
        connection.RespondToQuery(PublishedSql, PublishedSets(NullPublishedHeader(), withRows: false));

        var source = (await repository.GetPublishedTabulationSourceAsync(Guid.NewGuid()))!;

        Assert.Equal(string.Empty, source.Distribution + source.SchemeIdentifier + source.SchemeName + source.TabulationName + source.Comments
            + source.TestConsultantComments + source.TestConsultantCommentsAuthor + source.TestConsultantCommentsDept + source.StandardTabulationText + source.Signatory);
        Assert.False(source.RequiresAssessment || source.ShowUkasLogo || source.HasRatings || source.IntendedResultsOnly
            || source.SingleParticipantTabulation || source.AvailableToViewers);
        Assert.Null(source.CreationDate);
        Assert.Equal(default, source.DistributionDate);
        Assert.Equal(default, source.SchemeVersionDate);
    }

    [Fact]
    public async Task GetPublishedTabulationSourceAsync_MapsEveryResultSetAndLinksEveryTestToItsAssessmentResults()
    {
        var (repository, connection) = CreateRepository();
        connection.RespondToQuery(PublishedSql, PublishedSets(FullPublishedHeader(), withRows: true));

        var source = (await repository.GetPublishedTabulationSourceAsync(Guid.NewGuid()))!;

        var test = Assert.Single(source.Tests);
        Assert.Equal((TestId, "Culture", true), (test.TestId, test.Name, test.IsAssessmentSubmitted));
        Assert.Equal([new(SampleId, null, "S1", "Abortus")], test.Samples);
        Assert.Equal([new(MethodId, "Method")], test.MethodItems);
        Assert.Equal([new(ResultId, "Identity")], test.ResultItems);
        Assert.Equal([new(CategoryId, "Clinical")], test.CategoryItems);

        var participant = Assert.Single(source.Participants);
        Assert.Equal((ParticipantId, "L001", "Late"), (participant.ParticipantId, participant.LabCode, participant.NilReturnReason));
        Assert.Equal("2", participant.SampleRatings[SampleId]);
        Assert.Equal("PCR", participant.MethodResults[MethodId]);
        Assert.Equal("Abortus", participant.ResultItemResults[(SampleId, ResultId)]);
        var category = participant.CategoryResults[(SampleId, CategoryId)];
        Assert.Equal(((Guid?)null, 5, true), (category.SampleTestId, category.Score, category.IsAssessmentSubmitted));
        Assert.Equal(["Complete"], category.Criteria);
        Assert.Equal(new(3, "Issued comment", Day, Day.AddDays(1), Day.AddDays(2), null), participant.Assessments[(SampleId, TestId)]);

        Assert.Equal("ELISA", source.IntendedMethodResults[MethodId]);
        Assert.Equal(("Pos", ""), source.IntendedResultItemResults[(SampleId, ResultId)]);
        Assert.Equal([ParticipantId], source.Viewers[0].ParticipantIds);

        var attachment = Assert.Single(source.Attachments);
        Assert.Equal((AttachmentId, "issued.pdf", "application/pdf"), (attachment.AttachmentId, attachment.FileName, attachment.ContentType));
        Assert.Equal([9], attachment.FileData);
    }

    [Fact]
    public async Task GetPublishedTabulationSourceAsync_NullDetailColumns_MapToDefaults()
    {
        var (repository, connection) = CreateRepository();
        var data = PublishedSets(FullPublishedHeader(), withRows: false);
        data.Tables[1].Rows.Add(TestId, DBNull.Value);
        data.Tables[2].Rows.Add(SampleId, TestId, DBNull.Value, DBNull.Value);
        data.Tables[3].Rows.Add(MethodId, TestId, DBNull.Value);
        data.Tables[4].Rows.Add(ResultId, TestId, DBNull.Value);
        data.Tables[5].Rows.Add(CategoryId, TestId, DBNull.Value);
        data.Tables[7].Rows.Add(ParticipantId, DBNull.Value, DBNull.Value);
        data.Tables[8].Rows.Add(ParticipantId, SampleId, DBNull.Value);
        data.Tables[9].Rows.Add(ParticipantId, MethodId, DBNull.Value);
        data.Tables[10].Rows.Add(ParticipantId, ResultId, SampleId, DBNull.Value);
        data.Tables[11].Rows.Add(ParticipantId, CategoryId, DBNull.Value, SampleId);
        data.Tables[12].Rows.Add(ParticipantId, SampleId, CategoryId, DBNull.Value);
        data.Tables[13].Rows.Add(MethodId, DBNull.Value);
        data.Tables[14].Rows.Add(ResultId, SampleId, DBNull.Value, DBNull.Value);
        data.Tables[17].Rows.Add(TestId, SampleId, ParticipantId, DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value);
        data.Tables[18].Rows.Add(AttachmentId, DBNull.Value, DBNull.Value, DBNull.Value);
        connection.RespondToQuery(PublishedSql, data);

        var source = (await repository.GetPublishedTabulationSourceAsync(Guid.NewGuid()))!;

        var test = Assert.Single(source.Tests);
        Assert.Equal(string.Empty, test.Name + test.MethodItems[0].Name + test.ResultItems[0].Name + test.CategoryItems[0].Name
            + test.Samples[0].SampleNumber + test.Samples[0].TabulationDescription);
        var participant = Assert.Single(source.Participants);
        Assert.Equal(string.Empty, participant.LabCode + participant.NilReturnReason + participant.SampleRatings[SampleId]
            + participant.MethodResults[MethodId] + participant.ResultItemResults[(SampleId, ResultId)]);
        Assert.Equal(0, participant.CategoryResults[(SampleId, CategoryId)].Score);
        Assert.Equal([""], participant.CategoryResults[(SampleId, CategoryId)].Criteria);
        Assert.Equal(new(0, "", null, null, null, null), participant.Assessments[(SampleId, TestId)]);
        Assert.Equal("", source.IntendedMethodResults[MethodId]);
        Assert.Equal(("", ""), source.IntendedResultItemResults[(SampleId, ResultId)]);
        var attachment = Assert.Single(source.Attachments);
        Assert.Equal(("", ""), (attachment.FileName, attachment.ContentType));
        Assert.Empty(attachment.FileData);
    }

    private static DataTable PublishedListHeader() =>
        Table(("fldTabulationName", typeof(string)), ("fldDistribution", typeof(string)), ("fldSchemeIdentifier", typeof(string)), ("fldSchemeName", typeof(string)));

    private static DataTable PublishedListRows() =>
        Table(("fldPublishedTabulationId", typeof(Guid)), ("fldSignatory", typeof(string)), ("fldCreationDate", typeof(DateTime)));

    [Fact]
    public async Task GetPublishedTabulationsAsync_NoHeader_ReturnsNull()
    {
        var (repository, connection) = CreateRepository();
        connection.RespondToQuery(PublishedListSql, Sets(PublishedListHeader(), PublishedListRows()));

        Assert.Null(await repository.GetPublishedTabulationsAsync(TabulationId));

        Assert.Equal(TabulationId, Assert.Single(connection.ExecutedCommands).ParameterValue("TabulationId"));
    }

    [Fact]
    public async Task GetPublishedTabulationsAsync_MapsHeaderAndRowsInOrder()
    {
        var (repository, connection) = CreateRepository();
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        connection.RespondToQuery(PublishedListSql, Sets(
            Rows(PublishedListHeader(), ["Summary", "D1a/S1", "BT", "Brucella"]),
            Rows(PublishedListRows(), [first, "Dr A", new DateTime(2026, 6, 1, 9, 30, 0)], [second, DBNull.Value, new DateTime(2026, 6, 2)])));

        var page = (await repository.GetPublishedTabulationsAsync(TabulationId))!;

        Assert.Equal(("Summary", "D1a/S1", "BT", "Brucella"), (page.TabulationName, page.Distribution, page.SchemeIdentifier, page.SchemeName));
        Assert.Equal([new(first, "Dr A", new DateTime(2026, 6, 1, 9, 30, 0)), new(second, "", new DateTime(2026, 6, 2))], page.Tabulations);
    }

    [Fact]
    public async Task GetPublishedTabulationsAsync_NullHeaderColumns_MapToEmptyStrings()
    {
        var (repository, connection) = CreateRepository();
        connection.RespondToQuery(PublishedListSql, Sets(
            Rows(PublishedListHeader(), [DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value]),
            PublishedListRows()));

        var page = (await repository.GetPublishedTabulationsAsync(TabulationId))!;

        Assert.Equal(string.Empty, page.TabulationName + page.Distribution + page.SchemeIdentifier + page.SchemeName);
        Assert.Empty(page.Tabulations);
    }

    private static DataTable ScoreHeader() =>
        Table(("fldTabulationName", typeof(string)), ("fldSchemeName", typeof(string)), ("fldDistribution", typeof(string)), ("fldTestName", typeof(string)), ("fldAssessmentSubmitted", typeof(DateTime)));

    private static DataTable ScoreSamples() => Table(("fldSampleNumber", typeof(string)));

    private static DataTable ScoreReceipts() =>
        Table(("fldSubmissionDate", typeof(DateTime)), ("fldNonSubmissionDate", typeof(DateTime)), ("fldNonCapableDate", typeof(DateTime)), ("fldScore", typeof(int)), ("fldAssessmentDate", typeof(DateTime)));

    [Theory]
    [InlineData(false, DraftScoresSql)]
    [InlineData(true, PublishedScoresSql)]
    public async Task GetAssessmentScoresAsync_UsesTheDraftOrPublishedProcedureAndMapsEveryResultSet(bool isPublished, string sql)
    {
        var (repository, connection) = CreateRepository();
        connection.RespondToQuery(sql, Sets(
            Rows(ScoreHeader(), ["Summary", "Brucella", "D1a/S1", "Culture", Day]),
            Rows(ScoreSamples(), ["S1"], ["S2"]),
            Rows(ScoreReceipts(), [Day, DBNull.Value, DBNull.Value, 4, Day], [DBNull.Value, Day, Day, DBNull.Value, DBNull.Value])));

        var scores = (await repository.GetAssessmentScoresAsync(TestId, TabulationId, isPublished))!;

        var command = Assert.Single(connection.ExecutedCommands);
        Assert.Equal(sql, command.CommandText);
        Assert.Equal((TestId, TabulationId), ((Guid)command.ParameterValue("TestId")!, (Guid)command.ParameterValue("TabulationId")!));
        Assert.Equal(("Summary", "Brucella", "D1a/S1", "Culture", (DateTime?)Day), (scores.TabulationName, scores.SchemeName, scores.Distribution, scores.TestName, scores.AssessmentSubmitted));
        Assert.Equal(["S1", "S2"], scores.SampleNumbers);
        Assert.Equal([new(Day, null, null, 4, Day), new(null, Day, Day, 0, null)], scores.Receipts);
    }

    [Fact]
    public async Task GetAssessmentScoresAsync_NoHeader_ReturnsNull()
    {
        var (repository, connection) = CreateRepository();
        connection.RespondToQuery(DraftScoresSql, Sets(ScoreHeader(), ScoreSamples(), ScoreReceipts()));

        Assert.Null(await repository.GetAssessmentScoresAsync(TestId, TabulationId, false));
    }

    [Fact]
    public async Task GetAssessmentScoresAsync_NullHeaderColumns_MapToDefaults()
    {
        var (repository, connection) = CreateRepository();
        connection.RespondToQuery(DraftScoresSql, Sets(
            Rows(ScoreHeader(), [DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value]),
            ScoreSamples(),
            ScoreReceipts()));

        var scores = (await repository.GetAssessmentScoresAsync(TestId, TabulationId, false))!;

        Assert.Equal(string.Empty, scores.TabulationName + scores.SchemeName + scores.Distribution + scores.TestName);
        Assert.Null(scores.AssessmentSubmitted);
        Assert.Empty(scores.SampleNumbers);
        Assert.Empty(scores.Receipts);
    }
}
