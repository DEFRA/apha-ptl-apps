using PTL.Core.Distribution;

namespace PTL.Api.Tests.Distribution;

public class ParticipantNilReturnServiceTests
{
    private static readonly Guid SchemeId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid ParticipantId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private static ParticipantNilReturnService CreateService(FakeDistributionRepository repository) =>
        new(repository, new ActualResultsService(repository, TimeProvider.System), TimeProvider.System);

    private static void SeedOpenActualResults(FakeDistributionRepository repository, ActualResultsScenario scenario)
    {
        repository.IntendedResults[SchemeId] = scenario.Structure();
        repository.ActualResults[ParticipantId] = new ActualResultsData(
            new ActualResultsHeaderEntity
            {
                Distribution = "D1/S1",
                SchemeIdentifier = "BT",
                SchemeName = "Brucella",
                DeadlineDate = DateTime.UtcNow.AddDays(5),
                FirstEditDate = null
            },
            [],
            []);
    }

    [Fact]
    public async Task GetAsync_NoSavedReason_ReturnsEmptyReason()
    {
        var repository = new FakeDistributionRepository();
        var scenario = new ActualResultsScenario();
        SeedOpenActualResults(repository, scenario);
        var service = CreateService(repository);

        var result = await service.GetAsync(ParticipantId, SchemeId, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(ParticipantId, result!.DistributionParticipantId);
        Assert.Equal(string.Empty, result.NilReturnReason);
    }

    [Fact]
    public async Task SaveAsync_DeadlinePassed_ReturnsDeadlinePassed()
    {
        var repository = new FakeDistributionRepository();
        var scenario = new ActualResultsScenario();
        repository.IntendedResults[SchemeId] = scenario.Structure();
        repository.ActualResults[ParticipantId] = new ActualResultsData(
            new ActualResultsHeaderEntity
            {
                Distribution = "D1/S1",
                SchemeIdentifier = "BT",
                SchemeName = "Brucella",
                DeadlineDate = DateTime.UtcNow.AddDays(-1),
                FirstEditDate = null
            },
            [],
            []);
        var service = CreateService(repository);

        var outcome = await service.SaveAsync(ParticipantId, SchemeId, "No samples", CancellationToken.None);

        Assert.Equal(ParticipantNilReturnSaveStatus.DeadlinePassed, outcome.Status);
        Assert.Empty(repository.SavedParticipantNilReturns);
    }

    [Fact]
    public async Task SaveAsync_WhenResultsExist_ReturnsHasResults()
    {
        var repository = new FakeDistributionRepository();
        var scenario = new ActualResultsScenario();
        repository.IntendedResults[SchemeId] = scenario.Structure();
        repository.ActualResults[ParticipantId] = new ActualResultsData(
            new ActualResultsHeaderEntity
            {
                Distribution = "D1/S1",
                SchemeIdentifier = "BT",
                SchemeName = "Brucella",
                DeadlineDate = DateTime.UtcNow.AddDays(5),
                FirstEditDate = null
            },
            [],
            [new ActualMethodItemResultEntity { TestMethodItemId = scenario.MediumMethod, Result = "Agar" }]);
        var service = CreateService(repository);

        var outcome = await service.SaveAsync(ParticipantId, SchemeId, "No samples", CancellationToken.None);

        Assert.Equal(ParticipantNilReturnSaveStatus.HasResults, outcome.Status);
        Assert.Empty(repository.SavedParticipantNilReturns);
    }

    [Fact]
    public async Task SaveAsync_ReasonTooLong_ReturnsTooLong()
    {
        var repository = new FakeDistributionRepository();
        var scenario = new ActualResultsScenario();
        SeedOpenActualResults(repository, scenario);
        var service = CreateService(repository);

        var outcome = await service.SaveAsync(ParticipantId, SchemeId, new string('x', 101), CancellationToken.None);

        Assert.Equal(ParticipantNilReturnSaveStatus.TooLong, outcome.Status);
        Assert.Empty(repository.SavedParticipantNilReturns);
    }
}
