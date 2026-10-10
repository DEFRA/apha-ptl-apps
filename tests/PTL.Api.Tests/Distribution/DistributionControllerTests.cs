using Microsoft.AspNetCore.Mvc;
using PTL.Api.Controllers;
using PTL.Contracts.Distribution;
using PTL.Core.Distribution;

namespace PTL.Api.Tests.Distribution;

public class DistributionControllerTests
{
    private static DistributionController CreateController(FakeDistributionRepository repository) =>
        new(new DistributionService(repository));

    [Fact]
    public async Task GetDashboard_ReturnsTwelveMonthsWithMonthDescription()
    {
        var repository = new FakeDistributionRepository();
        var controller = CreateController(repository);

        var result = await controller.GetDashboard(2026, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var months = Assert.IsType<IReadOnlyList<DistributionDashboardMonthResponse>>(ok.Value, exactMatch: false);
        Assert.Equal(12, months.Count);
        Assert.Equal("Apr", months[0].MonthDescription);
        Assert.Equal("Mar", months[^1].MonthDescription);
    }

    [Fact]
    public async Task GetDashboard_InitialisedMonth_ReportsMonthlyDistributionIdAndPercentages()
    {
        var monthlyDistributionId = Guid.NewGuid();
        var repository = new FakeDistributionRepository();
        repository.Summaries.Add(new DistributionMonthSummaryEntity
        {
            MonthlyDistributionId = monthlyDistributionId,
            YearId = 2026,
            MonthId = 4,
            NumberOfSchemes = 4,
            NumberOfSampleNumbersDefined = 2,
        });
        var controller = CreateController(repository);

        var result = await controller.GetDashboard(2026, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var months = Assert.IsType<IReadOnlyList<DistributionDashboardMonthResponse>>(ok.Value, exactMatch: false);
        var april = months.Single(m => m.MonthId == 4 && m.YearId == 2026);
        Assert.Equal(monthlyDistributionId, april.MonthlyDistributionId);
        Assert.False(april.IsInitialisable);
        Assert.Equal(50m, april.SchedulingCompletePercentage);
    }

    [Fact]
    public async Task GetYears_ReturnsMappedYearOptions()
    {
        var repository = new FakeDistributionRepository();
        repository.Years.Add(new DistributionMonthYearEntity { YearId = 2026, MonthId = 5 });
        var controller = CreateController(repository);

        var result = await controller.GetYears(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var years = Assert.IsType<IReadOnlyList<DistributionYearOptionResponse>>(ok.Value, exactMatch: false);
        Assert.Contains(years, y => y.YearId == 2026 && y.Label == "2026/27");
    }

    [Fact]
    public async Task GetSchedule_MonthNotInitialised_ReturnsEmptyResponseWithNullId()
    {
        var repository = new FakeDistributionRepository();
        var controller = CreateController(repository);

        var result = await controller.GetSchedule(2026, 4, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<MonthlyDistributionResponse>(ok.Value);
        Assert.Null(response.MonthlyDistributionId);
        Assert.Empty(response.Schemes);
    }

    [Fact]
    public async Task GetSchedule_MonthInitialised_ReturnsMappedSchemes()
    {
        var monthlyDistributionId = Guid.NewGuid();
        var schemeId = Guid.NewGuid();
        var repository = new FakeDistributionRepository();
        repository.Schedules[(2026, 4)] = (monthlyDistributionId, [new MonthlyDistributionSchemeEntity
        {
            MonthlyDistributionSchemeId = schemeId,
            SchemeIdentifier = "S001",
            SchemeName = "Test Scheme",
            DistributionReference = "D26-01",
            DistributionReferenceSuffix = "A",
            ScheduleCode = "BA",
        }]);
        var controller = CreateController(repository);

        var result = await controller.GetSchedule(2026, 4, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<MonthlyDistributionResponse>(ok.Value);
        Assert.Equal(monthlyDistributionId, response.MonthlyDistributionId);
        var scheme = Assert.Single(response.Schemes);
        Assert.Equal("D26-01A/BA", scheme.DistributionReferenceFull);
    }

    [Fact]
    public async Task SaveSchedule_InvalidRow_ReturnsUnsuccessfulWithFieldErrors()
    {
        var schemeId = Guid.NewGuid();
        var repository = new FakeDistributionRepository();
        repository.Schedules[(2026, 4)] = (Guid.NewGuid(), [new MonthlyDistributionSchemeEntity { MonthlyDistributionSchemeId = schemeId }]);
        var controller = CreateController(repository);

        var rows = new List<MonthlyDistributionScheduleRowRequest>
        {
            new(schemeId, new DateTime(2026, 4, 1), new DateTime(2026, 4, 1), new DateTime(2026, 3, 30), new DateTime(2026, 4, 10), false),
        };

        var result = await controller.SaveSchedule(2026, 4, rows, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<MonthlyDistributionScheduleSaveResult>(ok.Value);
        Assert.False(response.Success);
        Assert.True(response.FieldErrorsBySchemeId.ContainsKey(schemeId));
    }
}
