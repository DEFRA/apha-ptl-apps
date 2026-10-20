using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using PTL.Contracts.Distribution;
using PTL.InternalWeb.Features.Distribution;
using PTL.InternalWeb.Tests.TestSupport;
using PTL.SharedUI.Notifications;

namespace PTL.InternalWeb.Tests.Features.Distribution;

// Results Receipt screen - legacy Internal User/ResultsReceipt.aspx.
public class DistributionControllerResultsReceiptTests
{
    private const int Year = 2026;
    private const int Month = 5;

    private static readonly Guid SchemeId = Guid.Parse("a0000000-0000-0000-0000-0000000000a1");
    private static readonly Guid Dp1 = Guid.Parse("d0000000-0000-0000-0000-0000000000d1");
    private static readonly Guid Dp2 = Guid.Parse("d0000000-0000-0000-0000-0000000000d2");

    private static DistributionController CreateController(FakeDistributionApiClient apiClient) =>
        new(apiClient)
        {
            TempData = new TempDataDictionary(new DefaultHttpContext(), new FakeTempDataProvider()),
        };

    private static ResultsReceiptResponse Receipt() =>
        new(Guid.NewGuid(), Year, Month,
        [
            new ResultsReceiptSchemeResponse(SchemeId, "D1/S1: BT Brucella", true, 50,
            [
                new ResultsReceiptParticipantResponse(Dp1, "L001: Lab One", new DateTime(2026, 6, 30), new DateTime(2026, 5, 3), new DateTime(2026, 5, 4), true, true, true, new DateTime(2026, 5, 5)),
                new ResultsReceiptParticipantResponse(Dp2, "L002: Lab Two", new DateTime(2026, 7, 1), null, null, false, false, false, null),
            ]),
        ]);

    private static ResultsReceiptViewModel Posted(bool extra1 = false, bool returned2 = false) =>
        new()
        {
            Schemes =
            [
                new ResultsReceiptSchemeRowViewModel
                {
                    MonthlyDistributionSchemeId = SchemeId,
                    Participants =
                    [
                        new ResultsReceiptParticipantRowViewModel { DistributionParticipantId = Dp1, ExtraDocumentsReturned = extra1 },
                        new ResultsReceiptParticipantRowViewModel { DistributionParticipantId = Dp2, ReturnedChecked = returned2 },
                    ],
                },
            ],
        };

    [Fact]
    public async Task ResultsReceipt_Get_MapsResponseOntoViewModel()
    {
        var controller = CreateController(new FakeDistributionApiClient { ResultsReceipt = Receipt() });

        var result = await controller.ResultsReceipt(Year, Month, CancellationToken.None);

        var model = Assert.IsType<ResultsReceiptViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Equal((Year, Month, "May 2026"), (model.YearId, model.MonthId, model.MonthDescription));
        var scheme = Assert.Single(model.Schemes);
        Assert.Equal(SchemeId, scheme.MonthlyDistributionSchemeId);
        Assert.Equal("D1/S1: BT Brucella", scheme.Description);
        Assert.True(scheme.RequiresAssessment);
        Assert.Equal(50, scheme.ProgressPercentage);
        Assert.Equal(2, scheme.Participants.Count);

        var first = scheme.Participants[0];
        Assert.Equal(Dp1, first.DistributionParticipantId);
        Assert.Equal("L001: Lab One", first.FullName);
        Assert.Equal(new DateTime(2026, 6, 30), first.DeadlineDate);
        Assert.Equal(new DateTime(2026, 5, 3), first.FirstEditDate);
        Assert.Equal(new DateTime(2026, 5, 4), first.LastEditDate);
        Assert.True(first.ExtraDocumentsReturned);
        Assert.True(first.CanViewResults);
        Assert.True(first.HasUnaccountedSamples);
        Assert.Equal(new DateTime(2026, 5, 5), first.ReturnedDate);
        Assert.False(first.ReturnedChecked);

        Assert.False(scheme.Participants[1].CanViewResults);
        Assert.Null(scheme.Participants[1].FirstEditDate);
    }

    [Fact]
    public async Task ResultsReceipt_Get_ApiReturnsNull_ReturnsNotFound()
    {
        var controller = CreateController(new FakeDistributionApiClient { ResultsReceipt = null });

        Assert.IsType<NotFoundResult>(await controller.ResultsReceipt(Year, Month, CancellationToken.None));
    }

    [Fact]
    public async Task ResultsReceipt_Post_Cancel_RedirectsToIndexWithoutCallingTheApi()
    {
        var apiClient = new FakeDistributionApiClient { ResultsReceiptSaveResult = new ResultsReceiptSaveResult(true) };
        var controller = CreateController(apiClient);

        var result = await controller.ResultsReceipt(Year, Month, Posted(), "cancel", CancellationToken.None);

        Assert.Equal("Index", Assert.IsType<RedirectToActionResult>(result).ActionName);
        Assert.Empty(apiClient.SavedResultsReceiptRequests);
        Assert.Null(controller.TempData.GetNotification());
    }

    [Theory]
    [InlineData("save", "Index")]
    [InlineData("apply", "ResultsReceipt")]
    public async Task ResultsReceipt_Post_Success_SendsTicksShowsSuccessAndRedirects(string command, string expectedAction)
    {
        var apiClient = new FakeDistributionApiClient { ResultsReceiptSaveResult = new ResultsReceiptSaveResult(true) };
        var controller = CreateController(apiClient);

        var result = await controller.ResultsReceipt(Year, Month, Posted(extra1: true, returned2: true), command, CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(expectedAction, redirect.ActionName);
        if (expectedAction == "ResultsReceipt")
        {
            Assert.Equal(Year, redirect.RouteValues!["year"]);
            Assert.Equal(Month, redirect.RouteValues["monthId"]);
        }

        var request = Assert.Single(apiClient.SavedResultsReceiptRequests);
        Assert.Equal(
            [new ResultsReceiptParticipantUpdateRequest(Dp1, true, false), new ResultsReceiptParticipantUpdateRequest(Dp2, false, true)],
            request.Updates);
        var notification = controller.TempData.GetNotification();
        Assert.Equal(NotificationType.Success, notification!.Type);
        Assert.Equal("Results Receipt updated successfully.", notification.Message);
    }

    [Theory]
    [InlineData("save", true)]
    [InlineData("apply", true)]
    [InlineData("save", false)]
    public async Task ResultsReceipt_Post_Failure_ShowsErrorAndRedirectsBackToTheScreen(string command, bool nullResult)
    {
        var apiClient = new FakeDistributionApiClient
        {
            ResultsReceiptSaveResult = nullResult ? null : new ResultsReceiptSaveResult(false),
        };
        var controller = CreateController(apiClient);

        var result = await controller.ResultsReceipt(Year, Month, Posted(), command, CancellationToken.None);

        Assert.Equal("ResultsReceipt", Assert.IsType<RedirectToActionResult>(result).ActionName);
        var notification = controller.TempData.GetNotification();
        Assert.Equal(NotificationType.Error, notification!.Type);
        Assert.Equal("Results Receipt could not be saved. Please try again later.", notification.Message);
    }

    [Theory]
    [InlineData("Actual Results")]
    [InlineData("Sample Receipt")]
    public void ViewStage_AcceptsTheResultsReceiptParticipantLinkTitles(string stage)
    {
        var view = Assert.IsType<ViewResult>(CreateController(new FakeDistributionApiClient()).ViewStage(stage));

        Assert.Equal("FeatureNotAvailable", view.ViewName);
        Assert.Equal(stage, view.Model);
    }

    [Fact]
    public void ViewStage_UnknownStage_FallsBackToGenericTitle()
    {
        var view = Assert.IsType<ViewResult>(CreateController(new FakeDistributionApiClient()).ViewStage("Nonsense"));

        Assert.Equal("Distribution", view.Model);
    }
}
