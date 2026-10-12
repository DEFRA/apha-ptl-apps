using PTL.Core.Distribution;

namespace PTL.Api.Tests.Distribution;

public class DistributionDashboardBuilderTests
{
    [Fact]
    public void Build_NoDistributionsEverInitialised_EveryMonthIsInitialisablePlaceholder()
    {
        var result = DistributionDashboardBuilder.Build([], financialYearId: 2025);

        Assert.Equal(12, result.Count);
        Assert.All(result, month =>
        {
            Assert.Null(month.MonthlyDistributionId);
            Assert.True(month.IsInitialisable);
        });

        // April -> March ordering, first/last calendar years.
        Assert.Equal(2025, result[0].YearId);
        Assert.Equal(4, result[0].MonthId);
        Assert.Equal(2026, result[^1].YearId);
        Assert.Equal(3, result[^1].MonthId);
    }

    [Fact]
    public void Build_AprilInitialised_MayBecomesInitialisableButNotJune()
    {
        var april = new DistributionMonthSummaryEntity
        {
            MonthlyDistributionId = Guid.NewGuid(),
            YearId = 2025,
            MonthId = 4,
        };

        var result = DistributionDashboardBuilder.Build([april], financialYearId: 2025);

        var aprilRow = result.Single(m => m.MonthId == 4 && m.YearId == 2025);
        Assert.Equal(april.MonthlyDistributionId, aprilRow.MonthlyDistributionId);
        Assert.False(aprilRow.IsInitialisable);

        var mayRow = result.Single(m => m.MonthId == 5 && m.YearId == 2025);
        Assert.Null(mayRow.MonthlyDistributionId);
        Assert.True(mayRow.IsInitialisable);

        var juneRow = result.Single(m => m.MonthId == 6 && m.YearId == 2025);
        Assert.Null(juneRow.MonthlyDistributionId);
        Assert.False(juneRow.IsInitialisable);
    }

    [Fact]
    public void Build_MarchInitialised_AprilNextFinancialYearBecomesInitialisable()
    {
        // March belongs to calendar year 2026 even though it is the last month of the 2025
        // financial year - exercises the nMonth=15 wrap-around key arithmetic.
        var march = new DistributionMonthSummaryEntity
        {
            MonthlyDistributionId = Guid.NewGuid(),
            YearId = 2026,
            MonthId = 3,
        };

        var result = DistributionDashboardBuilder.Build([march], financialYearId: 2025);

        var marchRow = result.Single(m => m.MonthId == 3 && m.YearId == 2026);
        Assert.False(marchRow.IsInitialisable);
        Assert.Equal(march.MonthlyDistributionId, marchRow.MonthlyDistributionId);
    }

    [Fact]
    public void Build_UnrelatedHistoricMonthInitialised_DoesNotUnlockFinancialYear()
    {
        var unrelated = new DistributionMonthSummaryEntity
        {
            MonthlyDistributionId = Guid.NewGuid(),
            YearId = 2020,
            MonthId = 7,
        };

        var result = DistributionDashboardBuilder.Build([unrelated], financialYearId: 2025);

        Assert.All(result, month => Assert.False(month.IsInitialisable));
    }

    [Theory]
    [InlineData(0, 0, 100)]
    [InlineData(5, 10, 50)]
    [InlineData(10, 10, 100)]
    public void SchedulingCompletePercentage_MatchesLegacyDivisionRule(int defined, int schemes, decimal expected)
    {
        var summary = new DistributionMonthSummaryEntity { NumberOfSampleNumbersDefined = defined, NumberOfSchemes = schemes };
        Assert.Equal(expected, summary.SchedulingCompletePercentage());
    }

    [Fact]
    public void TabulationsCompletePercentage_ZeroTabulationsAndZeroSchemes_Returns100()
    {
        var summary = new DistributionMonthSummaryEntity { NumberOfTabulations = 0, NumberOfSchemes = 0 };
        Assert.Equal(100m, summary.TabulationsCompletePercentage());
    }

    [Fact]
    public void TabulationsCompletePercentage_ZeroTabulationsButSchemesExist_ReturnsZero()
    {
        var summary = new DistributionMonthSummaryEntity { NumberOfTabulations = 0, NumberOfSchemes = 3 };
        Assert.Equal(0m, summary.TabulationsCompletePercentage());
    }

    [Fact]
    public void TabulationsCompletePercentage_SomeTabulationsComplete_ComputesRatio()
    {
        var summary = new DistributionMonthSummaryEntity { NumberOfTabulations = 4, NumberOfCompleteTabulations = 1, NumberOfSchemes = 4 };
        Assert.Equal(25m, summary.TabulationsCompletePercentage());
    }
}
