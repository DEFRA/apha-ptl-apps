using Microsoft.AspNetCore.Mvc;
using PTL.Api.Controllers;
using PTL.Contracts.Distribution;
using PTL.Core.Distribution;

namespace PTL.Api.Tests.Distribution;

public class DistributionControllerSampleReceiptTests
{
    private static readonly Guid ParticipantId = Guid.NewGuid();
    private static readonly Guid SchemeId = Guid.NewGuid();
    private static readonly Guid TestId = Guid.NewGuid();
    private static readonly Guid SampleId = Guid.NewGuid();

    private static DistributionController CreateController(FakeDistributionRepository repository) =>
        new(
            new DistributionService(repository),
            new SampleNumbersService(repository, new FakeSchemeService()),
            new IntendedResultsService(repository),
            new DistributionParticipantsService(repository),
            new PreparationService(repository),
            new ResultsReceiptService(repository, TimeProvider.System),
            new ActualResultsService(repository, TimeProvider.System),
            new FakePackagingService(),
            new FakePackagingOutputService(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<DistributionController>.Instance);

    private static FakeDistributionRepository Seeded(DateTime? readyForAssessor = null)
    {
        var repository = new FakeDistributionRepository();
        repository.SampleReceipts[(ParticipantId, SchemeId)] = new SampleReceiptData(
            new SampleReceiptSchemeEntity
            {
                DistributionReference = "D1", ScheduleCode = "S1", SchemeIdentifier = "BT", SchemeName = "Brucella", IsReadyForAssessor = readyForAssessor,
            },
            new SampleReceiptLabEntity { LabCode = "L001", LabName = "Lab One" },
            [new SampleReceiptTestEntity { TestId = TestId, TestType = "Culture" }],
            [new SampleReceiptSampleEntity { SampleReceiptId = SampleId, TestId = TestId, SampleNumber = "S001", SubmissionDate = new DateTime(2026, 5, 1) }]);
        return repository;
    }

    [Fact]
    public async Task GetSampleReceipt_NotFound_ReturnsNotFound()
    {
        var result = await CreateController(new FakeDistributionRepository()).GetSampleReceipt(SchemeId, ParticipantId, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task GetSampleReceipt_ReturnsMappedResponse()
    {
        var result = await CreateController(Seeded()).GetSampleReceipt(SchemeId, ParticipantId, CancellationToken.None);

        var response = Assert.IsType<SampleReceiptResponse>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal("D1/S1 : BT Brucella", response.SchemeHeading);
        Assert.Equal("Labcode L001: Lab One", response.LabHeading);
        Assert.False(response.IsReadOnly);
        var test = Assert.Single(response.Tests);
        Assert.Equal((TestId, "Culture"), (test.TestId, test.TestType));
        var sample = Assert.Single(test.Samples);
        Assert.Equal((SampleId, "S001", true, false, false), (sample.SampleReceiptId, sample.SampleNumber, sample.Submitted, sample.NotSubmitted, sample.NotInRepertoire));
    }

    [Fact]
    public async Task SaveSampleReceipt_NotFound_ReturnsNotFound()
    {
        var result = await CreateController(new FakeDistributionRepository())
            .SaveSampleReceipt(SchemeId, ParticipantId, new SampleReceiptSaveRequest([]), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task SaveSampleReceipt_Saved_WritesTheChangeAndReturnsSuccess()
    {
        var repository = Seeded();

        var result = await CreateController(repository).SaveSampleReceipt(
            SchemeId, ParticipantId, new SampleReceiptSaveRequest([new SampleReceiptSampleUpdateRequest(SampleId, false, true, false)]), CancellationToken.None);

        var response = Assert.IsType<SampleReceiptSaveResult>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.True(response.Success);
        Assert.False(response.ReadOnly);
        Assert.Single(repository.UpdatedSampleReceipts);
    }

    [Fact]
    public async Task SaveSampleReceipt_ReadyForAssessor_ReturnsReadOnlyAndWritesNothing()
    {
        var repository = Seeded(readyForAssessor: new DateTime(2026, 5, 2));

        var result = await CreateController(repository).SaveSampleReceipt(
            SchemeId, ParticipantId, new SampleReceiptSaveRequest([new SampleReceiptSampleUpdateRequest(SampleId, false, true, false)]), CancellationToken.None);

        var response = Assert.IsType<SampleReceiptSaveResult>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.False(response.Success);
        Assert.True(response.ReadOnly);
        Assert.Empty(repository.UpdatedSampleReceipts);
    }
}
