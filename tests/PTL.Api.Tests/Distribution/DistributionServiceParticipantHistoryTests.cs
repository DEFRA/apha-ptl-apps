using PTL.Api.Tests.Participant;
using PTL.Core.Distribution;
using ParticipantEntity = PTL.Core.Participant.Participant;

namespace PTL.Api.Tests.Distribution;

public class ParticipantDistributionHistoryServiceTests
{
    private readonly ActualResultsScenario _s = new();

    private static ParticipantDistributionHistoryService ServiceFor(
        FakeDistributionRepository repository, FakeParticipantRepository? participantRepository = null) =>
        new(repository, participantRepository ?? new FakeParticipantRepository(), new ActualResultsService(repository, TimeProvider.System));

    [Fact]
    public async Task GetParticipantDistributionHistoryAsync_ResolvesSsoIdThenDelegatesToRepository()
    {
        var participantId = Guid.NewGuid();
        var ssoId = Guid.NewGuid();
        var participantRepository = new FakeParticipantRepository();
        await participantRepository.CreateAsync(new ParticipantEntity { ParticipantId = participantId, SsoId = ssoId });

        var repository = new FakeDistributionRepository();
        repository.ParticipantHistoryBySsoId[ssoId] =
        [
            new ParticipantPastDistributionEntity
            {
                MonthlyDistributionSchemeId = Guid.NewGuid(),
                DistributionReference = "D1a/00001/1",
                SchemeName = "PT1234: Test Scheme",
                DistributionDate = new DateTime(2026, 1, 15),
                YearId = 2025,
                MonthId = 1,
            }
        ];
        var service = ServiceFor(repository, participantRepository);

        var result = await service.GetParticipantDistributionHistoryAsync(participantId, CancellationToken.None);

        var distribution = Assert.Single(result);
        Assert.Equal("D1a/00001/1", distribution.DistributionReference);
        Assert.Equal("PT1234", distribution.SchemeCode);
        Assert.Equal("Test Scheme", distribution.SchemeNameOnly);
    }

    [Fact]
    public async Task GetParticipantDistributionHistoryAsync_UnknownParticipant_ReturnsEmptyListWithoutCallingRepository()
    {
        var repository = new FakeDistributionRepository();
        var service = ServiceFor(repository);

        var result = await service.GetParticipantDistributionHistoryAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetActualResultsForParticipantAsync_UnknownParticipant_ReturnsNull()
    {
        var repository = _s.Seed(_s.FullyStored());
        var service = ServiceFor(repository);

        Assert.Null(await service.GetActualResultsForParticipantAsync(Guid.NewGuid(), _s.SchemeId, CancellationToken.None));
    }

    [Fact]
    public async Task GetActualResultsForParticipantAsync_NoResolvedDistributionParticipantId_ReturnsNull()
    {
        // The participant has no tlnkDistributionParticipant row for this distribution - either it
        // doesn't exist, or it belongs to someone else (Access Restrictions AC).
        var participantId = Guid.NewGuid();
        var participantRepository = new FakeParticipantRepository();
        await participantRepository.CreateAsync(new ParticipantEntity { ParticipantId = participantId, SsoId = Guid.NewGuid() });

        var repository = _s.Seed(_s.FullyStored());
        repository.ResolvedDistributionParticipantId = null;
        var service = ServiceFor(repository, participantRepository);

        Assert.Null(await service.GetActualResultsForParticipantAsync(participantId, _s.SchemeId, CancellationToken.None));
    }

    [Fact]
    public async Task GetActualResultsForParticipantAsync_ResolvedDistributionParticipantId_ReturnsTheirResults()
    {
        var participantId = Guid.NewGuid();
        var participantRepository = new FakeParticipantRepository();
        await participantRepository.CreateAsync(new ParticipantEntity { ParticipantId = participantId, SsoId = Guid.NewGuid() });

        var repository = _s.Seed(_s.FullyStored());
        repository.ResolvedDistributionParticipantId = _s.ParticipantId;
        var service = ServiceFor(repository, participantRepository);

        var page = await service.GetActualResultsForParticipantAsync(participantId, _s.SchemeId, CancellationToken.None);

        Assert.NotNull(page);
        Assert.Equal(_s.ParticipantId, page.DistributionParticipantId);
    }
}

