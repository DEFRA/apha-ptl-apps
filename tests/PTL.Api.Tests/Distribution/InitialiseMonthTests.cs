using Microsoft.AspNetCore.Mvc;
using PTL.Contracts.Distribution;
using PTL.Core.Distribution;

namespace PTL.Api.Tests.Distribution;

// Legacy SetupMonthlyDistribution (MenuDistributions.aspx "Initialise this Month").
public class InitialiseMonthTests
{
    private static readonly Guid SchemeId = Guid.Parse("11111111-0000-0000-0000-000000000001");
    private static readonly Guid OtherSchemeId = Guid.Parse("11111111-0000-0000-0000-000000000002");

    private static MonthSetupSchemeRow SchemeRow(
        Guid? schemeId = null, int week = 2, int dayOffset = 3, int deadline = 14, bool commentsRequired = false, bool combinedPackaging = false, bool asAvailable = false) =>
        new()
        {
            SchemeId = schemeId ?? SchemeId,
            WeekNumber = week,
            DeadlineDays = deadline,
            CommentsRequired = commentsRequired,
            DistributionReference = 17152,
            ScheduleCode = "BA",
            DayOffset = dayOffset,
            SchemeName = "Mastitis bovine",
            SchemeIdentifier = "PT0060",
            NumberOfSamples = 4,
            IsAsAvailable = asAvailable,
            StoreRatings = true,
            CombinedPackaging = combinedPackaging,
            VersionDate = new DateTime(2025, 11, 3),
        };

    private static MonthSetupParticipantRow ParticipantRow(Guid? schemeId = null) =>
        new()
        {
            SchemeId = schemeId ?? SchemeId,
            ParticipantId = Guid.NewGuid(),
            LabCode = "L001",
            LabName = "Lab One",
            NumberOfSetsRequired = 2,
            ContractId = Guid.NewGuid(),
        };

    private static MonthSetupPlan Build(int year, int month, MonthSetupSchemeRow[] schemes, MonthSetupParticipantRow[]? participants = null) =>
        MonthlyDistributionPlanner.Build(year, month, schemes, participants ?? []);

    [Fact]
    public void Build_NullArguments_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => MonthlyDistributionPlanner.Build(2026, 5, null!, []));
        Assert.Throws<ArgumentNullException>(() => MonthlyDistributionPlanner.Build(2026, 5, [], null!));
    }

    [Fact]
    public void Build_SetsTheDatesFromTheFirstFridayWeekAndDayOffsets()
    {
        // 1 May 2026 is a Friday, so the first Monday is 4 May and the first Friday is 1 May.
        var plan = Build(2026, 5, [SchemeRow(week: 2, dayOffset: 3, deadline: 14)]);

        var scheme = Assert.Single(plan.Schemes);
        Assert.Equal(new DateTime(2026, 5, 11), scheme.DistributionDate);
        Assert.Equal(new DateTime(2026, 5, 11), scheme.OverseasPostingDate);
        Assert.Equal(new DateTime(2026, 5, 25), scheme.DeadlineDate);
        Assert.Equal(new DateTime(2026, 6, 1), scheme.ResultsIssueTargetDate);
    }

    [Fact]
    public void Build_ExternalCommentsRequired_AddsFourteenDaysToTheResultsDate()
    {
        var scheme = Assert.Single(Build(2026, 5, [SchemeRow(commentsRequired: true)]).Schemes);

        Assert.Equal(new DateTime(2026, 6, 15), scheme.ResultsIssueTargetDate);
    }

    [Theory]
    [InlineData(3, 5)]
    [InlineData(4, 5)]
    [InlineData(5, 12)]
    [InlineData(6, 12)]
    public void Build_CombinedPackaging_PostsOverseasOnTheTuesdayAndALaterPostingDayWaitsAWeek(int dayOffset, int overseasDayOfMay)
    {
        var scheme = Assert.Single(Build(2026, 5, [SchemeRow(week: 2, dayOffset: dayOffset, combinedPackaging: true)]).Schemes);

        Assert.Equal(new DateTime(2026, 5, overseasDayOfMay), scheme.OverseasPostingDate);
    }

    // Sunday, Monday and Tuesday starts exercise the "first Monday inside the month" rule.
    [Theory]
    [InlineData(2026, 5, 2026, 5, 1)]
    [InlineData(2026, 9, 2026, 9, 4)]
    [InlineData(2026, 2, 2026, 1, 30)]
    [InlineData(2026, 6, 2026, 5, 29)]
    public void Build_FirstFridayIsThreeDaysBeforeTheFirstMondayOfTheMonth(int year, int month, int expectedYear, int expectedMonth, int expectedDay)
    {
        var scheme = Assert.Single(Build(year, month, [SchemeRow(week: 1, dayOffset: 0)]).Schemes);

        Assert.Equal(new DateTime(expectedYear, expectedMonth, expectedDay), scheme.DistributionDate);
    }

    [Fact]
    public void Build_CopiesTheSchemeDetailsAndStartsEveryDistributionBlank()
    {
        var plan = Build(2026, 5, [SchemeRow()]);

        Assert.NotEqual(Guid.Empty, plan.MonthlyDistributionId);
        Assert.Equal((2026, 5), (plan.YearId, plan.MonthId));
        var scheme = Assert.Single(plan.Schemes);
        Assert.Equal(plan.MonthlyDistributionId, scheme.MonthlyDistributionId);
        Assert.NotEqual(Guid.Empty, scheme.MonthlyDistributionSchemeId);
        Assert.Equal(SchemeId, scheme.SchemeId);
        Assert.Equal(("17152", string.Empty, "BA"), (scheme.DistributionReference, scheme.DistributionReferenceSuffix, scheme.ScheduleCode));
        Assert.Equal(("PT0060", "Mastitis bovine"), (scheme.SchemeIdentifier, scheme.SchemeName));
        Assert.Equal(string.Empty, scheme.Comments);
        Assert.False(scheme.HasIntendedResults);
        Assert.False(scheme.HasSampleNumbersDefined);
        Assert.False(scheme.IsCancelled);
        Assert.False(scheme.IsAsAvailable);
        Assert.True(scheme.StoreRatings);
        Assert.Equal(new DateTime(2025, 11, 3), scheme.SchemeVersionDate);
    }

    [Fact]
    public void Build_AsAvailableScheme_StartsCancelled()
    {
        var scheme = Assert.Single(Build(2026, 5, [SchemeRow(asAvailable: true)]).Schemes);

        Assert.True(scheme.IsAsAvailable);
        Assert.True(scheme.IsCancelled);
    }

    [Fact]
    public void Build_AColumnReturnedAsNull_FallsBackToTheTypeDefaultAsLegacySafeDataReaderDid()
    {
        var plan = Build(2026, 5, [new MonthSetupSchemeRow { SchemeId = SchemeId }], [new MonthSetupParticipantRow { SchemeId = SchemeId }]);

        var scheme = Assert.Single(plan.Schemes);
        Assert.Equal(("0", string.Empty, string.Empty, string.Empty), (scheme.DistributionReference, scheme.ScheduleCode, scheme.SchemeIdentifier, scheme.SchemeName));
        Assert.False(scheme.IsCancelled);
        Assert.False(scheme.StoreRatings);
        Assert.Equal(default, scheme.SchemeVersionDate);
        var participant = Assert.Single(plan.Participants);
        Assert.Equal((string.Empty, string.Empty, 0), (participant.LabCode, participant.LabName, participant.NumberOfSetsRequired));
    }

    [Fact]
    public void Build_Participants_JoinTheirSchemeWithNoOwnDeadlineAndAreNotAdditional()
    {
        var plan = Build(2026, 5, [SchemeRow(SchemeId), SchemeRow(OtherSchemeId, week: 3)], [ParticipantRow(SchemeId), ParticipantRow(OtherSchemeId), ParticipantRow(Guid.NewGuid())]);

        Assert.Equal(2, plan.Participants.Count);
        var first = plan.Participants[0];
        var owner = plan.Schemes.Single(s => s.SchemeId == SchemeId);
        Assert.Equal(owner.MonthlyDistributionSchemeId, first.MonthlyDistributionSchemeId);
        Assert.Null(first.DeadlineDate);
        Assert.Equal(owner.DeadlineDate, first.SchemeDeadlineDate);
        Assert.Equal(string.Empty, first.DeadlineChangeReason);
        Assert.False(first.AdditionalParticipant);
        Assert.Equal(("L001", "Lab One", 2), (first.LabCode, first.LabName, first.NumberOfSetsRequired));
        Assert.Equal(plan.Schemes.Single(s => s.SchemeId == OtherSchemeId).MonthlyDistributionSchemeId, plan.Participants[1].MonthlyDistributionSchemeId);
    }

    [Fact]
    public async Task DistributionService_InitialiseMonthAsync_PlansThroughTheRepositoryAndReportsWhetherItWasInitialised()
    {
        var repository = new FakeDistributionRepository();
        repository.SetupSchemeRows.Add(SchemeRow());
        repository.SetupParticipantRows.Add(ParticipantRow());
        var service = new DistributionService(repository);

        Assert.True(await service.InitialiseMonthAsync(2026, 5));
        Assert.Equal([(2026, 5)], repository.InitialisedMonths);
        Assert.Equal((2026, 5), (repository.LastMonthPlan!.YearId, repository.LastMonthPlan.MonthId));
        Assert.Single(repository.LastMonthPlan.Schemes);
        Assert.Single(repository.LastMonthPlan.Participants);

        repository.InitialiseMonthResult = false;
        Assert.False(await service.InitialiseMonthAsync(2026, 5));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Api_InitialiseMonth_ReportsWhetherTheMonthWasInitialised(bool initialised)
    {
        var repository = new FakeDistributionRepository { InitialiseMonthResult = initialised };
        var controller = new PTL.Api.Controllers.DistributionController(
            new DistributionService(repository),
            new SampleNumbersService(repository, new FakeSchemeService()),
            new IntendedResultsService(repository),
            new DistributionParticipantsService(repository),
            new PreparationService(repository),
            new ResultsReceiptService(repository, TimeProvider.System),
            new TabulationsService(repository, TimeProvider.System),
            new ActualResultsService(repository, TimeProvider.System),
            new ParticipantDistributionHistoryService(repository, new PTL.Api.Tests.Participant.FakeParticipantRepository(), new ActualResultsService(repository, TimeProvider.System)),
            new FakePackagingService(),
            new FakePackagingOutputService(),
            new PTL.Api.Tests.Contract.FakeAuditActorResolver("Guest"),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<PTL.Api.Controllers.DistributionController>.Instance);

        var result = await controller.InitialiseMonth(2026, 5, CancellationToken.None);

        var response = Assert.IsType<InitialiseMonthResult>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal(initialised, response.Initialised);
        Assert.Equal([(2026, 5)], repository.InitialisedMonths);
    }

    [Theory]
    [InlineData(2026, 0)]
    [InlineData(2026, 13)]
    [InlineData(0, 5)]
    public async Task Api_InitialiseMonth_InvalidMonth_ReturnsBadRequestAndWritesNothing(int year, int month)
    {
        var repository = new FakeDistributionRepository();
        var controller = new PTL.Api.Controllers.DistributionController(
            new DistributionService(repository),
            new SampleNumbersService(repository, new FakeSchemeService()),
            new IntendedResultsService(repository),
            new DistributionParticipantsService(repository),
            new PreparationService(repository),
            new ResultsReceiptService(repository, TimeProvider.System),
            new TabulationsService(repository, TimeProvider.System),
            new ActualResultsService(repository, TimeProvider.System),
            new ParticipantDistributionHistoryService(repository, new PTL.Api.Tests.Participant.FakeParticipantRepository(), new ActualResultsService(repository, TimeProvider.System)),
            new FakePackagingService(),
            new FakePackagingOutputService(),
            new PTL.Api.Tests.Contract.FakeAuditActorResolver("Guest"),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<PTL.Api.Controllers.DistributionController>.Instance);

        var result = await controller.InitialiseMonth(year, month, CancellationToken.None);

        Assert.IsType<BadRequestResult>(result.Result);
        Assert.Empty(repository.InitialisedMonths);
    }
}
