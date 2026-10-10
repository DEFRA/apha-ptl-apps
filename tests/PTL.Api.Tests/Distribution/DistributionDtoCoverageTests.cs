using PTL.Contracts.Distribution;
using PTL.Core.Distribution;

namespace PTL.Api.Tests.Distribution;

// Reads every property getter on the Distribution wire DTOs and Dapper entities. Coverlet only
// counts a property as covered when its getter actually runs, so constructing one is not enough -
// same pattern as Contracts/SharedDtoCoverageTests.
public class DistributionDtoCoverageTests
{
    [Fact]
    public void MonthlyDistributionResponse_ExposesEveryProperty()
    {
        var monthlyDistributionId = Guid.NewGuid();
        var schemeId = Guid.NewGuid();
        var monthlyDistributionSchemeId = Guid.NewGuid();

        var scheme = new MonthlyDistributionSchemeResponse(
            MonthlyDistributionSchemeId: monthlyDistributionSchemeId,
            SchemeId: schemeId,
            SchemeIdentifier: "PT0060",
            SchemeName: "Mastitis bovine",
            DistributionReferenceFull: "17152/BA",
            DistributionDate: new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc),
            OverseasPostingDate: new DateTime(2026, 4, 2, 0, 0, 0, DateTimeKind.Utc),
            DeadlineDate: new DateTime(2026, 4, 5, 0, 0, 0, DateTimeKind.Utc),
            ResultsIssueTargetDate: new DateTime(2026, 4, 10, 0, 0, 0, DateTimeKind.Utc),
            ParticipantCount: 4,
            TotalSetsOfSamplesRequired: 9,
            HasSampleNumbersDefined: true,
            HasIntendedResults: true,
            IsCancelled: false,
            IsAsAvailable: true);

        var response = new MonthlyDistributionResponse(monthlyDistributionId, 2026, 4, [scheme]);

        Assert.Equal(monthlyDistributionId, response.MonthlyDistributionId);
        Assert.Equal(2026, response.YearId);
        Assert.Equal(4, response.MonthId);
        Assert.Equal(monthlyDistributionSchemeId, Assert.Single(response.Schemes).MonthlyDistributionSchemeId);
        Assert.Equal(schemeId, scheme.SchemeId);
        Assert.Equal("PT0060", scheme.SchemeIdentifier);
        Assert.Equal("Mastitis bovine", scheme.SchemeName);
        Assert.Equal("17152/BA", scheme.DistributionReferenceFull);
        Assert.Equal(new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc), scheme.DistributionDate);
        Assert.Equal(new DateTime(2026, 4, 2, 0, 0, 0, DateTimeKind.Utc), scheme.OverseasPostingDate);
        Assert.Equal(new DateTime(2026, 4, 5, 0, 0, 0, DateTimeKind.Utc), scheme.DeadlineDate);
        Assert.Equal(new DateTime(2026, 4, 10, 0, 0, 0, DateTimeKind.Utc), scheme.ResultsIssueTargetDate);
        Assert.Equal(4, scheme.ParticipantCount);
        Assert.Equal(9, scheme.TotalSetsOfSamplesRequired);
        Assert.True(scheme.HasSampleNumbersDefined);
        Assert.True(scheme.HasIntendedResults);
        Assert.False(scheme.IsCancelled);
        Assert.True(scheme.IsAsAvailable);
    }

    [Fact]
    public void MonthlyDistributionScheduleRequestAndResult_ExposeEveryProperty()
    {
        var schemeId = Guid.NewGuid();

        var row = new MonthlyDistributionScheduleRowRequest(
            MonthlyDistributionSchemeId: schemeId,
            DistributionDate: new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc),
            OverseasPostingDate: new DateTime(2026, 4, 2, 0, 0, 0, DateTimeKind.Utc),
            DeadlineDate: new DateTime(2026, 4, 5, 0, 0, 0, DateTimeKind.Utc),
            ResultsIssueTargetDate: new DateTime(2026, 4, 10, 0, 0, 0, DateTimeKind.Utc),
            IsCancelled: true);

        Assert.Equal(schemeId, row.MonthlyDistributionSchemeId);
        Assert.Equal(new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc), row.DistributionDate);
        Assert.Equal(new DateTime(2026, 4, 2, 0, 0, 0, DateTimeKind.Utc), row.OverseasPostingDate);
        Assert.Equal(new DateTime(2026, 4, 5, 0, 0, 0, DateTimeKind.Utc), row.DeadlineDate);
        Assert.Equal(new DateTime(2026, 4, 10, 0, 0, 0, DateTimeKind.Utc), row.ResultsIssueTargetDate);
        Assert.True(row.IsCancelled);

        var result = new MonthlyDistributionScheduleSaveResult(
            false, new Dictionary<Guid, string[]> { [schemeId] = ["Deadline Date needs to be after the UK Posting date."] });

        Assert.False(result.Success);
        Assert.Equal("Deadline Date needs to be after the UK Posting date.", Assert.Single(result.FieldErrorsBySchemeId[schemeId]));
    }

    [Fact]
    public void DistributionDashboardResponses_ExposeEveryProperty()
    {
        var monthlyDistributionId = Guid.NewGuid();
        var month = new DistributionDashboardMonthResponse(
            YearId: 2026,
            MonthId: 4,
            MonthDescription: "Apr",
            MonthlyDistributionId: monthlyDistributionId,
            IsInitialisable: false,
            SchedulingCompletePercentage: 50m,
            PreparationCompletePercentage: 25m,
            PackagingCompletePercentage: 10m,
            ResultsCompletePercentage: 5m,
            TabulationsCompletePercentage: 1m);

        Assert.Equal(2026, month.YearId);
        Assert.Equal(4, month.MonthId);
        Assert.Equal("Apr", month.MonthDescription);
        Assert.Equal(monthlyDistributionId, month.MonthlyDistributionId);
        Assert.False(month.IsInitialisable);
        Assert.Equal(50m, month.SchedulingCompletePercentage);
        Assert.Equal(25m, month.PreparationCompletePercentage);
        Assert.Equal(10m, month.PackagingCompletePercentage);
        Assert.Equal(5m, month.ResultsCompletePercentage);
        Assert.Equal(1m, month.TabulationsCompletePercentage);

        var year = new DistributionYearOptionResponse(2026, "2026/27");
        Assert.Equal(2026, year.YearId);
        Assert.Equal("2026/27", year.Label);
    }

    [Fact]
    public void DistributionEntities_ExposeEveryProperty()
    {
        var monthlyDistributionId = Guid.NewGuid();
        var summary = new DistributionMonthSummaryEntity
        {
            MonthlyDistributionId = monthlyDistributionId,
            YearId = 2026,
            MonthId = 4,
            NumberOfSchemes = 10,
            NumberOfSampleNumbersDefined = 9,
            NumberOfPrepComplete = 8,
            NumberOfParticipants = 7,
            NumberOfPackagingComplete = 6,
            NumberOfResultsEntered = 5,
            NumberOfTabulations = 4,
            NumberOfCompleteTabulations = 3,
        };

        Assert.Equal(monthlyDistributionId, summary.MonthlyDistributionId);
        Assert.Equal(2026, summary.YearId);
        Assert.Equal(4, summary.MonthId);
        Assert.Equal(10, summary.NumberOfSchemes);
        Assert.Equal(9, summary.NumberOfSampleNumbersDefined);
        Assert.Equal(8, summary.NumberOfPrepComplete);
        Assert.Equal(7, summary.NumberOfParticipants);
        Assert.Equal(6, summary.NumberOfPackagingComplete);
        Assert.Equal(5, summary.NumberOfResultsEntered);
        Assert.Equal(4, summary.NumberOfTabulations);
        Assert.Equal(3, summary.NumberOfCompleteTabulations);

        var monthYear = new DistributionMonthYearEntity { YearId = 2026, MonthId = 4 };
        Assert.Equal(2026, monthYear.YearId);
        Assert.Equal(4, monthYear.MonthId);
    }

    [Fact]
    public void MonthlyDistributionSchemeEntity_ExposesEveryProperty()
    {
        var monthlyDistributionSchemeId = Guid.NewGuid();
        var monthlyDistributionId = Guid.NewGuid();
        var schemeId = Guid.NewGuid();

        var entity = new MonthlyDistributionSchemeEntity
        {
            MonthlyDistributionSchemeId = monthlyDistributionSchemeId,
            MonthlyDistributionId = monthlyDistributionId,
            SchemeId = schemeId,
            SchemeIdentifier = "PT0060",
            SchemeName = "Mastitis bovine",
            DistributionReference = "17152",
            DistributionReferenceSuffix = "A",
            ScheduleCode = "BA",
            DistributionDate = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc),
            OverseasPostingDate = new DateTime(2026, 4, 2, 0, 0, 0, DateTimeKind.Utc),
            DeadlineDate = new DateTime(2026, 4, 5, 0, 0, 0, DateTimeKind.Utc),
            ResultsIssueTargetDate = new DateTime(2026, 4, 10, 0, 0, 0, DateTimeKind.Utc),
            Comments = "Some comments",
            HasIntendedResults = true,
            HasSampleNumbersDefined = true,
            IsCancelled = true,
            IsAsAvailable = true,
            StoreRatings = true,
            SchemeVersionDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            ParticipantCount = 4,
            TotalSetsOfSamplesRequired = 9,
        };

        Assert.Equal(monthlyDistributionSchemeId, entity.MonthlyDistributionSchemeId);
        Assert.Equal(monthlyDistributionId, entity.MonthlyDistributionId);
        Assert.Equal(schemeId, entity.SchemeId);
        Assert.Equal("PT0060", entity.SchemeIdentifier);
        Assert.Equal("Mastitis bovine", entity.SchemeName);
        Assert.Equal("17152", entity.DistributionReference);
        Assert.Equal("A", entity.DistributionReferenceSuffix);
        Assert.Equal("BA", entity.ScheduleCode);
        Assert.Equal(new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc), entity.DistributionDate);
        Assert.Equal(new DateTime(2026, 4, 2, 0, 0, 0, DateTimeKind.Utc), entity.OverseasPostingDate);
        Assert.Equal(new DateTime(2026, 4, 5, 0, 0, 0, DateTimeKind.Utc), entity.DeadlineDate);
        Assert.Equal(new DateTime(2026, 4, 10, 0, 0, 0, DateTimeKind.Utc), entity.ResultsIssueTargetDate);
        Assert.Equal("Some comments", entity.Comments);
        Assert.True(entity.HasIntendedResults);
        Assert.True(entity.HasSampleNumbersDefined);
        Assert.True(entity.IsCancelled);
        Assert.True(entity.IsAsAvailable);
        Assert.True(entity.StoreRatings);
        Assert.Equal(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), entity.SchemeVersionDate);
        Assert.Equal(4, entity.ParticipantCount);
        Assert.Equal(9, entity.TotalSetsOfSamplesRequired);
        Assert.Equal("17152A/BA", entity.DistributionReferenceFull);

        var participant = new MonthlyDistributionParticipantAggregateEntity
        {
            DistributionSchemeId = monthlyDistributionSchemeId,
            NumberOfSetsRequired = 3,
        };

        Assert.Equal(monthlyDistributionSchemeId, participant.DistributionSchemeId);
        Assert.Equal(3, participant.NumberOfSetsRequired);
    }
}
