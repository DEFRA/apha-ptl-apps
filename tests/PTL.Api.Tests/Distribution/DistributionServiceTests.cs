using PTL.Core.Distribution;

namespace PTL.Api.Tests.Distribution;

public class DistributionServiceTests
{
    [Fact]
    public async Task GetDashboardAsync_DelegatesToBuilder()
    {
        var repository = new FakeDistributionRepository();
        repository.Summaries.Add(new DistributionMonthSummaryEntity { MonthlyDistributionId = Guid.NewGuid(), YearId = 2026, MonthId = 4 });
        var service = new DistributionService(repository);

        var result = await service.GetDashboardAsync(2026);

        Assert.Equal(12, result.Count);
        Assert.NotNull(result.Single(m => m.MonthId == 4 && m.YearId == 2026).MonthlyDistributionId);
    }

    [Fact]
    public async Task GetYearOptionsAsync_DelegatesToBuilder()
    {
        var repository = new FakeDistributionRepository();
        repository.Years.Add(new DistributionMonthYearEntity { YearId = 2026, MonthId = 6 });
        var service = new DistributionService(repository);

        var result = await service.GetYearOptionsAsync();

        Assert.Contains(result, y => y.YearId == 2026);
    }
}
