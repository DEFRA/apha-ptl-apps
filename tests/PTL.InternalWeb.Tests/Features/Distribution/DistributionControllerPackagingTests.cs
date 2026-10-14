using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using PTL.Contracts.Distribution;
using PTL.InternalWeb.Features.Distribution;
using PTL.InternalWeb.Tests.TestSupport;
using PTL.SharedUI.Notifications;

namespace PTL.InternalWeb.Tests.Features.Distribution;

// Sample Packaging screen - legacy Internal User/PackagingSheet.aspx.
public class DistributionControllerPackagingTests
{
    private const int Year = 2026;
    private const int Month = 5;
    private const string DocxContentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

    private static readonly Guid Dp1 = Guid.Parse("d0000000-0000-0000-0000-0000000000d1");
    private static readonly Guid Dp2 = Guid.Parse("d0000000-0000-0000-0000-0000000000d2");
    private static readonly Guid Dp3 = Guid.Parse("d0000000-0000-0000-0000-0000000000d3");

    private static DistributionController CreateController(FakeDistributionApiClient apiClient) =>
        new(apiClient)
        {
            TempData = new TempDataDictionary(new DefaultHttpContext(), new FakeTempDataProvider()),
        };

    private static PackagingSheetResponse Sheet(int mode = 0, int week = 1) =>
        new(mode, week, Year, Month, []);

    private static PackagingViewModel ModelOf(
        int? mode = 0, int week = 1, IEnumerable<string>? packed = null, IEnumerable<string>? posted = null, IEnumerable<string>? printKeys = null) =>
        new()
        {
            Mode = mode,
            Week = week,
            PackedTicks = [.. packed ?? []],
            PostedTicks = [.. posted ?? []],
            PrintKeys = [.. printKeys ?? []],
        };

    private static PackagingViewModel ViewModelOf(IActionResult result) =>
        Assert.IsType<PackagingViewModel>(Assert.IsType<ViewResult>(result).Model);

    private static string Tick(int mode, int week, Guid id) => PackagingViewModel.TickKey(mode, week, id);

    // ---------- GET ----------

    [Fact]
    public async Task Packaging_Get_NoModeChosen_ShowsTheEmptyPageWithoutCallingTheApi()
    {
        var apiClient = new FakeDistributionApiClient { Packaging = Sheet() };
        var controller = CreateController(apiClient);

        var result = await controller.Packaging(Year, Month, mode: null, week: 0, CancellationToken.None);

        var model = ViewModelOf(result);
        Assert.Null(model.Sheet);
        Assert.Null(model.Mode);
        Assert.Equal(Year, model.YearId);
        Assert.Equal(Month, model.MonthId);
        Assert.Equal("May 2026", model.MonthDescription);
        Assert.Equal(string.Empty, model.PrintTitle);
        Assert.Empty(apiClient.PackagingRequests);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 0)]
    [InlineData(0, 5)]
    [InlineData(1, 5)]
    [InlineData(0, -1)]
    [InlineData(3, 1)]
    [InlineData(-1, 1)]
    public async Task Packaging_Get_InvalidModeOrWeek_ShowsTheEmptyPageWithoutCallingTheApi(int mode, int week)
    {
        var apiClient = new FakeDistributionApiClient { Packaging = Sheet() };
        var controller = CreateController(apiClient);

        var result = await controller.Packaging(Year, Month, mode, week, CancellationToken.None);

        Assert.Null(ViewModelOf(result).Sheet);
        Assert.Empty(apiClient.PackagingRequests);
    }

    [Theory]
    [InlineData(0, 1, 1)]
    [InlineData(0, 4, 4)]
    [InlineData(1, 2, 2)]
    [InlineData(1, 4, 4)]
    [InlineData(2, 0, 0)]
    [InlineData(2, 3, 0)]
    public async Task Packaging_Get_ValidSelection_FetchesTheSheetAndDailyAlwaysSendsWeekZero(int mode, int week, int expectedWeek)
    {
        var sheet = Sheet(mode, expectedWeek);
        var apiClient = new FakeDistributionApiClient { Packaging = sheet };
        var controller = CreateController(apiClient);

        var result = await controller.Packaging(Year, Month, mode, week, CancellationToken.None);

        var model = ViewModelOf(result);
        Assert.Same(sheet, model.Sheet);
        Assert.Equal(mode, model.Mode);
        Assert.Equal(week, model.Week);
        Assert.Equal((Year, Month, mode, expectedWeek), Assert.Single(apiClient.PackagingRequests));
    }

    [Fact]
    public async Task Packaging_Get_ApiReturnsNoSheet_ReturnsNotFound()
    {
        var controller = CreateController(new FakeDistributionApiClient { Packaging = null });

        var result = await controller.Packaging(Year, Month, mode: 0, week: 1, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    // ---------- print title and month description ----------

    [Theory]
    [InlineData(0, 2, "Week 2 - May 2026 - Group Packaging")]
    [InlineData(1, 1, "Week 1 - May 2026 - Participant Combined Packaging")]
    [InlineData(2, 0, "May 2026 - Weekly")]
    [InlineData(2, 3, "May 2026 - Weekly")]
    [InlineData(0, 0, "")]
    [InlineData(1, 0, "")]
    [InlineData(3, 1, "")]
    public async Task Packaging_Get_PrintTitleDescribesTheChosenOption(int mode, int week, string expected)
    {
        var controller = CreateController(new FakeDistributionApiClient { Packaging = Sheet(mode, week) });

        var result = await controller.Packaging(Year, Month, mode, week, CancellationToken.None);

        Assert.Equal(expected, ViewModelOf(result).PrintTitle);
    }

    [Fact]
    public async Task Packaging_Get_NoModeHasNoPrintTitle()
    {
        var controller = CreateController(new FakeDistributionApiClient());

        var result = await controller.Packaging(Year, Month, mode: null, week: 2, CancellationToken.None);

        Assert.Equal(string.Empty, ViewModelOf(result).PrintTitle);
    }

    [Theory]
    [InlineData(2025, 9, "September 2025")]
    [InlineData(2026, 1, "January 2026")]
    [InlineData(2026, 12, "December 2026")]
    public async Task Packaging_Get_MonthDescriptionIsTheFullUkMonthNameAndYear(int year, int monthId, string expected)
    {
        var controller = CreateController(new FakeDistributionApiClient());

        var result = await controller.Packaging(year, monthId, mode: null, week: 0, CancellationToken.None);

        Assert.Equal(expected, ViewModelOf(result).MonthDescription);
    }

    // ---------- POST: cancel / select ----------

    [Theory]
    [InlineData("cancel")]
    [InlineData("CANCEL")]
    public async Task Packaging_Post_Cancel_RedirectsToIndexWithoutCallingTheApi(string command)
    {
        var apiClient = new FakeDistributionApiClient();
        var controller = CreateController(apiClient);

        var result = await controller.Packaging(Year, Month, ModelOf(packed: [Tick(0, 1, Dp1)]), command, CancellationToken.None);

        Assert.Equal(nameof(DistributionController.Index), Assert.IsType<RedirectToActionResult>(result).ActionName);
        Assert.Empty(apiClient.SavedPackagingRequests);
        Assert.Empty(apiClient.PackagingRequests);
        Assert.Null(controller.TempData.GetNotification());
    }

    [Fact]
    public async Task Packaging_Post_Select_RedisplaysTheChosenSheetKeepingUnsavedTicks()
    {
        var apiClient = new FakeDistributionApiClient { Packaging = Sheet(1, 2) };
        var controller = CreateController(apiClient);
        var model = ModelOf(1, 2, packed: [Tick(1, 2, Dp1)], posted: [Tick(0, 1, Dp2)], printKeys: ["key1"]);

        var result = await controller.Packaging(Year, Month, model, "select", CancellationToken.None);

        var rebuilt = ViewModelOf(result);
        Assert.Same(model, rebuilt);
        Assert.Same(apiClient.Packaging, rebuilt.Sheet);
        Assert.Equal([Tick(1, 2, Dp1)], rebuilt.PackedTicks);
        Assert.Equal([Tick(0, 1, Dp2)], rebuilt.PostedTicks);
        Assert.Equal(["key1"], rebuilt.PrintKeys);
        Assert.Equal("May 2026", rebuilt.MonthDescription);
        Assert.Equal((Year, Month, 1, 2), Assert.Single(apiClient.PackagingRequests));
        Assert.Empty(apiClient.SavedPackagingRequests);
    }

    [Fact]
    public async Task Packaging_Post_Select_NoModeChosen_RedisplaysWithoutASheet()
    {
        var apiClient = new FakeDistributionApiClient { Packaging = Sheet() };
        var controller = CreateController(apiClient);

        var result = await controller.Packaging(Year, Month, ModelOf(mode: null, week: 0), "select", CancellationToken.None);

        Assert.Null(ViewModelOf(result).Sheet);
        Assert.Empty(apiClient.PackagingRequests);
    }

    [Fact]
    public async Task Packaging_Post_Select_ApiReturnsNoSheet_ReturnsNotFound()
    {
        var controller = CreateController(new FakeDistributionApiClient { Packaging = null });

        var result = await controller.Packaging(Year, Month, ModelOf(0, 1), "select", CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Packaging_Post_UnknownOrMissingCommand_RedisplaysLikeSelect()
    {
        var apiClient = new FakeDistributionApiClient { Packaging = Sheet() };
        var controller = CreateController(apiClient);

        var unknown = await controller.Packaging(Year, Month, ModelOf(0, 1), "something-else", CancellationToken.None);
        var missing = await controller.Packaging(Year, Month, ModelOf(0, 1), null!, CancellationToken.None);

        Assert.Same(apiClient.Packaging, ViewModelOf(unknown).Sheet);
        Assert.Same(apiClient.Packaging, ViewModelOf(missing).Sheet);
        Assert.Empty(apiClient.SavedPackagingRequests);
        Assert.Empty(apiClient.PackagingLabelRequests);
        Assert.Empty(apiClient.PackagingCustomsRequests);
    }

    // ---------- POST: save / apply ----------

    [Fact]
    public async Task Packaging_Post_Save_PackedTickAloneSendsAPackedOnlyUpdate()
    {
        var apiClient = new FakeDistributionApiClient { PackagingSaveResult = new PackagingSaveResult(true) };
        var controller = CreateController(apiClient);

        await controller.Packaging(Year, Month, ModelOf(packed: [Tick(0, 1, Dp1)]), "save", CancellationToken.None);

        var update = Assert.Single(Assert.Single(apiClient.SavedPackagingRequests).Updates);
        Assert.Equal(new PackagingStageUpdateRequest(0, 1, Dp1, Packed: true, Posted: false), update);
    }

    [Fact]
    public async Task Packaging_Post_Save_PostedTickAloneSendsAPostedOnlyUpdate()
    {
        var apiClient = new FakeDistributionApiClient { PackagingSaveResult = new PackagingSaveResult(true) };
        var controller = CreateController(apiClient);

        await controller.Packaging(Year, Month, ModelOf(posted: [Tick(1, 2, Dp2)]), "save", CancellationToken.None);

        var update = Assert.Single(Assert.Single(apiClient.SavedPackagingRequests).Updates);
        Assert.Equal(new PackagingStageUpdateRequest(1, 2, Dp2, Packed: false, Posted: true), update);
    }

    [Fact]
    public async Task Packaging_Post_Save_PackedAndPostedTicksForTheSameRowAreMerged()
    {
        var apiClient = new FakeDistributionApiClient { PackagingSaveResult = new PackagingSaveResult(true) };
        var controller = CreateController(apiClient);
        var model = ModelOf(packed: [Tick(2, 0, Dp3)], posted: [Tick(2, 0, Dp3)]);

        await controller.Packaging(Year, Month, model, "save", CancellationToken.None);

        var update = Assert.Single(Assert.Single(apiClient.SavedPackagingRequests).Updates);
        Assert.Equal(new PackagingStageUpdateRequest(2, 0, Dp3, Packed: true, Posted: true), update);
    }

    [Fact]
    public async Task Packaging_Post_Save_TicksFromSeveralSheetsAreAllSentAndKeepTheirOwnModeAndWeek()
    {
        var apiClient = new FakeDistributionApiClient { PackagingSaveResult = new PackagingSaveResult(true) };
        var controller = CreateController(apiClient);
        var model = ModelOf(
            packed: [Tick(0, 1, Dp1), Tick(1, 3, Dp2), Tick(0, 1, Dp1)],
            posted: [Tick(1, 3, Dp2), Tick(2, 0, Dp3)]);

        await controller.Packaging(Year, Month, model, "save", CancellationToken.None);

        var updates = Assert.Single(apiClient.SavedPackagingRequests).Updates;
        Assert.Equal(3, updates.Count);
        Assert.Contains(new PackagingStageUpdateRequest(0, 1, Dp1, true, false), updates);
        Assert.Contains(new PackagingStageUpdateRequest(1, 3, Dp2, true, true), updates);
        Assert.Contains(new PackagingStageUpdateRequest(2, 0, Dp3, false, true), updates);
    }

    [Fact]
    public async Task Packaging_Post_Save_MalformedTicksAreIgnored()
    {
        var apiClient = new FakeDistributionApiClient { PackagingSaveResult = new PackagingSaveResult(true) };
        var controller = CreateController(apiClient);
        string[] malformed =
        [
            string.Empty,
            "garbage",
            "0:1",
            $"0:1:{Dp2}:extra",
            $"x:1:{Dp2}",
            $"0:y:{Dp2}",
            "0:1:not-a-guid",
            $"-1:1:{Dp2}",
            $"0:-1:{Dp2}",
            $"+0:1:{Dp2}",
        ];
        var model = ModelOf(packed: [.. malformed, Tick(0, 1, Dp1)], posted: malformed);

        await controller.Packaging(Year, Month, model, "save", CancellationToken.None);

        var update = Assert.Single(Assert.Single(apiClient.SavedPackagingRequests).Updates);
        Assert.Equal(new PackagingStageUpdateRequest(0, 1, Dp1, true, false), update);
    }

    [Fact]
    public async Task Packaging_Post_Save_NoTicksStillCallsTheApiWithAnEmptyList()
    {
        var apiClient = new FakeDistributionApiClient { PackagingSaveResult = new PackagingSaveResult(true) };
        var controller = CreateController(apiClient);

        await controller.Packaging(Year, Month, ModelOf(), "save", CancellationToken.None);

        Assert.Empty(Assert.Single(apiClient.SavedPackagingRequests).Updates);
    }

    [Theory]
    [InlineData("save")]
    [InlineData("SAVE")]
    public async Task Packaging_Post_Save_SuccessShowsNotificationAndLeavesForTheDashboard(string command)
    {
        var apiClient = new FakeDistributionApiClient { PackagingSaveResult = new PackagingSaveResult(true) };
        var controller = CreateController(apiClient);

        var result = await controller.Packaging(Year, Month, ModelOf(packed: [Tick(0, 1, Dp1)]), command, CancellationToken.None);

        Assert.Equal(nameof(DistributionController.Index), Assert.IsType<RedirectToActionResult>(result).ActionName);
        var notification = controller.TempData.GetNotification();
        Assert.Equal(NotificationType.Success, notification!.Type);
        Assert.Equal("Sample Packaging updated successfully.", notification.Message);
    }

    [Fact]
    public async Task Packaging_Post_Save_EmailFailureShowsAWarningNamingTheRecipientsButStillLeaves()
    {
        var apiClient = new FakeDistributionApiClient
        {
            PackagingSaveResult = new PackagingSaveResult(true, ["lab@example.test (17152A/BA)", "other@example.test (17152A/BA)"]),
        };
        var controller = CreateController(apiClient);

        var result = await controller.Packaging(Year, Month, ModelOf(posted: [Tick(0, 1, Dp1)]), "save", CancellationToken.None);

        Assert.Equal(nameof(DistributionController.Index), Assert.IsType<RedirectToActionResult>(result).ActionName);
        var notification = controller.TempData.GetNotification();
        Assert.Equal(NotificationType.Warning, notification!.Type);
        Assert.Contains("could not be sent to: lab@example.test (17152A/BA), other@example.test (17152A/BA)", notification.Message);
    }

    [Theory]
    [InlineData("apply")]
    [InlineData("APPLY")]
    public async Task Packaging_Post_Apply_SuccessStaysOnThePackagingScreenForTheSameModeAndWeek(string command)
    {
        var apiClient = new FakeDistributionApiClient { PackagingSaveResult = new PackagingSaveResult(true) };
        var controller = CreateController(apiClient);

        var result = await controller.Packaging(Year, Month, ModelOf(1, 3), command, CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(DistributionController.Packaging), redirect.ActionName);
        Assert.Equal(Year, redirect.RouteValues!["year"]);
        Assert.Equal(Month, redirect.RouteValues["monthId"]);
        Assert.Equal(1, redirect.RouteValues["mode"]);
        Assert.Equal(3, redirect.RouteValues["week"]);
        var notification = controller.TempData.GetNotification();
        Assert.Equal(NotificationType.Success, notification!.Type);
        Assert.Equal("Sample Packaging updated successfully.", notification.Message);
    }

    [Fact]
    public async Task Packaging_Post_Apply_WithNoModeChosenRedirectsWithANullMode()
    {
        var apiClient = new FakeDistributionApiClient { PackagingSaveResult = new PackagingSaveResult(true) };
        var controller = CreateController(apiClient);

        var result = await controller.Packaging(Year, Month, ModelOf(mode: null, week: 0), "apply", CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(DistributionController.Packaging), redirect.ActionName);
        Assert.Null(redirect.RouteValues!["mode"]);
    }

    [Theory]
    [InlineData("save")]
    [InlineData("apply")]
    public async Task Packaging_Post_SaveOrApply_ResultNotSuccessful_ShowsErrorAndRedirectsBackToPackaging(string command)
    {
        var apiClient = new FakeDistributionApiClient { PackagingSaveResult = new PackagingSaveResult(false) };
        var controller = CreateController(apiClient);

        var result = await controller.Packaging(Year, Month, ModelOf(2, 0), command, CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(DistributionController.Packaging), redirect.ActionName);
        Assert.Equal(2, redirect.RouteValues!["mode"]);
        Assert.Equal(0, redirect.RouteValues["week"]);
        var notification = controller.TempData.GetNotification();
        Assert.Equal(NotificationType.Error, notification!.Type);
        Assert.Equal("Sample Packaging could not be saved. Please try again later.", notification.Message);
    }

    [Theory]
    [InlineData("save")]
    [InlineData("apply")]
    public async Task Packaging_Post_SaveOrApply_NullResult_ShowsErrorAndRedirectsBackToPackaging(string command)
    {
        var apiClient = new FakeDistributionApiClient { PackagingSaveResult = null };
        var controller = CreateController(apiClient);

        var result = await controller.Packaging(Year, Month, ModelOf(0, 4), command, CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(DistributionController.Packaging), redirect.ActionName);
        Assert.Equal(0, redirect.RouteValues!["mode"]);
        Assert.Equal(4, redirect.RouteValues["week"]);
        var notification = controller.TempData.GetNotification();
        Assert.Equal(NotificationType.Error, notification!.Type);
        Assert.Equal("Sample Packaging could not be saved. Please try again later.", notification.Message);
    }

    // ---------- POST: labels ----------

    [Fact]
    public async Task Packaging_Post_Labels_WithLabels_ShowsThePrintableLabelsView()
    {
        var labels = new PackagingLabelsResponse([new PackagingLabelResponse(["A Contact", "An Org"]), new PackagingLabelResponse(["Another"])]);
        var apiClient = new FakeDistributionApiClient { PackagingLabels = labels, Packaging = Sheet() };
        var controller = CreateController(apiClient);

        var result = await controller.Packaging(Year, Month, ModelOf(0, 2, printKeys: ["key1", "key2"]), "labels", CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal("PackagingLabels", view.ViewName);
        var model = Assert.IsType<PackagingLabelsViewModel>(view.Model);
        Assert.Equal(Year, model.YearId);
        Assert.Equal(Month, model.MonthId);
        Assert.Equal(0, model.Mode);
        Assert.Equal(2, model.Week);
        Assert.Equal("May 2026", model.MonthDescription);
        Assert.Same(labels.Labels, model.Labels);
        var request = Assert.Single(apiClient.PackagingLabelRequests);
        string[] expectedKeys = ["key1", "key2"];
        Assert.Equal(expectedKeys, request.SelectedKeys);
        Assert.Equal(0, request.Mode);
        Assert.Equal(2, request.Week);
        Assert.Empty(apiClient.PackagingRequests);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Packaging_Post_Labels_NoLabels_ReDisplaysTheSheetWithTheNoLabelsMessage(bool apiReturnsEmptyList)
    {
        var apiClient = new FakeDistributionApiClient
        {
            PackagingLabels = apiReturnsEmptyList ? new PackagingLabelsResponse([]) : null,
            Packaging = Sheet(1, 2),
        };
        var controller = CreateController(apiClient);

        var result = await controller.Packaging(Year, Month, ModelOf(1, 2, printKeys: ["key1"]), "labels", CancellationToken.None);

        var model = ViewModelOf(result);
        Assert.Equal("No Labels to Print", model.Message);
        Assert.Null(model.MessageDetail);
        Assert.Same(apiClient.Packaging, model.Sheet);
        Assert.Single(apiClient.PackagingLabelRequests);
    }

    [Fact]
    public async Task Packaging_Post_Labels_NoModeChosen_ShowsTheNoLabelsMessageWithoutCallingTheLabelsApi()
    {
        var apiClient = new FakeDistributionApiClient { PackagingLabels = new PackagingLabelsResponse([new PackagingLabelResponse(["x"])]) };
        var controller = CreateController(apiClient);

        var result = await controller.Packaging(Year, Month, ModelOf(mode: null, week: 0), "labels", CancellationToken.None);

        var model = ViewModelOf(result);
        Assert.Equal("No Labels to Print", model.Message);
        Assert.Null(model.Sheet);
        Assert.Empty(apiClient.PackagingLabelRequests);
    }

    [Fact]
    public async Task Packaging_Post_Labels_NoLabelsAndSheetGone_ReturnsNotFound()
    {
        var controller = CreateController(new FakeDistributionApiClient { PackagingLabels = null, Packaging = null });

        var result = await controller.Packaging(Year, Month, ModelOf(0, 1), "labels", CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    // ---------- POST: customs ----------

    [Fact]
    public async Task Packaging_Post_Customs_Success_ReturnsTheWordDocumentForDownload()
    {
        var apiClient = new FakeDistributionApiClient
        {
            PackagingCustoms = new PackagingCustomsResult([1, 2, 3], "Daily packaging May 2026.docx", null),
        };
        var controller = CreateController(apiClient);

        var result = await controller.Packaging(Year, Month, ModelOf(2, 0, printKeys: ["key1"]), "customs", CancellationToken.None);

        var file = Assert.IsType<FileContentResult>(result);
        Assert.Equal(DocxContentType, file.ContentType);
        Assert.Equal("Daily packaging May 2026.docx", file.FileDownloadName);
        Assert.Equal([1, 2, 3], file.FileContents);
        var request = Assert.Single(apiClient.PackagingCustomsRequests);
        Assert.Equal(2, request.Mode);
        Assert.Equal(0, request.Week);
        Assert.Equal(["key1"], request.SelectedKeys);
        Assert.Empty(apiClient.PackagingRequests);
    }

    [Fact]
    public async Task Packaging_Post_Customs_Failure_ReDisplaysTheSheetWithTheMailMergeMessageAndDetail()
    {
        var apiClient = new FakeDistributionApiClient
        {
            PackagingCustoms = new PackagingCustomsResult(null, string.Empty, "Please select schemes to print"),
            Packaging = Sheet(2, 0),
        };
        var controller = CreateController(apiClient);

        var result = await controller.Packaging(Year, Month, ModelOf(2, 0), "customs", CancellationToken.None);

        var model = ViewModelOf(result);
        Assert.Equal("There was a problem with the Mail Merge", model.Message);
        Assert.Equal("Please select schemes to print", model.MessageDetail);
        Assert.Same(apiClient.Packaging, model.Sheet);
    }

    [Fact]
    public async Task Packaging_Post_Customs_NoModeChosen_DoesNothingAndRedisplays()
    {
        var apiClient = new FakeDistributionApiClient();
        var controller = CreateController(apiClient);

        var result = await controller.Packaging(Year, Month, ModelOf(mode: null, week: 0), "customs", CancellationToken.None);

        var model = ViewModelOf(result);
        Assert.Null(model.Message);
        Assert.Null(model.Sheet);
        Assert.Empty(apiClient.PackagingCustomsRequests);
    }
}
