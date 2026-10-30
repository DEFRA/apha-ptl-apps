using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using PTL.Api.Controllers;
using PTL.Contracts.Distribution;
using PTL.Core.Distribution;
using static PTL.Api.Tests.Distribution.TabulationsTestData;

namespace PTL.Api.Tests.Distribution;

// View Comments endpoints - the actions take ITestConsultantCommentsService via [FromServices].
public class DistributionControllerCommentsTests
{
    private static readonly Guid SchemeId = Guid.Parse("c1000000-0000-0000-0000-0000000000c1");
    private static readonly Guid AttachmentId = Guid.Parse("c1000000-0000-0000-0000-0000000000c2");

    private readonly FakeDistributionRepository _repository = new();
    private readonly FakeAttachmentStagingStore _store = new();

    private TestConsultantCommentsService Service => new(_repository, _store, Options.Create(new AttachmentOptions()), Clock());

    private DistributionController CreateController(string? userName = null) =>
        new(
            new DistributionService(_repository),
            new SampleNumbersService(_repository, new FakeSchemeService()),
            new IntendedResultsService(_repository),
            new DistributionParticipantsService(_repository),
            new PreparationService(_repository),
            new ResultsReceiptService(_repository, TimeProvider.System),
            new TabulationsService(_repository, Clock()),
            new ActualResultsService(_repository, TimeProvider.System),
            new ParticipantDistributionHistoryService(_repository, new PTL.Api.Tests.Participant.FakeParticipantRepository(), new ActualResultsService(_repository, TimeProvider.System)),
            new FakePackagingService(),
            new FakePackagingOutputService(),
            new PTL.Api.Tests.Contract.FakeAuditActorResolver(userName ?? "Guest"),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<DistributionController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };

    private void Seed(string contentType = "application/pdf")
    {
        var entity = new TestConsultantCommentsEntity
        {
            MonthlyDistributionSchemeId = SchemeId,
            DistributionReference = "D1",
            ScheduleCode = "S1",
            TestConsultantComments = "<p>Old</p>",
        };
        entity.Attachments.Add(new TabulationSourceAttachment(AttachmentId, "saved file.pdf", contentType, [1, 2, 3]));
        _repository.TestConsultantComments[SchemeId] = entity;
    }

    private static T OkValue<T>(ActionResult<T> result) => Assert.IsType<T>(Assert.IsType<OkObjectResult>(result.Result).Value);

    [Fact]
    public async Task GetComments_Missing_ReturnsNotFound()
    {
        Assert.IsType<NotFoundResult>((await CreateController().GetTestConsultantComments(SchemeId, Service, CancellationToken.None)).Result);
    }

    [Fact]
    public async Task GetComments_ReturnsThePage()
    {
        Seed();

        var page = OkValue(await CreateController().GetTestConsultantComments(SchemeId, Service, CancellationToken.None));

        Assert.Equal("<p>Old</p>", page.CommentsHtml);
        Assert.Equal(AttachmentId, Assert.Single(page.Attachments).AttachmentId);
    }

    [Fact]
    public async Task SaveComments_Missing_ReturnsNotFound()
    {
        var result = await CreateController().SaveTestConsultantComments(SchemeId, new("<p>x</p>", false, "why"), Service, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task SaveComments_AuditsAsTheSignedInUser()
    {
        Seed();

        var result = OkValue(await CreateController("DOMAIN\\tc").SaveTestConsultantComments(
            SchemeId, new("<p>New</p>", false, "Typo"), Service, CancellationToken.None));

        Assert.True(result.Success);
        Assert.Equal(("DOMAIN\\tc", "Typo"), (Assert.Single(_repository.UpdatedComments).Audit.AuditWho, _repository.UpdatedComments[0].Audit.AuditWhy));
    }

    [Fact]
    public async Task SaveComments_NoUserName_AuditsAsGuest()
    {
        Seed();

        await CreateController().SaveTestConsultantComments(SchemeId, new("<p>New</p>", false, "Typo"), Service, CancellationToken.None);

        Assert.Equal("Guest", Assert.Single(_repository.UpdatedComments).Audit.AuditWho);
    }

    [Fact]
    public async Task SaveComments_AuditReasonAndErrors_AreReturnedAsOk()
    {
        Seed();
        var controller = CreateController();

        var audit = OkValue(await controller.SaveTestConsultantComments(SchemeId, new("<p>New</p>", false), Service, CancellationToken.None));
        var invalid = OkValue(await controller.SaveTestConsultantComments(SchemeId, new("", false), Service, CancellationToken.None));

        Assert.True(audit.AuditReasonRequired);
        Assert.Equal("Comments for Distribution D1/S1", audit.AuditObjectName);
        Assert.Equal("Comments required.", invalid.Errors["Comments"]);
        Assert.Empty(_repository.UpdatedComments);
    }

    [Fact]
    public async Task StageAttachment_Missing_ReturnsNotFound()
    {
        var result = await CreateController().StageTestConsultantCommentsAttachment(
            SchemeId, new("a.pdf", "application/pdf", [1], null, null), Service, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task StageAttachment_ReturnsTheResultAndStagesTheFile()
    {
        Seed();

        var result = OkValue(await CreateController().StageTestConsultantCommentsAttachment(
            SchemeId, new("b.pdf", "application/pdf", [1], null, [AttachmentId]), Service, CancellationToken.None));

        Assert.True(result.Success);
        Assert.Equal("b.pdf", _store.Files[(SchemeId, result.Token)].FileName);
    }

    [Fact]
    public async Task StageAttachment_ValidationError_IsReturnedAsOk()
    {
        Seed();

        var result = OkValue(await CreateController().StageTestConsultantCommentsAttachment(
            SchemeId, new("b.pdf", "application/pdf", [1], null, null), Service, CancellationToken.None));

        Assert.False(result.Success);
        Assert.Equal("Only one attachment is allowed.", result.Errors["File"]);
    }

    [Fact]
    public async Task DiscardStaged_DeletesTheTokensAndReturnsNoContent()
    {
        var token = Guid.NewGuid();

        var result = await CreateController().DiscardStagedTestConsultantCommentsAttachments(SchemeId, new([token]), Service, CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        Assert.Equal([(SchemeId, token)], _store.Deleted);
    }

    [Fact]
    public async Task DiscardStaged_NullBody_ReturnsNoContent()
    {
        var result = await CreateController().DiscardStagedTestConsultantCommentsAttachments(SchemeId, null!, Service, CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        Assert.Empty(_store.Deleted);
    }

    [Fact]
    public async Task GetAttachment_Missing_ReturnsNotFound()
    {
        Seed();

        Assert.IsType<NotFoundResult>(await CreateController().GetTestConsultantCommentsAttachment(SchemeId, Guid.NewGuid(), Service, CancellationToken.None));
        Assert.IsType<NotFoundResult>(await CreateController().GetTestConsultantCommentsAttachment(Guid.NewGuid(), AttachmentId, Service, CancellationToken.None));
    }

    [Theory]
    [InlineData("application/pdf")]
    [InlineData("image/png")]
    [InlineData("image/jpeg")]
    [InlineData("image/gif")]
    public async Task GetAttachment_SafeType_IsServedInline(string contentType)
    {
        Seed(contentType);
        var controller = CreateController();

        var file = Assert.IsType<FileContentResult>(await controller.GetTestConsultantCommentsAttachment(SchemeId, AttachmentId, Service, CancellationToken.None));

        Assert.Equal(contentType, file.ContentType);
        Assert.Equal([1, 2, 3], file.FileContents);
        var disposition = controller.Response.Headers.ContentDisposition.ToString();
        Assert.StartsWith("inline", disposition, StringComparison.Ordinal);
        Assert.Contains("filename=\"saved file.pdf\"", disposition, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("application/vnd.ms-excel")]
    [InlineData("text/html")]
    public async Task GetAttachment_OtherType_IsDownloadedAsOctetStream(string contentType)
    {
        Seed(contentType);
        var controller = CreateController();

        var file = Assert.IsType<FileContentResult>(await controller.GetTestConsultantCommentsAttachment(SchemeId, AttachmentId, Service, CancellationToken.None));

        Assert.Equal("application/octet-stream", file.ContentType);
        Assert.StartsWith("attachment", controller.Response.Headers.ContentDisposition.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetStagedAttachment_Missing_ReturnsNotFound()
    {
        Assert.IsType<NotFoundResult>(await CreateController().GetStagedTestConsultantCommentsAttachment(SchemeId, Guid.NewGuid(), Service, CancellationToken.None));
    }

    [Theory]
    [InlineData("image/jpeg", "image/jpeg", "inline")]
    [InlineData("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "application/octet-stream", "attachment")]
    public async Task GetStagedAttachment_ServesTheStagedFile(string storedType, string servedType, string disposition)
    {
        var token = Guid.NewGuid();
        _store.Files[(SchemeId, token)] = new StagedAttachment("staged.jpg", storedType, [4, 5]);
        var controller = CreateController();

        var file = Assert.IsType<FileContentResult>(await controller.GetStagedTestConsultantCommentsAttachment(SchemeId, token, Service, CancellationToken.None));

        Assert.Equal(servedType, file.ContentType);
        Assert.Equal([4, 5], file.FileContents);
        var header = controller.Response.Headers.ContentDisposition.ToString();
        Assert.StartsWith(disposition, header, StringComparison.Ordinal);
        Assert.Contains("filename=staged.jpg", header, StringComparison.Ordinal);
    }
}
