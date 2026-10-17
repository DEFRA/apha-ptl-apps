using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using PTL.ApiClient.Distribution;
using PTL.Contracts.Distribution;
using PTL.InternalWeb.Confirmation;
using PTL.InternalWeb.Features.Distribution;
using PTL.InternalWeb.Navigation;
using PTL.InternalWeb.Tests.TestSupport;
using PTL.SharedUI.Notifications;

namespace PTL.InternalWeb.Tests.Features.Distribution;

public class DistributionControllerTests
{
    private static DistributionController CreateController(IDistributionApiClient apiClient) =>
        new(apiClient)
        {
            TempData = new TempDataDictionary(new DefaultHttpContext(), new FakeTempDataProvider())
        };
    [Fact]
    public async Task Index_NoYearSpecified_UsesCurrentFinancialYearDefault()
    {
        var apiClient = new FakeDistributionApiClient
        {
            Years = [new DistributionYearOptionResponse(2026, "2026/27")],
            Months = [new DistributionDashboardMonthResponse(2026, 4, "Apr", null, true, 0, 0, 0, 0, 0)],
        };
        var controller = CreateController(apiClient);

        var result = await controller.Index(year: null, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<DistributionDashboardViewModel>(view.Model);
        Assert.Single(model.Months);
        Assert.Equal("Apr", model.Months[0].MonthDescription);
        Assert.True(model.Months[0].IsInitialisable);
    }

    [Fact]
    public async Task Index_InitialisedMonth_RoundsPercentagesForDisplay()
    {
        var apiClient = new FakeDistributionApiClient
        {
            Years = [new DistributionYearOptionResponse(2026, "2026/27")],
            Months = [new DistributionDashboardMonthResponse(2026, 4, "Apr", Guid.NewGuid(), false, 33.33m, 0, 0, 0, 0)],
        };
        var controller = CreateController(apiClient);

        var result = await controller.Index(2026, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<DistributionDashboardViewModel>(view.Model);
        Assert.True(model.Months[0].IsInitialised);
        Assert.Equal(33, model.Months[0].SchedulingCompletePercentage);
    }

    [Fact]
    public void ViewStage_ReturnsFeatureNotAvailableWithStageName()
    {
        var controller = CreateController(new FakeDistributionApiClient());

        var result = controller.ViewStage("Scheduling");

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal("FeatureNotAvailable", view.ViewName);
        Assert.Equal("Scheduling", view.Model);
    }

    [Theory]
    [InlineData(2026, 4, 2026)]
    [InlineData(2026, 12, 2026)]
    [InlineData(2027, 1, 2026)]
    [InlineData(2027, 3, 2026)]
    public async Task InitialiseMonth_Initialised_ShowsTheBannerAndReturnsToTheFinancialYear(int year, int monthId, int financialYear)
    {
        var apiClient = new FakeDistributionApiClient { InitialiseMonthResult = new InitialiseMonthResult(true) };
        var controller = CreateController(apiClient);

        var result = await controller.InitialiseMonth(year, monthId, ConfirmationFlow.Confirmed, CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);
        Assert.Equal(financialYear, redirect.RouteValues!["year"]);
        Assert.Equal([(year, monthId)], apiClient.InitialisedMonths);
        var notification = controller.TempData.GetNotification();
        Assert.Equal(NotificationType.Success, notification!.Type);
        Assert.Equal("Month initialised successfully.", notification.Message);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InitialiseMonth_AlreadyInitialisedOrNoResult_ShowsNoBannerAndRedisplaysTheDashboard(bool hasResult)
    {
        var apiClient = new FakeDistributionApiClient { InitialiseMonthResult = hasResult ? new InitialiseMonthResult(false) : null };
        var controller = CreateController(apiClient);

        var result = await controller.InitialiseMonth(2026, 5, ConfirmationFlow.Confirmed, CancellationToken.None);

        Assert.Equal("Index", Assert.IsType<RedirectToActionResult>(result).ActionName);
        Assert.Null(controller.TempData.GetNotification());
    }

    [Theory]
    [InlineData(2026, 0)]
    [InlineData(2026, 13)]
    [InlineData(0, 5)]
    public async Task InitialiseMonth_InvalidMonth_ReturnsBadRequestWithoutCallingTheApi(int year, int monthId)
    {
        var apiClient = new FakeDistributionApiClient();
        var controller = CreateController(apiClient);

        Assert.IsType<BadRequestResult>(await controller.InitialiseMonth(year, monthId, ConfirmationFlow.Confirmed, CancellationToken.None));
        Assert.Empty(apiClient.InitialisedMonths);
    }

    [Fact]
    public async Task InitialiseMonth_NotConfirmed_ShowsTheConfirmationPageAndInitialisesNothing()
    {
        var apiClient = new FakeDistributionApiClient { InitialiseMonthResult = new InitialiseMonthResult(true) };
        var controller = CreateController(apiClient);
        controller.Url = new FakeUrlHelper();

        var result = await controller.InitialiseMonth(2026, 5, null, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal(ConfirmationFlow.ViewName, view.ViewName);
        var model = Assert.IsType<ConfirmationPageViewModel>(view.Model);
        Assert.Equal("Are you sure you wish to initialise this month?", model.Message);
        Assert.Contains("you will no longer be able to add schemes to this month", model.WarningText, StringComparison.Ordinal);
        Assert.EndsWith("/Index", model.CancelUrl, StringComparison.Ordinal);
        Assert.Equal(nameof(DistributionController.Index), controller.ViewData[BreadcrumbRouteValues.Action]);
        Assert.Empty(apiClient.InitialisedMonths);
        Assert.Null(controller.TempData.GetNotification());
    }

    [Fact]
    public async Task Schedule_Get_MonthNotInitialised_ReturnsNotFound()
    {
        var apiClient = new FakeDistributionApiClient { Schedule = new MonthlyDistributionResponse(null, 2026, 4, []) };
        var controller = CreateController(apiClient);

        var result = await controller.Schedule(2026, 4, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Schedule_Get_MonthInitialised_ReturnsViewModelWithSchemes()
    {
        var schemeId = Guid.NewGuid();
        var apiClient = new FakeDistributionApiClient
        {
            Schedule = new MonthlyDistributionResponse(Guid.NewGuid(), 2026, 4,
            [
                new MonthlyDistributionSchemeResponse(schemeId, Guid.NewGuid(), "S001", "Test Scheme", "D26-01A",
                    new DateTime(2026, 4, 1), new DateTime(2026, 4, 1), new DateTime(2026, 4, 5), new DateTime(2026, 4, 10),
                    3, 5, true, false, false, false),
            ]),
        };
        var controller = CreateController(apiClient);

        var result = await controller.Schedule(2026, 4, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<MonthlyDistributionScheduleViewModel>(view.Model);
        Assert.Equal("Apr 2026", model.MonthYearLabel);
        var row = Assert.Single(model.Schemes);
        Assert.Equal(schemeId, row.MonthlyDistributionSchemeId);
        Assert.Equal("D26-01A", row.DistributionReferenceFull);
    }

    [Fact]
    public async Task Schedule_Post_CancelCommand_RedirectsWithoutSaving()
    {
        var apiClient = new FakeDistributionApiClient();
        var controller = CreateController(apiClient);
        var model = new MonthlyDistributionScheduleViewModel { YearId = 2026, MonthId = 4 };

        var result = await controller.Schedule(2026, 4, model, "cancel", CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(DistributionController.Index), redirect.ActionName);
        Assert.Empty(apiClient.SavedRows);
        Assert.Null(controller.TempData.GetNotification());
    }

    // Matches the save notification every other migrated save screen raises.
    [Theory]
    [InlineData("save")]
    [InlineData("apply")]
    public async Task Schedule_Post_SaveSucceeds_ShowsSuccessNotification(string command)
    {
        var apiClient = new FakeDistributionApiClient
        {
            SaveResult = new MonthlyDistributionScheduleSaveResult(true, new Dictionary<Guid, string[]>()),
        };
        var controller = CreateController(apiClient);
        var model = new MonthlyDistributionScheduleViewModel { YearId = 2026, MonthId = 4 };

        await controller.Schedule(2026, 4, model, command, CancellationToken.None);

        var notification = controller.TempData.GetNotification();
        Assert.Equal(NotificationType.Success, notification!.Type);
        Assert.Equal("Monthly Distributions updated successfully.", notification.Message);
    }

    [Fact]
    public async Task Schedule_Post_ApiUnavailable_ShowsErrorNotification()
    {
        var controller = CreateController(new FakeDistributionApiClient { SaveResult = null });
        var model = new MonthlyDistributionScheduleViewModel { YearId = 2026, MonthId = 4 };

        var result = await controller.Schedule(2026, 4, model, "save", CancellationToken.None);

        Assert.IsType<ViewResult>(result);
        var notification = controller.TempData.GetNotification();
        Assert.Equal(NotificationType.Error, notification!.Type);
        Assert.Equal("Monthly Distributions could not be saved. Please try again later.", notification.Message);
    }

    [Fact]
    public async Task Schedule_Post_InvalidModelState_RedisplaysViewWithoutSaving()
    {
        var apiClient = new FakeDistributionApiClient();
        var controller = CreateController(apiClient);
        controller.ModelState.AddModelError("Schemes[0].DistributionDate", "Distribution Date required.");
        var model = new MonthlyDistributionScheduleViewModel { YearId = 2026, MonthId = 4 };

        var result = await controller.Schedule(2026, 4, model, "save", CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Same(model, view.Model);
        Assert.Empty(apiClient.SavedRows);
    }

    [Fact]
    public async Task Schedule_Post_SaveCommand_ValidModel_PersistsAndRedirectsToIndex()
    {
        var schemeId = Guid.NewGuid();
        var apiClient = new FakeDistributionApiClient { SaveResult = new MonthlyDistributionScheduleSaveResult(true, new Dictionary<Guid, string[]>()) };
        var controller = CreateController(apiClient);
        var model = new MonthlyDistributionScheduleViewModel
        {
            YearId = 2026,
            MonthId = 4,
            Schemes =
            [
                new MonthlyDistributionScheduleRowViewModel
                {
                    MonthlyDistributionSchemeId = schemeId,
                    DistributionDate = new DateTime(2026, 4, 1),
                    OverseasPostingDate = new DateTime(2026, 4, 1),
                    DeadlineDate = new DateTime(2026, 4, 5),
                    ResultsIssueTargetDate = new DateTime(2026, 4, 10),
                },
            ],
        };

        var result = await controller.Schedule(2026, 4, model, "save", CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(DistributionController.Index), redirect.ActionName);
        Assert.Single(apiClient.SavedRows);
    }

    [Fact]
    public async Task Schedule_Post_ApplyCommand_ValidModel_RedirectsBackToSchedule()
    {
        var apiClient = new FakeDistributionApiClient { SaveResult = new MonthlyDistributionScheduleSaveResult(true, new Dictionary<Guid, string[]>()) };
        var controller = CreateController(apiClient);
        var model = new MonthlyDistributionScheduleViewModel { YearId = 2026, MonthId = 4, Schemes = [] };

        var result = await controller.Schedule(2026, 4, model, "apply", CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(DistributionController.Schedule), redirect.ActionName);
    }

    [Fact]
    public async Task Schedule_Post_SaveFails_RedisplaysViewWithFieldErrors()
    {
        var schemeId = Guid.NewGuid();
        var apiClient = new FakeDistributionApiClient
        {
            SaveResult = new MonthlyDistributionScheduleSaveResult(false, new Dictionary<Guid, string[]> { [schemeId] = ["Deadline Date needs to be after the UK Posting date."] }),
        };
        var controller = CreateController(apiClient);
        var model = new MonthlyDistributionScheduleViewModel
        {
            YearId = 2026,
            MonthId = 4,
            Schemes =
            [
                new MonthlyDistributionScheduleRowViewModel
                {
                    MonthlyDistributionSchemeId = schemeId,
                    DistributionDate = new DateTime(2026, 4, 1),
                    OverseasPostingDate = new DateTime(2026, 4, 1),
                    DeadlineDate = new DateTime(2026, 3, 30),
                    ResultsIssueTargetDate = new DateTime(2026, 4, 10),
                },
            ],
        };

        var result = await controller.Schedule(2026, 4, model, "save", CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Same(model, view.Model);
        Assert.False(controller.ModelState.IsValid);
    }

    [Fact]
    public void ToggleCancelled_FlipsMatchingRowAndRedisplaysWithoutSaving()
    {
        var schemeId = Guid.NewGuid();
        var apiClient = new FakeDistributionApiClient();
        var controller = CreateController(apiClient);
        var model = new MonthlyDistributionScheduleViewModel
        {
            YearId = 2026,
            MonthId = 4,
            Schemes = [new MonthlyDistributionScheduleRowViewModel { MonthlyDistributionSchemeId = schemeId, IsCancelled = false }],
        };

        var result = controller.ToggleCancelled(2026, 4, schemeId, model, ConfirmationFlow.Confirmed);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal("Schedule", view.ViewName);
        var updatedModel = Assert.IsType<MonthlyDistributionScheduleViewModel>(view.Model);
        Assert.True(updatedModel.Schemes.Single().IsCancelled);
        Assert.Empty(apiClient.SavedRows);
    }

    [Fact]
    public void ToggleCancelled_NotConfirmed_AsksFirstAndLeavesTheRowUnchanged()
    {
        var schemeId = Guid.NewGuid();
        var controller = CreateController(new FakeDistributionApiClient());
        var model = new MonthlyDistributionScheduleViewModel
        {
            Schemes = [new MonthlyDistributionScheduleRowViewModel { MonthlyDistributionSchemeId = schemeId, IsCancelled = false }],
        };

        var result = controller.ToggleCancelled(2026, 4, schemeId, model, null);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal(ConfirmationFlow.ViewName, view.ViewName);
        var page = Assert.IsType<ConfirmationPageViewModel>(view.Model);
        Assert.Equal("Are you sure you wish to Cancel this Distribution?", page.Message);
        Assert.Null(page.CancelUrl);
        Assert.False(model.Schemes.Single().IsCancelled);
    }

    [Fact]
    public void ToggleCancelled_Declined_RedisplaysTheScheduleUnchanged()
    {
        var schemeId = Guid.NewGuid();
        var controller = CreateController(new FakeDistributionApiClient());
        var model = new MonthlyDistributionScheduleViewModel
        {
            Schemes = [new MonthlyDistributionScheduleRowViewModel { MonthlyDistributionSchemeId = schemeId, IsCancelled = false }],
        };

        var result = controller.ToggleCancelled(2026, 4, schemeId, model, ConfirmationFlow.Cancelled);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal("Schedule", view.ViewName);
        Assert.False(Assert.IsType<MonthlyDistributionScheduleViewModel>(view.Model).Schemes.Single().IsCancelled);
    }

    [Fact]
    public async Task Schedule_Get_ApiReturnsNull_ReturnsNotFound()
    {
        var controller = CreateController(new FakeDistributionApiClient());

        var result = await controller.Schedule(2026, 4, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Schedule_Post_SaveReturnsNull_RedisplaysViewWithoutAddingFieldErrors()
    {
        var apiClient = new FakeDistributionApiClient { SaveResult = null };
        var controller = CreateController(apiClient);
        var model = new MonthlyDistributionScheduleViewModel { YearId = 2026, MonthId = 4, Schemes = [] };

        var result = await controller.Schedule(2026, 4, model, "save", CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Same(model, view.Model);
        Assert.Equal(2026, model.YearId);
        Assert.Equal(4, model.MonthId);
    }

    [Fact]
    public async Task Schedule_Post_FieldErrorForUnknownScheme_IsIgnored()
    {
        var apiClient = new FakeDistributionApiClient
        {
            SaveResult = new MonthlyDistributionScheduleSaveResult(false, new Dictionary<Guid, string[]> { [Guid.NewGuid()] = ["Deadline Date needs to be after the UK Posting date."] }),
        };
        var controller = CreateController(apiClient);
        var model = new MonthlyDistributionScheduleViewModel { YearId = 2026, MonthId = 4, Schemes = [] };

        var result = await controller.Schedule(2026, 4, model, "save", CancellationToken.None);

        Assert.IsType<ViewResult>(result);
        Assert.True(controller.ModelState.IsValid);
    }

    [Fact]
    public void ToggleCancelled_PublishesScheduleAsTheBreadcrumbAction()
    {
        var controller = CreateController(new FakeDistributionApiClient());

        controller.ToggleCancelled(2026, 4, Guid.NewGuid(), new MonthlyDistributionScheduleViewModel(), ConfirmationFlow.Confirmed);

        Assert.Equal(nameof(DistributionController.Schedule), controller.ViewData[BreadcrumbRouteValues.Action]);
    }

    [Fact]
    public async Task ChangeDeadlineAndRepeatDistribution_PublishParticipantsAsTheBreadcrumbAction()
    {
        var changeController = CreateController(new FakeDistributionApiClient { ChangeDeadlineResult = new ChangeDeadlineResult(true, null, null) });
        await changeController.ChangeDeadline(2026, 4, ParticipantsSchemeId, DistributionParticipantId, PostedParticipantsModel(new DateTime(2026, 5, 1), "A reason"), CancellationToken.None);

        var repeatController = CreateController(new FakeDistributionApiClient { RepeatDistributionResult = new RepeatDistributionResult(true, null) });
        await repeatController.RepeatDistribution(2026, 4, ParticipantsSchemeId, PostedRepeatModel(), CancellationToken.None);

        Assert.Equal(nameof(DistributionController.Participants), changeController.ViewData[BreadcrumbRouteValues.Action]);
        Assert.Equal(nameof(DistributionController.Participants), repeatController.ViewData[BreadcrumbRouteValues.Action]);
    }

    [Fact]
    public void ToggleCancelled_UnknownScheme_LeavesEveryRowUnchanged()
    {
        var schemeId = Guid.NewGuid();
        var controller = CreateController(new FakeDistributionApiClient());
        var model = new MonthlyDistributionScheduleViewModel
        {
            Schemes = [new MonthlyDistributionScheduleRowViewModel { MonthlyDistributionSchemeId = schemeId, IsCancelled = false }],
        };

        var result = controller.ToggleCancelled(2026, 4, Guid.NewGuid(), model, ConfirmationFlow.Confirmed);

        var view = Assert.IsType<ViewResult>(result);
        var updatedModel = Assert.IsType<MonthlyDistributionScheduleViewModel>(view.Model);
        Assert.False(updatedModel.Schemes.Single().IsCancelled);
    }

    private static readonly Guid ParticipantsSchemeId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid DistributionParticipantId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid AdditionalParticipantSchemeId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private static DistributionParticipantsResponse ParticipantsResponse(bool isRepeatDistribution = false) =>
        new(
            ParticipantsSchemeId,
            2026,
            4,
            "PT0060",
            "Mastitis bovine",
            "17152A/BA",
            isRepeatDistribution,
            [new DistributionParticipantResponse(DistributionParticipantId, "L001: Lab One", new DateTime(2026, 4, 5), string.Empty, false)],
            [new DistributionAdditionalParticipantResponse(AdditionalParticipantSchemeId, "L002: Lab Two")]);

    [Fact]
    public async Task Participants_Get_UnknownScheme_ReturnsNotFound()
    {
        var controller = CreateController(new FakeDistributionApiClient());

        var result = await controller.Participants(2026, 4, ParticipantsSchemeId, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Participants_Get_MapsResponseOntoViewModel()
    {
        var apiClient = new FakeDistributionApiClient { Participants = ParticipantsResponse(isRepeatDistribution: true) };
        var controller = CreateController(apiClient);

        var result = await controller.Participants(2026, 4, ParticipantsSchemeId, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<DistributionParticipantsViewModel>(view.Model);
        Assert.Equal(ParticipantsSchemeId, model.MonthlyDistributionSchemeId);
        Assert.Equal(2026, model.YearId);
        Assert.Equal(4, model.MonthId);
        Assert.Equal("PT0060: Mastitis bovine", model.SchemeHeading);
        Assert.True(model.IsRepeatDistribution);
        Assert.Equal("Repeat Distribution", model.DistributionListTitle);
        var participant = Assert.Single(model.Participants);
        Assert.Equal(DistributionParticipantId, participant.DistributionParticipantId);
        Assert.Equal("L001: Lab One", participant.FullName);
        Assert.Equal("L002: Lab Two", Assert.Single(model.AdditionalParticipants).FullName);
    }

    private static DistributionParticipantsViewModel PostedParticipantsModel(DateTime? newDate = null, string? newReason = null) => new()
    {
        MonthlyDistributionSchemeId = ParticipantsSchemeId,
        YearId = 2026,
        MonthId = 4,
        Participants =
        [
            new DistributionParticipantRowViewModel
            {
                DistributionParticipantId = DistributionParticipantId,
                FullName = "L001: Lab One",
                DeadlineDate = new DateTime(2026, 4, 5),
                NewDeadlineDate = newDate,
                NewDeadlineReason = newReason,
            },
        ],
    };

    [Fact]
    public async Task ChangeDeadline_Post_Success_ShowsSuccessNotificationAndRedirectsToParticipants()
    {
        var apiClient = new FakeDistributionApiClient { ChangeDeadlineResult = new ChangeDeadlineResult(true, null, null) };
        var controller = CreateController(apiClient);
        var model = PostedParticipantsModel(new DateTime(2026, 5, 1), "A reason");

        var result = await controller.ChangeDeadline(2026, 4, ParticipantsSchemeId, DistributionParticipantId, model, CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(DistributionController.Participants), redirect.ActionName);
        var notification = controller.TempData.GetNotification();
        Assert.Equal(NotificationType.Success, notification!.Type);
        Assert.Equal("Deadline Date updated successfully.", notification.Message);
        var saved = Assert.Single(apiClient.SavedDeadlines);
        Assert.Equal(DistributionParticipantId, saved.DistributionParticipantId);
        Assert.Equal(new DateTime(2026, 5, 1), saved.Request.NewDeadlineDate);
        Assert.Equal("A reason", saved.Request.Reason);
    }

    [Fact]
    public async Task ChangeDeadline_Post_ValidationFailure_AddsFieldErrorsAndRehydrates()
    {
        var apiClient = new FakeDistributionApiClient
        {
            ChangeDeadlineResult = new ChangeDeadlineResult(false, "Deadline Date required.", "A reason for changing the deadline date is required."),
            Participants = ParticipantsResponse(),
        };
        var controller = CreateController(apiClient);
        var model = PostedParticipantsModel(null, null);

        var result = await controller.ChangeDeadline(2026, 4, ParticipantsSchemeId, DistributionParticipantId, model, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal(nameof(DistributionController.Participants), view.ViewName);
        Assert.False(controller.ModelState.IsValid);
        Assert.True(controller.ModelState.ContainsKey($"{nameof(model.Participants)}[0].{nameof(DistributionParticipantRowViewModel.NewDeadlineDate)}"));
        Assert.True(controller.ModelState.ContainsKey($"{nameof(model.Participants)}[0].{nameof(DistributionParticipantRowViewModel.NewDeadlineReason)}"));
        Assert.Null(controller.TempData.GetNotification());
    }

    // Repository could not find the participant - distinct from a validation failure, so no field
    // errors are added, only the page is redisplayed with fresh data.
    [Fact]
    public async Task ChangeDeadline_Post_ParticipantNotFound_RedisplaysWithoutFieldErrorsOrNotification()
    {
        var apiClient = new FakeDistributionApiClient
        {
            ChangeDeadlineResult = new ChangeDeadlineResult(false, null, null),
            Participants = ParticipantsResponse(),
        };
        var controller = CreateController(apiClient);
        var model = PostedParticipantsModel(new DateTime(2026, 5, 1), "A reason");

        var result = await controller.ChangeDeadline(2026, 4, ParticipantsSchemeId, DistributionParticipantId, model, CancellationToken.None);

        Assert.IsType<ViewResult>(result);
        Assert.True(controller.ModelState.IsValid);
        Assert.Null(controller.TempData.GetNotification());
    }

    [Fact]
    public async Task ChangeDeadline_Post_ApiUnavailable_ShowsErrorNotificationAndRehydrates()
    {
        var apiClient = new FakeDistributionApiClient { ChangeDeadlineResult = null, Participants = ParticipantsResponse() };
        var controller = CreateController(apiClient);
        var model = PostedParticipantsModel(new DateTime(2026, 5, 1), "A reason");

        var result = await controller.ChangeDeadline(2026, 4, ParticipantsSchemeId, DistributionParticipantId, model, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        Assert.IsType<DistributionParticipantsViewModel>(view.Model);
        var notification = controller.TempData.GetNotification();
        Assert.Equal(NotificationType.Error, notification!.Type);
        Assert.Equal("The deadline date could not be saved. Please try again later.", notification.Message);
    }

    private static DistributionParticipantsViewModel PostedRepeatModel(bool selectParticipant = true, bool selectAdditional = false) => new()
    {
        MonthlyDistributionSchemeId = ParticipantsSchemeId,
        YearId = 2026,
        MonthId = 4,
        Participants = [new DistributionParticipantRowViewModel { DistributionParticipantId = DistributionParticipantId, Selected = selectParticipant }],
        AdditionalParticipants = [new DistributionAdditionalParticipantRowViewModel { ParticipantSchemeId = AdditionalParticipantSchemeId, Selected = selectAdditional }],
    };

    [Fact]
    public async Task RepeatDistribution_Post_Success_ShowsSuccessNotificationAndRedirectsToSchedule()
    {
        var apiClient = new FakeDistributionApiClient { RepeatDistributionResult = new RepeatDistributionResult(true, null) };
        var controller = CreateController(apiClient);
        var model = PostedRepeatModel();

        var result = await controller.RepeatDistribution(2026, 4, ParticipantsSchemeId, model, CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(DistributionController.Schedule), redirect.ActionName);
        Assert.Equal(2026, redirect.RouteValues!["year"]);
        Assert.Equal(4, redirect.RouteValues["monthId"]);
        var notification = controller.TempData.GetNotification();
        Assert.Equal(NotificationType.Success, notification!.Type);
        Assert.Equal("Distribution repeated successfully.", notification.Message);
        var saved = Assert.Single(apiClient.SavedRepeatRequests);
        Assert.Contains(saved.Selections, s => s.Id == DistributionParticipantId && !s.IsAdditional);
    }

    [Fact]
    public async Task RepeatDistribution_Post_MixesOriginalAndAdditionalSelections()
    {
        var apiClient = new FakeDistributionApiClient { RepeatDistributionResult = new RepeatDistributionResult(true, null) };
        var controller = CreateController(apiClient);
        var model = PostedRepeatModel(selectParticipant: true, selectAdditional: true);

        await controller.RepeatDistribution(2026, 4, ParticipantsSchemeId, model, CancellationToken.None);

        var saved = Assert.Single(apiClient.SavedRepeatRequests);
        Assert.Equal(2, saved.Selections.Count);
        Assert.Contains(saved.Selections, s => s.Id == AdditionalParticipantSchemeId && s.IsAdditional);
    }

    // Legacy client-side checkifchecked() guard, re-enforced server-side.
    [Fact]
    public async Task RepeatDistribution_Post_EmptySelection_ShowsApiErrorMessageAndRedisplays()
    {
        var apiClient = new FakeDistributionApiClient
        {
            RepeatDistributionResult = new RepeatDistributionResult(false, "Please tick at least one participant to be included in the repeat of this distribution."),
            Participants = ParticipantsResponse(),
        };
        var controller = CreateController(apiClient);
        var model = PostedRepeatModel(selectParticipant: false, selectAdditional: false);

        var result = await controller.RepeatDistribution(2026, 4, ParticipantsSchemeId, model, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal(nameof(DistributionController.Participants), view.ViewName);
        var notification = controller.TempData.GetNotification();
        Assert.Equal(NotificationType.Error, notification!.Type);
        Assert.Equal("Please tick at least one participant to be included in the repeat of this distribution.", notification.Message);
    }

    [Fact]
    public async Task RepeatDistribution_Post_ApiUnavailable_ShowsDefaultErrorNotification()
    {
        var apiClient = new FakeDistributionApiClient { RepeatDistributionResult = null, Participants = ParticipantsResponse() };
        var controller = CreateController(apiClient);
        var model = PostedRepeatModel();

        var result = await controller.RepeatDistribution(2026, 4, ParticipantsSchemeId, model, CancellationToken.None);

        Assert.IsType<ViewResult>(result);
        var notification = controller.TempData.GetNotification();
        Assert.Equal(NotificationType.Error, notification!.Type);
        Assert.Equal("The distribution could not be repeated. Please try again later.", notification.Message);
    }

    // Rehydration itself cannot reach the API - the originally posted model is redisplayed as-is.
    [Fact]
    public async Task RepeatDistribution_Post_RehydrationApiReturnsNull_KeepsPostedModel()
    {
        var apiClient = new FakeDistributionApiClient { RepeatDistributionResult = null, Participants = null };
        var controller = CreateController(apiClient);
        var model = PostedRepeatModel();

        var result = await controller.RepeatDistribution(2026, 4, ParticipantsSchemeId, model, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Same(model, view.Model);
    }

    private static readonly Guid PreparationSchemeId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid PreparationSampleId = Guid.Parse("55555555-5555-5555-5555-555555555555");

    private static PreparationSheetResponse PreparationResponse() =>
        new(
            Guid.NewGuid(),
            2026,
            4,
            [
                new PreparationSchemeResponse(
                    PreparationSchemeId,
                    "PT0060",
                    "Mastitis bovine",
                    "17152A/BA",
                    9,
                    "Keep chilled",
                    DiaryEntry: null,
                    PrepStartDate: null,
                    PrepCompleteDate: null,
                    Samples: [new PreparationSampleResponse(PreparationSampleId, "S1", "Internal 1")]),
            ]);

    [Fact]
    public async Task Preparation_Get_MonthNotInitialised_ReturnsNotFound()
    {
        var controller = CreateController(new FakeDistributionApiClient());

        var result = await controller.Preparation(2026, 4, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Preparation_Get_MonthInitialised_ReturnsViewModelWithSchemesAndSamples()
    {
        var apiClient = new FakeDistributionApiClient { Preparation = PreparationResponse() };
        var controller = CreateController(apiClient);

        var result = await controller.Preparation(2026, 4, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<PreparationViewModel>(view.Model);
        Assert.Equal(2026, model.YearId);
        Assert.Equal(4, model.MonthId);
        Assert.Equal("Apr 2026", model.MonthDescription);
        var row = Assert.Single(model.Schemes);
        Assert.Equal(PreparationSchemeId, row.MonthlyDistributionSchemeId);
        Assert.Equal("17152A/BA PT0060: Mastitis bovine", row.SchemeHeading);
        Assert.True(row.HasSamples);
        Assert.Equal("S1", Assert.Single(row.Samples).SampleNumber);
    }

    [Fact]
    public async Task Preparation_Post_CancelCommand_RedirectsToIndexWithoutSaving()
    {
        var apiClient = new FakeDistributionApiClient();
        var controller = CreateController(apiClient);
        var model = new PreparationViewModel { YearId = 2026, MonthId = 4 };

        var result = await controller.Preparation(2026, 4, model, "cancel", CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(DistributionController.Index), redirect.ActionName);
        Assert.Empty(apiClient.SavedPreparationRequests);
    }

    [Fact]
    public async Task Preparation_Post_SaveCommand_PersistsAndRedirectsToIndex()
    {
        var apiClient = new FakeDistributionApiClient { PreparationSaveResult = new PreparationSaveResult(true) };
        var controller = CreateController(apiClient);
        var model = new PreparationViewModel
        {
            YearId = 2026,
            MonthId = 4,
            Schemes = [new PreparationSchemeRowViewModel { MonthlyDistributionSchemeId = PreparationSchemeId, DiaryEntryChecked = true }],
        };

        var result = await controller.Preparation(2026, 4, model, "save", CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(DistributionController.Index), redirect.ActionName);
        var saved = Assert.Single(apiClient.SavedPreparationRequests);
        Assert.True(Assert.Single(saved.Updates).DiaryEntryChecked);
        var notification = controller.TempData.GetNotification();
        Assert.Equal(NotificationType.Success, notification!.Type);
        Assert.Equal("Sample Preparation updated successfully.", notification.Message);
    }

    [Fact]
    public async Task Preparation_Post_ApplyCommand_PersistsAndRedirectsBackToPreparation()
    {
        var apiClient = new FakeDistributionApiClient { PreparationSaveResult = new PreparationSaveResult(true) };
        var controller = CreateController(apiClient);
        var model = new PreparationViewModel { YearId = 2026, MonthId = 4, Schemes = [] };

        var result = await controller.Preparation(2026, 4, model, "apply", CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(DistributionController.Preparation), redirect.ActionName);
        Assert.Equal(2026, redirect.RouteValues!["year"]);
        Assert.Equal(4, redirect.RouteValues["monthId"]);
    }

    [Fact]
    public async Task Preparation_Post_SaveFails_ShowsErrorNotificationAndRedirectsBackToPreparation()
    {
        var apiClient = new FakeDistributionApiClient { PreparationSaveResult = new PreparationSaveResult(false) };
        var controller = CreateController(apiClient);
        var model = new PreparationViewModel { YearId = 2026, MonthId = 4, Schemes = [] };

        var result = await controller.Preparation(2026, 4, model, "save", CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(DistributionController.Preparation), redirect.ActionName);
        var notification = controller.TempData.GetNotification();
        Assert.Equal(NotificationType.Error, notification!.Type);
        Assert.Equal("Sample Preparation could not be saved. Please try again later.", notification.Message);
    }

    [Fact]
    public async Task Preparation_Post_ApiUnavailable_ShowsErrorNotificationAndRedirectsBackToPreparation()
    {
        var apiClient = new FakeDistributionApiClient { PreparationSaveResult = null };
        var controller = CreateController(apiClient);
        var model = new PreparationViewModel { YearId = 2026, MonthId = 4, Schemes = [] };

        var result = await controller.Preparation(2026, 4, model, "save", CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(DistributionController.Preparation), redirect.ActionName);
        var notification = controller.TempData.GetNotification();
        Assert.Equal(NotificationType.Error, notification!.Type);
        Assert.Equal("Sample Preparation could not be saved. Please try again later.", notification.Message);
    }

    [Fact]
    public async Task PrintableResults_Get_UnknownScheme_ReturnsNotFound()
    {
        var controller = CreateController(new FakeDistributionApiClient());

        var result = await controller.PrintableResults(2026, 4, ParticipantsSchemeId, showValues: false, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task PrintableResults_Get_KnownScheme_ReturnsMappedWorksheet()
    {
        var apiClient = new FakeDistributionApiClient
        {
            IntendedResults = new IntendedResultsResponse(
                ParticipantsSchemeId, "17152A/BA", "PT0060", "Mastitis bovine", false, []),
        };
        var controller = CreateController(apiClient);

        var result = await controller.PrintableResults(2026, 4, ParticipantsSchemeId, showValues: false, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<PrintableResultsWorksheet>(view.Model);
        Assert.Equal(ParticipantsSchemeId, model.MonthlyDistributionSchemeId);
        Assert.Equal(2026, model.YearId);
        Assert.Equal(4, model.MonthId);
        Assert.Equal("PT0060: Mastitis bovine", model.SchemeHeading);
    }

    [Fact]
    public async Task PrintableResults_Get_HasIntendedResultsFalse_ClampsShowValuesToFalse()
    {
        var apiClient = new FakeDistributionApiClient
        {
            IntendedResults = new IntendedResultsResponse(
                ParticipantsSchemeId, "17152A/BA", "PT0060", "Mastitis bovine", false, [], HasIntendedResults: false),
        };
        var controller = CreateController(apiClient);

        var result = await controller.PrintableResults(2026, 4, ParticipantsSchemeId, showValues: true, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<PrintableResultsWorksheet>(view.Model);
        Assert.False(model.ShowValues);
    }
}
