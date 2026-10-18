using Microsoft.AspNetCore.Mvc;
using PTL.Api.Controllers;
using PTL.Contracts.Distribution;
using PTL.Core.Distribution;

namespace PTL.Api.Tests.Distribution;

public class DistributionControllerResultsReceiptTests
{
    private static readonly DateTime SchemeDeadline = new(2026, 6, 30);
    private static readonly Guid PostedParticipant = Guid.NewGuid();
    private static readonly Guid UnpostedParticipant = Guid.NewGuid();
    private static readonly Guid SchemeId = Guid.NewGuid();

    private static DistributionController CreateController(FakeDistributionRepository repository) =>
        new(
            new DistributionService(repository),
            new SampleNumbersService(repository, new FakeSchemeService()),
            new IntendedResultsService(repository),
            new DistributionParticipantsService(repository),
            new PreparationService(repository),
            new ResultsReceiptService(repository, TimeProvider.System),
            new TabulationsService(repository, TimeProvider.System),
            new ActualResultsService(repository, TimeProvider.System),
            new FakePackagingService(),
            new FakePackagingOutputService(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<DistributionController>.Instance);

    private static FakeDistributionRepository Seeded()
    {
        var repository = new FakeDistributionRepository();
        repository.ResultsReceipts[(2026, 5)] = new ResultsReceiptData(Guid.NewGuid(), 2026, 5,
        [
            new ResultsReceiptSchemeEntity
            {
                MonthlyDistributionSchemeId = SchemeId,
                SchemeIdentifier = "BT",
                SchemeName = "Brucella",
                DistributionReference = "D1",
                ScheduleCode = "S1",
                HasSampleNumbersDefined = true,
                DeadlineDate = SchemeDeadline,
                RequiresAssessment = true,
                Participants =
                [
                    new ResultsReceiptParticipantEntity
                    {
                        DistributionParticipantId = PostedParticipant, LabCode = "L001", LabName = "Lab One",
                        PostedDate = new DateTime(2026, 5, 2), FirstEditDate = new DateTime(2026, 5, 3), LastEditDate = new DateTime(2026, 5, 4),
                        ExtraDocumentsReturned = true, HasUnaccountedSamples = true, ReturnedDate = new DateTime(2026, 5, 5),
                    },
                    new ResultsReceiptParticipantEntity
                    {
                        DistributionParticipantId = UnpostedParticipant, LabCode = "L002", LabName = "Lab Two",
                        DeadlineDate = new DateTime(2026, 7, 1),
                    },
                ],
            },
        ]);
        return repository;
    }

    [Fact]
    public async Task GetResultsReceipt_NoDistribution_ReturnsNotFound()
    {
        var result = await CreateController(new FakeDistributionRepository()).GetResultsReceipt(2026, 5, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task GetResultsReceipt_ReturnsMappedResponse()
    {
        var result = await CreateController(Seeded()).GetResultsReceipt(2026, 5, CancellationToken.None);

        var response = Assert.IsType<ResultsReceiptResponse>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal((2026, 5), (response.YearId, response.MonthId));
        var scheme = Assert.Single(response.Schemes);
        Assert.Equal("D1/S1: BT Brucella", scheme.Description);
        Assert.True(scheme.RequiresAssessment);
        Assert.Equal(50, scheme.ProgressPercentage);

        var posted = scheme.Participants[0];
        Assert.Equal("L001: Lab One", posted.FullName);
        Assert.Equal(SchemeDeadline, posted.DeadlineDate);
        Assert.Equal(new DateTime(2026, 5, 3), posted.FirstEditDate);
        Assert.Equal(new DateTime(2026, 5, 4), posted.LastEditDate);
        Assert.True(posted.ExtraDocumentsReturned);
        Assert.True(posted.CanViewResults);
        Assert.True(posted.HasUnaccountedSamples);
        Assert.Equal(new DateTime(2026, 5, 5), posted.ReturnedDate);

        var unposted = scheme.Participants[1];
        Assert.Equal(new DateTime(2026, 7, 1), unposted.DeadlineDate);
        Assert.False(unposted.CanViewResults);
        Assert.Null(unposted.FirstEditDate);
    }

    [Fact]
    public async Task SaveResultsReceipt_NoDistribution_ReturnsNotFound()
    {
        var request = new ResultsReceiptSaveRequest([new ResultsReceiptParticipantUpdateRequest(PostedParticipant, true, false)]);

        var result = await CreateController(new FakeDistributionRepository()).SaveResultsReceipt(2026, 5, request, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task SaveResultsReceipt_PersistsChangesAndReturnsSuccess()
    {
        var repository = Seeded();
        var request = new ResultsReceiptSaveRequest([new ResultsReceiptParticipantUpdateRequest(UnpostedParticipant, true, true)]);

        var result = await CreateController(repository).SaveResultsReceipt(2026, 5, request, CancellationToken.None);

        var save = Assert.IsType<ResultsReceiptSaveResult>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.True(save.Success);
        var saved = Assert.Single(repository.UpdatedResultsReceiptParticipants);
        Assert.Equal(UnpostedParticipant, saved.DistributionParticipantId);
        Assert.True(saved.ExtraDocumentsReturned);
        Assert.NotNull(saved.ReturnedDate);
    }
}
