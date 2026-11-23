using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PTL.Api.Controllers;
using PTL.Contracts.Distribution;
using PTL.Core.Distribution;

namespace PTL.Api.Tests.Distribution;

// ExternalWeb-only "for-user" tabulation endpoints - IParticipantTabulationService via [FromServices].
public class DistributionControllerTabulationForUserTests
{
    private static readonly ExternalTabulationCaller NoCaller = new(null, null, null);

    private readonly FakeDistributionRepository _repository = new();

    private IParticipantTabulationService Service =>
        new ParticipantTabulationService(_repository, new TabulationReportService(_repository));

    private DistributionController CreateController() =>
        new(
            new DistributionService(_repository),
            new SampleNumbersService(_repository, new FakeSchemeService()),
            new IntendedResultsService(_repository),
            new DistributionParticipantsService(_repository),
            new PreparationService(_repository),
            new ResultsReceiptService(_repository, TimeProvider.System),
            new TabulationsService(_repository, TimeProvider.System),
            new ActualResultsService(_repository, TimeProvider.System),
            new ParticipantDistributionHistoryService(_repository, new PTL.Api.Tests.Participant.FakeParticipantRepository(), new ActualResultsService(_repository, TimeProvider.System)),
            new FakePackagingService(),
            new FakePackagingOutputService(),
            new PTL.Api.Tests.Contract.FakeAuditActorResolver("Guest"),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<DistributionController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };

    [Fact]
    public async Task GetTabulationsForUser_NoClaims_ReturnsEmptyList()
    {
        var result = await CreateController().GetTabulationsForUser(NoCaller, Service, CancellationToken.None);

        var list = Assert.IsType<PublishedTabulationSummaryListResponse>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Empty(list.Tabulations);
    }

    [Fact]
    public async Task GetTabulationsForUser_ContactId_ReturnsThatContactsList()
    {
        var ssoIdExt = Guid.NewGuid();
        _repository.TabulationsForExternalUser[(ssoIdExt, null)] =
        [
            new PublishedTabulationSummaryEntity { TabulationId = Guid.NewGuid(), SchemeName = "BT: Brucella", DistributionReference = "D1a/S1" },
        ];

        var result = await CreateController().GetTabulationsForUser(new ExternalTabulationCaller(ssoIdExt, null, null), Service, CancellationToken.None);

        var list = Assert.IsType<PublishedTabulationSummaryListResponse>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal("BT", Assert.Single(list.Tabulations).SchemeCode);
    }

    [Fact]
    public async Task GetPublishedTabulationForUser_NotInCallersOwnList_ReturnsNotFound()
    {
        var result = await CreateController().GetPublishedTabulationForUser(Guid.NewGuid(), NoCaller, Service, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task GetPublishedTabulationForUser_InCallersOwnList_ReturnsTheReport()
    {
        var ssoIdExt = Guid.NewGuid();
        var tabulationId = Guid.NewGuid();
        _repository.TabulationsForExternalUser[(ssoIdExt, null)] = [new PublishedTabulationSummaryEntity { TabulationId = tabulationId }];
        _repository.PublishedTabulationSources[tabulationId] = new TabulationSource { IsPublished = true, TabulationName = "Summary" };

        var result = await CreateController().GetPublishedTabulationForUser(tabulationId, new ExternalTabulationCaller(ssoIdExt, null, null), Service, CancellationToken.None);

        Assert.IsType<TabulationReportResponse>(Assert.IsType<OkObjectResult>(result.Result).Value);
    }

    [Fact]
    public async Task GetAssessmentResultsForUser_NotInCallersOwnList_ReturnsNotFound()
    {
        var result = await CreateController().GetAssessmentResultsForUser(Guid.NewGuid(), Guid.NewGuid(), NoCaller, Service, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task GetPublishedTabulationAttachmentForUser_NotInCallersOwnList_ReturnsNotFound()
    {
        var result = await CreateController().GetPublishedTabulationAttachmentForUser(Guid.NewGuid(), Guid.NewGuid(), NoCaller, Service, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }
}
