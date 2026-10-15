using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using PTL.Contracts.Distribution;
using PTL.InternalWeb.Features.Distribution;
using PTL.InternalWeb.Navigation;
using PTL.InternalWeb.Tests.TestSupport;
using PTL.SharedUI.Notifications;

namespace PTL.InternalWeb.Tests.Features.Distribution;

// Tabulations Send actions staged until Save - legacy Scheduling/Tabulations.aspx GridViewMDS_RowCommand.
public class DistributionControllerTabulationSendTests
{
    private static readonly Guid SchemeA = Guid.Parse("aaaaaaaa-0000-0000-0000-0000000000f1");
    private static readonly Guid SchemeB = Guid.Parse("aaaaaaaa-0000-0000-0000-0000000000f2");

    private static DistributionController CreateController(FakeDistributionApiClient apiClient) =>
        new(apiClient) { TempData = new TempDataDictionary(new DefaultHttpContext(), new FakeTempDataProvider()) };

    private static TabulationsSchemeResponse Scheme(Guid id) =>
        new(
            id, "BT: Brucella", "D1a/S1", new DateTime(2026, 5, 20), new DateTime(2026, 6, 30), false,
            ["A"],
            [],
            new TabulationActionsResponse(
                new TabulationActionResponse("Amend Results", true, string.Empty, "None"),
                new TabulationActionResponse("Send To Test Consultant", true, string.Empty, "SendToTestConsultant"),
                new TabulationActionResponse("Send For Assessment", true, string.Empty, "SendForAssessment"),
                new TabulationActionResponse("Send For Results Sign-Off", true, string.Empty, "SendToResultsSignOff")));

    private static TabulationSendResponse Send(string question, string command = "SendToTestConsultant") =>
        new(SchemeA, "BT: Brucella", "D1a/S1", command, "Send To Test Consultant", question);

    private static FakeDistributionApiClient ApiClient(TabulationSendResponse? stage = null) =>
        new() { TabulationsPreview = new TabulationsResponse(2026, 5, [Scheme(SchemeA)]), TabulationStage = stage };

    private static string Value(Guid scheme, string command) => $"{scheme}:{command}";

    private static Task<IActionResult> PostAsync(
        DistributionController controller, string? command = null, string? stage = null, bool confirmed = false, string[]? staged = null) =>
        controller.Tabulations(2026, 5, command, stage, confirmed, staged, CancellationToken.None);

    private static TabulationsViewModel AssertTabulationsView(IActionResult result)
    {
        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal("Tabulations", view.ViewName);
        return Assert.IsType<TabulationsViewModel>(view.Model);
    }

    // ---- Cancel / Save ----

    [Theory]
    [InlineData("cancel")]
    [InlineData("CANCEL")]
    public async Task Post_Cancel_RedirectsToTheDistributionsMenuWithoutSaving(string command)
    {
        var apiClient = ApiClient();

        var result = await PostAsync(CreateController(apiClient), command, staged: [Value(SchemeA, "SendToTestConsultant")]);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);
        Assert.Equal(2026, redirect.RouteValues!["year"]);
        Assert.False(redirect.RouteValues.ContainsKey("monthId"));
        Assert.Empty(apiClient.SavedTabulations);
        Assert.Empty(apiClient.PreviewedTabulations);
    }

    [Theory]
    [InlineData("save")]
    [InlineData("SAVE")]
    public async Task Post_SaveSucceeds_SavesEveryStagedSendAndRedirectsToTheDistributionsMenu(string command)
    {
        var apiClient = ApiClient();
        var controller = CreateController(apiClient);

        var result = await PostAsync(controller, command, staged: [Value(SchemeA, "SendToTestConsultant"), Value(SchemeB, "SendToResultsSignOff")]);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);
        Assert.Equal(2026, redirect.RouteValues!["year"]);
        Assert.Equal(
            [new TabulationStagedSendRequest(SchemeA, "SendToTestConsultant"), new TabulationStagedSendRequest(SchemeB, "SendToResultsSignOff")],
            Assert.Single(apiClient.SavedTabulations));
        Assert.Null(controller.TempData.GetNotification());
    }

    [Fact]
    public async Task Post_SaveFails_ShowsAnErrorNotificationAndRedirectsBackToTabulations()
    {
        var apiClient = ApiClient();
        apiClient.SaveTabulationsResult = false;
        var controller = CreateController(apiClient);

        var result = await PostAsync(controller, "save", staged: [Value(SchemeA, "SendToTestConsultant")]);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Tabulations", redirect.ActionName);
        Assert.Equal((2026, 5), ((int)redirect.RouteValues!["year"]!, (int)redirect.RouteValues["monthId"]!));
        var notification = controller.TempData.GetNotification();
        Assert.Equal(NotificationType.Error, notification!.Type);
        Assert.Equal(
            "The changes could not be saved because the Tabulations have been changed by someone else. Please try again.",
            notification.Message);
    }

    // ---- Stage ----

    [Fact]
    public async Task Post_StageWithAQuestionNotYetConfirmed_ShowsTheConfirmationWithTheEarlierStagedSends()
    {
        var send = Send("Are you sure?");
        var apiClient = ApiClient(send);
        var controller = CreateController(apiClient);

        var result = await PostAsync(controller, stage: Value(SchemeA, "SendToTestConsultant"), staged: [Value(SchemeB, "SendToResultsSignOff")]);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal("SendTabulation", view.ViewName);
        var model = Assert.IsType<TabulationSendViewModel>(view.Model);
        Assert.Equal((2026, 5), (model.YearId, model.MonthId));
        Assert.Same(send, model.Send);
        Assert.Equal([new TabulationStagedSendRequest(SchemeB, "SendToResultsSignOff")], model.Staged);
        Assert.Equal("Tabulations", controller.ViewData[BreadcrumbRouteValues.Action]);
        Assert.Equal(
            new TabulationStageRequest([new TabulationStagedSendRequest(SchemeB, "SendToResultsSignOff")], SchemeA, "SendToTestConsultant"),
            Assert.Single(apiClient.StagedTabulationSends),
            new StageRequestComparer());
        Assert.Empty(apiClient.PreviewedTabulations);
    }

    [Fact]
    public async Task Post_StageWithAQuestionConfirmed_AddsTheSendToTheStagedListShownByThePreview()
    {
        var apiClient = ApiClient(Send("Are you sure?"));

        var result = await PostAsync(
            CreateController(apiClient), stage: Value(SchemeA, "SendToTestConsultant"), confirmed: true, staged: [Value(SchemeB, "SendToResultsSignOff")]);

        var model = AssertTabulationsView(result);
        TabulationStagedSendRequest[] expected =
            [new(SchemeB, "SendToResultsSignOff"), new(SchemeA, "SendToTestConsultant")];
        Assert.Equal(expected, Assert.Single(apiClient.PreviewedTabulations));
        Assert.Equal(expected, model.Staged);
    }

    [Fact]
    public async Task Post_StageWithoutAQuestion_IsAddedWithoutConfirmation()
    {
        var apiClient = ApiClient(Send(string.Empty, "SendForAssessment"));

        var result = await PostAsync(CreateController(apiClient), stage: Value(SchemeA, "SendForAssessment"));

        var model = AssertTabulationsView(result);
        Assert.Equal([new TabulationStagedSendRequest(SchemeA, "SendForAssessment")], model.Staged);
        Assert.Equal(model.Staged, Assert.Single(apiClient.PreviewedTabulations));
    }

    [Fact]
    public async Task Post_StageNotAvailable_IsNotAdded()
    {
        var apiClient = ApiClient(stage: null);

        var result = await PostAsync(
            CreateController(apiClient), stage: Value(SchemeA, "SendToTestConsultant"), confirmed: true, staged: [Value(SchemeB, "SendToResultsSignOff")]);

        var model = AssertTabulationsView(result);
        Assert.Single(apiClient.StagedTabulationSends);
        Assert.Equal([new TabulationStagedSendRequest(SchemeB, "SendToResultsSignOff")], model.Staged);
    }

    [Theory]
    [InlineData("not-a-guid:SendToTestConsultant")]
    [InlineData("aaaaaaaa-0000-0000-0000-0000000000f1:")]
    [InlineData("aaaaaaaa-0000-0000-0000-0000000000f1")]
    [InlineData("")]
    public async Task Post_MalformedStageAndStagedValues_AreIgnored(string malformed)
    {
        var apiClient = ApiClient(Send(string.Empty));

        var result = await PostAsync(CreateController(apiClient), stage: malformed, staged: [malformed, Value(SchemeB, "SendToResultsSignOff")]);

        var model = AssertTabulationsView(result);
        Assert.Empty(apiClient.StagedTabulationSends);
        Assert.Equal([new TabulationStagedSendRequest(SchemeB, "SendToResultsSignOff")], model.Staged);
    }

    [Fact]
    public async Task Post_NoStageNoCommandAndNullStaged_PreviewsWithNothingStaged()
    {
        var apiClient = ApiClient();

        var result = await CreateController(apiClient).Tabulations(2026, 5, null, null, false, null, CancellationToken.None);

        var model = AssertTabulationsView(result);
        Assert.Empty(model.Staged);
        Assert.Empty(Assert.Single(apiClient.PreviewedTabulations));
        Assert.Empty(apiClient.StagedTabulationSends);
    }

    // ---- Preview ----

    [Fact]
    public async Task Post_PreviewReturnsNull_ReturnsNotFound()
    {
        var apiClient = new FakeDistributionApiClient { TabulationsPreview = null };

        var result = await PostAsync(CreateController(apiClient), staged: [Value(SchemeA, "SendToTestConsultant")]);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Post_Preview_MapsOntoTheTabulationsViewModelWithTheStagedSends()
    {
        var apiClient = ApiClient();

        var result = await PostAsync(CreateController(apiClient), staged: [Value(SchemeA, "SendToTestConsultant")]);

        var model = AssertTabulationsView(result);
        Assert.Equal((2026, 5, "May 2026"), (model.YearId, model.MonthId, model.MonthDescription));
        Assert.Equal(apiClient.TabulationsPreview!.Schemes, model.Schemes);
        Assert.Equal([new TabulationStagedSendRequest(SchemeA, "SendToTestConsultant")], model.Staged);
    }

    [Fact]
    public void TabulationSendViewModel_DefaultsToNothingStaged()
    {
        var model = new TabulationSendViewModel { YearId = 2026, MonthId = 5, Send = Send("q") };

        Assert.Empty(model.Staged);
    }

    // TabulationStageRequest holds a list, so record equality is by reference.
    private sealed class StageRequestComparer : IEqualityComparer<TabulationStageRequest>
    {
        public bool Equals(TabulationStageRequest? x, TabulationStageRequest? y) =>
            x is not null && y is not null
            && x.MonthlyDistributionSchemeId == y.MonthlyDistributionSchemeId
            && x.Command == y.Command
            && x.Staged.SequenceEqual(y.Staged);

        public int GetHashCode(TabulationStageRequest obj) => obj.MonthlyDistributionSchemeId.GetHashCode();
    }
}

