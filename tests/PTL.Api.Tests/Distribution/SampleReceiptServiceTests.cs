using PTL.Core.Distribution;

namespace PTL.Api.Tests.Distribution;

// Sample Receipt rules from legacy SampleReceipt.aspx / PtaBusinessObjects ResultsReceipt.TestCollection.
public class SampleReceiptServiceTests
{
    private static readonly DateTime Now = new(2026, 5, 12, 15, 30, 0);
    private static readonly DateTime Today = new(2026, 5, 12);
    private static readonly DateTime Earlier = new(2026, 5, 1);

    private static readonly Guid ParticipantId = Guid.NewGuid();
    private static readonly Guid SchemeId = Guid.NewGuid();
    private static readonly Guid CultureTest = Guid.NewGuid();
    private static readonly Guid SerologyTest = Guid.NewGuid();
    private static readonly Guid EmptyTest = Guid.NewGuid();

    private readonly FakeDistributionRepository _repository = new();
    private readonly ResultsReceiptService _service;

    public SampleReceiptServiceTests() => _service = new ResultsReceiptService(_repository, new LocalClock(Now));

    private static SampleReceiptSampleEntity Sample(
        Guid? testId = null, string number = "S001", DateTime? submitted = null, DateTime? notSubmitted = null, DateTime? notInRepertoire = null) =>
        new()
        {
            SampleReceiptId = Guid.NewGuid(),
            TestId = testId ?? CultureTest,
            SampleNumber = number,
            SubmissionDate = submitted,
            NonSubmissionDate = notSubmitted,
            NonCapableDate = notInRepertoire,
        };

    private SampleReceiptData Seed(DateTime? readyForAssessor = null, bool withTests = true, params SampleReceiptSampleEntity[] samples)
    {
        var data = new SampleReceiptData(
            new SampleReceiptSchemeEntity
            {
                DistributionReference = "D1",
                DistributionReferenceSuffix = "a",
                ScheduleCode = "S1",
                IsReadyForAssessor = readyForAssessor,
                SchemeIdentifier = "BT",
                SchemeName = "Brucella",
            },
            new SampleReceiptLabEntity { LabCode = "L001", LabName = "Lab One" },
            withTests
                ?
                [
                    new SampleReceiptTestEntity { TestId = CultureTest, TestType = "Culture" },
                    new SampleReceiptTestEntity { TestId = SerologyTest, TestType = "Serology" },
                    new SampleReceiptTestEntity { TestId = EmptyTest, TestType = "No samples" },
                ]
                : [],
            samples);
        _repository.SampleReceipts[(ParticipantId, SchemeId)] = data;
        return data;
    }

    private Task<SampleReceiptSaveOutcome> SaveAsync(params SampleReceiptUpdate[] updates) =>
        _service.SaveSampleReceiptAsync(ParticipantId, SchemeId, updates);

    private static SampleReceiptUpdate Ticks(SampleReceiptSampleEntity sample, bool submitted = false, bool notSubmitted = false, bool notInRepertoire = false) =>
        new(sample.SampleReceiptId, submitted, notSubmitted, notInRepertoire);

    // ---------- Get ----------

    [Fact]
    public async Task Get_NotFound_ReturnsNull() =>
        Assert.Null(await _service.GetSampleReceiptAsync(ParticipantId, SchemeId));

    [Fact]
    public async Task Get_BuildsLegacyHeadingsAndOnlyRendersTestsThatHaveSamples()
    {
        Seed(null, true,
            Sample(CultureTest, "S001", submitted: Earlier),
            Sample(CultureTest, "S002", notSubmitted: Earlier),
            Sample(SerologyTest, "S003", notInRepertoire: Earlier),
            Sample(SerologyTest, "S004"));

        var page = await _service.GetSampleReceiptAsync(ParticipantId, SchemeId);

        Assert.NotNull(page);
        Assert.Equal("D1a/S1 : BT Brucella", page.SchemeHeading);
        Assert.Equal("Labcode L001: Lab One", page.LabHeading);
        Assert.False(page.IsReadOnly);
        Assert.Equal(["Culture", "Serology"], page.Tests.Select(t => t.TestType));

        var culture = page.Tests[0].Samples;
        Assert.Equal(("S001", true, false, false), (culture[0].SampleNumber, culture[0].Submitted, culture[0].NotSubmitted, culture[0].NotInRepertoire));
        Assert.Equal(("S002", false, true, false), (culture[1].SampleNumber, culture[1].Submitted, culture[1].NotSubmitted, culture[1].NotInRepertoire));

        var serology = page.Tests[1].Samples;
        Assert.Equal(("S003", false, false, true), (serology[0].SampleNumber, serology[0].Submitted, serology[0].NotSubmitted, serology[0].NotInRepertoire));
        Assert.Equal(("S004", false, false, false), (serology[1].SampleNumber, serology[1].Submitted, serology[1].NotSubmitted, serology[1].NotInRepertoire));
    }

    [Fact]
    public async Task Get_NoTests_IsReadOnly()
    {
        Seed(null, false);

        var page = await _service.GetSampleReceiptAsync(ParticipantId, SchemeId);

        Assert.True(page!.IsReadOnly);
        Assert.Empty(page.Tests);
    }

    [Fact]
    public async Task Get_ReadyForAssessor_IsReadOnlyButStillListsTheSamples()
    {
        Seed(Earlier, true, Sample(CultureTest, "S001", submitted: Earlier));

        var page = await _service.GetSampleReceiptAsync(ParticipantId, SchemeId);

        Assert.True(page!.IsReadOnly);
        Assert.Single(page.Tests);
    }

    // ---------- Save: guards ----------

    [Fact]
    public async Task Save_NotFound_ReturnsNotFoundAndWritesNothing()
    {
        var outcome = await SaveAsync();

        Assert.Equal(SampleReceiptSaveStatus.NotFound, outcome.Status);
        Assert.False(outcome.Success);
        Assert.Empty(_repository.UpdatedSampleReceipts);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Save_ReadOnly_RefusesAndWritesNothing(bool withTests)
    {
        var sample = Sample();
        Seed(withTests ? Earlier : null, withTests, sample);

        var outcome = await SaveAsync(Ticks(sample, submitted: true));

        Assert.Equal(SampleReceiptSaveStatus.ReadOnly, outcome.Status);
        Assert.False(outcome.Success);
        Assert.Empty(_repository.UpdatedSampleReceipts);
    }

    // ---------- Save: legacy Checkbox_CheckChanged ----------

    [Theory]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    public async Task Save_TickingOneOnAnEmptySample_StampsTodayAndLeavesTheOthersEmpty(bool submitted, bool notSubmitted, bool notInRepertoire)
    {
        var sample = Sample();
        Seed(null, true, sample);

        var outcome = await SaveAsync(Ticks(sample, submitted, notSubmitted, notInRepertoire));

        Assert.True(outcome.Success);
        var written = Assert.Single(_repository.UpdatedSampleReceipts);
        Assert.Equal(submitted ? Today : null, written.SubmissionDate);
        Assert.Equal(notSubmitted ? Today : null, written.NonSubmissionDate);
        Assert.Equal(notInRepertoire ? Today : null, written.NonCapableDate);
    }

    [Fact]
    public async Task Save_TickingAnotherOverAStoredOne_ClearsTheStoredDateAndStampsToday()
    {
        var sample = Sample(submitted: Earlier);
        Seed(null, true, sample);

        await SaveAsync(Ticks(sample, submitted: true, notSubmitted: true));

        var written = Assert.Single(_repository.UpdatedSampleReceipts);
        Assert.Null(written.SubmissionDate);
        Assert.Equal(Today, written.NonSubmissionDate);
        Assert.Null(written.NonCapableDate);
    }

    [Fact]
    public async Task Save_UntickingTheStoredOne_ClearsItsDate()
    {
        var sample = Sample(notInRepertoire: Earlier);
        Seed(null, true, sample);

        await SaveAsync(Ticks(sample));

        var written = Assert.Single(_repository.UpdatedSampleReceipts);
        Assert.Null(written.SubmissionDate);
        Assert.Null(written.NonSubmissionDate);
        Assert.Null(written.NonCapableDate);
    }

    [Fact]
    public async Task Save_NothingChanged_WritesNothingAndKeepsTheOriginalDate()
    {
        var sample = Sample(submitted: Earlier);
        var empty = Sample(CultureTest, "S002");
        Seed(null, true, sample, empty);

        var outcome = await SaveAsync(Ticks(sample, submitted: true), Ticks(empty));

        Assert.True(outcome.Success);
        Assert.Empty(_repository.UpdatedSampleReceipts);
        Assert.Equal(Earlier, sample.SubmissionDate);
    }

    [Fact]
    public async Task Save_SeveralNewTicksAtOnce_TheFirstInLegacyOrderWins()
    {
        var sample = Sample();
        Seed(null, true, sample);

        await SaveAsync(Ticks(sample, submitted: true, notSubmitted: true, notInRepertoire: true));

        var written = Assert.Single(_repository.UpdatedSampleReceipts);
        Assert.Equal(Today, written.SubmissionDate);
        Assert.Null(written.NonSubmissionDate);
        Assert.Null(written.NonCapableDate);
    }

    [Fact]
    public async Task Save_AnotherNewTickOnlyBeatsTheStoredOneBecauseItIsNew()
    {
        var sample = Sample(submitted: Earlier);
        Seed(null, true, sample);

        await SaveAsync(Ticks(sample, submitted: true, notInRepertoire: true));

        var written = Assert.Single(_repository.UpdatedSampleReceipts);
        Assert.Null(written.SubmissionDate);
        Assert.Equal(Today, written.NonCapableDate);
    }

    [Theory]
    [InlineData(true, false, "submitted")]
    [InlineData(false, true, "notSubmitted")]
    public async Task Save_InconsistentStoredRowWithTwoDates_KeepsOneOfThePostedTicksAndItsOriginalDate(bool submitted, bool notSubmitted, string kept)
    {
        var sample = Sample(submitted: Earlier, notSubmitted: Earlier.AddDays(1));
        Seed(null, true, sample);

        await SaveAsync(Ticks(sample, submitted, notSubmitted));

        var written = Assert.Single(_repository.UpdatedSampleReceipts);
        Assert.Equal(kept == "submitted" ? Earlier : null, written.SubmissionDate);
        Assert.Equal(kept == "notSubmitted" ? Earlier.AddDays(1) : null, written.NonSubmissionDate);
        Assert.Null(written.NonCapableDate);
    }

    [Fact]
    public async Task Save_OnlyChangedRowsAreWritten_UnknownIdsAreIgnored_AndTheLastDuplicateWins()
    {
        var changed = Sample(CultureTest, "S001");
        var untouched = Sample(CultureTest, "S002", submitted: Earlier);
        Seed(null, true, changed, untouched);

        var outcome = await SaveAsync(
            Ticks(changed, submitted: true),
            Ticks(changed, notSubmitted: true),
            Ticks(untouched, submitted: true),
            new SampleReceiptUpdate(Guid.NewGuid(), true, false, false));

        Assert.True(outcome.Success);
        var written = Assert.Single(_repository.UpdatedSampleReceipts);
        Assert.Equal(changed.SampleReceiptId, written.SampleReceiptId);
        Assert.Null(written.SubmissionDate);
        Assert.Equal(Today, written.NonSubmissionDate);
    }

    [Fact]
    public void Outcome_SuccessIsOnlyTrueWhenSaved()
    {
        Assert.True(new SampleReceiptSaveOutcome(SampleReceiptSaveStatus.Saved).Success);
        Assert.False(new SampleReceiptSaveOutcome(SampleReceiptSaveStatus.NotFound).Success);
        Assert.False(new SampleReceiptSaveOutcome(SampleReceiptSaveStatus.ReadOnly).Success);
    }
}
