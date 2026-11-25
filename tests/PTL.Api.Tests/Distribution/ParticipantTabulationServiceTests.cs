using Microsoft.Extensions.Logging.Abstractions;
using PTL.Contracts.Distribution;
using PTL.Core.Distribution;

namespace PTL.Api.Tests.Distribution;

public class ParticipantTabulationServiceTests
{
    private static readonly Guid SsoIdExt = Guid.Parse("6a8754e2-2ce7-f111-84e7-70a8a53681e7");
    private static readonly Guid LabId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private readonly FakeDistributionRepository _repository = new();

    private ParticipantTabulationService Service => new(_repository, new TabulationReportService(_repository), NullLogger<ParticipantTabulationService>.Instance);

    private static PublishedTabulationSummaryEntity Tabulation(Guid? id = null) => new()
    {
        TabulationId = id ?? Guid.NewGuid(),
        DistributionReference = "D1a/S1",
        SchemeName = "BT: Brucella",
        TabulationName = "Summary",
        DistributionDate = new DateTime(2026, 5, 1),
        YearId = 2026,
        MonthId = 5,
    };

    [Fact]
    public async Task GetTabulationsForUserAsync_ContactIdOnly_ReturnsTheTestConsultantOrViewerList()
    {
        _repository.TabulationsForExternalUser[(SsoIdExt, null)] = [Tabulation()];

        var result = await Service.GetTabulationsForUserAsync(new ExternalTabulationCaller(SsoIdExt, null, null), CancellationToken.None);

        var tabulation = Assert.Single(result.Tabulations);
        Assert.Equal("BT", tabulation.SchemeCode);
        Assert.Equal("Brucella", tabulation.SchemeName);
        Assert.Equal((SsoIdExt, (Guid?)null), Assert.Single(_repository.ExternalUserTabulationRequests));
    }

    [Fact]
    public async Task GetTabulationsForUserAsync_SelectedLab_IsPassedWithTheContactId()
    {
        _repository.TabulationsForExternalUser[(SsoIdExt, LabId)] = [Tabulation(), Tabulation()];

        var result = await Service.GetTabulationsForUserAsync(new ExternalTabulationCaller(SsoIdExt, LabId, null), CancellationToken.None);

        Assert.Equal(2, result.Tabulations.Count);
        Assert.Equal((SsoIdExt, (Guid?)LabId), Assert.Single(_repository.ExternalUserTabulationRequests));
    }

    [Fact]
    public async Task GetTabulationsForUserAsync_EmptyLabId_IsSentAsNoLab()
    {
        await Service.GetTabulationsForUserAsync(new ExternalTabulationCaller(SsoIdExt, Guid.Empty, null), CancellationToken.None);

        Assert.Equal((SsoIdExt, (Guid?)null), Assert.Single(_repository.ExternalUserTabulationRequests));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public async Task GetTabulationsForUserAsync_NoContactId_ReturnsEmptyWithoutQuerying(string? ssoIdExt)
    {
        var caller = new ExternalTabulationCaller(ssoIdExt is null ? null : Guid.Parse(ssoIdExt), LabId, Guid.NewGuid());

        var result = await Service.GetTabulationsForUserAsync(caller, CancellationToken.None);

        Assert.Empty(result.Tabulations);
        Assert.Empty(_repository.ExternalUserTabulationRequests);
    }

    [Fact]
    public async Task GetTabulationsForUserAsync_NullCaller_Throws() =>
        await Assert.ThrowsAsync<ArgumentNullException>(() => Service.GetTabulationsForUserAsync(null!, CancellationToken.None));

    [Fact]
    public async Task GetPublishedTabulationForUserAsync_IdNotInCallersOwnList_ReturnsNull()
    {
        _repository.TabulationsForExternalUser[(SsoIdExt, null)] = [Tabulation()];
        var someoneElsesTabulationId = Guid.NewGuid();
        _repository.PublishedTabulationSources[someoneElsesTabulationId] = new TabulationSource { IsPublished = true, TabulationName = "Other" };

        var result = await Service.GetPublishedTabulationForUserAsync(someoneElsesTabulationId, new ExternalTabulationCaller(SsoIdExt, null, null), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetPublishedTabulationForUserAsync_AnotherContactsReport_ReturnsNull()
    {
        var tabulationId = Guid.NewGuid();
        _repository.TabulationsForExternalUser[(Guid.NewGuid(), null)] = [Tabulation(tabulationId)];
        _repository.PublishedTabulationSources[tabulationId] = new TabulationSource { IsPublished = true, TabulationName = "Summary" };

        var result = await Service.GetPublishedTabulationForUserAsync(tabulationId, new ExternalTabulationCaller(SsoIdExt, null, null), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetPublishedTabulationForUserAsync_IdInCallersOwnList_ReturnsTheReport()
    {
        var tabulationId = Guid.NewGuid();
        _repository.TabulationsForExternalUser[(SsoIdExt, LabId)] = [Tabulation(tabulationId)];
        _repository.PublishedTabulationSources[tabulationId] = new TabulationSource { IsPublished = true, TabulationName = "Summary" };

        var result = await Service.GetPublishedTabulationForUserAsync(tabulationId, new ExternalTabulationCaller(SsoIdExt, LabId, null), CancellationToken.None);

        Assert.NotNull(result);
    }

    [Fact]
    public async Task GetPublishedTabulationForUserAsync_NoContactId_ReturnsNull()
    {
        var tabulationId = Guid.NewGuid();
        _repository.PublishedTabulationSources[tabulationId] = new TabulationSource { IsPublished = true, TabulationName = "Summary" };

        var result = await Service.GetPublishedTabulationForUserAsync(tabulationId, new ExternalTabulationCaller(null, LabId, null), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetAttachmentForUserAsync_IdNotInCallersOwnList_ReturnsNull()
    {
        var result = await Service.GetAttachmentForUserAsync(Guid.NewGuid(), Guid.NewGuid(), new ExternalTabulationCaller(SsoIdExt, null, null), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetAttachmentForUserAsync_IdInCallersOwnList_ReturnsThePublishedAttachment()
    {
        var tabulationId = Guid.NewGuid();
        var attachment = new TabulationSourceAttachment(Guid.NewGuid(), "guide.pdf", "application/pdf", [1, 2, 3]);
        var source = new TabulationSource { IsPublished = true, TabulationName = "Summary" };
        source.Attachments.Add(attachment);
        _repository.PublishedTabulationSources[tabulationId] = source;
        _repository.TabulationsForExternalUser[(SsoIdExt, null)] = [Tabulation(tabulationId)];

        var result = await Service.GetAttachmentForUserAsync(tabulationId, attachment.AttachmentId, new ExternalTabulationCaller(SsoIdExt, null, null), CancellationToken.None);

        Assert.Same(attachment, result);
    }

    [Fact]
    public async Task GetAssessmentResultsForUserAsync_IdNotInCallersOwnList_ReturnsNull()
    {
        var result = await Service.GetAssessmentResultsForUserAsync(Guid.NewGuid(), Guid.NewGuid(), new ExternalTabulationCaller(SsoIdExt, null, null), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetAssessmentResultsForUserAsync_IdInCallersOwnList_LooksUpThePublishedScores()
    {
        var tabulationId = Guid.NewGuid();
        _repository.TabulationsForExternalUser[(SsoIdExt, null)] = [Tabulation(tabulationId)];

        var result = await Service.GetAssessmentResultsForUserAsync(tabulationId, Guid.NewGuid(), new ExternalTabulationCaller(SsoIdExt, null, null), CancellationToken.None);

        Assert.Null(result);
        Assert.Single(_repository.ExternalUserTabulationRequests);
    }

    [Fact]
    public async Task PerIdLookups_NullCaller_Throw()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => Service.GetPublishedTabulationForUserAsync(Guid.NewGuid(), null!, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentNullException>(() => Service.GetAssessmentResultsForUserAsync(Guid.NewGuid(), Guid.NewGuid(), null!, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentNullException>(() => Service.GetAttachmentForUserAsync(Guid.NewGuid(), Guid.NewGuid(), null!, CancellationToken.None));
    }
}
