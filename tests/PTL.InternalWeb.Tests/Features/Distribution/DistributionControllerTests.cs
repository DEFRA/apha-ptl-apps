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
}
