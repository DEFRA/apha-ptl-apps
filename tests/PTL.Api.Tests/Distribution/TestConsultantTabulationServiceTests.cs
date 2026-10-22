using PTL.Contracts.Distribution;
using PTL.Core.Distribution;
using static PTL.Api.Tests.Distribution.TestConsultantTestData;

namespace PTL.Api.Tests.Distribution;

public class TestConsultantTabulationServiceTests
{
    private readonly FakeTestConsultantTabulationRepository _repository = new();
    private readonly FakeDistributionRepository _distributionRepository = new();

    private TestConsultantTabulationService Service => new(_repository, _distributionRepository);

    private void SeedScheme(TestConsultantSchemeState? state = null, bool submitted = false, string comments = "")
    {
        _repository.States[SchemeId] = state ?? State(submitted: submitted, comments: comments);
        _repository.Tabulations[(SchemeId, false)] = Entity(submitted, comments);
        _repository.Tabulations[(SchemeId, true)] = Entity(submitted, comments);
    }

    // ---- Distributions ------------------------------------------------------------------------

    [Fact]
    public async Task GetDistributionsAsync_MapsEachSchemeWithItsTitleAndStatus()
    {
        _repository.Distributions.Add(new TestConsultantDistributionEntity
        {
            YearId = 2026,
            MonthId = 6,
            MonthlyDistributionSchemeId = SchemeId,
            DistributionReference = "17208",
            DistributionReferenceSuffix = "/BA",
            ScheduleCode = "PT0074",
            SchemeIdentifier = "S1",
            SchemeName = "Pasteurella",
            TargetDate = new DateTime(2026, 7, 21),
        });
        _repository.Distributions.Add(new TestConsultantDistributionEntity { YearId = 2026, MonthId = 5, IsCommentSubmitted = true });

        var result = await Service.GetDistributionsAsync(ConsultantId);

        Assert.Equal(ConsultantId, _repository.DistributionsRequestedFor);
        var response = result.Value!;
        var open = response.Distributions[0];
        Assert.Equal(("17208/BA/PT0074: S1 Pasteurella", 2026, 6, new DateTime(2026, 7, 21), false, "Awaiting comments"),
            (open.DistributionTitle, open.YearId, open.MonthId, open.TargetDate, open.IsCommentSubmitted, open.CommentStatus));
        Assert.Equal(("Comments submitted", true), (response.Distributions[1].CommentStatus, response.Distributions[1].IsCommentSubmitted));
    }

    [Fact]
    public async Task GetDistributionsAsync_NoDistributions_IsEmpty() =>
        Assert.Empty((await Service.GetDistributionsAsync(ConsultantId)).Value!.Distributions);

    [Fact]
    public async Task GetDistributionsAsync_InactiveConsultant_IsForbiddenAndReadsNothing()
    {
        _repository.InactiveConsultants.Add(ConsultantId);
        _repository.Distributions.Add(new TestConsultantDistributionEntity { YearId = 2026, MonthId = 6 });

        var result = await Service.GetDistributionsAsync(ConsultantId);

        Assert.Equal((TestConsultantAccess.Forbidden, null), (result.Access, result.Value));
        Assert.Null(_repository.DistributionsRequestedFor);
    }

    [Fact]
    public async Task GetTabulationAsync_InactiveAllocatedConsultant_IsForbiddenAndReadsNothing()
    {
        SeedScheme();
        _repository.InactiveConsultants.Add(ConsultantId);

        var result = await Service.GetTabulationAsync(ConsultantId, SchemeId, TestConsultantTabulationFilter.None);

        Assert.Equal(TestConsultantAccess.Forbidden, result.Access);
        Assert.Empty(_repository.TabulationRequests);
    }

    [Fact]
    public async Task ExportAsync_InactiveAllocatedConsultant_IsForbiddenAndReadsNothing()
    {
        SeedScheme();
        _repository.InactiveConsultants.Add(ConsultantId);

        var result = await Service.ExportAsync(ConsultantId, SchemeId, TestConsultantTabulationFilter.None);

        Assert.Equal(TestConsultantAccess.Forbidden, result.Access);
        Assert.Empty(_repository.TabulationRequests);
    }

    [Fact]
    public async Task SaveCommentsAsync_InactiveAllocatedConsultant_IsForbiddenAndWritesNothing()
    {
        SeedScheme();
        _repository.InactiveConsultants.Add(ConsultantId);

        var result = await Service.SaveCommentsAsync(ConsultantId, SchemeId, new("<p>Hi</p>", true));

        Assert.Equal(TestConsultantAccess.Forbidden, result.Access);
        Assert.Empty(_distributionRepository.UpdatedComments);
    }

    // ---- Tabulation ---------------------------------------------------------------------------

    [Fact]
    public async Task GetTabulationAsync_NullFilter_Throws() =>
        await Assert.ThrowsAsync<ArgumentNullException>(() => Service.GetTabulationAsync(ConsultantId, SchemeId, null!));

    [Fact]
    public async Task GetTabulationAsync_AllocatedConsultant_ReturnsTheTabulation()
    {
        SeedScheme();

        var result = await Service.GetTabulationAsync(ConsultantId, SchemeId, TestConsultantTabulationFilter.None);

        Assert.Equal(TestConsultantAccess.Granted, result.Access);
        Assert.Equal("FINAL REPORT for D1a/S1 BT: Brucella", result.Value!.Title);
        Assert.Equal([(SchemeId, false), (SchemeId, true)], _repository.TabulationRequests);
    }

    [Fact]
    public async Task GetTabulationAsync_AppliesTheFilter()
    {
        SeedScheme();

        var result = await Service.GetTabulationAsync(ConsultantId, SchemeId, new TestConsultantTabulationFilter(["26/2"], false));

        Assert.Equal(["PCR", "Method Details", "26/1"], result.Value!.ResultsTables[0].Rows[0].Cells.Select(c => c.Text));
    }

    [Fact]
    public async Task GetTabulationAsync_UnknownScheme_IsNotFound()
    {
        var result = await Service.GetTabulationAsync(ConsultantId, SchemeId, TestConsultantTabulationFilter.None);

        Assert.Equal((TestConsultantAccess.NotFound, null), (result.Access, result.Value));
        Assert.Empty(_repository.TabulationRequests);
    }

    [Fact]
    public async Task GetTabulationAsync_ConsultantNotAllocated_IsForbiddenAndReadsNothing()
    {
        SeedScheme();

        var result = await Service.GetTabulationAsync(OtherConsultantId, SchemeId, TestConsultantTabulationFilter.None);

        Assert.Equal(TestConsultantAccess.Forbidden, result.Access);
        Assert.Empty(_repository.TabulationRequests);
    }

    [Fact]
    public async Task GetTabulationAsync_EmptyConsultantId_IsForbidden()
    {
        SeedScheme(new TestConsultantSchemeState(Guid.Empty, null, null, false, string.Empty, Guid.Empty));

        Assert.Equal(TestConsultantAccess.Forbidden, (await Service.GetTabulationAsync(Guid.Empty, SchemeId, TestConsultantTabulationFilter.None)).Access);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task GetTabulationAsync_AnyOfTheThreeAllocatedConsultants_IsGranted(int slot)
    {
        SeedScheme(new TestConsultantSchemeState(
            slot == 1 ? ConsultantId : OtherConsultantId, slot == 2 ? ConsultantId : null, slot == 3 ? ConsultantId : null, false, string.Empty, Guid.Empty));

        Assert.Equal(TestConsultantAccess.Granted, (await Service.GetTabulationAsync(ConsultantId, SchemeId, TestConsultantTabulationFilter.None)).Access);
    }

    [Fact]
    public async Task GetTabulationAsync_TabulationOutsideRetention_IsNotFound()
    {
        _repository.States[SchemeId] = State();
        _repository.Tabulations[(SchemeId, false)] = Entity();

        Assert.Equal(TestConsultantAccess.NotFound, (await Service.GetTabulationAsync(ConsultantId, SchemeId, TestConsultantTabulationFilter.None)).Access);
    }

    // ---- Export -------------------------------------------------------------------------------

    [Fact]
    public async Task ExportAsync_NullFilter_Throws() =>
        await Assert.ThrowsAsync<ArgumentNullException>(() => Service.ExportAsync(ConsultantId, SchemeId, null!));

    [Fact]
    public async Task ExportAsync_ReturnsAWorkbookNamedAfterTheDistribution()
    {
        SeedScheme();

        var result = await Service.ExportAsync(ConsultantId, SchemeId, TestConsultantTabulationFilter.None);

        Assert.Equal(TestConsultantAccess.Granted, result.Access);
        Assert.Equal("D1a_S1_BT.xlsx", result.Value!.FileName);
        Assert.Equal([0x50, 0x4B], result.Value.Content[..2]);
    }

    [Fact]
    public async Task ExportAsync_WithRatings_IncludesThemInTheWorkbook()
    {
        SeedScheme();
        _repository.Tabulations[(SchemeId, true)] = Entity(hasRatings: true);

        var result = await Service.ExportAsync(ConsultantId, SchemeId, TestConsultantTabulationFilter.None);

        Assert.True(result.Value!.Content.Length > 0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExportAsync_NotAllowed_ReturnsNoFile(bool unknownScheme)
    {
        if (!unknownScheme)
        {
            SeedScheme();
        }

        var result = await Service.ExportAsync(OtherConsultantId, SchemeId, TestConsultantTabulationFilter.None);

        Assert.Equal(unknownScheme ? TestConsultantAccess.NotFound : TestConsultantAccess.Forbidden, result.Access);
        Assert.Null(result.Value);
    }

    // ---- Save ---------------------------------------------------------------------------------

    [Fact]
    public async Task SaveCommentsAsync_NullRequest_Throws() =>
        await Assert.ThrowsAsync<ArgumentNullException>(() => Service.SaveCommentsAsync(ConsultantId, SchemeId, null!));

    [Fact]
    public async Task SaveCommentsAsync_UnknownScheme_IsNotFound()
    {
        var result = await Service.SaveCommentsAsync(ConsultantId, SchemeId, new("<p>Hi</p>", false));

        Assert.Equal(TestConsultantAccess.NotFound, result.Access);
        Assert.Empty(_distributionRepository.UpdatedComments);
    }

    [Fact]
    public async Task SaveCommentsAsync_ConsultantNotAllocated_IsForbiddenAndWritesNothing()
    {
        SeedScheme();

        var result = await Service.SaveCommentsAsync(OtherConsultantId, SchemeId, new("<p>Hi</p>", true));

        Assert.Equal(TestConsultantAccess.Forbidden, result.Access);
        Assert.Empty(_distributionRepository.UpdatedComments);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SaveCommentsAsync_AlreadySubmitted_IsLockedAndWritesNothing(bool submit)
    {
        SeedScheme(submitted: true, comments: "<p>Final</p>");

        var result = (await Service.SaveCommentsAsync(ConsultantId, SchemeId, new("<p>Changed</p>", submit))).Value!;

        Assert.Equal((false, true), (result.Success, result.IsLocked));
        Assert.Empty(result.Errors);
        Assert.Empty(_distributionRepository.UpdatedComments);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("<p> </p>")]
    public async Task SaveCommentsAsync_NoComments_IsInvalid(string? comments)
    {
        SeedScheme();

        var result = (await Service.SaveCommentsAsync(ConsultantId, SchemeId, new(comments, true))).Value!;

        Assert.False(result.Success);
        Assert.Equal(TestConsultantTabulationService.CommentsRequiredMessage, result.Errors["Comments"]);
        Assert.Empty(_distributionRepository.UpdatedComments);
    }

    [Fact]
    public async Task SaveCommentsAsync_TooLong_IsInvalid()
    {
        SeedScheme();

        var result = (await Service.SaveCommentsAsync(ConsultantId, SchemeId, new("<p>" + new string('a', 50000) + "</p>", false))).Value!;

        Assert.False(result.Success);
        Assert.Equal(TestConsultantTabulationService.CommentsTooLongMessage, result.Errors["Comments"]);
        Assert.Empty(_distributionRepository.UpdatedComments);
    }

    [Fact]
    public async Task SaveCommentsAsync_NewDraft_SavesItAsTheConsultantsWithoutSubmitting()
    {
        SeedScheme();
        _repository.Names[ConsultantId] = "A Author";

        var result = (await Service.SaveCommentsAsync(ConsultantId, SchemeId, new("<p onclick=\"x()\">Draft</p>", false))).Value!;

        Assert.True(result.Success);
        var saved = Assert.Single(_distributionRepository.UpdatedComments);
        Assert.Equal((SchemeId, "<p>Draft</p>", ConsultantId, false), (saved.SchemeId, saved.Comments, saved.AuthorId, saved.IsCommentSubmitted));
        Assert.Equal(("A Author", "Test Consultant saved draft comments.", "<p>Draft</p>", true, string.Empty, false),
            (saved.Audit.AuditWho, saved.Audit.AuditWhy, saved.Audit.TestConsultantComments, saved.Audit.TestConsultantCommentsUpdate, saved.Audit.FileNames, saved.Audit.FileNamesUpdate));
        Assert.Empty(saved.Removed);
        Assert.Empty(saved.Added);
    }

    [Fact]
    public async Task SaveCommentsAsync_UnchangedDraft_WritesNothing()
    {
        SeedScheme(comments: "<p>Draft</p>");

        var result = (await Service.SaveCommentsAsync(ConsultantId, SchemeId, new("<p>Draft</p>", false))).Value!;

        Assert.True(result.Success);
        Assert.Empty(_distributionRepository.UpdatedComments);
    }

    [Fact]
    public async Task SaveCommentsAsync_ChangedDraft_TakesOverTheAuthor()
    {
        SeedScheme(State(comments: "<p>Old</p>", author: OtherConsultantId));

        await Service.SaveCommentsAsync(ConsultantId, SchemeId, new("<p>New</p>", false));

        Assert.Equal(ConsultantId, Assert.Single(_distributionRepository.UpdatedComments).AuthorId);
    }

    [Fact]
    public async Task SaveCommentsAsync_Submit_SubmitsWithAnAuditRow()
    {
        SeedScheme();

        var result = (await Service.SaveCommentsAsync(ConsultantId, SchemeId, new("<p>Final</p>", true))).Value!;

        Assert.True(result.Success);
        var saved = Assert.Single(_distributionRepository.UpdatedComments);
        Assert.Equal(("<p>Final</p>", ConsultantId, true), (saved.Comments, saved.AuthorId, saved.IsCommentSubmitted));
        Assert.Equal("Test Consultant submitted comments for sign-off.", saved.Audit.AuditWhy);
    }

    [Fact]
    public async Task SaveCommentsAsync_SubmitUnchangedDraft_StillSubmitsAndKeepsTheAuthor()
    {
        SeedScheme(State(comments: "<p>Draft</p>", author: OtherConsultantId));

        await Service.SaveCommentsAsync(ConsultantId, SchemeId, new("<p>Draft</p>", true));

        var saved = Assert.Single(_distributionRepository.UpdatedComments);
        Assert.Equal((OtherConsultantId, true, true), (saved.AuthorId, saved.IsCommentSubmitted, saved.Audit.TestConsultantCommentsUpdate));
    }

    [Fact]
    public async Task SaveCommentsAsync_NoNameOnRecord_AuditsTheIdAndTruncatesToTheColumnLength()
    {
        SeedScheme();
        await Service.SaveCommentsAsync(ConsultantId, SchemeId, new("<p>Draft</p>", false));
        _repository.Names[ConsultantId] = new string('n', 80);
        _repository.States[SchemeId] = State();
        await Service.SaveCommentsAsync(ConsultantId, SchemeId, new("<p>Other</p>", false));

        Assert.Equal(ConsultantId.ToString(), _distributionRepository.UpdatedComments[0].Audit.AuditWho);
        Assert.Equal(new string('n', 50), _distributionRepository.UpdatedComments[1].Audit.AuditWho);
    }
}
