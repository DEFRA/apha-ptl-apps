using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PTL.Api.Controllers;
using PTL.Contracts.Distribution;
using PTL.Core.Distribution;
using static PTL.Api.Tests.Distribution.TabulationsTestData;

namespace PTL.Api.Tests.Distribution;

// Tabulation, Issued Tabulations, Issued Tabulation, (Issued) Assessment Results and the tabulation
// attachment download - the actions take ITabulationReportService via [FromServices].
public class DistributionControllerTabulationReportTests
{
    private static readonly Guid TabulationId = Guid.NewGuid();
    private static readonly Guid PublishedId = Guid.NewGuid();
    private static readonly Guid TestId = Guid.NewGuid();
    private static readonly Guid AttachmentId = Guid.NewGuid();

    private readonly FakeDistributionRepository _repository = new();

    private TabulationReportService Service => new(_repository);

    private DistributionController CreateController() =>
        new(
            new DistributionService(_repository),
            new SampleNumbersService(_repository, new FakeSchemeService()),
            new IntendedResultsService(_repository),
            new DistributionParticipantsService(_repository),
            new PreparationService(_repository),
            new ResultsReceiptService(_repository, TimeProvider.System),
            new TabulationsService(_repository, Clock()),
            new ActualResultsService(_repository, TimeProvider.System),
            new FakePackagingService(),
            new FakePackagingOutputService(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<DistributionController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };

    private static TabulationSource SingleParticipantSource(Guid participantId, bool published = false)
    {
        var source = new TabulationSource { IsPublished = published, SingleParticipantTabulation = true, TabulationName = "Summary" };
        source.Participants.Add(new TabulationSourceParticipant { ParticipantId = participantId, LabCode = "L001" });
        return source;
    }

    private static TabulationSource WithAttachment(string fileName, string contentType)
    {
        var source = new TabulationSource();
        source.Attachments.Add(new(AttachmentId, fileName, contentType, [1, 2, 3]));
        return source;
    }

    [Fact]
    public async Task GetTabulation_Missing_ReturnsNotFound()
    {
        var result = await CreateController().GetTabulation(TabulationId, null, null, Service, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task GetTabulation_ReturnsTheReportForTheRequestedParticipant()
    {
        var participant = Guid.NewGuid();
        _repository.TabulationSources[TabulationId] = SingleParticipantSource(participant);

        var result = await CreateController().GetTabulation(TabulationId, participant, null, Service, CancellationToken.None);

        var report = Assert.IsType<TabulationReportResponse>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal(participant, report.SelectedParticipantId);
        Assert.False(report.IsPublished);
    }

    [Fact]
    public async Task GetTabulation_NoFilters_DefaultsToAllParticipants()
    {
        _repository.TabulationSources[TabulationId] = SingleParticipantSource(Guid.NewGuid());

        var result = await CreateController().GetTabulation(TabulationId, null, null, Service, CancellationToken.None);

        var report = Assert.IsType<TabulationReportResponse>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal(Guid.Empty, report.SelectedParticipantId);
        Assert.Equal(Guid.Empty, report.SelectedViewerId);
    }

    [Fact]
    public async Task GetPublishedTabulations_MissingOrPresent()
    {
        var controller = CreateController();
        Assert.IsType<NotFoundResult>((await controller.GetPublishedTabulations(TabulationId, Service, CancellationToken.None)).Result);

        _repository.PublishedTabulations[TabulationId] = new PublishedTabulationsSource("Summary", "D1a/S1", "BT", "Brucella", []);

        var result = await controller.GetPublishedTabulations(TabulationId, Service, CancellationToken.None);

        var page = Assert.IsType<PublishedTabulationsResponse>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal("D1a/S1 BT: Brucella", page.Title);
        Assert.Empty(page.Tabulations);
    }

    [Fact]
    public async Task GetPublishedTabulation_MissingOrPresent()
    {
        var controller = CreateController();
        Assert.IsType<NotFoundResult>((await controller.GetPublishedTabulation(PublishedId, null, null, Service, CancellationToken.None)).Result);

        var participant = Guid.NewGuid();
        _repository.PublishedTabulationSources[PublishedId] = SingleParticipantSource(participant, published: true);

        var result = await controller.GetPublishedTabulation(PublishedId, participant, Guid.NewGuid(), Service, CancellationToken.None);

        var report = Assert.IsType<TabulationReportResponse>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.True(report.IsPublished);
        Assert.Equal(participant, report.SelectedParticipantId);
    }

    [Fact]
    public async Task GetAssessmentResults_ReadsTheDraftScores()
    {
        var controller = CreateController();
        Assert.IsType<NotFoundResult>((await controller.GetAssessmentResults(TabulationId, TestId, Service, CancellationToken.None)).Result);

        _repository.AssessmentScores[(TestId, TabulationId, false)] = new AssessmentScoreSource("Summary", "Brucella", "D1", "Culture", null, [], []);

        var result = await controller.GetAssessmentResults(TabulationId, TestId, Service, CancellationToken.None);

        var results = Assert.IsType<AssessmentResultsResponse>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.False(results.IsAssessmentSubmitted);
        Assert.Equal("Test name: Culture", results.Titles[5]);
    }

    [Fact]
    public async Task GetPublishedAssessmentResults_ReadsThePublishedScores()
    {
        var controller = CreateController();
        _repository.AssessmentScores[(TestId, PublishedId, false)] = new AssessmentScoreSource("Draft", "", "", "", null, [], []);
        Assert.IsType<NotFoundResult>((await controller.GetPublishedAssessmentResults(PublishedId, TestId, Service, CancellationToken.None)).Result);

        _repository.AssessmentScores[(TestId, PublishedId, true)] = new AssessmentScoreSource("Issued", "", "", "", DateTime.Today, [], []);

        var result = await controller.GetPublishedAssessmentResults(PublishedId, TestId, Service, CancellationToken.None);

        var results = Assert.IsType<AssessmentResultsResponse>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal("Tabulation: Issued", results.Titles[4]);
    }

    [Fact]
    public async Task GetTabulationAttachment_Missing_ReturnsNotFound()
    {
        _repository.TabulationSources[TabulationId] = WithAttachment("a.pdf", "application/pdf");

        var controller = CreateController();

        Assert.IsType<NotFoundResult>(await controller.GetTabulationAttachment(TabulationId, Guid.NewGuid(), Service, CancellationToken.None));
        Assert.IsType<NotFoundResult>(await controller.GetTabulationAttachment(Guid.NewGuid(), AttachmentId, Service, CancellationToken.None));
        Assert.False(controller.Response.Headers.ContainsKey("Content-Disposition"));
    }

    [Theory]
    [InlineData("application/pdf")]
    [InlineData("IMAGE/PNG")]
    [InlineData("image/jpeg")]
    [InlineData("image/gif")]
    public async Task GetTabulationAttachment_SafeType_IsServedInlineWithItsOwnContentType(string contentType)
    {
        _repository.TabulationSources[TabulationId] = WithAttachment("Supporting doc.pdf", contentType);
        var controller = CreateController();

        var result = await controller.GetTabulationAttachment(TabulationId, AttachmentId, Service, CancellationToken.None);

        var file = Assert.IsType<FileContentResult>(result);
        Assert.Equal(contentType, file.ContentType);
        Assert.Equal([1, 2, 3], file.FileContents);
        var disposition = controller.Response.Headers.ContentDisposition.ToString();
        Assert.StartsWith("inline", disposition, StringComparison.Ordinal);
        Assert.Contains("filename=\"Supporting doc.pdf\"", disposition, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("text/html")]
    [InlineData("image/svg+xml")]
    [InlineData("")]
    public async Task GetPublishedTabulationAttachment_OtherType_IsDownloadedAsOctetStream(string contentType)
    {
        _repository.PublishedTabulationSources[PublishedId] = WithAttachment("page.html", contentType);
        var controller = CreateController();

        var result = await controller.GetPublishedTabulationAttachment(PublishedId, AttachmentId, Service, CancellationToken.None);

        var file = Assert.IsType<FileContentResult>(result);
        Assert.Equal("application/octet-stream", file.ContentType);
        var disposition = controller.Response.Headers.ContentDisposition.ToString();
        Assert.StartsWith("attachment", disposition, StringComparison.Ordinal);
        Assert.Contains("filename=page.html", disposition, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetPublishedTabulationAttachment_ReadsOnlyThePublishedCopy()
    {
        _repository.TabulationSources[PublishedId] = WithAttachment("draft.pdf", "application/pdf");

        var result = await CreateController().GetPublishedTabulationAttachment(PublishedId, AttachmentId, Service, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }
}
