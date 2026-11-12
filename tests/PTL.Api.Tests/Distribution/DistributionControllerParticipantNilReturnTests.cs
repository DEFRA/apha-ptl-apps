using Microsoft.AspNetCore.Mvc;
using PTL.Api.Controllers;
using PTL.Api.Tests.Participant;
using PTL.Contracts.Distribution;
using PTL.Core.Distribution;
using PTL.Core.Participant;

namespace PTL.Api.Tests.Distribution;

public class DistributionControllerParticipantNilReturnTests
{
    private static readonly Guid SchemeId = Guid.NewGuid();
    private static readonly Guid ParticipantId = Guid.NewGuid();

    internal static DistributionController CreateController(FakeDistributionRepository repository, FakeParticipantRepository? participantRepository = null)
    {
        participantRepository ??= new FakeParticipantRepository();

        return new DistributionController(
            new DistributionService(repository),
            new SampleNumbersService(repository, new FakeSchemeService()),
            new IntendedResultsService(repository),
            new DistributionParticipantsService(repository),
            new PreparationService(repository),
            new ResultsReceiptService(repository, TimeProvider.System),
            new TabulationsService(repository, TimeProvider.System),
            new ActualResultsService(repository, TimeProvider.System),
            new ParticipantDistributionHistoryService(repository, participantRepository, new ActualResultsService(repository, TimeProvider.System)),
            new FakePackagingService(),
            new FakePackagingOutputService(),
            new PTL.Api.Tests.Contract.FakeAuditActorResolver("Guest"),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<DistributionController>.Instance);
    }

    [Fact]
    public async Task GetParticipantNilReturn_NotFound_ReturnsNotFound()
    {
        var repo = new FakeDistributionRepository();
        var controller = CreateController(repo);

        var result = await controller.GetParticipantNilReturn(SchemeId, ParticipantId, new ParticipantNilReturnService(repo, new ActualResultsService(repo, TimeProvider.System), TimeProvider.System), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task SaveParticipantNilReturn_ReasonRequired_ReturnsValidationFlags()
    {
        var repo = new FakeDistributionRepository();
        repo.ActualResults[ParticipantId] = new ActualResultsData(
            new ActualResultsHeaderEntity { Distribution = "D1/S1", SchemeIdentifier = "BT", SchemeName = "Brucella", DeadlineDate = DateTime.UtcNow.AddDays(1), FirstEditDate = null },
            [],
            []);
        repo.IntendedResults[SchemeId] = new ActualResultsScenario().Structure();

        var controller = CreateController(repo);
        var service = new ParticipantNilReturnService(repo, new ActualResultsService(repo, TimeProvider.System), TimeProvider.System);

        var result = await controller.SaveParticipantNilReturn(SchemeId, ParticipantId, new ParticipantNilReturnSaveRequest(""), service, CancellationToken.None);

        var payload = Assert.IsType<ParticipantNilReturnSaveResult>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.False(payload.Success);
        Assert.True(payload.ReasonRequired);
    }

    [Fact]
    public async Task SaveParticipantNilReturn_Success_StampsSampleReceiptAsNotSubmitted_AndClearsOtherStatuses()
    {
        var expectedToday = TimeProvider.System.GetLocalNow().Date;

        var repo = new FakeDistributionRepository();
        repo.ActualResults[ParticipantId] = new ActualResultsData(
            new ActualResultsHeaderEntity { Distribution = "D1/S1", SchemeIdentifier = "BT", SchemeName = "Brucella", DeadlineDate = expectedToday.AddDays(5), FirstEditDate = null },
            [],
            []);
        repo.IntendedResults[SchemeId] = new ActualResultsScenario().Structure();

        var sample1 = new SampleReceiptSampleEntity
        {
            SampleReceiptId = Guid.NewGuid(),
            TestId = Guid.NewGuid(),
            SampleNumber = "S1",
            SubmissionDate = expectedToday.AddDays(-1),
            NonSubmissionDate = null,
            NonCapableDate = null
        };
        var sample2 = new SampleReceiptSampleEntity
        {
            SampleReceiptId = Guid.NewGuid(),
            TestId = Guid.NewGuid(),
            SampleNumber = "S2",
            SubmissionDate = null,
            NonSubmissionDate = null,
            NonCapableDate = expectedToday.AddDays(-2)
        };

        repo.SampleReceipts[(ParticipantId, SchemeId)] = new SampleReceiptData(
            new SampleReceiptSchemeEntity
            {
                DistributionReference = "D1",
                DistributionReferenceSuffix = "a",
                ScheduleCode = "S1",
                SchemeName = "Brucella",
                SchemeIdentifier = "BT",
                IsReadyForAssessor = null
            },
            new SampleReceiptLabEntity { LabCode = "L1", LabName = "Lab 1" },
            [new SampleReceiptTestEntity { TestId = sample1.TestId, TestType = "Test" }],
            [sample1, sample2]);

        var service = new ParticipantNilReturnService(repo, new ActualResultsService(repo, TimeProvider.System), TimeProvider.System);
        var controller = CreateController(repo);

        var result = await controller.SaveParticipantNilReturn(
            SchemeId,
            ParticipantId,
            new ParticipantNilReturnSaveRequest("No samples received"),
            service,
            CancellationToken.None);

        var payload = Assert.IsType<ParticipantNilReturnSaveResult>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.True(payload.Success);

        Assert.Equal(2, repo.UpdatedSampleReceipts.Count);
        Assert.All(repo.UpdatedSampleReceipts, s =>
        {
            Assert.Null(s.SubmissionDate);
            Assert.Null(s.NonCapableDate);
            Assert.Equal(expectedToday, s.NonSubmissionDate);
        });
    }
}
