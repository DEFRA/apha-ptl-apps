using PTL.Core.Distribution;

namespace PTL.Api.Tests.Distribution;

public class DistributionYearOptionsBuilderTests
{
    [Fact]
    public void Build_NoExistingDistributions_ReturnsCurrentAndFutureYearOnly()
    {
        var result = DistributionYearOptionsBuilder.Build([], now: new DateTime(2026, 6, 1));

        Assert.Equal(2, result.Count);
        Assert.Equal(2026, result[0].YearId);
        Assert.Equal(2027, result[1].YearId);
    }

    [Fact]
    public void Build_BeforeApril_CurrentFinancialYearIsPreviousCalendarYear()
    {
        var result = DistributionYearOptionsBuilder.Build([], now: new DateTime(2026, 2, 1));

        Assert.Contains(result, y => y.YearId == 2025);
    }

    [Fact]
    public void Build_ExistingDistributionYears_DeduplicatedAndLabelled()
    {
        var rows = new[]
        {
            new DistributionMonthYearEntity { YearId = 2026, MonthId = 6 },
            new DistributionMonthYearEntity { YearId = 2026, MonthId = 7 }, // same financial year as the row above
            new DistributionMonthYearEntity { YearId = 2026, MonthId = 2 }, // calendar 2026 but financial year 2025
        };

        var result = DistributionYearOptionsBuilder.Build(rows, now: new DateTime(2026, 6, 1));

        Assert.Equal(["2026/27", "2025/26"], result.Take(2).Select(y => y.Label));
    }

    [Fact]
    public void GetDefaultFinancialYearId_MarchBelongsToPreviousFinancialYear()
    {
        Assert.Equal(2025, DistributionYearOptionsBuilder.GetDefaultFinancialYearId(new DateTime(2026, 3, 31)));
        Assert.Equal(2026, DistributionYearOptionsBuilder.GetDefaultFinancialYearId(new DateTime(2026, 4, 1)));
    }
}
