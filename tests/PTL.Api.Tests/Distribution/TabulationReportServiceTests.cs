using PTL.Contracts.Distribution;
using PTL.Core.Distribution;

namespace PTL.Api.Tests.Distribution;

public class TabulationReportServiceTests
{
    private static readonly Guid TabulationId = Guid.NewGuid();
    private static readonly Guid PublishedId = Guid.NewGuid();
    private static readonly Guid AttachmentId = Guid.NewGuid();

    private static (TabulationReportService Service, FakeDistributionRepository Repository) Create()
    {
        var repository = new FakeDistributionRepository();
        return (new TabulationReportService(repository), repository);
    }

    private static TabulationSource WithAttachment(bool published, string fileName)
    {
        var source = new TabulationSource { IsPublished = published, TabulationName = fileName };
        source.Attachments.Add(new(AttachmentId, fileName, "application/pdf", [1, 2, 3]));
        return source;
    }

    // ---- Results Sign-off review (legacy Results Sign-off/Tabulation.aspx draw filter) ----------

    private static TabulationSource ReviewSource(bool withMethodItem = false)
    {
        var source = new TabulationSource
        {
            TabulationId = TabulationId,
            AvailableToViewers = true,
            IntendedResultsOnly = false,
        };
        var test = new TabulationSourceTest { TestId = Guid.NewGuid(), Name = "Culture" };
        test.Samples.Add(new(Guid.NewGuid(), null, "26/2", "Two"));
        test.Samples.Add(new(Guid.NewGuid(), null, "26/1", "One"));
        test.ResultItems.Add(new(Guid.NewGuid(), "Isolate"));
        if (withMethodItem)
        {
            test.MethodItems.Add(new(Guid.NewGuid(), "Medium"));
        }

        source.Tests.Add(test);
        var viewer = new TabulationSourceViewer { ViewerId = Guid.NewGuid(), Name = "Viewer One" };
        source.Viewers.Add(viewer);
        source.Attachments.Add(new(AttachmentId, "support.pdf", "application/pdf", [1]));
        return source;
    }

    private static IEnumerable<string> CellTexts(ResultsSignOffReviewResponse review) =>
        review.Report.Tables.SelectMany(t => t.Rows).SelectMany(r => r.Cells).Select(c => c.Text);

    [Fact]
    public async Task GetSignOffReviewAsync_Missing_ReturnsNull() =>
        Assert.Null(await Create().Service.GetSignOffReviewAsync(TabulationId, Guid.Empty, Guid.Empty, TestConsultantTabulationFilter.None));

    [Fact]
    public async Task GetSignOffReviewAsync_NullFilter_Throws() =>
        await Assert.ThrowsAsync<ArgumentNullException>(() => Create().Service.GetSignOffReviewAsync(TabulationId, Guid.Empty, Guid.Empty, null!));

    [Fact]
    public async Task GetSignOffReviewAsync_OffersEverySampleAndMethodItemsWhateverIsHidden()
    {
        var (service, repository) = Create();
        repository.TabulationSources[TabulationId] = ReviewSource(withMethodItem: true);

        var review = await service.GetSignOffReviewAsync(TabulationId, Guid.Empty, Guid.Empty, new TestConsultantTabulationFilter(["26/2"], true));

        Assert.Equal(["26/1", "26/2"], review!.SampleNumbers);
        Assert.True(review.HasMethodItems);
    }

    [Fact]
    public async Task GetSignOffReviewAsync_WithoutAFilter_DrawsEverySample()
    {
        var (service, repository) = Create();
        repository.TabulationSources[TabulationId] = ReviewSource();

        var review = await service.GetSignOffReviewAsync(TabulationId, Guid.Empty, Guid.Empty, TestConsultantTabulationFilter.None);

        Assert.False(review!.HasMethodItems);
        Assert.Contains("26/1", CellTexts(review));
        Assert.Contains("26/2", CellTexts(review));
    }

    [Fact]
    public async Task GetSignOffReviewAsync_LeavesOutTheHiddenSampleColumns()
    {
        var (service, repository) = Create();
        repository.TabulationSources[TabulationId] = ReviewSource();

        var review = await service.GetSignOffReviewAsync(TabulationId, Guid.Empty, Guid.Empty, new TestConsultantTabulationFilter(["26/2"], false));

        Assert.Contains("26/1", CellTexts(review!));
        Assert.DoesNotContain("26/2", CellTexts(review!));
    }

    [Fact]
    public async Task GetSignOffReviewAsync_KeepsTheAttachmentAndViewerChoiceWhileFiltered()
    {
        var (service, repository) = Create();
        repository.TabulationSources[TabulationId] = ReviewSource();

        var review = await service.GetSignOffReviewAsync(TabulationId, Guid.Empty, Guid.Empty, new TestConsultantTabulationFilter(["26/2"], false));

        Assert.Equal(AttachmentId, review!.Report.Attachment!.AttachmentId);
        Assert.Equal(["Normal View", "Viewer One"], review.Report.ViewerOptions.Select(o => o.Text));
    }

    [Fact]
    public async Task GetTabulationAsync_Missing_ReturnsNull() =>
        Assert.Null(await Create().Service.GetTabulationAsync(TabulationId, Guid.Empty, Guid.Empty));

    [Fact]
    public async Task GetTabulationAsync_BuildsTheDraftReportWithTheRequestedParticipant()
    {
        var (service, repository) = Create();
        var participant = Guid.NewGuid();
        var source = new TabulationSource { TabulationId = TabulationId, SingleParticipantTabulation = true };
        source.Participants.Add(new TabulationSourceParticipant { ParticipantId = participant, LabCode = "L001" });
        repository.TabulationSources[TabulationId] = source;

        var report = await service.GetTabulationAsync(TabulationId, participant, Guid.Empty);

        Assert.NotNull(report);
        Assert.False(report!.IsPublished);
        Assert.Equal(participant, report.SelectedParticipantId);
    }

    [Fact]
    public async Task GetPublishedTabulationAsync_MissingOrPresent()
    {
        var (service, repository) = Create();
        Assert.Null(await service.GetPublishedTabulationAsync(PublishedId, Guid.Empty, Guid.Empty));

        repository.PublishedTabulationSources[PublishedId] = new TabulationSource { TabulationId = PublishedId, IsPublished = true, Signatory = "Dr S" };

        var report = await service.GetPublishedTabulationAsync(PublishedId, Guid.Empty, Guid.Empty);

        Assert.True(report!.IsPublished);
        Assert.Equal("Dr S", report.Signatory!.Signatory);
    }

    [Fact]
    public async Task GetPublishedTabulationsAsync_Missing_ReturnsNull() =>
        Assert.Null(await Create().Service.GetPublishedTabulationsAsync(TabulationId));

    [Fact]
    public async Task GetPublishedTabulationsAsync_BuildsTitleAndRowsInOrder()
    {
        var (service, repository) = Create();
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        repository.PublishedTabulations[TabulationId] = new PublishedTabulationsSource(
            "Summary", "D1a/S1", "BT", "Brucella",
            [new(first, "Dr A", new DateTime(2026, 6, 1, 9, 0, 0)), new(second, "Dr B", new DateTime(2026, 6, 2, 10, 0, 0))]);

        var page = await service.GetPublishedTabulationsAsync(TabulationId);

        Assert.Equal(TabulationId, page!.DistributionTabulationId);
        Assert.Equal("D1a/S1 BT: Brucella", page.Title);
        Assert.Equal("Summary", page.TabulationName);
        Assert.Equal(
            [(first, new DateTime(2026, 6, 1, 9, 0, 0), "Dr A"), (second, new DateTime(2026, 6, 2, 10, 0, 0), "Dr B")],
            page.Tabulations.Select(t => (t.PublishedTabulationId, t.CreationDate, t.Signatory)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GetAssessmentResultsAsync_UsesTheDraftOrPublishedScores(bool isPublished)
    {
        var (service, repository) = Create();
        var testId = Guid.NewGuid();
        Assert.Null(await service.GetAssessmentResultsAsync(testId, TabulationId, isPublished));

        repository.AssessmentScores[(testId, TabulationId, isPublished)] =
            new AssessmentScoreSource("Summary", "Brucella", "D1", "Culture", DateTime.Today, ["S1"], [new(DateTime.Today, null, null, 4, DateTime.Today)]);

        var results = await service.GetAssessmentResultsAsync(testId, TabulationId, isPublished);

        Assert.True(results!.IsAssessmentSubmitted);
        Assert.Equal(1, results.Scores.Single(s => s.Score == 4).Frequency);
        Assert.Null(await service.GetAssessmentResultsAsync(testId, TabulationId, !isPublished));
    }

    [Fact]
    public async Task GetAttachmentAsync_ReadsTheDraftOrPublishedTabulationsOwnAttachment()
    {
        var (service, repository) = Create();
        repository.TabulationSources[TabulationId] = WithAttachment(false, "draft.pdf");
        repository.PublishedTabulationSources[PublishedId] = WithAttachment(true, "issued.pdf");

        Assert.Equal("draft.pdf", (await service.GetAttachmentAsync(TabulationId, false, AttachmentId))!.FileName);
        Assert.Equal("issued.pdf", (await service.GetAttachmentAsync(PublishedId, true, AttachmentId))!.FileName);
        Assert.Null(await service.GetAttachmentAsync(TabulationId, true, AttachmentId));
        Assert.Null(await service.GetAttachmentAsync(PublishedId, false, AttachmentId));
        Assert.Null(await service.GetAttachmentAsync(TabulationId, false, Guid.NewGuid()));
    }
}
