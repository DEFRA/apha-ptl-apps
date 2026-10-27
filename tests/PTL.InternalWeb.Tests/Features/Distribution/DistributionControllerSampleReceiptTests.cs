using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using PTL.Contracts.Distribution;
using PTL.InternalWeb.Features.Distribution;
using PTL.InternalWeb.Tests.TestSupport;
using PTL.SharedUI.Notifications;

namespace PTL.InternalWeb.Tests.Features.Distribution;

// Sample Receipt screen - legacy Internal User/SampleReceipt.aspx (sitemap title "Submission Status").
public class DistributionControllerSampleReceiptTests
{
    private const int Year = 2026;
    private const int Month = 5;

    private static readonly Guid SchemeId = Guid.Parse("a0000000-0000-0000-0000-0000000000a1");
    private static readonly Guid ParticipantId = Guid.Parse("d0000000-0000-0000-0000-0000000000d1");
    private static readonly Guid TestId = Guid.Parse("e0000000-0000-0000-0000-0000000000e1");
    private static readonly Guid Sample1 = Guid.Parse("50000000-0000-0000-0000-000000000001");
    private static readonly Guid Sample2 = Guid.Parse("50000000-0000-0000-0000-000000000002");

    private static DistributionController CreateController(FakeDistributionApiClient apiClient) =>
        new(apiClient, new FakeSearchApiClient(), new FakeLookupApiClient())
        {
            TempData = new TempDataDictionary(new DefaultHttpContext(), new FakeTempDataProvider()),
        };

    private static SampleReceiptResponse Page(bool readOnly = false) =>
        new("D1/S1 : BT Brucella", "Labcode L001: Lab One", readOnly,
        [
            new SampleReceiptTestResponse(TestId, "Culture",
            [
                new SampleReceiptSampleResponse(Sample1, "S001", true, false, false),
                new SampleReceiptSampleResponse(Sample2, "S002", false, false, false),
            ]),
        ]);

    private static SampleReceiptViewModel Posted() =>
        new()
        {
            Tests =
            [
                new SampleReceiptTestViewModel
                {
                    TestId = TestId,
                    Samples =
                    [
                        new SampleReceiptSampleViewModel { SampleReceiptId = Sample1, Submitted = true },
                        new SampleReceiptSampleViewModel { SampleReceiptId = Sample2, NotSubmitted = true },
                    ],
                },
            ],
        };

    private static Task<IActionResult> Post(DistributionController controller, string command) =>
        controller.SampleReceipt(Year, Month, SchemeId, ParticipantId, Posted(), command, CancellationToken.None);

    [Fact]
    public async Task Get_ApiReturnsNull_ReturnsNotFound()
    {
        var controller = CreateController(new FakeDistributionApiClient { SampleReceipt = null });

        Assert.IsType<NotFoundResult>(await controller.SampleReceipt(Year, Month, SchemeId, ParticipantId, CancellationToken.None));
    }

    [Fact]
    public async Task Get_MapsThePageOntoTheViewModel()
    {
        var controller = CreateController(new FakeDistributionApiClient { SampleReceipt = Page(readOnly: true) });

        var result = await controller.SampleReceipt(Year, Month, SchemeId, ParticipantId, CancellationToken.None);

        var model = Assert.IsType<SampleReceiptViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Equal((Year, Month, SchemeId, ParticipantId), (model.YearId, model.MonthId, model.MonthlyDistributionSchemeId, model.DistributionParticipantId));
        Assert.Equal("D1/S1 : BT Brucella", model.SchemeHeading);
        Assert.Equal("Labcode L001: Lab One", model.LabHeading);
        Assert.True(model.IsReadOnly);

        var test = Assert.Single(model.Tests);
        Assert.Equal((TestId, "Culture"), (test.TestId, test.TestType));
        Assert.Equal([Sample1, Sample2], test.Samples.Select(s => s.SampleReceiptId));
        Assert.Equal(["S001", "S002"], test.Samples.Select(s => s.SampleNumber));
        Assert.Equal((true, false, false), (test.Samples[0].Submitted, test.Samples[0].NotSubmitted, test.Samples[0].NotInRepertoire));
    }

    [Fact]
    public async Task Post_Cancel_ReturnsToResultsReceiptWithoutCallingTheApi()
    {
        var apiClient = new FakeDistributionApiClient();
        var controller = CreateController(apiClient);

        var redirect = Assert.IsType<RedirectToActionResult>(await Post(controller, "cancel"));

        Assert.Equal(nameof(DistributionController.ResultsReceipt), redirect.ActionName);
        Assert.Equal((Year, Month), (redirect.RouteValues!["year"], redirect.RouteValues["monthId"]));
        Assert.Empty(apiClient.SavedSampleReceiptRequests);
    }

    [Theory]
    [InlineData("save", nameof(DistributionController.ResultsReceipt))]
    [InlineData("apply", nameof(DistributionController.SampleReceipt))]
    public async Task Post_Saved_SendsTheTicksAndLandsWhereLegacyDoes(string command, string expectedAction)
    {
        var apiClient = new FakeDistributionApiClient { SampleReceiptSaveResult = new SampleReceiptSaveResult(true) };
        var controller = CreateController(apiClient);

        var redirect = Assert.IsType<RedirectToActionResult>(await Post(controller, command));

        Assert.Equal(expectedAction, redirect.ActionName);
        Assert.Equal(
            [new SampleReceiptSampleUpdateRequest(Sample1, true, false, false), new SampleReceiptSampleUpdateRequest(Sample2, false, true, false)],
            Assert.Single(apiClient.SavedSampleReceiptRequests).Samples);
        var notification = controller.TempData.GetNotification();
        Assert.Equal(NotificationType.Success, notification!.Type);
        Assert.Equal("Submission Status updated successfully.", notification.Message);
    }

    [Fact]
    public async Task Post_Apply_StaysOnSampleReceiptWithTheRouteValues()
    {
        var controller = CreateController(new FakeDistributionApiClient { SampleReceiptSaveResult = new SampleReceiptSaveResult(true) });

        var redirect = Assert.IsType<RedirectToActionResult>(await Post(controller, "apply"));

        Assert.Equal((Year, Month, SchemeId, ParticipantId),
            (redirect.RouteValues!["year"], redirect.RouteValues["monthId"], redirect.RouteValues["schemeId"], redirect.RouteValues["participantId"]));
    }

    [Theory]
    [InlineData(false, "Submission Status could not be saved. Please try again later.")]
    [InlineData(true, "Submission Status can no longer be changed.")]
    public async Task Post_NotSaved_ReturnsToTheScreenWithAnErrorNotification(bool readOnly, string message)
    {
        var controller = CreateController(new FakeDistributionApiClient { SampleReceiptSaveResult = new SampleReceiptSaveResult(false, readOnly) });

        var redirect = Assert.IsType<RedirectToActionResult>(await Post(controller, "save"));

        Assert.Equal(nameof(DistributionController.SampleReceipt), redirect.ActionName);
        var notification = controller.TempData.GetNotification();
        Assert.Equal(NotificationType.Error, notification!.Type);
        Assert.Equal(message, notification.Message);
    }

    [Fact]
    public async Task Post_ApiReturnsNull_ReturnsToTheScreenWithAnErrorNotification()
    {
        var controller = CreateController(new FakeDistributionApiClient { SampleReceiptSaveResult = null });

        var redirect = Assert.IsType<RedirectToActionResult>(await Post(controller, "save"));

        Assert.Equal(nameof(DistributionController.SampleReceipt), redirect.ActionName);
        Assert.Equal("Submission Status could not be saved. Please try again later.", controller.TempData.GetNotification()!.Message);
    }
}
