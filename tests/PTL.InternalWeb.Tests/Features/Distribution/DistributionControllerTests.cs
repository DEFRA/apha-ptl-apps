using Microsoft.AspNetCore.Mvc;
using PTL.Contracts.Distribution;
using PTL.InternalWeb.Features.Distribution;
using PTL.InternalWeb.Tests.TestSupport;

namespace PTL.InternalWeb.Tests.Features.Distribution;

public class DistributionControllerTests
{
    [Fact]
    public async Task Index_NoYearSpecified_UsesCurrentFinancialYearDefault()
    {
        var apiClient = new FakeDistributionApiClient
        {
            Years = [new DistributionYearOptionResponse(2026, "2026/27")],
            Months = [new DistributionDashboardMonthResponse(2026, 4, "Apr", null, true, 0, 0, 0, 0, 0)],
        };
        var controller = new DistributionController(apiClient);

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
        var controller = new DistributionController(apiClient);

        var result = await controller.Index(2026, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<DistributionDashboardViewModel>(view.Model);
        Assert.True(model.Months[0].IsInitialised);
        Assert.Equal(33, model.Months[0].SchedulingCompletePercentage);
    }

    [Fact]
    public void ViewStage_ReturnsFeatureNotAvailableWithStageName()
    {
        var controller = new DistributionController(new FakeDistributionApiClient());

        var result = controller.ViewStage("Scheduling");

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal("FeatureNotAvailable", view.ViewName);
        Assert.Equal("Scheduling", view.Model);
    }

    [Fact]
    public void InitialiseMonth_ReturnsFeatureNotAvailable()
    {
        var controller = new DistributionController(new FakeDistributionApiClient());

        var result = controller.InitialiseMonth(2026, 4);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal("FeatureNotAvailable", view.ViewName);
        Assert.Equal("Initialise This Month", view.Model);
    }

    [Fact]
    public async Task Schedule_Get_MonthNotInitialised_ReturnsNotFound()
    {
        var apiClient = new FakeDistributionApiClient { Schedule = new MonthlyDistributionResponse(null, 2026, 4, []) };
        var controller = new DistributionController(apiClient);

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
        var controller = new DistributionController(apiClient);

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
        var controller = new DistributionController(apiClient);
        var model = new MonthlyDistributionScheduleViewModel { YearId = 2026, MonthId = 4 };

        var result = await controller.Schedule(2026, 4, model, "cancel", CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(DistributionController.Index), redirect.ActionName);
        Assert.Empty(apiClient.SavedRows);
    }

    [Fact]
    public async Task Schedule_Post_InvalidModelState_RedisplaysViewWithoutSaving()
    {
        var apiClient = new FakeDistributionApiClient();
        var controller = new DistributionController(apiClient);
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
        var controller = new DistributionController(apiClient);
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
        var controller = new DistributionController(apiClient);
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
        var controller = new DistributionController(apiClient);
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
        var controller = new DistributionController(apiClient);
        var model = new MonthlyDistributionScheduleViewModel
        {
            YearId = 2026,
            MonthId = 4,
            Schemes = [new MonthlyDistributionScheduleRowViewModel { MonthlyDistributionSchemeId = schemeId, IsCancelled = false }],
        };

        var result = controller.ToggleCancelled(2026, 4, schemeId, model);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal("Schedule", view.ViewName);
        var updatedModel = Assert.IsType<MonthlyDistributionScheduleViewModel>(view.Model);
        Assert.True(updatedModel.Schemes.Single().IsCancelled);
        Assert.Empty(apiClient.SavedRows);
    }

    [Fact]
    public async Task Schedule_Get_ApiReturnsNull_ReturnsNotFound()
    {
        var controller = new DistributionController(new FakeDistributionApiClient());

        var result = await controller.Schedule(2026, 4, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Schedule_Post_SaveReturnsNull_RedisplaysViewWithoutAddingFieldErrors()
    {
        var apiClient = new FakeDistributionApiClient { SaveResult = null };
        var controller = new DistributionController(apiClient);
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
        var controller = new DistributionController(apiClient);
        var model = new MonthlyDistributionScheduleViewModel { YearId = 2026, MonthId = 4, Schemes = [] };

        var result = await controller.Schedule(2026, 4, model, "save", CancellationToken.None);

        Assert.IsType<ViewResult>(result);
        Assert.True(controller.ModelState.IsValid);
    }

    [Fact]
    public void ToggleCancelled_UnknownScheme_LeavesEveryRowUnchanged()
    {
        var schemeId = Guid.NewGuid();
        var controller = new DistributionController(new FakeDistributionApiClient());
        var model = new MonthlyDistributionScheduleViewModel
        {
            Schemes = [new MonthlyDistributionScheduleRowViewModel { MonthlyDistributionSchemeId = schemeId, IsCancelled = false }],
        };

        var result = controller.ToggleCancelled(2026, 4, Guid.NewGuid(), model);

        var view = Assert.IsType<ViewResult>(result);
        var updatedModel = Assert.IsType<MonthlyDistributionScheduleViewModel>(view.Model);
        Assert.False(updatedModel.Schemes.Single().IsCancelled);
    }
}
