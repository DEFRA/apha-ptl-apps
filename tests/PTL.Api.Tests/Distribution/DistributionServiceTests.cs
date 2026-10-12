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

    [Fact]
    public async Task SaveMonthlyDistributionScheduleAsync_AnyRowInvalid_PersistsNothing()
    {
        var monthlyDistributionId = Guid.NewGuid();
        var schemeId = Guid.NewGuid();
        var repository = new FakeDistributionRepository();
        repository.Schedules[(2026, 4)] = (monthlyDistributionId, [new MonthlyDistributionSchemeEntity { MonthlyDistributionSchemeId = schemeId }]);
        var service = new DistributionService(repository);

        var rows = new List<MonthlyDistributionScheduleRowUpdate>
        {
            new(schemeId, new DateTime(2026, 4, 1), new DateTime(2026, 4, 1), new DateTime(2026, 3, 30), new DateTime(2026, 4, 10), false),
        };

        var outcome = await service.SaveMonthlyDistributionScheduleAsync(2026, 4, rows);

        Assert.False(outcome.Success);
        Assert.True(outcome.FieldErrorsBySchemeId.ContainsKey(schemeId));
        Assert.Empty(repository.UpdatedSchemes);
    }

    [Fact]
    public async Task SaveMonthlyDistributionScheduleAsync_AllRowsValid_RoundTripsUnchangedFieldsAndPersists()
    {
        var monthlyDistributionId = Guid.NewGuid();
        var schemeId = Guid.NewGuid();
        var repository = new FakeDistributionRepository();
        repository.Schedules[(2026, 4)] = (monthlyDistributionId, [new MonthlyDistributionSchemeEntity
        {
            MonthlyDistributionSchemeId = schemeId,
            Comments = "Existing comments",
            HasIntendedResults = true,
            StoreRatings = true,
        }]);
        var service = new DistributionService(repository);

        var rows = new List<MonthlyDistributionScheduleRowUpdate>
        {
            new(schemeId, new DateTime(2026, 4, 1), new DateTime(2026, 4, 1), new DateTime(2026, 4, 5), new DateTime(2026, 4, 10), true),
        };

        var outcome = await service.SaveMonthlyDistributionScheduleAsync(2026, 4, rows);

        Assert.True(outcome.Success);
        var updated = Assert.Single(repository.UpdatedSchemes);
        Assert.Equal(new DateTime(2026, 4, 1), updated.DistributionDate);
        Assert.Equal(new DateTime(2026, 4, 5), updated.DeadlineDate);
        Assert.True(updated.IsCancelled);
        Assert.Equal("Existing comments", updated.Comments);
        Assert.True(updated.HasIntendedResults);
        Assert.True(updated.StoreRatings);
    }

    [Fact]
    public async Task SaveMonthlyDistributionScheduleAsync_RowForUnknownScheme_IsSkipped()
    {
        var knownSchemeId = Guid.NewGuid();
        var repository = new FakeDistributionRepository();
        repository.Schedules[(2026, 4)] = (Guid.NewGuid(), [new MonthlyDistributionSchemeEntity { MonthlyDistributionSchemeId = knownSchemeId }]);
        var service = new DistributionService(repository);

        var rows = new List<MonthlyDistributionScheduleRowUpdate>
        {
            new(knownSchemeId, new DateTime(2026, 4, 1), new DateTime(2026, 4, 1), new DateTime(2026, 4, 5), new DateTime(2026, 4, 10), false),
            new(Guid.NewGuid(), new DateTime(2026, 4, 1), new DateTime(2026, 4, 1), new DateTime(2026, 4, 5), new DateTime(2026, 4, 10), false),
        };

        var outcome = await service.SaveMonthlyDistributionScheduleAsync(2026, 4, rows);

        Assert.True(outcome.Success);
        Assert.Equal(knownSchemeId, Assert.Single(repository.UpdatedSchemes).MonthlyDistributionSchemeId);
    }

    [Fact]
    public async Task SaveMonthlyDistributionScheduleAsync_MonthNotInitialised_ReturnsUnsuccessful()
    {
        var repository = new FakeDistributionRepository();
        var service = new DistributionService(repository);

        var outcome = await service.SaveMonthlyDistributionScheduleAsync(2026, 4, [
            new MonthlyDistributionScheduleRowUpdate(Guid.NewGuid(), new DateTime(2026, 4, 1), new DateTime(2026, 4, 1), new DateTime(2026, 4, 5), new DateTime(2026, 4, 10), false),
        ]);

        Assert.False(outcome.Success);
        Assert.Empty(repository.UpdatedSchemes);
    }
}
