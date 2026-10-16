using PTL.Core.Distribution;

namespace PTL.Api.Tests.Distribution;

// Legacy Internal User/ActualResults.aspx + PtaBusinessObjects ActualResultsSet read/save rules.
public class ActualResultsServiceTests
{
    private static readonly DateTime Now = new(2026, 6, 15, 12, 0, 0);

    private readonly ActualResultsScenario _s = new();

    private static ActualResultsService ServiceFor(FakeDistributionRepository repository, DateTime? now = null) =>
        new(repository, new LocalClock(now ?? Now));

    private async Task<ActualResultsSaveOutcome> SaveAsync(
        FakeDistributionRepository repository, ActualResultsUpdate update, DateTime? now = null) =>
        await ServiceFor(repository, now).SaveActualResultsAsync(_s.ParticipantId, _s.SchemeId, update);

    // ---------- Get ----------

    [Fact]
    public async Task Get_ParticipantNotReturned_ReturnsNull()
    {
        var repository = new FakeDistributionRepository();
        repository.IntendedResults[_s.SchemeId] = _s.Structure();

        Assert.Null(await ServiceFor(repository).GetActualResultsAsync(_s.ParticipantId, _s.SchemeId));
    }

    [Fact]
    public async Task Get_StructureMissing_ReturnsNull()
    {
        var repository = new FakeDistributionRepository();
        repository.ActualResults[_s.ParticipantId] = _s.FullyStored();

        Assert.Null(await ServiceFor(repository).GetActualResultsAsync(_s.ParticipantId, _s.SchemeId));
    }

    [Fact]
    public async Task Get_BuildsHeadingAuditNameDeadlineAndIdentifiers()
    {
        var repository = _s.Seed(_s.FullyStored());

        var page = await ServiceFor(repository).GetActualResultsAsync(_s.ParticipantId, _s.SchemeId);

        Assert.NotNull(page);
        Assert.Equal(_s.ParticipantId, page.DistributionParticipantId);
        Assert.Equal(_s.SchemeId, page.MonthlyDistributionSchemeId);
        Assert.Equal("D1/S1 BT: Brucella", page.Heading);
        Assert.Equal("Results Received for D1/S1 BT: Brucella", page.AuditObjectName);
        Assert.Equal(ActualResultsScenario.Deadline, page.DeadlineDate);
        Assert.False(page.IsNew);
    }

    [Fact]
    public async Task Get_NoFirstEditDate_IsNew()
    {
        var repository = _s.Seed(ActualResultsScenario.Untouched());

        var page = await ServiceFor(repository).GetActualResultsAsync(_s.ParticipantId, _s.SchemeId);

        Assert.True(page!.IsNew);
    }

    // The read-only page and Save share one rule, so the boundaries match the Save deadline theories below.
    [Theory]
    [InlineData(false, "2026-07-01T09:00:00", false)]
    [InlineData(false, "2026-07-01T09:00:01", true)]
    [InlineData(true, "2026-06-30T22:59:59", false)]
    [InlineData(true, "2026-06-30T23:00:00", true)]
    public async Task Get_IsReadOnly_OnceTheSameDeadlineRuleAsSaveHasPassed(bool isNew, string now, bool expectedReadOnly)
    {
        var repository = _s.Seed(isNew
            ? ActualResultsScenario.Untouched(deadline: new DateTime(2026, 6, 30, 10, 0, 0))
            : _s.FullyStored(deadline: new DateTime(2026, 6, 30, 10, 0, 0)));

        var page = await ServiceFor(repository, DateTime.Parse(now, System.Globalization.CultureInfo.InvariantCulture))
            .GetActualResultsAsync(_s.ParticipantId, _s.SchemeId);

        Assert.Equal(expectedReadOnly, page!.IsReadOnly);
    }

    [Fact]
    public async Task Get_TestsAreOrderedByOrder_AndATestWithNoApplicableSampleIsOmitted()
    {
        var repository = _s.Seed(_s.FullyStored());

        var page = await ServiceFor(repository).GetActualResultsAsync(_s.ParticipantId, _s.SchemeId);

        Assert.Equal(["Serology", "Culture"], page!.Tests.Select(t => t.TestName));
        Assert.DoesNotContain(page.Tests, t => t.TestId == _s.UnusedTest);
        Assert.DoesNotContain(page.Tests.SelectMany(t => t.MethodItems), m => m.TestMethodItemId == _s.UnusedMethod);
    }

    [Fact]
    public async Task Get_OnlyApplicableSampleTestPairsBecomeRows()
    {
        var repository = _s.Seed(_s.FullyStored());

        var page = await ServiceFor(repository).GetActualResultsAsync(_s.ParticipantId, _s.SchemeId);

        var serology = page!.Tests.Single(t => t.TestId == _s.SerologyTest);
        var row = Assert.Single(serology.Samples);
        Assert.Equal((_s.Sample1, "S001"), (row.SampleId, row.SampleNumber));
        Assert.Equal(2, page.Tests.Single(t => t.TestId == _s.CultureTest).Samples.Count);
    }

    [Fact]
    public async Task Get_ResultItemsAndMethodItemsAreOrdered_AndStoredResultsOverlay()
    {
        var repository = _s.Seed(_s.FullyStored());

        var page = await ServiceFor(repository).GetActualResultsAsync(_s.ParticipantId, _s.SchemeId);

        var culture = page!.Tests.Single(t => t.TestId == _s.CultureTest);
        Assert.Equal(["Growth", "Count"], culture.ResultItems.Select(r => r.Name));
        Assert.Equal("^\\d+$", culture.ResultItems[1].FormatString);
        Assert.Equal("Whole number", culture.ResultItems[1].ReturnValueType);
        Assert.Equal(["Medium", "Incubation"], culture.MethodItems.Select(m => m.Name));
        Assert.Equal(["Broth", "37C"], culture.MethodItems.Select(m => m.Result));
        Assert.Equal("Agar|Broth", culture.MethodItems[0].FormatString);

        var first = culture.Samples[0];
        Assert.Equal([_s.GrowthItem, _s.CountItem], first.Values.Select(v => v.TestResultItemId));
        Assert.Equal([_s.CultureS1Growth, _s.CultureS1Count], first.Values.Select(v => v.SampleResultItemId));
        Assert.Equal(["Positive", "12"], first.Values.Select(v => v.Result));
        Assert.Equal(["Negative", "7"], culture.Samples[1].Values.Select(v => v.Result));
    }

    [Fact]
    public async Task Get_NoStoredResults_ShowsEmptyStrings()
    {
        var repository = _s.Seed(ActualResultsScenario.Untouched());

        var page = await ServiceFor(repository).GetActualResultsAsync(_s.ParticipantId, _s.SchemeId);

        var culture = page!.Tests.Single(t => t.TestId == _s.CultureTest);
        Assert.All(culture.Samples.SelectMany(s => s.Values), v => Assert.Equal(string.Empty, v.Result));
        Assert.All(culture.MethodItems, m => Assert.Equal(string.Empty, m.Result));
    }

    [Fact]
    public async Task Get_NullStoredResult_IsShownAsEmptyString()
    {
        var actual = new ActualResultsData(
            ActualResultsScenario.Header(new DateTime(2026, 6, 1)),
            [new ActualSampleResultEntity { SampleResultItemId = _s.CultureS1Count, Result = null }],
            [new ActualMethodItemResultEntity { TestMethodItemId = _s.IncubationMethod, Result = null }]);
        var repository = _s.Seed(actual);

        var page = await ServiceFor(repository).GetActualResultsAsync(_s.ParticipantId, _s.SchemeId);

        var culture = page!.Tests.Single(t => t.TestId == _s.CultureTest);
        Assert.Equal(string.Empty, culture.Samples[0].Values[1].Result);
        Assert.Equal(string.Empty, culture.MethodItems.Single(m => m.TestMethodItemId == _s.IncubationMethod).Result);
    }

    [Fact]
    public async Task Get_NullNamesAndNumbers_BecomeEmptyStrings()
    {
        var structure = _s.Structure();
        structure.Samples[0].SampleNumber = null;
        structure.Tests.Single(t => t.TestId == _s.SerologyTest).TestName = null;
        structure.TestResultItems.Single(r => r.TestResultItemId == _s.TitreItem).Name = null;
        structure.TestResultItems.Single(r => r.TestResultItemId == _s.TitreItem).FormatString = null;
        structure.TestResultItems.Single(r => r.TestResultItemId == _s.TitreItem).ReturnValueType = null;
        var method = structure.TestMethodItems.Single(m => m.TestMethodItemId == _s.MediumMethod);
        (method.Name, method.FormatString, method.ReturnValueType) = (null, null, null);
        var repository = new FakeDistributionRepository();
        repository.ActualResults[_s.ParticipantId] = _s.FullyStored();
        repository.IntendedResults[_s.SchemeId] = structure;

        var page = await ServiceFor(repository).GetActualResultsAsync(_s.ParticipantId, _s.SchemeId);

        var serology = page!.Tests.Single(t => t.TestId == _s.SerologyTest);
        Assert.Equal(string.Empty, serology.TestName);
        Assert.Equal(string.Empty, serology.Samples[0].SampleNumber);
        var titre = Assert.Single(serology.ResultItems);
        Assert.Equal((string.Empty, string.Empty, string.Empty), (titre.Name, titre.FormatString, titre.ReturnValueType));
        var medium = page.Tests.Single(t => t.TestId == _s.CultureTest).MethodItems[0];
        Assert.Equal((string.Empty, string.Empty, string.Empty), (medium.Name, medium.FormatString, medium.ReturnValueType));
    }

    [Fact]
    public async Task Get_SampleWithoutASampleTestRow_OrWithoutAResultItemRow_IsSkipped()
    {
        var structure = _s.Structure();
        // Sample 2 has no Culture SampleTest at all; Sample 1 lost its Count SampleResultItem.
        var stripped = structure with
        {
            SampleTests = [.. structure.SampleTests.Where(st => !(st.SampleId == _s.Sample2 && st.TestId == _s.CultureTest))],
            SampleResultItems = [.. structure.SampleResultItems.Where(r => r.SampleResultItemId != _s.CultureS1Count)],
        };
        var repository = new FakeDistributionRepository();
        repository.ActualResults[_s.ParticipantId] = _s.FullyStored();
        repository.IntendedResults[_s.SchemeId] = stripped;

        var page = await ServiceFor(repository).GetActualResultsAsync(_s.ParticipantId, _s.SchemeId);

        var culture = page!.Tests.Single(t => t.TestId == _s.CultureTest);
        var row = Assert.Single(culture.Samples);
        Assert.Equal(_s.Sample1, row.SampleId);
        Assert.Equal([_s.CultureS1Growth], row.Values.Select(v => v.SampleResultItemId));
    }

    // ---------- Save: lookup ----------

    [Fact]
    public async Task Save_ParticipantNotReturned_ReturnsNotFoundAndWritesNothing()
    {
        var repository = new FakeDistributionRepository();
        repository.IntendedResults[_s.SchemeId] = _s.Structure();

        var outcome = await SaveAsync(repository, _s.PostStored());

        Assert.Equal(ActualResultsSaveStatus.NotFound, outcome.Status);
        Assert.False(outcome.Success);
        Assert.Empty(repository.SavedActualResults);
    }

    [Fact]
    public async Task Save_StructureMissing_ReturnsNotFound()
    {
        var repository = new FakeDistributionRepository();
        repository.ActualResults[_s.ParticipantId] = _s.FullyStored();

        var outcome = await SaveAsync(repository, _s.PostStored());

        Assert.Equal(ActualResultsSaveStatus.NotFound, outcome.Status);
        Assert.Empty(repository.SavedActualResults);
    }

    // ---------- Save: validation ----------

    [Fact]
    public async Task Save_InvalidRegexValues_FailsWholePageKeyedByItemIdAndWritesNothing()
    {
        var repository = _s.Seed(_s.FullyStored());

        var outcome = await SaveAsync(repository, _s.PostStored(auditWhy: "typo", edit: (samples, methods) =>
        {
            samples[_s.CultureS1Count] = "twelve";
            samples[_s.SerologyS1Titre] = "12345";
            methods[_s.IncubationMethod] = string.Empty;
            methods[_s.MediumMethod] = "anything - dropdowns are never validated";
            samples[_s.CultureS2Growth] = "not an option - dropdowns are never validated";
        }));

        Assert.Equal(ActualResultsSaveStatus.ValidationFailed, outcome.Status);
        Assert.False(outcome.Success);
        Assert.Equal(2, outcome.FieldErrorsByItemId.Count);
        Assert.Equal("Input must be of format: Whole number", outcome.FieldErrorsByItemId[_s.CultureS1Count]);
        Assert.Equal("Input must be of format: Up to 3 digits", outcome.FieldErrorsByItemId[_s.SerologyS1Titre]);
        Assert.Empty(repository.SavedActualResults);
    }

    [Fact]
    public async Task Save_InvalidMethodItemValue_IsKeyedByTestMethodItemId()
    {
        var structure = _s.Structure();
        structure.TestMethodItems.Single(m => m.TestMethodItemId == _s.IncubationMethod).FormatString = "^\\d+C$";
        structure.TestMethodItems.Single(m => m.TestMethodItemId == _s.IncubationMethod).ReturnValueType = "Degrees C";
        var repository = new FakeDistributionRepository();
        repository.ActualResults[_s.ParticipantId] = _s.FullyStored();
        repository.IntendedResults[_s.SchemeId] = structure;

        var outcome = await SaveAsync(repository, _s.PostStored(auditWhy: "x", edit: (_, methods) => methods[_s.IncubationMethod] = "warm"));

        Assert.Equal(ActualResultsSaveStatus.ValidationFailed, outcome.Status);
        var error = Assert.Single(outcome.FieldErrorsByItemId);
        Assert.Equal((_s.IncubationMethod, "Input must be of format: Degrees C"), (error.Key, error.Value));
        Assert.Empty(repository.SavedActualResults);
    }

    [Fact]
    public async Task Save_EmptyValuesNeverFailValidation()
    {
        var repository = _s.Seed(_s.FullyStored());

        var outcome = await SaveAsync(repository, _s.PostStored(auditWhy: "cleared", edit: (samples, methods) =>
        {
            samples[_s.CultureS1Count] = string.Empty;
            samples[_s.SerologyS1Titre] = string.Empty;
            methods[_s.IncubationMethod] = string.Empty;
        }));

        Assert.Equal(ActualResultsSaveStatus.Saved, outcome.Status);
        Assert.Empty(outcome.FieldErrorsByItemId);
    }

    // ---------- Save: dirty tracking ----------

    [Fact]
    public async Task Save_ExistingSetWithNothingChanged_ReturnsNothingToSaveWithoutWriting()
    {
        var repository = _s.Seed(_s.FullyStored());

        var outcome = await SaveAsync(repository, _s.PostStored());

        Assert.Equal(ActualResultsSaveStatus.NothingToSave, outcome.Status);
        Assert.True(outcome.Success);
        Assert.Empty(repository.SavedActualResults);
    }

    [Fact]
    public async Task Save_ExistingSetPostingNothing_ReturnsNothingToSave()
    {
        var repository = _s.Seed(_s.FullyStored());

        var outcome = await SaveAsync(repository, new ActualResultsUpdate([], [], "tester", string.Empty));

        Assert.Equal(ActualResultsSaveStatus.NothingToSave, outcome.Status);
        Assert.Empty(repository.SavedActualResults);
    }

    [Fact]
    public async Task Save_NothingChangedButPastDeadline_StillReportsNothingToSave()
    {
        var repository = _s.Seed(_s.FullyStored());

        var outcome = await SaveAsync(repository, _s.PostStored(), now: ActualResultsScenario.Deadline.AddDays(30));

        Assert.Equal(ActualResultsSaveStatus.NothingToSave, outcome.Status);
    }

    [Fact]
    public async Task Save_ResultItemWithoutAStoredRow_CountsAsDirtyEvenWhenUntouched()
    {
        var stored = _s.FullyStored();
        var actual = stored with { SampleResults = [.. stored.SampleResults.Where(r => r.SampleResultItemId != _s.SerologyS2Titre)] };
        var repository = _s.Seed(actual);

        var outcome = await SaveAsync(repository, _s.PostStored(auditWhy: "completing the set"));

        Assert.Equal(ActualResultsSaveStatus.Saved, outcome.Status);
        var write = Assert.Single(repository.SavedActualResults);
        var inserted = Assert.Single(write.SampleItems);
        Assert.Equal(new ActualResultWriteItem(_s.SerologyS2Titre, string.Empty, IsInsert: true), inserted);
        Assert.Empty(write.MethodItems);
    }

    [Fact]
    public async Task Save_MethodItemWithoutAStoredRow_CountsAsDirtyAndIsInserted()
    {
        var stored = _s.FullyStored();
        var actual = stored with { MethodItemResults = [.. stored.MethodItemResults.Where(r => r.TestMethodItemId != _s.UnusedMethod)] };
        var repository = _s.Seed(actual);

        var outcome = await SaveAsync(repository, _s.PostStored(auditWhy: "completing the set"));

        Assert.Equal(ActualResultsSaveStatus.Saved, outcome.Status);
        var write = Assert.Single(repository.SavedActualResults);
        Assert.Equal(new ActualResultWriteItem(_s.UnusedMethod, string.Empty, IsInsert: true), Assert.Single(write.MethodItems));
        Assert.Empty(write.SampleItems);
    }

    [Fact]
    public async Task Save_NewSetUntouched_IsAlwaysDirtyAndRecordsBlankResults()
    {
        var repository = _s.Seed(ActualResultsScenario.Untouched());

        var outcome = await SaveAsync(repository, new ActualResultsUpdate([], [], "tester", string.Empty));

        Assert.Equal(ActualResultsSaveStatus.Saved, outcome.Status);
        var write = Assert.Single(repository.SavedActualResults);
        Assert.All(write.SampleItems, i => Assert.Equal(string.Empty, i.Result));
        Assert.All(write.MethodItems, i => Assert.Equal(string.Empty, i.Result));
    }

    // ---------- Save: deadline ----------

    [Theory]
    [InlineData("2026-06-30T23:00:00", false)]
    [InlineData("2026-06-30T23:00:01", true)]
    [InlineData("2026-07-01T00:00:00", true)]
    public async Task Save_ExistingSet_DeadlinePlus23HoursCutOff(string now, bool expectedDeadlinePassed)
    {
        var repository = _s.Seed(_s.FullyStored());

        var outcome = await SaveAsync(
            repository, _s.PostStored(auditWhy: "changed", edit: (samples, _) => samples[_s.CultureS1Count] = "13"), DateTime.Parse(now, System.Globalization.CultureInfo.InvariantCulture));

        Assert.Equal(expectedDeadlinePassed ? ActualResultsSaveStatus.DeadlinePassed : ActualResultsSaveStatus.Saved, outcome.Status);
        Assert.Equal(expectedDeadlinePassed ? 0 : 1, repository.SavedActualResults.Count);
        Assert.False(expectedDeadlinePassed && outcome.Success);
    }

    [Theory]
    [InlineData("2026-06-30T22:59:59", false)]
    [InlineData("2026-06-30T23:00:00", true)]
    [InlineData("2026-07-01T09:00:00", true)]
    public async Task Save_NewSet_IsClosedFromDeadlineDay2300(string now, bool expectedDeadlinePassed)
    {
        var repository = _s.Seed(ActualResultsScenario.Untouched(deadline: new DateTime(2026, 6, 30, 10, 0, 0)));

        var outcome = await SaveAsync(repository, _s.PostStored(), DateTime.Parse(now, System.Globalization.CultureInfo.InvariantCulture));

        Assert.Equal(expectedDeadlinePassed ? ActualResultsSaveStatus.DeadlinePassed : ActualResultsSaveStatus.Saved, outcome.Status);
        Assert.Equal(expectedDeadlinePassed ? 0 : 1, repository.SavedActualResults.Count);
    }

    [Fact]
    public async Task Save_ExistingSetWithDeadlineTimeOfDay_UsesTheFullDeadlineTimestampPlus23Hours()
    {
        // Deadline 10:00 -> existing sets may still save until 09:00 the next day.
        var repository = _s.Seed(_s.FullyStored(deadline: new DateTime(2026, 6, 30, 10, 0, 0)));
        var update = _s.PostStored(auditWhy: "late fix", edit: (samples, _) => samples[_s.CultureS1Count] = "99");

        var open = await SaveAsync(repository, update, new DateTime(2026, 7, 1, 8, 59, 0));
        var closed = await SaveAsync(repository, update, new DateTime(2026, 7, 1, 9, 1, 0));

        Assert.Equal(ActualResultsSaveStatus.Saved, open.Status);
        Assert.Equal(ActualResultsSaveStatus.DeadlinePassed, closed.Status);
        Assert.Single(repository.SavedActualResults);
    }

    // ---------- Save: audit reason ----------

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task Save_ExistingSetChangedWithoutAuditReason_RequiresReasonAndWritesNothing(string? auditWhy)
    {
        var repository = _s.Seed(_s.FullyStored());

        var outcome = await SaveAsync(
            repository, _s.PostStored(auditWhy: auditWhy!, edit: (samples, _) => samples[_s.CultureS1Count] = "13"));

        Assert.Equal(ActualResultsSaveStatus.AuditReasonRequired, outcome.Status);
        Assert.False(outcome.Success);
        Assert.Equal("Results Received for D1/S1 BT: Brucella", outcome.AuditObjectName);
        Assert.Empty(outcome.FieldErrorsByItemId);
        Assert.Empty(repository.SavedActualResults);
    }

    [Fact]
    public async Task Save_ExistingSetChangedWithAuditReason_WritesChangedRowsAsUpdatesAndKeepsFirstEditDate()
    {
        var firstEdit = new DateTime(2026, 6, 1, 9, 30, 0);
        var repository = _s.Seed(_s.FullyStored(firstEdit));

        var outcome = await SaveAsync(repository, _s.PostStored(auditWho: "APHA\\jbloggs", auditWhy: "Lab corrected values", edit: (samples, methods) =>
        {
            samples[_s.CultureS1Count] = "13";
            methods[_s.IncubationMethod] = "38C";
        }));

        Assert.Equal(ActualResultsSaveStatus.Saved, outcome.Status);
        Assert.True(outcome.Success);
        var write = Assert.Single(repository.SavedActualResults);
        Assert.Equal(_s.ParticipantId, write.DistributionParticipantId);
        Assert.False(write.IsNew);
        Assert.Equal(firstEdit, write.FirstEditDate);
        Assert.Equal(Now, write.LastEditDate);
        Assert.Equal("APHA\\jbloggs", write.AuditWho);
        Assert.Equal("Lab corrected values", write.AuditWhy);
        Assert.Equal(new ActualResultWriteItem(_s.CultureS1Count, "13", IsInsert: false), Assert.Single(write.SampleItems));
        Assert.Equal(new ActualResultWriteItem(_s.IncubationMethod, "38C", IsInsert: false), Assert.Single(write.MethodItems));
        Assert.Empty(write.RatingSampleIds);
        Assert.Null(write.DefaultRatingId);
    }

    [Fact]
    public async Task Save_ExistingSetMixOfChangedAndMissingRows_FlagsOnlyMissingRowsAsInserts()
    {
        var stored = _s.FullyStored();
        var actual = stored with { SampleResults = [.. stored.SampleResults.Where(r => r.SampleResultItemId != _s.UnusedS1)] };
        var repository = _s.Seed(actual);

        await SaveAsync(repository, _s.PostStored(auditWhy: "why", edit: (samples, _) => samples[_s.CultureS2Growth] = "Positive"));

        var write = Assert.Single(repository.SavedActualResults);
        Assert.Equal(
            [new ActualResultWriteItem(_s.CultureS2Growth, "Positive", IsInsert: false), new ActualResultWriteItem(_s.UnusedS1, string.Empty, IsInsert: true)],
            write.SampleItems);
    }

    [Fact]
    public async Task Save_UnchangedRowsAreNotRewritten_WhenAnotherRowChanges()
    {
        var repository = _s.Seed(_s.FullyStored());

        await SaveAsync(repository, _s.PostStored(auditWhy: "why", edit: (samples, _) => samples[_s.CultureS1Count] = "13"));

        var write = Assert.Single(repository.SavedActualResults);
        Assert.Single(write.SampleItems);
        Assert.Empty(write.MethodItems);
    }

    // ---------- Save: new set ----------

    [Fact]
    public async Task Save_NewSet_InsertsEveryStructureItemIncludingNonApplicableAndMethodItems()
    {
        var repository = _s.Seed(ActualResultsScenario.Untouched());

        var outcome = await SaveAsync(repository, _s.PostStored(auditWho: "APHA\\jbloggs", auditWhy: "ignored for a first save"));

        Assert.Equal(ActualResultsSaveStatus.Saved, outcome.Status);
        var write = Assert.Single(repository.SavedActualResults);
        Assert.True(write.IsNew);
        Assert.Equal((Now, Now), (write.FirstEditDate, write.LastEditDate));
        Assert.Equal("APHA\\jbloggs", write.AuditWho);
        Assert.Equal(string.Empty, write.AuditWhy);
        Assert.All(write.SampleItems.Concat(write.MethodItems), i => Assert.True(i.IsInsert));
        Assert.Equal(_s.AllSampleResultItemIds.Order(), write.SampleItems.Select(i => i.ItemId).Order());
        Assert.Equal(_s.AllMethodItemIds.Order(), write.MethodItems.Select(i => i.ItemId).Order());

        var values = write.SampleItems.ToDictionary(i => i.ItemId, i => i.Result);
        Assert.Equal("12", values[_s.CultureS1Count]);
        Assert.Equal("5", values[_s.SerologyS1Titre]);
        Assert.Equal(string.Empty, values[_s.SerologyS2Titre]);
        Assert.Equal("37C", write.MethodItems.Single(i => i.ItemId == _s.IncubationMethod).Result);
    }

    [Fact]
    public async Task Save_NewSetWithoutStoreRatings_DoesNotReadOrWriteRatings()
    {
        var repository = _s.Seed(ActualResultsScenario.Untouched(), storeRatings: false);
        repository.RatingIds.Add(Guid.NewGuid());

        await SaveAsync(repository, _s.PostStored());

        var write = Assert.Single(repository.SavedActualResults);
        Assert.Null(write.DefaultRatingId);
        Assert.Empty(write.RatingSampleIds);
    }

    [Fact]
    public async Task Save_NewSetWithStoreRatings_UsesFirstRatingAsDefaultForEverySample()
    {
        var repository = _s.Seed(ActualResultsScenario.Untouched(), storeRatings: true);
        var defaultRating = Guid.NewGuid();
        repository.RatingIds.AddRange([defaultRating, Guid.NewGuid()]);

        var outcome = await SaveAsync(repository, _s.PostStored());

        Assert.Equal(ActualResultsSaveStatus.Saved, outcome.Status);
        var write = Assert.Single(repository.SavedActualResults);
        Assert.Equal(defaultRating, write.DefaultRatingId);
        Assert.Equal([_s.Sample1, _s.Sample2], write.RatingSampleIds);
    }

    [Fact]
    public async Task Save_NewSetWithStoreRatingsButNoRatings_Throws()
    {
        var repository = _s.Seed(ActualResultsScenario.Untouched(), storeRatings: true);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => SaveAsync(repository, _s.PostStored()));

        Assert.Equal("No Default Rating in database.", ex.Message);
        Assert.Empty(repository.SavedActualResults);
    }

    [Fact]
    public async Task Save_ExistingSetWithStoreRatings_DoesNotWriteRatings()
    {
        var repository = _s.Seed(_s.FullyStored(), storeRatings: true);
        repository.RatingIds.Add(Guid.NewGuid());

        await SaveAsync(repository, _s.PostStored(auditWhy: "why", edit: (samples, _) => samples[_s.CultureS1Count] = "13"));

        var write = Assert.Single(repository.SavedActualResults);
        Assert.Null(write.DefaultRatingId);
        Assert.Empty(write.RatingSampleIds);
    }

    // ---------- Save: posted payload edge cases ----------

    [Fact]
    public async Task Save_PostedIdsNotInTheStructure_AreIgnored()
    {
        var repository = _s.Seed(_s.FullyStored());
        var update = _s.PostStored(edit: (samples, methods) =>
        {
            samples[Guid.NewGuid()] = "not a real item - and would fail a regex if it were";
            methods[Guid.NewGuid()] = "ignored";
        });

        var outcome = await SaveAsync(repository, update);

        Assert.Equal(ActualResultsSaveStatus.NothingToSave, outcome.Status);
        Assert.Empty(repository.SavedActualResults);
    }

    [Fact]
    public async Task Save_NullPostedResult_IsTreatedAsEmpty()
    {
        var repository = _s.Seed(_s.FullyStored());
        var update = new ActualResultsUpdate(
            [new ActualResultsSampleValueUpdate(_s.CultureS1Count, null!)],
            [new ActualResultsMethodItemUpdate(_s.IncubationMethod, null!)],
            "tester",
            "cleared by user");

        var outcome = await SaveAsync(repository, update);

        Assert.Equal(ActualResultsSaveStatus.Saved, outcome.Status);
        var write = Assert.Single(repository.SavedActualResults);
        Assert.Equal(new ActualResultWriteItem(_s.CultureS1Count, string.Empty, IsInsert: false), Assert.Single(write.SampleItems));
        Assert.Equal(new ActualResultWriteItem(_s.IncubationMethod, string.Empty, IsInsert: false), Assert.Single(write.MethodItems));
    }

    [Fact]
    public async Task Save_NullStoredResult_IsComparedAsEmpty()
    {
        var actual = new ActualResultsData(
            ActualResultsScenario.Header(new DateTime(2026, 6, 1)),
            [.. _s.StoredSampleValues.Select(kv => new ActualSampleResultEntity { SampleResultItemId = kv.Key, Result = kv.Key == _s.CultureS1Count ? null : kv.Value })],
            [.. _s.StoredMethodValues.Select(kv => new ActualMethodItemResultEntity { TestMethodItemId = kv.Key, Result = kv.Value })]);
        var repository = _s.Seed(actual);

        var outcome = await SaveAsync(repository, _s.PostStored(auditWhy: "set count", edit: (samples, _) => samples[_s.CultureS1Count] = string.Empty));

        // Stored null == posted "" so that row is not dirty; the other rows are unchanged too.
        Assert.Equal(ActualResultsSaveStatus.NothingToSave, outcome.Status);
    }

    [Fact]
    public void Outcome_Of_SharesNoErrorsAndSuccessIsOnlySavedOrNothingToSave()
    {
        Assert.True(ActualResultsSaveOutcome.Of(ActualResultsSaveStatus.Saved).Success);
        Assert.True(ActualResultsSaveOutcome.Of(ActualResultsSaveStatus.NothingToSave).Success);
        foreach (var failure in new[]
        {
            ActualResultsSaveStatus.NotFound, ActualResultsSaveStatus.ValidationFailed,
            ActualResultsSaveStatus.DeadlinePassed, ActualResultsSaveStatus.AuditReasonRequired,
        })
        {
            var outcome = ActualResultsSaveOutcome.Of(failure);
            Assert.False(outcome.Success);
            Assert.Empty(outcome.FieldErrorsByItemId);
            Assert.Null(outcome.AuditObjectName);
        }
    }
}
