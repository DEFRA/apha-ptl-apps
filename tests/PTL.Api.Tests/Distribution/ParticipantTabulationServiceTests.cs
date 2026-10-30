using PTL.Api.Tests.ExternalUser;
using PTL.Api.Tests.Participant;
using PTL.Core.Distribution;
using PTL.Core.Viewer;
using ParticipantEntity = PTL.Core.Participant.Participant;
using TestConsultantEntity = PTL.Core.TestConsultant.TestConsultant;

namespace PTL.Api.Tests.Distribution;

public class ParticipantTabulationServiceTests
{
    private readonly FakeDistributionRepository _repository = new();
    private readonly FakeParticipantRepository _participantRepository = new();
    private readonly FakeViewerRepository _viewerRepository = new();
    private readonly FakeTestConsultantRepository _testConsultantRepository = new();

    private ParticipantTabulationService Service =>
        new(_repository, _participantRepository, _viewerRepository, _testConsultantRepository, new TabulationReportService(_repository));

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
    public async Task GetTabulationsForUserAsync_UnknownParticipant_ReturnsEmptyList()
    {
        var result = await Service.GetTabulationsForUserAsync(Guid.NewGuid(), null, null, CancellationToken.None);

        Assert.Empty(result.Tabulations);
    }

    [Fact]
    public async Task GetTabulationsForUserAsync_ResolvesParticipantSsoIdAndReturnsTheirTabulations()
    {
        var participantId = Guid.NewGuid();
        var ssoId = Guid.NewGuid();
        await _participantRepository.CreateAsync(new ParticipantEntity { ParticipantId = participantId, SsoId = ssoId });
        _repository.TabulationsBySsoId[ssoId] = [Tabulation()];

        var result = await Service.GetTabulationsForUserAsync(participantId, null, null, CancellationToken.None);

        var tabulation = Assert.Single(result.Tabulations);
        Assert.Equal("BT", tabulation.SchemeCode);
        Assert.Equal("Brucella", tabulation.SchemeName);
    }

    [Fact]
    public async Task GetTabulationsForUserAsync_ResolvesViewerSsoIdByFetchingAllViewers()
    {
        var viewerId = Guid.NewGuid();
        var ssoId = Guid.NewGuid();
        await _viewerRepository.CreateAsync(new ViewerEntity { ViewerId = viewerId, SsoId = ssoId, Name = "Jane", Email = "jane@example.com" });
        _repository.TabulationsBySsoId[ssoId] = [Tabulation()];

        var result = await Service.GetTabulationsForUserAsync(null, viewerId, null, CancellationToken.None);

        Assert.Single(result.Tabulations);
    }

    [Fact]
    public async Task GetTabulationsForUserAsync_ResolvesTestConsultantSsoIdByFetchingAllTestConsultants()
    {
        var testConsultantId = Guid.NewGuid();
        var ssoId = Guid.NewGuid();
        await _testConsultantRepository.CreateAsync(new TestConsultantEntity { ExternalTestConsultantId = testConsultantId, SsoId = ssoId, Name = "Dr Smith", Email = "smith@example.com" });
        _repository.TabulationsBySsoId[ssoId] = [Tabulation()];

        var result = await Service.GetTabulationsForUserAsync(null, null, testConsultantId, CancellationToken.None);

        Assert.Single(result.Tabulations);
    }

    [Fact]
    public async Task GetPublishedTabulationForUserAsync_IdNotInCallersOwnList_ReturnsNull()
    {
        var participantId = Guid.NewGuid();
        var ssoId = Guid.NewGuid();
        await _participantRepository.CreateAsync(new ParticipantEntity { ParticipantId = participantId, SsoId = ssoId });
        _repository.TabulationsBySsoId[ssoId] = [Tabulation()];
        var someoneElsesTabulationId = Guid.NewGuid();

        var result = await Service.GetPublishedTabulationForUserAsync(someoneElsesTabulationId, participantId, null, null, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetPublishedTabulationForUserAsync_IdInCallersOwnList_ReturnsTheReport()
    {
        var participantId = Guid.NewGuid();
        var ssoId = Guid.NewGuid();
        var tabulationId = Guid.NewGuid();
        await _participantRepository.CreateAsync(new ParticipantEntity { ParticipantId = participantId, SsoId = ssoId });
        _repository.TabulationsBySsoId[ssoId] = [Tabulation(tabulationId)];
        _repository.PublishedTabulationSources[tabulationId] = new TabulationSource { IsPublished = true, TabulationName = "Summary" };

        var result = await Service.GetPublishedTabulationForUserAsync(tabulationId, participantId, null, null, CancellationToken.None);

        Assert.NotNull(result);
    }

    [Fact]
    public async Task GetAttachmentForUserAsync_IdNotInCallersOwnList_ReturnsNull()
    {
        var result = await Service.GetAttachmentForUserAsync(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, null, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetAssessmentResultsForUserAsync_IdNotInCallersOwnList_ReturnsNull()
    {
        var result = await Service.GetAssessmentResultsForUserAsync(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, null, CancellationToken.None);

        Assert.Null(result);
    }
}
