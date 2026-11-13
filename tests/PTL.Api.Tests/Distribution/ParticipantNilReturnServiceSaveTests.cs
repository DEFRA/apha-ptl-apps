using PTL.Core.Distribution;

namespace PTL.Api.Tests.Distribution;

public class ParticipantNilReturnServiceSaveTests
{
    private static readonly Guid SchemeId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid ParticipantId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly DateTimeOffset Now = new(2026, 11, 12, 9, 30, 0, TimeSpan.Zero);

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }

    private static ParticipantNilReturnService CreateService(FakeDistributionRepository repository) =>
        new(repository, new ActualResultsService(repository, TimeProvider.System), new FixedTimeProvider(Now));

    private static void SeedOpenActualResults(
        FakeDistributionRepository repository,
        IReadOnlyList<ActualSampleResultEntity>? sampleResults = null,
        ActualResultsScenario? scenario = null)
    {
        scenario ??= new ActualResultsScenario();
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
            sampleResults ?? [],
            []);
    }

    private static void SeedSampleReceipt(FakeDistributionRepository repository, params SampleReceiptSampleEntity[] samples) =>
        repository.SampleReceipts[(ParticipantId, SchemeId)] =
            new SampleReceiptData(new SampleReceiptSchemeEntity(), new SampleReceiptLabEntity(), [], samples);

    [Fact]
    public async Task GetAsync_NoActualResultsForTheParticipant_ReturnsNull()
    {
        var service = CreateService(new FakeDistributionRepository());

        Assert.Null(await service.GetAsync(ParticipantId, SchemeId, CancellationToken.None));
    }

    [Fact]
    public async Task GetAsync_WithASavedReason_ReturnsIt()
    {
        var repository = new FakeDistributionRepository();
        SeedOpenActualResults(repository);
        repository.ParticipantNilReturns[ParticipantId] = new ParticipantNilReturnData(ParticipantId, "No samples received");

        var result = await CreateService(repository).GetAsync(ParticipantId, SchemeId, CancellationToken.None);

        Assert.Equal("No samples received", result!.NilReturnReason);
    }

    [Fact]
    public async Task SaveAsync_NoActualResultsForTheParticipant_ReturnsNotFound()
    {
        var repository = new FakeDistributionRepository();

        var outcome = await CreateService(repository).SaveAsync(ParticipantId, SchemeId, "No samples", CancellationToken.None);

        Assert.Equal(ParticipantNilReturnSaveStatus.NotFound, outcome.Status);
        Assert.False(outcome.Success);
        Assert.Empty(repository.SavedParticipantNilReturns);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SaveAsync_ABlankReason_ReturnsReasonRequired(string? reason)
    {
        var repository = new FakeDistributionRepository();
        SeedOpenActualResults(repository);

        var outcome = await CreateService(repository).SaveAsync(ParticipantId, SchemeId, reason, CancellationToken.None);

        Assert.Equal(ParticipantNilReturnSaveStatus.ReasonRequired, outcome.Status);
        Assert.Empty(repository.SavedParticipantNilReturns);
    }

    [Fact]
    public async Task SaveAsync_AResultAlreadyEnteredOnASample_ReturnsHasResults()
    {
        var repository = new FakeDistributionRepository();
        var scenario = new ActualResultsScenario();
        SeedOpenActualResults(repository, [new ActualSampleResultEntity { SampleResultItemId = scenario.CultureS1Growth, Result = "Positive" }], scenario);

        var outcome = await CreateService(repository).SaveAsync(ParticipantId, SchemeId, "No samples", CancellationToken.None);

        Assert.Equal(ParticipantNilReturnSaveStatus.HasResults, outcome.Status);
        Assert.Empty(repository.SavedParticipantNilReturns);
    }

    [Fact]
    public async Task SaveAsync_AValidReason_SavesItTrimmedAndMarksEverySampleNotSubmittedToday()
    {
        var repository = new FakeDistributionRepository();
        SeedOpenActualResults(repository);
        var earlier = new DateTime(2026, 10, 1);
        var submitted = new SampleReceiptSampleEntity { SampleNumber = "1", SubmissionDate = earlier };
        var notCapable = new SampleReceiptSampleEntity { SampleNumber = "2", NonCapableDate = earlier };
        var alreadyNotSubmitted = new SampleReceiptSampleEntity { SampleNumber = "3", NonSubmissionDate = earlier };
        SeedSampleReceipt(repository, submitted, notCapable, alreadyNotSubmitted);

        var outcome = await CreateService(repository).SaveAsync(ParticipantId, SchemeId, "  No samples received  ", CancellationToken.None);

        Assert.True(outcome.Success);
        Assert.Equal([(ParticipantId, "No samples received")], repository.SavedParticipantNilReturns);
        Assert.Equal(3, repository.UpdatedSampleReceipts.Count);
        Assert.All(repository.UpdatedSampleReceipts, sample =>
        {
            Assert.Null(sample.SubmissionDate);
            Assert.Null(sample.NonCapableDate);
        });
        Assert.Equal(Now.Date, submitted.NonSubmissionDate);
        Assert.Equal(Now.Date, notCapable.NonSubmissionDate);
        // An existing not-submitted date is kept, not overwritten.
        Assert.Equal(earlier, alreadyNotSubmitted.NonSubmissionDate);
    }

    [Fact]
    public async Task SaveAsync_AValidReasonWithNoSampleReceiptRows_SavesTheReasonAndUpdatesNoSamples()
    {
        var repository = new FakeDistributionRepository();
        SeedOpenActualResults(repository);

        var outcome = await CreateService(repository).SaveAsync(ParticipantId, SchemeId, "No samples", CancellationToken.None);

        Assert.True(outcome.Success);
        Assert.Single(repository.SavedParticipantNilReturns);
        Assert.Empty(repository.UpdatedSampleReceipts);
    }
}
