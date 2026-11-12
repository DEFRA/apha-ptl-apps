using Microsoft.AspNetCore.Mvc;
using PTL.Api.Controllers;
using PTL.Contracts.Distribution;
using PTL.Core.Distribution;

namespace PTL.Api.Tests.Distribution;

public class DistributionControllerParticipantNilReturnTests
{
    private static readonly Guid SchemeId = Guid.NewGuid();
    private static readonly Guid ParticipantId = Guid.NewGuid();

    private static ParticipantNilReturnController CreateController(FakeDistributionRepository repository) =>
        new(new ParticipantNilReturnService(repository, new ActualResultsService(repository, TimeProvider.System), TimeProvider.System));

    [Fact]
    public async Task GetParticipantNilReturn_NotFound_ReturnsNotFound()
    {
        var controller = CreateController(new FakeDistributionRepository());

        var result = await controller.GetParticipantNilReturn(SchemeId, ParticipantId, CancellationToken.None);

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

        var result = await controller.SaveParticipantNilReturn(SchemeId, ParticipantId, new ParticipantNilReturnSaveRequest(""), CancellationToken.None);

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

        var controller = CreateController(repo);

        var result = await controller.SaveParticipantNilReturn(
            SchemeId,
            ParticipantId,
            new ParticipantNilReturnSaveRequest("No samples received"),
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
