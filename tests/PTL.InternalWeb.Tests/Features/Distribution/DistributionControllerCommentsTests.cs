using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using PTL.ApiClient.Distribution;
using PTL.Contracts.Distribution;
using PTL.InternalWeb.Features.Distribution;
using PTL.InternalWeb.Tests.TestSupport;
using PTL.SharedUI.Notifications;

namespace PTL.InternalWeb.Tests.Features.Distribution;

// View Comments - legacy Scheduling/ViewComments.aspx (+ Audit.aspx). Attachment uploads and
// removals are staged in the form and only written by Save.
public class DistributionControllerCommentsTests
{
    private static readonly Guid SchemeId = Guid.Parse("cccccccc-0000-0000-0000-0000000000a1");
    private static readonly Guid SavedId = Guid.Parse("cccccccc-0000-0000-0000-0000000000a2");
    private static readonly Guid OtherSavedId = Guid.Parse("cccccccc-0000-0000-0000-0000000000a3");
    private static readonly Guid Token = Guid.Parse("cccccccc-0000-0000-0000-0000000000b1");
    private static readonly Guid OtherToken = Guid.Parse("cccccccc-0000-0000-0000-0000000000b2");

    private const string AuditObject = "Comments for Distribution D1a/S1";

    private static TestConsultantCommentsResponse Comments(bool canSubmit = true) =>
        new(
            SchemeId, "D1a/S1 BT: Brucella", "Comments by Dr A", "<p>Stored</p>", canSubmit,
            [new CommentsAttachmentResponse(SavedId, "saved.pdf")],
            [".pdf", ".docx"], "PDF, Word", "10MB");

    private static FakeDistributionApiClient ApiClient(TestConsultantCommentsSaveResult? save = null) =>
        new() { TestConsultantComments = Comments(), CommentsSaveResult = save };

    private static DistributionController CreateController(FakeDistributionApiClient apiClient) =>
        new(apiClient)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
            TempData = new TempDataDictionary(new DefaultHttpContext(), new FakeTempDataProvider()),
        };

    private static TestConsultantCommentsSaveResult Saved() => new(true, false, string.Empty, new Dictionary<string, string>());

    private static TestConsultantCommentsSaveResult AuditRequired() => new(false, true, AuditObject, new Dictionary<string, string>());

    private static ViewCommentsViewModel Posted(string? comments = "<p>New</p>") => new()
    {
        Comments = comments,
        StagedAttachments = [$"{Token}|new.pdf"],
        RemovedAttachmentIds = [SavedId],
    };

    private static Task<IActionResult> PostAsync(DistributionController controller, ViewCommentsViewModel model, string? command) =>
        controller.ViewComments(2026, 5, SchemeId, model, command, CancellationToken.None);

    private static ViewCommentsViewModel AssertRedisplayed(IActionResult result)
    {
        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal("ViewComments", view.ViewName);
        return Assert.IsType<ViewCommentsViewModel>(view.Model);
    }

    private static void AssertRedirectToTabulations(IActionResult result)
    {
        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Tabulations", redirect.ActionName);
        Assert.Equal((2026, 5), ((int)redirect.RouteValues!["year"]!, (int)redirect.RouteValues["monthId"]!));
        Assert.False(redirect.RouteValues.ContainsKey("schemeId"));
    }

    private static string ErrorOf(DistributionController controller, string key) =>
        Assert.Single(controller.ModelState[key]!.Errors).ErrorMessage;

    // ---- GET ----

    [Fact]
    public async Task Get_ApiReturnsNull_ReturnsNotFound()
    {
        var result = await CreateController(new FakeDistributionApiClient()).ViewComments(2026, 5, SchemeId, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Get_MapsTheCommentsOntoTheViewModel()
    {
        var result = await CreateController(ApiClient()).ViewComments(2026, 5, SchemeId, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Null(view.ViewName);
        var model = Assert.IsType<ViewCommentsViewModel>(view.Model);
        Assert.Equal((2026, 5, SchemeId), (model.YearId, model.MonthId, model.MonthlyDistributionSchemeId));
        Assert.Equal(("D1a/S1 BT: Brucella", "Comments by Dr A", "<p>Stored</p>"), (model.DistributionTitle, model.AuthorText, model.Comments));
        Assert.True(model.CanSubmit);
        Assert.Equal([new CommentsAttachmentResponse(SavedId, "saved.pdf")], model.SavedAttachments);
        Assert.Equal([".pdf", ".docx"], model.AllowedFileTypes);
        Assert.Equal(("PDF, Word", "10MB"), (model.AllowedFileTypesText, model.MaxFileSizeText));
        Assert.Empty(model.StagedAttachments);
        Assert.Empty(model.RemovedAttachmentIds);
        Assert.False(model.IsAuditStep);
    }

    // ---- POST: cancel ----

    [Theory]
    [InlineData("cancel")]
    [InlineData("CANCEL")]
    public async Task Post_Cancel_DiscardsTheStagedUploadsAndReturnsToTabulations(string command)
    {
        var apiClient = ApiClient(Saved());
        var model = Posted();
        model.StagedAttachments.Add($"{OtherToken}|other.pdf");

        var result = await PostAsync(CreateController(apiClient), model, command);

        AssertRedirectToTabulations(result);
        Assert.Equal([Token, OtherToken], Assert.Single(apiClient.DiscardedStagedAttachments));
        Assert.Empty(apiClient.SavedComments);
    }

    [Fact]
    public async Task Post_CancelWithNothingStaged_DoesNotCallDiscard()
    {
        var apiClient = ApiClient(Saved());

        var result = await PostAsync(CreateController(apiClient), new ViewCommentsViewModel { RemovedAttachmentIds = [SavedId] }, "cancel");

        AssertRedirectToTabulations(result);
        Assert.Empty(apiClient.DiscardedStagedAttachments);
        Assert.Empty(apiClient.SavedComments);
    }

    [Fact]
    public async Task Post_CancelAudit_ReturnsToThePageKeepingTheUnsavedChanges()
    {
        var apiClient = ApiClient(Saved());
        var controller = CreateController(apiClient);
        controller.ModelState.AddModelError("AuditWhy", "stale");
        var model = Posted();
        model.IsAuditStep = true;
        model.AuditWhy = "Because";
        model.PendingCommand = "submit";
        model.AuditObjectName = AuditObject;

        var result = await PostAsync(controller, model, "cancelAudit");

        var shown = AssertRedisplayed(result);
        Assert.False(shown.IsAuditStep);
        Assert.Null(shown.AuditWhy);
        Assert.Null(shown.PendingCommand);
        Assert.Equal("<p>New</p>", shown.Comments);
        Assert.Equal([$"{Token}|new.pdf"], shown.StagedAttachments);
        Assert.Equal([SavedId], shown.RemovedAttachmentIds);
        Assert.True(controller.ModelState.IsValid);
        Assert.Empty(apiClient.SavedComments);
        Assert.Empty(apiClient.DiscardedStagedAttachments);
    }

    // ---- POST: validation ----

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Post_AuditStepWithoutAReason_RedisplaysTheAuditStepWithAnError(string? auditWhy)
    {
        var apiClient = ApiClient(Saved());
        var controller = CreateController(apiClient);
        var model = Posted();
        model.IsAuditStep = true;
        model.AuditWhy = auditWhy;
        model.PendingCommand = "save";

        var shown = AssertRedisplayed(await PostAsync(controller, model, "save"));

        Assert.True(shown.IsAuditStep);
        Assert.Equal("save", shown.PendingCommand);
        Assert.Equal("The reason for these changes must be specified", ErrorOf(controller, "AuditWhy"));
        Assert.Empty(apiClient.SavedComments);
    }

    [Fact]
    public async Task Post_InvalidModel_RedisplaysWithThePostedValuesAndDoesNotSave()
    {
        var apiClient = ApiClient(Saved());
        var controller = CreateController(apiClient);
        controller.ModelState.AddModelError("Comments", "Comments required.");

        var shown = AssertRedisplayed(await PostAsync(controller, Posted(comments: null), "save"));

        Assert.Null(shown.Comments);
        Assert.Equal([$"{Token}|new.pdf"], shown.StagedAttachments);
        Assert.Equal("D1a/S1 BT: Brucella", shown.DistributionTitle);
        Assert.Empty(apiClient.SavedComments);
    }

    [Fact]
    public async Task Post_RedisplayWhenTheCommentsHaveGone_ReturnsNotFound()
    {
        var apiClient = new FakeDistributionApiClient { TestConsultantComments = null };
        var controller = CreateController(apiClient);
        controller.ModelState.AddModelError("Comments", "Comments required.");

        Assert.IsType<NotFoundResult>(await PostAsync(controller, Posted(), "save"));
    }

    // ---- POST: save request ----

    [Theory]
    [InlineData("save", false)]
    [InlineData("SUBMIT", true)]
    [InlineData(null, false)]
    public async Task Post_SendsTheCommentsSubmitFlagStagedTokensAndRemovedIds(string? command, bool submit)
    {
        var apiClient = ApiClient(Saved());
        var model = Posted();
        model.StagedAttachments.Add("not-a-guid|bad.pdf");
        model.StagedAttachments.Add("no-separator");
        model.AuditWhy = "not an audit step";

        await PostAsync(CreateController(apiClient), model, command);

        var request = Assert.Single(apiClient.SavedComments);
        Assert.Equal("<p>New</p>", request.Comments);
        Assert.Equal(submit, request.Submit);
        Assert.Null(request.AuditWhy);
        Assert.Equal([Token], request.StagedAttachmentTokens!);
        Assert.Equal([SavedId], request.RemovedAttachmentIds!);
    }

    [Theory]
    [InlineData("submit", true)]
    [InlineData("save", false)]
    public async Task Post_AuditStep_UsesThePendingCommandAndSendsTheReason(string pendingCommand, bool submit)
    {
        var apiClient = ApiClient(Saved());
        var model = Posted();
        model.IsAuditStep = true;
        model.PendingCommand = pendingCommand;
        model.AuditWhy = "Typo";

        var result = await PostAsync(CreateController(apiClient), model, null);

        AssertRedirectToTabulations(result);
        var request = Assert.Single(apiClient.SavedComments);
        Assert.Equal(submit, request.Submit);
        Assert.Equal("Typo", request.AuditWhy);
    }

    [Theory]
    [InlineData("save", "Comments updated successfully.")]
    [InlineData("submit", "Comments submitted successfully.")]
    public async Task Post_SaveSucceeds_ShowsTheSuccessBanner(string command, string message)
    {
        var controller = CreateController(ApiClient(Saved()));

        AssertRedirectToTabulations(await PostAsync(controller, Posted(), command));

        var notification = controller.TempData.GetNotification();
        Assert.Equal(NotificationType.Success, notification!.Type);
        Assert.Equal(message, notification.Message);
    }

    [Fact]
    public async Task Post_AuditStepSaveSucceeds_ShowsTheSuccessBanner()
    {
        var controller = CreateController(ApiClient(Saved()));
        var model = Posted();
        model.IsAuditStep = true;
        model.PendingCommand = "save";
        model.AuditWhy = "Typo";

        AssertRedirectToTabulations(await PostAsync(controller, model, "save"));

        Assert.Equal("Comments updated successfully.", controller.TempData.GetNotification()!.Message);
    }

    [Fact]
    public async Task Post_AuditReasonRequiredOrErrors_ShowNoSuccessBanner()
    {
        var auditController = CreateController(ApiClient(AuditRequired()));
        AssertRedisplayed(await PostAsync(auditController, Posted(), "save"));
        Assert.Null(auditController.TempData.GetNotification());

        var errorController = CreateController(ApiClient(new TestConsultantCommentsSaveResult(false, false, string.Empty, new Dictionary<string, string> { ["Comments"] = "bad" })));
        AssertRedisplayed(await PostAsync(errorController, Posted(), "save"));
        Assert.Null(errorController.TempData.GetNotification());
    }

    [Fact]
    public async Task Post_SaveReturnsNull_ReturnsNotFound()
    {
        var apiClient = ApiClient(save: null);

        Assert.IsType<NotFoundResult>(await PostAsync(CreateController(apiClient), Posted(), "save"));
        Assert.Single(apiClient.SavedComments);
    }

    [Theory]
    [InlineData("save", "save")]
    [InlineData("submit", "submit")]
    [InlineData(null, "save")]
    public async Task Post_AuditReasonRequired_SwitchesToTheAuditStepWithThePendingCommand(string? command, string pending)
    {
        var apiClient = ApiClient(AuditRequired());
        var controller = CreateController(apiClient);

        var shown = AssertRedisplayed(await PostAsync(controller, Posted(), command));

        Assert.True(shown.IsAuditStep);
        Assert.Equal(AuditObject, shown.AuditObjectName);
        Assert.Equal(pending, shown.PendingCommand);
        Assert.Equal("<p>New</p>", shown.Comments);
        Assert.Equal([$"{Token}|new.pdf"], shown.StagedAttachments);
        Assert.Equal([SavedId], shown.RemovedAttachmentIds);
        Assert.True(controller.ModelState.IsValid);
    }

    [Fact]
    public async Task Post_SaveErrors_AreMappedToCommentsOrFile()
    {
        var apiClient = ApiClient(new TestConsultantCommentsSaveResult(false, false, string.Empty, new Dictionary<string, string>
        {
            ["Comments"] = "Comments are too long",
            ["File"] = "The attachment has expired",
        }));
        var controller = CreateController(apiClient);

        var shown = AssertRedisplayed(await PostAsync(controller, Posted(), "save"));

        Assert.False(shown.IsAuditStep);
        Assert.Equal(2, controller.ModelState.ErrorCount);
        Assert.Equal("Comments are too long", ErrorOf(controller, "Comments"));
        Assert.Equal("The attachment has expired", ErrorOf(controller, "File"));
    }

    [Fact]
    public async Task Post_SaveErrorUnderAnyOtherKey_IsShownAgainstTheFile()
    {
        var apiClient = ApiClient(new TestConsultantCommentsSaveResult(false, false, string.Empty, new Dictionary<string, string>
        {
            ["AuditWhy"] = "Reason too long",
        }));
        var controller = CreateController(apiClient);

        AssertRedisplayed(await PostAsync(controller, Posted(), "save"));

        Assert.Equal("Reason too long", ErrorOf(controller, "File"));
    }

    [Theory]
    [InlineData("save")]
    [InlineData("submit")]
    public async Task Post_Saved_ReturnsToTabulationsWithoutDiscarding(string command)
    {
        var apiClient = ApiClient(Saved());

        AssertRedirectToTabulations(await PostAsync(CreateController(apiClient), Posted(), command));
        Assert.Empty(apiClient.DiscardedStagedAttachments);
    }

    [Fact]
    public async Task Post_NullModel_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => PostAsync(CreateController(ApiClient()), null!, "save"));
    }

    // ---- Upload ----

    private static FormFile File(byte[] data, string name = "report.pdf") =>
        new(new MemoryStream(data), 0, data.Length, "file", name) { Headers = new HeaderDictionary(), ContentType = "application/pdf" };

    private static Task<IActionResult> UploadAsync(DistributionController controller, ViewCommentsViewModel model, IFormFile? file) =>
        controller.ViewCommentsUpload(2026, 5, SchemeId, model, file, CancellationToken.None);

    [Fact]
    public async Task Upload_SendsTheFileBytesAndTheUnsavedChanges_AndStagesTheReturnedToken()
    {
        var apiClient = ApiClient();
        apiClient.CommentsStageResult = new CommentsAttachmentStageResult(true, OtherToken, "report.pdf", new Dictionary<string, string>());
        var controller = CreateController(apiClient);
        controller.ModelState.AddModelError("Comments", "stale");

        var shown = AssertRedisplayed(await UploadAsync(controller, Posted(), File([1, 2, 3])));

        var request = Assert.Single(apiClient.StagedAttachmentRequests);
        Assert.Equal(("report.pdf", "application/pdf"), (request.FileName, request.ContentType));
        Assert.Equal([1, 2, 3], request.FileData!);
        Assert.Equal([Token], request.StagedAttachmentTokens!);
        Assert.Equal([SavedId], request.RemovedAttachmentIds!);
        Assert.Equal([$"{Token}|new.pdf", $"{OtherToken}|report.pdf"], shown.StagedAttachments);
        Assert.Equal("<p>New</p>", shown.Comments);
        Assert.True(controller.ModelState.IsValid);
    }

    [Fact]
    public async Task Upload_NoFile_SendsNullsAndShowsTheFileErrors()
    {
        var apiClient = ApiClient();
        apiClient.CommentsStageResult = new CommentsAttachmentStageResult(false, Guid.Empty, string.Empty, new Dictionary<string, string>
        {
            ["File"] = "Please select a file to upload",
        });
        var controller = CreateController(apiClient);

        var shown = AssertRedisplayed(await UploadAsync(controller, new ViewCommentsViewModel(), null));

        var request = Assert.Single(apiClient.StagedAttachmentRequests);
        Assert.Null(request.FileName);
        Assert.Null(request.ContentType);
        Assert.Null(request.FileData);
        Assert.Empty(shown.StagedAttachments);
        Assert.Equal("Please select a file to upload", ErrorOf(controller, "File"));
    }

    [Fact]
    public async Task Upload_EmptyFile_SendsNoData()
    {
        var apiClient = ApiClient();
        apiClient.CommentsStageResult = new CommentsAttachmentStageResult(false, Guid.Empty, string.Empty, new Dictionary<string, string> { ["File"] = "Empty" });

        await UploadAsync(CreateController(apiClient), new ViewCommentsViewModel(), File([]));

        var request = Assert.Single(apiClient.StagedAttachmentRequests);
        Assert.Equal("report.pdf", request.FileName);
        Assert.Null(request.FileData);
    }

    [Fact]
    public async Task Upload_ApiReturnsNull_ReturnsNotFound()
    {
        var apiClient = ApiClient();

        Assert.IsType<NotFoundResult>(await UploadAsync(CreateController(apiClient), Posted(), File([1])));
    }

    [Fact]
    public async Task Upload_NullModel_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => UploadAsync(CreateController(ApiClient()), null!, null));
    }

    // ---- Remove ----

    private static Task<IActionResult> RemoveAsync(DistributionController controller, Guid attachmentId, ViewCommentsViewModel model) =>
        controller.ViewCommentsRemoveAttachment(2026, 5, SchemeId, attachmentId, model, CancellationToken.None);

    [Fact]
    public async Task Remove_StagedUpload_IsDroppedFromTheListAndDiscarded()
    {
        var apiClient = ApiClient();
        var model = Posted();
        model.StagedAttachments.Add($"{OtherToken}|other.pdf");
        var controller = CreateController(apiClient);
        controller.ModelState.AddModelError("Comments", "stale");

        var shown = AssertRedisplayed(await RemoveAsync(controller, Token, model));

        Assert.Equal([$"{OtherToken}|other.pdf"], shown.StagedAttachments);
        Assert.Equal([SavedId], shown.RemovedAttachmentIds);
        Assert.Equal([Token], Assert.Single(apiClient.DiscardedStagedAttachments));
        Assert.True(controller.ModelState.IsValid);
    }

    [Fact]
    public async Task Remove_InvalidPostedField_RedisplaysTheFormWithoutChangingIt()
    {
        var apiClient = ApiClient();
        var model = Posted();
        model.StagedAttachments.Add($"{OtherToken}|other.pdf");
        var controller = CreateController(apiClient);
        controller.ModelState.AddModelError("AuditWhy", "too long");

        var shown = AssertRedisplayed(await RemoveAsync(controller, Token, model));

        Assert.Equal([$"{Token}|new.pdf", $"{OtherToken}|other.pdf"], shown.StagedAttachments);
        Assert.Empty(apiClient.DiscardedStagedAttachments);
        Assert.False(controller.ModelState.IsValid);
    }

    [Fact]
    public async Task Upload_InvalidPostedField_RedisplaysTheFormWithoutCallingTheApi()
    {
        var apiClient = ApiClient();
        var controller = CreateController(apiClient);
        controller.ModelState.AddModelError("AuditWhy", "too long");

        var shown = AssertRedisplayed(await UploadAsync(controller, Posted(), File([1, 2, 3])));

        Assert.Empty(apiClient.StagedAttachmentRequests);
        Assert.Equal([$"{Token}|new.pdf"], shown.StagedAttachments);
        Assert.False(controller.ModelState.IsValid);
    }

    [Fact]
    public async Task Remove_SavedAttachment_IsMarkedForRemovalOnce()
    {
        var apiClient = ApiClient();
        var model = Posted();

        var shown = AssertRedisplayed(await RemoveAsync(CreateController(apiClient), OtherSavedId, model));
        Assert.Equal([SavedId, OtherSavedId], shown.RemovedAttachmentIds);

        shown = AssertRedisplayed(await RemoveAsync(CreateController(apiClient), SavedId, model));
        Assert.Equal([SavedId, OtherSavedId], shown.RemovedAttachmentIds);
        Assert.Equal([$"{Token}|new.pdf"], shown.StagedAttachments);
        Assert.Empty(apiClient.DiscardedStagedAttachments);
    }

    [Fact]
    public async Task Remove_CommentsGone_ReturnsNotFound()
    {
        var apiClient = new FakeDistributionApiClient();

        Assert.IsType<NotFoundResult>(await RemoveAsync(CreateController(apiClient), SavedId, Posted()));
    }

    [Fact]
    public async Task Remove_NullModel_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => RemoveAsync(CreateController(ApiClient()), SavedId, null!));
    }

    // ---- Downloads ----

    private static TabulationAttachmentFile Download() => new([9, 8], "application/pdf", "attachment; filename=\"saved.pdf\"");

    [Fact]
    public async Task Attachment_Missing_ReturnsNotFound()
    {
        Assert.IsType<NotFoundResult>(await CreateController(ApiClient()).ViewCommentsAttachment(SchemeId, SavedId, CancellationToken.None));
    }

    [Fact]
    public async Task StagedAttachment_Missing_ReturnsNotFound()
    {
        Assert.IsType<NotFoundResult>(await CreateController(ApiClient()).ViewCommentsStagedAttachment(SchemeId, Token, CancellationToken.None));
    }

    [Fact]
    public async Task Attachment_ReturnsTheFileWithTheContentDisposition()
    {
        var apiClient = ApiClient();
        apiClient.CommentsAttachment = Download();
        var controller = CreateController(apiClient);

        var file = Assert.IsType<FileContentResult>(await controller.ViewCommentsAttachment(SchemeId, SavedId, CancellationToken.None));

        Assert.Equal([9, 8], file.FileContents);
        Assert.Equal("application/pdf", file.ContentType);
        Assert.Equal("attachment; filename=\"saved.pdf\"", controller.Response.Headers.ContentDisposition.ToString());
    }

    [Fact]
    public async Task StagedAttachment_ReturnsTheFileWithTheContentDisposition()
    {
        var apiClient = ApiClient();
        apiClient.StagedCommentsAttachment = Download() with { ContentDisposition = "inline; filename=\"new.pdf\"" };
        var controller = CreateController(apiClient);

        var file = Assert.IsType<FileContentResult>(await controller.ViewCommentsStagedAttachment(SchemeId, Token, CancellationToken.None));

        Assert.Equal([9, 8], file.FileContents);
        Assert.Equal("inline; filename=\"new.pdf\"", controller.Response.Headers.ContentDisposition.ToString());
    }
}
