using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using PTL.ApiClient.Distribution;
using PTL.Contracts.Distribution;
using PTL.InternalWeb.Authorization;
using PTL.InternalWeb.Confirmation;
using PTL.InternalWeb.Features.ResultsSignOff;
using PTL.InternalWeb.Tests.TestSupport;
using PTL.SharedUI.Notifications;

namespace PTL.InternalWeb.Tests.Features.ResultsSignOff;

public class ResultsSignOffControllerTests
{
    private static readonly Guid TabulationId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid TestId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid AttachmentId = Guid.Parse("44444444-4444-4444-4444-444444444444");

    private readonly FakeResultsSignOffApiClient _signOff = new();
    private readonly FakeDistributionApiClient _distribution = new();

    private ResultsSignOffController Controller()
    {
        var httpContext = new DefaultHttpContext();
        return new ResultsSignOffController(_signOff, _distribution)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext },
            TempData = new TempDataDictionary(httpContext, new FakeTempDataProvider()),
            Url = new FakeUrlHelper(),
        };
    }

    private static TabulationReportResponse Report() =>
        new(
            TabulationId, false, TabulationId, "FINAL REPORT for D1a/S1 BT: Brucella", "Summary", "Date Of Distribution: 03/05/2026",
            "", "", "", "Comments :", false, null, [], Guid.Empty, [], Guid.Empty, [], null, null,
            new TabulationPrintResponse([], [], []));

    [Fact]
    public async Task Index_ShowsTheQueueFromTheApi()
    {
        _signOff.Queue = new ResultsSignOffQueueResponse([new ResultsSignOffMonthResponse(2026, 6, [])]);

        var result = await Controller().Index(CancellationToken.None);

        var model = Assert.IsType<ResultsSignOffQueueViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Equal("June 2026", Assert.Single(model.Months).Title);
    }

    [Fact]
    public void Controller_IsOpenOnlyToTheResultsSignOffRole() =>
        Assert.Equal(PtlPolicies.ResultsSignOff, typeof(ResultsSignOffController).GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true).Cast<AuthorizeAttribute>().Single().Policy);

    // ---- Review ---------------------------------------------------------------------------------

    [Fact]
    public async Task Tabulation_UnknownTabulation_IsNotFound()
    {
        var result = await Controller().Tabulation(TabulationId, null, null, new ResultsSignOffDraw(), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
        Assert.Equal([(TabulationId, Guid.Empty, Guid.Empty, TestConsultantTabulationFilter.None)], _signOff.RequestedReviews);
    }

    [Fact]
    public async Task Tabulation_PassesTheParticipantAndViewerFiltersOnAndShowsTheReport()
    {
        var participant = Guid.NewGuid();
        var viewer = Guid.NewGuid();
        var review = new ResultsSignOffReviewResponse(Report(), false, ["26/1"]);
        _signOff.Review = review;

        var result = await Controller().Tabulation(TabulationId, participant, viewer, new ResultsSignOffDraw(), CancellationToken.None);

        var model = Assert.IsType<ResultsSignOffReviewViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Same(review.Report, model.Report);
        Assert.Equal(TabulationId, model.TabulationId);
        Assert.Equal([(TabulationId, participant, viewer)], _signOff.RequestedReviews.Select(r => (r.TabulationId, r.ParticipantId, r.ViewerId)));
    }

    [Fact]
    public async Task Tabulation_FirstVisit_TicksEveryColumnAndAsksForTheWholeTabulation()
    {
        _signOff.Review = new ResultsSignOffReviewResponse(Report(), true, ["26/1", "26/2"]);

        var result = await Controller().Tabulation(TabulationId, null, null, new ResultsSignOffDraw(), CancellationToken.None);

        var draw = Assert.IsType<ResultsSignOffReviewViewModel>(Assert.IsType<ViewResult>(result).Model).Draw;
        Assert.Equal(TestConsultantTabulationFilter.None, Assert.Single(_signOff.RequestedReviews).Filter);
        Assert.Equal(["26/1", "26/2"], draw.AllSamples);
        Assert.Equal(["26/1", "26/2"], draw.ShownSamples);
        Assert.True(draw.ShowMethodItems);
    }

    [Fact]
    public async Task Tabulation_Drawn_SendsTheUntickedColumnsAsTheFilterAndKeepsTheTicks()
    {
        _signOff.Review = new ResultsSignOffReviewResponse(Report(), true, ["26/1", "26/2"]);
        var draw = new ResultsSignOffDraw { IsFiltered = true, AllSamples = ["26/1", "26/2"], ShownSamples = ["26/1"], ShowMethodItems = false };

        var result = await Controller().Tabulation(TabulationId, null, null, draw, CancellationToken.None);

        var filter = Assert.Single(_signOff.RequestedReviews).Filter;
        Assert.Equal(["26/2"], filter.HiddenSamples);
        Assert.True(filter.HideMethodItems);
        var shown = Assert.IsType<ResultsSignOffReviewViewModel>(Assert.IsType<ViewResult>(result).Model).Draw;
        Assert.Equal(["26/1"], shown.ShownSamples);
        Assert.False(shown.ShowMethodItems);
    }

    [Fact]
    public async Task AssessmentResults_UnknownTest_IsNotFound()
    {
        var result = await Controller().AssessmentResults(TabulationId, TestId, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
        Assert.Equal([(TabulationId, TestId, false)], _distribution.RequestedAssessmentResults);
    }

    [Fact]
    public async Task AssessmentResults_ShowsTheDraftsResults()
    {
        var results = new AssessmentResultsResponse(true, ["Chart"], "Assessment Score", "Frequency", []);
        _distribution.AssessmentResults = results;

        var result = await Controller().AssessmentResults(TabulationId, TestId, CancellationToken.None);

        var model = Assert.IsType<ResultsSignOffAssessmentViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Same(results, model.Results);
        Assert.Equal(TabulationId, model.TabulationId);
    }

    [Fact]
    public async Task Attachment_UnknownAttachment_IsNotFound() =>
        Assert.IsType<NotFoundResult>(await Controller().Attachment(TabulationId, AttachmentId, CancellationToken.None));

    [Fact]
    public async Task Attachment_ReturnsTheFileWithItsDisposition()
    {
        _distribution.TabulationAttachment = new TabulationAttachmentFile([1, 2, 3], "application/pdf", "inline; filename=support.pdf");
        var controller = Controller();

        var result = await controller.Attachment(TabulationId, AttachmentId, CancellationToken.None);

        var file = Assert.IsType<FileContentResult>(result);
        Assert.Equal(("application/pdf", 3), (file.ContentType, file.FileContents.Length));
        Assert.Equal("inline; filename=support.pdf", controller.Response.Headers.ContentDisposition.ToString());
    }

    // ---- Reject ---------------------------------------------------------------------------------

    [Theory]
    [InlineData(null)]
    [InlineData(ConfirmationFlow.Cancelled)]
    public async Task Reject_NotYetConfirmed_AsksFirstAndRejectsNothing(string? confirmation)
    {
        var result = await Controller().Reject(TabulationId, confirmation, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal(ConfirmationFlow.ViewName, view.ViewName);
        var page = Assert.IsType<ConfirmationPageViewModel>(view.Model);
        Assert.Equal((ResultsSignOffController.RejectQuestion, ResultsSignOffController.RejectMessage, "Reject", true),
            (page.Title, page.Message, page.ConfirmText, page.IsDestructive));
        Assert.NotNull(page.CancelUrl);
        Assert.Empty(_signOff.Rejected);
    }

    [Fact]
    public async Task Reject_Confirmed_RejectsAndReturnsToTheQueueWithAConfirmation()
    {
        var controller = Controller();

        var result = await controller.Reject(TabulationId, ConfirmationFlow.Confirmed, CancellationToken.None);

        Assert.Equal(nameof(ResultsSignOffController.Index), Assert.IsType<RedirectToActionResult>(result).ActionName);
        Assert.Equal([TabulationId], _signOff.Rejected);
        var notification = controller.TempData.GetNotification();
        Assert.Equal((NotificationType.Success, ResultsSignOffController.RejectedNotification), (notification?.Type, notification?.Message));
    }

    [Fact]
    public async Task Reject_NoLongerAwaiting_ReturnsToTheQueueWithAnError()
    {
        _signOff.RejectStatus = ResultsSignOffApiStatus.NotAwaiting;
        var controller = Controller();

        var result = await controller.Reject(TabulationId, ConfirmationFlow.Confirmed, CancellationToken.None);

        Assert.IsType<RedirectToActionResult>(result);
        var notification = controller.TempData.GetNotification();
        Assert.Equal((NotificationType.Error, ResultsSignOffController.NoLongerAwaitingNotification), (notification?.Type, notification?.Message));
    }

    [Fact]
    public async Task Reject_UnknownTabulation_IsNotFound()
    {
        _signOff.RejectStatus = ResultsSignOffApiStatus.NotFound;

        Assert.IsType<NotFoundResult>(await Controller().Reject(TabulationId, ConfirmationFlow.Confirmed, CancellationToken.None));
    }

    // ---- Authorise ------------------------------------------------------------------------------

    [Theory]
    [InlineData(null)]
    [InlineData(ConfirmationFlow.Cancelled)]
    public async Task Authorise_NotYetConfirmed_AsksFirstAndAuthorisesNothing(string? confirmation)
    {
        var result = await Controller().Authorise(TabulationId, confirmation, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal(ConfirmationFlow.ViewName, view.ViewName);
        var page = Assert.IsType<ConfirmationPageViewModel>(view.Model);
        Assert.Equal((ResultsSignOffController.AuthoriseQuestion, ResultsSignOffController.AuthoriseMessage, "Authorise", false),
            (page.Title, page.Message, page.ConfirmText, page.IsDestructive));
        Assert.NotNull(page.CancelUrl);
        Assert.Empty(_signOff.Authorised);
    }

    [Fact]
    public async Task Authorise_Confirmed_AuthorisesAndReturnsToTheQueueWithAConfirmation()
    {
        var controller = Controller();

        var result = await controller.Authorise(TabulationId, ConfirmationFlow.Confirmed, CancellationToken.None);

        Assert.Equal(nameof(ResultsSignOffController.Index), Assert.IsType<RedirectToActionResult>(result).ActionName);
        Assert.Equal([TabulationId], _signOff.Authorised);
        var notification = controller.TempData.GetNotification();
        Assert.Equal((NotificationType.Success, ResultsSignOffController.AuthorisedNotification), (notification?.Type, notification?.Message));
    }

    [Theory]
    [InlineData(ResultsSignOffApiStatus.NotAwaiting, ResultsSignOffController.NoLongerAwaitingNotification)]
    [InlineData(ResultsSignOffApiStatus.NoSignatory, ResultsSignOffController.NoSignatoryNotification)]
    public async Task Authorise_Refused_ReturnsToTheQueueWithAnError(ResultsSignOffApiStatus status, string message)
    {
        _signOff.AuthoriseStatus = status;
        var controller = Controller();

        var result = await controller.Authorise(TabulationId, ConfirmationFlow.Confirmed, CancellationToken.None);

        Assert.IsType<RedirectToActionResult>(result);
        var notification = controller.TempData.GetNotification();
        Assert.Equal((NotificationType.Error, message), (notification?.Type, notification?.Message));
    }

    [Fact]
    public async Task Authorise_UnknownTabulation_IsNotFound()
    {
        _signOff.AuthoriseStatus = ResultsSignOffApiStatus.NotFound;

        Assert.IsType<NotFoundResult>(await Controller().Authorise(TabulationId, ConfirmationFlow.Confirmed, CancellationToken.None));
    }
}
