using Microsoft.Extensions.Options;
using PTL.Contracts.Distribution;
using PTL.Core.Distribution;
using static PTL.Api.Tests.Distribution.TabulationsTestData;

namespace PTL.Api.Tests.Distribution;

// View Comments - legacy Scheduling/ViewComments.aspx + ResultsTabulation.TestConsultantComments.
// Uploads are staged (IAttachmentStagingStore) and removals kept on the page until Save.
public class TestConsultantCommentsServiceTests
{
    private const string Stored = "<p>Stored comments</p>";
    private const string AuditObject = "Comments for Distribution D1a/S1";
    private const string OneAttachment = "Only one attachment is allowed.";

    private static readonly Guid SchemeId = Guid.Parse("c0000000-0000-0000-0000-0000000000c1");
    private static readonly Guid AuthorId = Guid.Parse("c0000000-0000-0000-0000-0000000000c2");
    private static readonly Guid SavedAttachmentId = Guid.Parse("c0000000-0000-0000-0000-0000000000c3");

    private readonly FakeDistributionRepository _repository = new();
    private readonly FakeAttachmentStagingStore _store = new();
    private AttachmentOptions _options = new();

    private TestConsultantCommentsService Service => new(_repository, _store, Options.Create(_options), Clock());

    private TestConsultantCommentsEntity Seed(
        bool isCommentSubmitted = false,
        bool requiresAssessment = false,
        string author = "Dr Who",
        string dept = "Virology",
        string comments = Stored,
        bool withAttachment = false)
    {
        var entity = new TestConsultantCommentsEntity
        {
            MonthlyDistributionSchemeId = SchemeId,
            SchemeIdentifier = "BT",
            SchemeName = "Brucella",
            DistributionReference = "D1",
            DistributionReferenceSuffix = "a",
            ScheduleCode = "S1",
            TestConsultantComments = comments,
            IsCommentSubmitted = isCommentSubmitted,
            RequiresAssessment = requiresAssessment,
            TestConsultantCommentsAuthorId = AuthorId,
            TestConsultantCommentsAuthor = author,
            TestConsultantCommentsDept = dept,
        };
        if (withAttachment)
        {
            entity.Attachments.Add(new TabulationSourceAttachment(SavedAttachmentId, "saved.pdf", "application/pdf", [1, 2]));
        }

        _repository.TestConsultantComments[SchemeId] = entity;
        return entity;
    }

    private Guid StageInStore(string fileName = "new.pdf", string contentType = "application/pdf")
    {
        var token = Guid.NewGuid();
        _store.Files[(SchemeId, token)] = new StagedAttachment(fileName, contentType, [9, 8, 7]);
        return token;
    }

    private static TestConsultantCommentsSaveRequest Save(
        string? comments = Stored, bool submit = false, string? auditWhy = null, IReadOnlyList<Guid>? staged = null, IReadOnlyList<Guid>? removed = null) =>
        new(comments, submit, auditWhy, staged, removed);

    private static CommentsAttachmentStageRequest Upload(
        string? fileName = "report.pdf", byte[]? data = null, string? contentType = "application/pdf", IReadOnlyList<Guid>? staged = null, IReadOnlyList<Guid>? removed = null) =>
        new(fileName, contentType, data ?? [1, 2, 3], staged, removed);

    private static void AssertError(TestConsultantCommentsSaveResult? result, string key, string message)
    {
        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.False(result.AuditReasonRequired);
        Assert.Equal(message, Assert.Single(result.Errors, e => e.Key == key).Value);
    }

    private static void AssertStageError(CommentsAttachmentStageResult? result, string message)
    {
        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.Equal(Guid.Empty, result.Token);
        Assert.Equal(message, Assert.Single(result.Errors, e => e.Key == "File").Value);
    }

    // ---- GetAsync ----

    [Fact]
    public async Task Get_MissingScheme_ReturnsNull()
    {
        Assert.Null(await Service.GetAsync(SchemeId));
    }

    [Fact]
    public async Task Get_MapsTitleCommentsAttachmentsAndTheLegacyFileTexts()
    {
        Seed(comments: "<p>Hi</p><script>x</script>", withAttachment: true);

        var page = await Service.GetAsync(SchemeId);

        Assert.NotNull(page);
        Assert.Equal(SchemeId, page.MonthlyDistributionSchemeId);
        Assert.Equal("D1a/S1 BT: Brucella", page.DistributionTitle);
        Assert.Equal("Comments provided by Dr Who, Virology", page.AuthorText);
        Assert.Equal("<p>Hi</p>x", page.CommentsHtml);
        Assert.True(page.CanSubmit);
        Assert.Equal([new CommentsAttachmentResponse(SavedAttachmentId, "saved.pdf")], page.Attachments);
        Assert.Equal([".pdf", ".jpeg", ".jpg", ".xls", ".xlsx", ".pptx"], page.AllowedFileTypes);
        Assert.Equal("PDF (.pdf), JPEG (.jpeg), JPG (.jpg), XLS (.xls), XLSX (.xlsx), PPTX (.pptx)", page.AllowedFileTypesText);
        Assert.Equal("10MB", page.MaxFileSizeText);
    }

    [Theory]
    [InlineData("", "", "No Comments have been provided. Add your own comments below.")]
    [InlineData("", "Virology", "No Comments have been provided. Add your own comments below.")]
    [InlineData("Dr Who", "", "Comments provided by Dr Who")]
    public async Task Get_AuthorText_FollowsLegacy(string author, string dept, string expected)
    {
        Seed(author: author, dept: dept);

        Assert.Equal(expected, (await Service.GetAsync(SchemeId))!.AuthorText);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task Get_SubmittedOrAssessmentScheme_CannotSubmit(bool submitted, bool requiresAssessment)
    {
        Seed(isCommentSubmitted: submitted, requiresAssessment: requiresAssessment);

        Assert.False((await Service.GetAsync(SchemeId))!.CanSubmit);
    }

    [Fact]
    public async Task Get_ConfiguredOptions_AreReflectedInTheTexts()
    {
        _options = new AttachmentOptions { AllowedFileTypes = [".docx"], MaxFileSize = 1572864 };
        Seed();

        var page = await Service.GetAsync(SchemeId);

        Assert.Equal([".docx"], page!.AllowedFileTypes);
        Assert.Equal("DOCX (.docx)", page.AllowedFileTypesText);
        Assert.Equal("1.5MB", page.MaxFileSizeText);
    }

    // ---- SaveAsync: validation ----

    [Fact]
    public async Task Save_NullRequest_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => Service.SaveAsync(SchemeId, null!, "who"));
    }

    [Fact]
    public async Task Save_MissingScheme_ReturnsNull()
    {
        Assert.Null(await Service.SaveAsync(SchemeId, Save(), "who"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("<p> </p>")]
    public async Task Save_NoComments_IsRequiredBeforeAnyAttachmentCheck(string? comments)
    {
        Seed(withAttachment: true);
        var token = StageInStore();

        var result = await Service.SaveAsync(SchemeId, Save(comments, staged: [token]), "who");

        AssertError(result, "Comments", "Comments required.");
        Assert.Empty(_repository.UpdatedComments);
        Assert.Empty(_store.Deleted);
    }

    [Fact]
    public async Task Save_CommentsTooLong_IsRejected()
    {
        Seed();

        var result = await Service.SaveAsync(SchemeId, Save(new string('a', 50001)), "who");

        AssertError(result, "Comments", TestConsultantCommentsService.CommentsTooLongMessage);
        Assert.Empty(_repository.UpdatedComments);
    }

    [Fact]
    public async Task Save_ExactlyTheMaximumLength_IsAccepted()
    {
        Seed();

        var result = await Service.SaveAsync(SchemeId, Save(new string('a', 50000), auditWhy: "why"), "who");

        Assert.True(result!.Success);
        Assert.Single(_repository.UpdatedComments);
    }

    [Fact]
    public async Task Save_SavedAttachmentKeptPlusAStagedOne_IsOneAttachmentTooMany()
    {
        Seed(withAttachment: true);
        var token = StageInStore();

        var result = await Service.SaveAsync(SchemeId, Save(staged: [token], auditWhy: "why"), "who");

        AssertError(result, "File", OneAttachment);
        Assert.Empty(_repository.UpdatedComments);
        Assert.Empty(_store.Deleted);
    }

    [Fact]
    public async Task Save_TwoStagedFiles_IsOneAttachmentTooMany()
    {
        Seed();

        var result = await Service.SaveAsync(SchemeId, Save(staged: [StageInStore("a.pdf"), StageInStore("b.pdf")], auditWhy: "why"), "who");

        AssertError(result, "File", OneAttachment);
    }

    [Fact]
    public async Task Save_RemovedIdNotBelongingToTheScheme_IsIgnored()
    {
        Seed(withAttachment: true);
        var token = StageInStore();

        var result = await Service.SaveAsync(SchemeId, Save(staged: [token], removed: [Guid.NewGuid()], auditWhy: "why"), "who");

        AssertError(result, "File", OneAttachment);
    }

    [Fact]
    public async Task Save_StagedTokenNotInTheStore_IsIgnored()
    {
        Seed();

        var result = await Service.SaveAsync(SchemeId, Save(staged: [Guid.NewGuid()]), "who");

        Assert.True(result!.Success);
        Assert.Empty(_repository.UpdatedComments);
        Assert.Empty(_store.Deleted);
    }

    // ---- SaveAsync: change detection + audit ----

    [Fact]
    public async Task Save_NothingChanged_SucceedsWithoutWriting()
    {
        Seed();

        var result = await Service.SaveAsync(SchemeId, Save(removed: [Guid.NewGuid()]), "who");

        Assert.Equal((true, false), (result!.Success, result.AuditReasonRequired));
        Assert.Empty(result.Errors);
        Assert.Empty(_repository.UpdatedComments);
    }

    [Fact]
    public async Task Save_NothingChangedOnASubmittedScheme_SucceedsWithoutWriting()
    {
        Seed(isCommentSubmitted: true);

        var result = await Service.SaveAsync(SchemeId, Save(submit: true), "who");

        Assert.True(result!.Success);
        Assert.Empty(_repository.UpdatedComments);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("  ")]
    public async Task Save_CommentsChangedWithoutAReason_AsksForTheAuditReason(string? auditWhy)
    {
        Seed();

        var result = await Service.SaveAsync(SchemeId, Save("<p>New</p>", auditWhy: auditWhy), "who");

        Assert.Equal((false, true, AuditObject), (result!.Success, result.AuditReasonRequired, result.AuditObjectName));
        Assert.Empty(result.Errors);
        Assert.Empty(_repository.UpdatedComments);
    }

    [Fact]
    public async Task Save_SubmitOnly_AsksForTheAuditReason()
    {
        Seed();

        var result = await Service.SaveAsync(SchemeId, Save(submit: true), "who");

        Assert.True(result!.AuditReasonRequired);
    }

    [Fact]
    public async Task Save_AssessmentScheme_SetsTheSubmittedFlagOnSave()
    {
        Seed(requiresAssessment: true);

        var result = await Service.SaveAsync(SchemeId, Save(auditWhy: "why"), "who");

        Assert.True(result!.Success);
        var update = Assert.Single(_repository.UpdatedComments);
        Assert.True(update.IsCommentSubmitted);
        Assert.False(update.Audit.TestConsultantCommentsUpdate);
        Assert.False(update.Audit.FileNamesUpdate);
    }

    [Fact]
    public async Task Save_StagedFileOnly_AsksForTheAuditReasonAndKeepsTheStagedFile()
    {
        Seed();
        var token = StageInStore();

        var result = await Service.SaveAsync(SchemeId, Save(staged: [token]), "who");

        Assert.True(result!.AuditReasonRequired);
        Assert.Empty(_store.Deleted);
        Assert.True(_store.Files.ContainsKey((SchemeId, token)));
    }

    [Fact]
    public async Task Save_CommentsChangedWithAReason_WritesTheSanitisedCommentsAndTheAudit()
    {
        Seed(isCommentSubmitted: true, withAttachment: true);

        var result = await Service.SaveAsync(SchemeId, Save("<p>New</p><a href=\"x\">link</a>", auditWhy: "Typo"), "DOMAIN\\user");

        Assert.Equal((true, false), (result!.Success, result.AuditReasonRequired));
        var update = Assert.Single(_repository.UpdatedComments);
        Assert.Equal(SchemeId, update.SchemeId);
        Assert.Equal("<p>New</p>link", update.Comments);
        Assert.Equal(AuthorId, update.AuthorId);
        Assert.True(update.IsCommentSubmitted);
        Assert.Equal(new CommentsAudit("DOMAIN\\user", "Typo", "<p>New</p>link", true, "saved.pdf", false), update.Audit);
        Assert.Empty(update.Removed);
        Assert.Empty(update.Added);
    }

    [Fact]
    public async Task Save_Submit_SetsTheFlag()
    {
        Seed();

        await Service.SaveAsync(SchemeId, Save(submit: true, auditWhy: "Done"), "who");

        Assert.True(Assert.Single(_repository.UpdatedComments).IsCommentSubmitted);
    }

    [Fact]
    public async Task Save_RemoveTheSavedFileAndAddAStagedOne_WritesBothThenDeletesTheStagedFile()
    {
        Seed(withAttachment: true);
        var token = StageInStore("new.xlsx", "application/vnd.ms-excel");

        var result = await Service.SaveAsync(
            SchemeId, Save(staged: [token, token], removed: [SavedAttachmentId, SavedAttachmentId], auditWhy: "Replace"), "who");

        Assert.True(result!.Success);
        var update = Assert.Single(_repository.UpdatedComments);
        Assert.Equal([SavedAttachmentId], update.Removed);
        var added = Assert.Single(update.Added);
        Assert.Equal(("new.xlsx", "application/vnd.ms-excel", Now), (added.FileName, added.ContentType, added.UploadDate));
        Assert.Equal([9, 8, 7], added.FileData);
        Assert.Equal(new CommentsAudit("who", "Replace", Stored, false, "new.xlsx", true), update.Audit);
        Assert.Equal([(SchemeId, token)], _store.Deleted);
    }

    [Fact]
    public async Task Save_RemoveOnly_RecordsTheEmptyFileList()
    {
        Seed(withAttachment: true);

        await Service.SaveAsync(SchemeId, Save(removed: [SavedAttachmentId], auditWhy: "Wrong file"), "who");

        var update = Assert.Single(_repository.UpdatedComments);
        Assert.Equal(("", true), (update.Audit.FileNames, update.Audit.FileNamesUpdate));
        Assert.Equal([SavedAttachmentId], update.Removed);
        Assert.Empty(update.Added);
    }

    // ---- StageAttachmentAsync ----

    [Fact]
    public async Task Stage_NullRequest_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => Service.StageAttachmentAsync(SchemeId, null!));
    }

    [Fact]
    public async Task Stage_MissingScheme_ReturnsNull()
    {
        Assert.Null(await Service.StageAttachmentAsync(SchemeId, Upload()));
    }

    [Fact]
    public async Task Stage_SavedAttachmentNotRemoved_IsCheckedBeforeTheFile()
    {
        Seed(withAttachment: true);

        AssertStageError(await Service.StageAttachmentAsync(SchemeId, Upload(fileName: null, data: [])), OneAttachment);
        Assert.Empty(_store.Files);
    }

    [Fact]
    public async Task Stage_AnotherStagedFile_IsOneAttachmentTooMany()
    {
        Seed();

        AssertStageError(await Service.StageAttachmentAsync(SchemeId, Upload(staged: [Guid.NewGuid()])), OneAttachment);
    }

    [Fact]
    public async Task Stage_SavedAttachmentRemoved_AllowsANewUpload()
    {
        Seed(withAttachment: true);

        var result = await Service.StageAttachmentAsync(SchemeId, Upload(removed: [SavedAttachmentId]));

        Assert.True(result!.Success);
    }

    [Theory]
    [InlineData(null, new byte[] { 1 })]
    [InlineData(" ", new byte[] { 1 })]
    [InlineData("a.pdf", new byte[0])]
    public async Task Stage_NoFile_AsksForAFile(string? fileName, byte[] data)
    {
        Seed();

        AssertStageError(await Service.StageAttachmentAsync(SchemeId, Upload(fileName, data)), "Please select a file to upload.");
    }

    [Fact]
    public async Task Stage_NullData_AsksForAFile()
    {
        Seed();

        AssertStageError(
            await Service.StageAttachmentAsync(SchemeId, new CommentsAttachmentStageRequest("a.pdf", "application/pdf", null, null, null)),
            "Please select a file to upload.");
    }

    [Theory]
    [InlineData("virus.exe")]
    [InlineData("noextension")]
    public async Task Stage_DisallowedExtension_ListsTheAllowedTypes(string fileName)
    {
        Seed();

        AssertStageError(await Service.StageAttachmentAsync(SchemeId, Upload(fileName)), "Only PDF, JPEG, JPG, XLS, XLSX, PPTX files are allowed.");
        Assert.Empty(_store.Files);
    }

    [Fact]
    public async Task Stage_TooLarge_ReportsTheLimit()
    {
        Seed();

        AssertStageError(
            await Service.StageAttachmentAsync(SchemeId, Upload(data: new byte[_options.MaxFileSize + 1])),
            "File size exceeds the 10MB limit.");
    }

    [Fact]
    public async Task Stage_ValidFile_IsSavedInTheStoreUnderANewToken()
    {
        Seed();

        var result = await Service.StageAttachmentAsync(SchemeId, Upload("folder/Report.PDF", new byte[_options.MaxFileSize], "application/pdf"));

        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotEqual(Guid.Empty, result.Token);
        Assert.Equal("Report.PDF", result.FileName);
        Assert.Empty(result.Errors);
        var stored = _store.Files[(SchemeId, result.Token)];
        Assert.Equal(("Report.PDF", "application/pdf", _options.MaxFileSize), (stored.FileName, stored.ContentType, stored.FileData.Length));
        Assert.Empty(_repository.UpdatedComments);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task Stage_NoContentType_DefaultsToOctetStream(string? contentType)
    {
        Seed();

        var result = await Service.StageAttachmentAsync(SchemeId, Upload("a.jpg", contentType: contentType));

        Assert.Equal("application/octet-stream", _store.Files[(SchemeId, result!.Token)].ContentType);
    }

    [Fact]
    public async Task Stage_EachUpload_GetsItsOwnToken()
    {
        Seed();

        var first = await Service.StageAttachmentAsync(SchemeId, Upload());
        var second = await Service.StageAttachmentAsync(SchemeId, Upload());

        Assert.NotEqual(first!.Token, second!.Token);
    }

    // ---- Discard / downloads ----

    [Fact]
    public async Task DiscardStaged_DeletesEachDistinctToken()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();

        await Service.DiscardStagedAttachmentsAsync(SchemeId, [a, b, a]);

        Assert.Equal([(SchemeId, a), (SchemeId, b)], _store.Deleted);
    }

    [Fact]
    public async Task DiscardStaged_NullTokens_DeletesNothing()
    {
        await Service.DiscardStagedAttachmentsAsync(SchemeId, null!);

        Assert.Empty(_store.Deleted);
    }

    [Fact]
    public async Task GetAttachment_ReturnsTheSchemesAttachmentOnly()
    {
        Seed(withAttachment: true);

        Assert.Equal("saved.pdf", (await Service.GetAttachmentAsync(SchemeId, SavedAttachmentId))!.FileName);
        Assert.Null(await Service.GetAttachmentAsync(SchemeId, Guid.NewGuid()));
        Assert.Null(await Service.GetAttachmentAsync(Guid.NewGuid(), SavedAttachmentId));
    }

    [Fact]
    public async Task GetStagedAttachment_ReadsTheStore()
    {
        var token = StageInStore("staged.pdf");

        Assert.Equal("staged.pdf", (await Service.GetStagedAttachmentAsync(SchemeId, token))!.FileName);
        Assert.Null(await Service.GetStagedAttachmentAsync(Guid.NewGuid(), token));
    }
}
