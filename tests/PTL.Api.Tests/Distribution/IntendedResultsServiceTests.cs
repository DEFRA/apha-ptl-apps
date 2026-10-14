using PTL.Core.Distribution;

namespace PTL.Api.Tests.Distribution;

// Pins legacy Scheduling/IntendedResults.aspx(.vb) read/save behaviour: IntendedResultsSet.Fetch
// -> PopulateTabContainer for the read, LoadObjectFromForm -> Save() for the write.
public class IntendedResultsServiceTests
{
    private static readonly Guid SchemeId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid SampleOneId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid SampleTwoId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid TestId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid SampleOneTestId = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private static readonly Guid SampleTwoTestId = Guid.Parse("66666666-6666-6666-6666-666666666666");
    private static readonly Guid MethodItemId = Guid.Parse("77777777-7777-7777-7777-777777777777");
    private static readonly Guid ResultItemId = Guid.Parse("88888888-8888-8888-8888-888888888888");
    private static readonly Guid RangeResultItemId = Guid.Parse("99999999-9999-9999-9999-999999999999");
    private static readonly Guid SampleOneResultValueId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid SampleOneRangeValueId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid SampleTwoResultValueId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

    private const string AuditWho = "APHA\\tester";
    private const string AuditReason = "Corrected a result after re-checking the original data";

    private static MonthlyDistributionSchemeEntity Scheme(bool hasSampleNumbersDefined = true) => new()
    {
        MonthlyDistributionSchemeId = SchemeId,
        SchemeIdentifier = "PT0060",
        SchemeName = "Mastitis bovine",
        DistributionReference = "17152",
        DistributionReferenceSuffix = "A",
        ScheduleCode = "BA",
        HasSampleNumbersDefined = hasSampleNumbersDefined,
    };

    // Everything stored exactly as fetched: nothing has ever been entered yet, so
    // RequiresAuditReason starts false and HasIntendedResults is still false (first save).
    private static IntendedResultsData Data() => new(
        new IntendedResultsHeaderEntity
        {
            MonthlyDistributionSchemeId = SchemeId,
            TestConsultantTabulationId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"),
            StoreRatings = true,
            HasSampleNumbersDefined = true,
        },
        [
            new DistributionSampleEntity { SampleId = SampleOneId, DistributionSchemeId = SchemeId, SampleNumber = "S1", InternalDescription = "Internal 1" },
            new DistributionSampleEntity { SampleId = SampleTwoId, DistributionSchemeId = SchemeId, SampleNumber = "S2", InternalDescription = "Internal 2" },
        ],
        [
            new DistributionSampleTestEntity { SampleTestId = SampleOneTestId, SampleId = SampleOneId, TestId = TestId, IsApplicable = true },
            new DistributionSampleTestEntity { SampleTestId = SampleTwoTestId, SampleId = SampleTwoId, TestId = TestId, IsApplicable = false },
        ],
        [new DistributionTestEntity { TestId = TestId, TestName = "Somatic cell count", Order = 1 }],
        [new DistributionTestMethodItemEntity
        {
            TestMethodItemId = MethodItemId, TestId = TestId, Name = "Method A", Order = 1,
            FormatString = "^[0-9]+$", ReturnValueType = "Numeric", IntendedResult = "",
        }],
        [
            new DistributionTestResultItemEntity
            {
                TestResultItemId = ResultItemId, TestId = TestId, Name = "Result A", Order = 1,
                FormatString = "^[0-9]+$", ReturnValueType = "Numeric", IsRange = false,
            },
            new DistributionTestResultItemEntity
            {
                TestResultItemId = RangeResultItemId, TestId = TestId, Name = "Result B", Order = 2,
                FormatString = "^[0-9]+$", ReturnValueType = "Numeric", IsRange = true,
            },
        ],
        [],
        [],
        [
            new DistributionSampleResultItemEntity
            {
                SampleResultItemId = SampleOneResultValueId, SampleId = SampleOneId, SampleTestId = SampleOneTestId,
                TestResultItemId = ResultItemId, IntendedResult = "", IntendedResult2 = "",
            },
            new DistributionSampleResultItemEntity
            {
                SampleResultItemId = SampleOneRangeValueId, SampleId = SampleOneId, SampleTestId = SampleOneTestId,
                TestResultItemId = RangeResultItemId, IntendedResult = "", IntendedResult2 = "",
            },
            new DistributionSampleResultItemEntity
            {
                SampleResultItemId = SampleTwoResultValueId, SampleId = SampleTwoId, SampleTestId = SampleTwoTestId,
                TestResultItemId = ResultItemId, IntendedResult = "", IntendedResult2 = "",
            },
        ]);

    // A distribution that has already been saved once - HasIntendedResults is already true, so
    // only the posted edits (or lack of them) can make this save dirty.
    private static IntendedResultsData SavedData(bool anyResultEverEntered = false)
    {
        var data = Data();
        data.Header.HasIntendedResults = true;
        if (anyResultEverEntered)
        {
            var value = data.SampleResultItems.First(s => s.SampleResultItemId == SampleOneResultValueId);
            value.IntendedResult = "100";
        }

        return data;
    }

    private static (IntendedResultsService Service, FakeDistributionRepository Repository) CreateService(
        MonthlyDistributionSchemeEntity? scheme = null, IntendedResultsData? data = null)
    {
        var repository = new FakeDistributionRepository();
        if (scheme is not null)
        {
            repository.SchemesById[SchemeId] = scheme;
        }

        if (data is not null)
        {
            repository.IntendedResults[SchemeId] = data;
        }

        return (new IntendedResultsService(repository), repository);
    }

    private static IntendedResultsMethodItemUpdate MethodUpdate(string intendedResult) => new(MethodItemId, intendedResult);

    private static IntendedResultsSampleResultValueUpdate Value(Guid sampleResultItemId, string intendedResult, string intendedResult2 = "") =>
        new(sampleResultItemId, intendedResult, intendedResult2);

    private static IntendedResultsSampleRowUpdate SampleOneRow(bool isApplicable, params IntendedResultsSampleResultValueUpdate[] values) =>
        new(SampleOneId, isApplicable, values);

    // Sample One posted back exactly as stored, so nothing on it is dirty.
    private static IntendedResultsSampleRowUpdate UnchangedSampleOneRow() =>
        SampleOneRow(true, Value(SampleOneResultValueId, ""), Value(SampleOneRangeValueId, "", ""));

    private static IntendedResultsTestUpdate TestUpdate(
        IReadOnlyList<IntendedResultsMethodItemUpdate>? methodItems = null,
        IReadOnlyList<IntendedResultsResultItemUpdate>? resultItems = null,
        IReadOnlyList<IntendedResultsSampleRowUpdate>? samples = null) =>
        new(TestId, resultItems ?? [], methodItems ?? [new IntendedResultsMethodItemUpdate(MethodItemId, "")], samples ?? [UnchangedSampleOneRow()]);

    private static IntendedResultsUpdate Update(
        IntendedResultsTestUpdate test, bool requiresAuditReason = false, string auditWho = AuditWho, string auditWhy = "") =>
        new([test], requiresAuditReason, auditWho, auditWhy);

    // ---- GetIntendedResultsAsync ----

    [Fact]
    public async Task GetIntendedResultsAsync_SchemeNotFound_ReturnsNull()
    {
        var (service, _) = CreateService(scheme: null, data: Data());

        Assert.Null(await service.GetIntendedResultsAsync(SchemeId, CancellationToken.None));
    }

    // Legacy Page_Load: "Samples numbers must be defined before setting the Intended Results".
    [Fact]
    public async Task GetIntendedResultsAsync_SampleNumbersNotYetDefined_ReturnsNull()
    {
        var (service, _) = CreateService(Scheme(hasSampleNumbersDefined: false), Data());

        Assert.Null(await service.GetIntendedResultsAsync(SchemeId, CancellationToken.None));
    }

    [Fact]
    public async Task GetIntendedResultsAsync_IntendedResultsDataNotFound_ReturnsNull()
    {
        var (service, _) = CreateService(Scheme(), data: null);

        Assert.Null(await service.GetIntendedResultsAsync(SchemeId, CancellationToken.None));
    }

    [Fact]
    public async Task GetIntendedResultsAsync_MapsHeadingFromScheme()
    {
        var (service, _) = CreateService(Scheme(), Data());

        var page = await service.GetIntendedResultsAsync(SchemeId, CancellationToken.None);

        Assert.NotNull(page);
        Assert.Equal(SchemeId, page!.MonthlyDistributionSchemeId);
        Assert.Equal("17152A/BA", page.DistributionReferenceFull);
        Assert.Equal("PT0060", page.SchemeIdentifier);
        Assert.Equal("Mastitis bovine", page.SchemeName);
    }

    [Fact]
    public async Task GetIntendedResultsAsync_MapsTestsResultItemsAndMethodItemsInOrder()
    {
        var (service, _) = CreateService(Scheme(), Data());

        var page = await service.GetIntendedResultsAsync(SchemeId, CancellationToken.None);

        var test = Assert.Single(page!.Tests);
        Assert.Equal(TestId, test.TestId);
        Assert.Equal("Somatic cell count", test.TestName);
        Assert.Equal(["Result A", "Result B"], test.ResultItems.Select(r => r.Name));
        Assert.Equal(["Method A"], test.MethodItems.Select(m => m.Name));
    }

    [Fact]
    public async Task GetIntendedResultsAsync_ResultItem_PassesThroughIsRangeFromStorage()
    {
        var (service, _) = CreateService(Scheme(), Data());

        var page = await service.GetIntendedResultsAsync(SchemeId, CancellationToken.None);

        var test = Assert.Single(page!.Tests);
        Assert.False(test.ResultItems.Single(r => r.TestResultItemId == ResultItemId).IsRange);
        Assert.True(test.ResultItems.Single(r => r.TestResultItemId == RangeResultItemId).IsRange);
    }

    [Fact]
    public async Task GetIntendedResultsAsync_MethodItem_MapsFormatStringReturnValueTypeAndIntendedResult()
    {
        var data = Data();
        data.TestMethodItems[0].IntendedResult = "Positive";
        var (service, _) = CreateService(Scheme(), data);

        var page = await service.GetIntendedResultsAsync(SchemeId, CancellationToken.None);

        var methodItem = Assert.Single(Assert.Single(page!.Tests).MethodItems);
        Assert.Equal("^[0-9]+$", methodItem.FormatString);
        Assert.Equal("Numeric", methodItem.ReturnValueType);
        Assert.Equal("Positive", methodItem.IntendedResult);
    }

    [Fact]
    public async Task GetIntendedResultsAsync_SampleValues_MatchedByTestResultItemIdInResultItemOrder()
    {
        var data = Data();
        data.SampleResultItems.Single(s => s.SampleResultItemId == SampleOneResultValueId).IntendedResult = "12";
        data.SampleResultItems.Single(s => s.SampleResultItemId == SampleOneRangeValueId).IntendedResult = "5";
        data.SampleResultItems.Single(s => s.SampleResultItemId == SampleOneRangeValueId).IntendedResult2 = "15";
        var (service, _) = CreateService(Scheme(), data);

        var page = await service.GetIntendedResultsAsync(SchemeId, CancellationToken.None);

        var sample = Assert.Single(page!.Tests).Samples.Single(s => s.SampleId == SampleOneId);
        Assert.Equal(2, sample.Values.Count);
        Assert.Equal(ResultItemId, sample.Values[0].TestResultItemId);
        Assert.Equal("12", sample.Values[0].IntendedResult);
        Assert.Equal(RangeResultItemId, sample.Values[1].TestResultItemId);
        Assert.Equal("5", sample.Values[1].IntendedResult);
        Assert.Equal("15", sample.Values[1].IntendedResult2);
    }

    [Fact]
    public async Task GetIntendedResultsAsync_SampleAllocation_IsApplicablePassesThroughFromSampleTest()
    {
        var (service, _) = CreateService(Scheme(), Data());

        var page = await service.GetIntendedResultsAsync(SchemeId, CancellationToken.None);

        var test = Assert.Single(page!.Tests);
        Assert.True(test.Samples.Single(s => s.SampleId == SampleOneId).IsApplicable);
        Assert.False(test.Samples.Single(s => s.SampleId == SampleTwoId).IsApplicable);
    }

    // No tblSampleTest row at all for this sample/test pair - never allocated.
    [Fact]
    public async Task GetIntendedResultsAsync_SampleWithNoSampleTestRow_IsApplicableFalseAndValuesAreBlank()
    {
        var data = Data();
        var withoutSampleTests = data with { SampleTests = [] };
        var (service, _) = CreateService(Scheme(), withoutSampleTests);

        var page = await service.GetIntendedResultsAsync(SchemeId, CancellationToken.None);

        var sample = Assert.Single(page!.Tests).Samples.Single(s => s.SampleId == SampleOneId);
        Assert.False(sample.IsApplicable);
        Assert.All(sample.Values, v => Assert.Equal(Guid.Empty, v.SampleResultItemId));
        Assert.All(sample.Values, v => Assert.Equal(string.Empty, v.IntendedResult));
    }

    // A sample/test pair exists but has no matching tblSampleResultItem row for a given result
    // item - still produces a value entry, just an empty/default one, so the table never has a
    // ragged row.
    [Fact]
    public async Task GetIntendedResultsAsync_SampleTestWithoutMatchingSampleResultItem_ProducesDefaultValue()
    {
        var data = Data();
        var withoutSampleTwoResultItems = data with { SampleResultItems = [.. data.SampleResultItems.Where(s => s.SampleId != SampleTwoId)] };
        var (service, _) = CreateService(Scheme(), withoutSampleTwoResultItems);

        var page = await service.GetIntendedResultsAsync(SchemeId, CancellationToken.None);

        var sample = Assert.Single(page!.Tests).Samples.Single(s => s.SampleId == SampleTwoId);
        Assert.Equal(2, sample.Values.Count);
        Assert.All(sample.Values, v => Assert.Equal(Guid.Empty, v.SampleResultItemId));
        Assert.All(sample.Values, v => Assert.Equal(string.Empty, v.IntendedResult));
    }

    [Fact]
    public async Task GetIntendedResultsAsync_NullNames_AreCoercedToEmpty()
    {
        var data = Data();
        data.Tests[0].TestName = null;
        data.TestResultItems[0].Name = null;
        data.TestMethodItems[0].Name = null;
        var (service, _) = CreateService(Scheme(), data);

        var page = await service.GetIntendedResultsAsync(SchemeId, CancellationToken.None);

        var test = Assert.Single(page!.Tests);
        Assert.Equal(string.Empty, test.TestName);
        Assert.Equal(string.Empty, test.ResultItems[0].Name);
        Assert.Equal(string.Empty, test.MethodItems[0].Name);
    }

    [Fact]
    public async Task GetIntendedResultsAsync_NothingEverEntered_RequiresAuditReasonIsFalse()
    {
        var (service, _) = CreateService(Scheme(), Data());

        var page = await service.GetIntendedResultsAsync(SchemeId, CancellationToken.None);

        Assert.False(page!.RequiresAuditReason);
    }

    [Fact]
    public async Task GetIntendedResultsAsync_MethodItemAlreadyHasAValue_RequiresAuditReasonIsTrue()
    {
        var data = Data();
        data.TestMethodItems[0].IntendedResult = "Positive";
        var (service, _) = CreateService(Scheme(), data);

        var page = await service.GetIntendedResultsAsync(SchemeId, CancellationToken.None);

        Assert.True(page!.RequiresAuditReason);
    }

    [Theory]
    [InlineData("12", "")]
    [InlineData("", "15")]
    public async Task GetIntendedResultsAsync_SampleResultItemAlreadyHasAValue_RequiresAuditReasonIsTrue(string intendedResult, string intendedResult2)
    {
        var data = Data();
        var value = data.SampleResultItems.Single(s => s.SampleResultItemId == SampleOneResultValueId);
        value.IntendedResult = intendedResult;
        value.IntendedResult2 = intendedResult2;
        var (service, _) = CreateService(Scheme(), data);

        var page = await service.GetIntendedResultsAsync(SchemeId, CancellationToken.None);

        Assert.True(page!.RequiresAuditReason);
    }

    // ---- SaveIntendedResultsAsync ----

    [Fact]
    public async Task SaveIntendedResultsAsync_DistributionCannotBeReRead_FailsWithNoFieldErrors()
    {
        var (service, repository) = CreateService(Scheme(), data: null);

        var outcome = await service.SaveIntendedResultsAsync(SchemeId, Update(TestUpdate()), CancellationToken.None);

        Assert.False(outcome.Success);
        Assert.Empty(outcome.FieldErrorsByItemId);
        Assert.Empty(repository.SavedIntendedResults);
    }

    [Fact]
    public async Task SaveIntendedResultsAsync_InvalidMethodItemValue_FailsAndWritesNothing()
    {
        var (service, repository) = CreateService(Scheme(), Data());
        var update = Update(TestUpdate(methodItems: [MethodUpdate("abc")]));

        var outcome = await service.SaveIntendedResultsAsync(SchemeId, update, CancellationToken.None);

        Assert.False(outcome.Success);
        var errors = Assert.Single(outcome.FieldErrorsByItemId);
        Assert.Equal(MethodItemId, errors.Key);
        Assert.Contains(errors.Value, e => e.Field == "IntendedResult" && e.Message == "Input must be of format: Numeric");
        Assert.Empty(repository.SavedIntendedResults);
    }

    [Fact]
    public async Task SaveIntendedResultsAsync_InvalidSampleResultValue_FailsAndWritesNothing()
    {
        var (service, repository) = CreateService(Scheme(), Data());
        var update = Update(TestUpdate(samples: [SampleOneRow(true, Value(SampleOneResultValueId, "abc"), Value(SampleOneRangeValueId, "", ""))]));

        var outcome = await service.SaveIntendedResultsAsync(SchemeId, update, CancellationToken.None);

        Assert.False(outcome.Success);
        var errors = Assert.Single(outcome.FieldErrorsByItemId);
        Assert.Equal(SampleOneResultValueId, errors.Key);
        Assert.Contains(errors.Value, e => e.Field == "IntendedResult");
        Assert.Empty(repository.SavedIntendedResults);
    }

    // IntendedResult2 is only validated for a range result item.
    [Fact]
    public async Task SaveIntendedResultsAsync_InvalidSecondValue_OnlyValidatedWhenResultItemIsRange()
    {
        var (service, repository) = CreateService(Scheme(), Data());
        var update = Update(TestUpdate(samples:
            [SampleOneRow(true, Value(SampleOneResultValueId, ""), Value(SampleOneRangeValueId, "5", "xyz"))]));

        var outcome = await service.SaveIntendedResultsAsync(SchemeId, update, CancellationToken.None);

        Assert.False(outcome.Success);
        var errors = Assert.Single(outcome.FieldErrorsByItemId[SampleOneRangeValueId], e => e.Field == "IntendedResult2");
        Assert.Equal("Input must be of format: Numeric", errors.Message);
        Assert.Empty(repository.SavedIntendedResults);
    }

    // Legacy disables every Result Item control for a non-applicable sample/test pair, so a
    // disabled RegularExpressionValidator never fires.
    [Fact]
    public async Task SaveIntendedResultsAsync_NonApplicableSample_SkipsValidationEvenWhenValueIsInvalid()
    {
        var (service, repository) = CreateService(Scheme(), Data());
        var update = Update(TestUpdate(samples:
        [
            UnchangedSampleOneRow(),
            new IntendedResultsSampleRowUpdate(SampleTwoId, false, [Value(SampleTwoResultValueId, "not-numeric")]),
        ]));

        var outcome = await service.SaveIntendedResultsAsync(SchemeId, update, CancellationToken.None);

        Assert.True(outcome.Success);
    }

    [Fact]
    public async Task SaveIntendedResultsAsync_UnknownMethodItemId_IsIgnored()
    {
        var (service, repository) = CreateService(Scheme(), Data());
        var update = Update(TestUpdate(methodItems: [new IntendedResultsMethodItemUpdate(Guid.NewGuid(), "whatever")]));

        var outcome = await service.SaveIntendedResultsAsync(SchemeId, update, CancellationToken.None);

        Assert.True(outcome.Success);
        Assert.Empty(outcome.FieldErrorsByItemId);
    }

    [Fact]
    public async Task SaveIntendedResultsAsync_UnknownSampleResultItemId_IsIgnored()
    {
        var (service, repository) = CreateService(Scheme(), SavedData());
        var update = Update(TestUpdate(samples: [SampleOneRow(true, Value(Guid.NewGuid(), "whatever"))]));

        var outcome = await service.SaveIntendedResultsAsync(SchemeId, update, CancellationToken.None);

        Assert.True(outcome.Success);
        Assert.Empty(outcome.FieldErrorsByItemId);
        Assert.Empty(repository.SavedIntendedResults);
    }

    [Fact]
    public async Task SaveIntendedResultsAsync_NothingDirtyAndAlreadySaved_SucceedsWithoutRepositoryCall()
    {
        var (service, repository) = CreateService(Scheme(), SavedData());

        var outcome = await service.SaveIntendedResultsAsync(SchemeId, Update(TestUpdate()), CancellationToken.None);

        Assert.True(outcome.Success);
        Assert.Empty(repository.SavedIntendedResults);
    }

    // Legacy's mIntendedResults.HasIntendedResults = True runs unconditionally, so the very first
    // save always writes even when every posted value matches what was already stored.
    [Fact]
    public async Task SaveIntendedResultsAsync_FirstSave_WritesEvenWhenNothingElseIsDirty()
    {
        var (service, repository) = CreateService(Scheme(), Data());

        var outcome = await service.SaveIntendedResultsAsync(SchemeId, Update(TestUpdate()), CancellationToken.None);

        Assert.True(outcome.Success);
        var saved = Assert.Single(repository.SavedIntendedResults);
        Assert.True(saved.Header.HasIntendedResults);
        Assert.Empty(saved.TestMethodItems);
        Assert.Empty(saved.SampleResultItems);
    }

    [Fact]
    public async Task SaveIntendedResultsAsync_MethodItemChanged_WritesOnlyThatMethodItem()
    {
        var (service, repository) = CreateService(Scheme(), SavedData());

        await service.SaveIntendedResultsAsync(SchemeId, Update(TestUpdate(methodItems: [MethodUpdate("54")])), CancellationToken.None);

        var saved = Assert.Single(repository.SavedIntendedResults);
        var methodItem = Assert.Single(saved.TestMethodItems);
        Assert.Equal(MethodItemId, methodItem.TestMethodItemId);
        Assert.Equal("54", methodItem.IntendedResult);
        Assert.Empty(saved.SampleResultItems);
        Assert.Empty(saved.TestResultItems);
    }

    [Fact]
    public async Task SaveIntendedResultsAsync_ResultItemIsRangeToggled_WritesTestResultItem()
    {
        var (service, repository) = CreateService(Scheme(), SavedData());
        var update = Update(TestUpdate(resultItems: [new IntendedResultsResultItemUpdate(ResultItemId, true)]));

        await service.SaveIntendedResultsAsync(SchemeId, update, CancellationToken.None);

        var saved = Assert.Single(repository.SavedIntendedResults);
        var resultItem = Assert.Single(saved.TestResultItems);
        Assert.Equal(ResultItemId, resultItem.TestResultItemId);
        Assert.True(resultItem.IsRange);
    }

    [Fact]
    public async Task SaveIntendedResultsAsync_ResultItemIsRangeUnchanged_WritesNothingForTheResultItem()
    {
        var (service, repository) = CreateService(Scheme(), SavedData());
        var update = Update(TestUpdate(
            methodItems: [MethodUpdate("54")],
            resultItems: [new IntendedResultsResultItemUpdate(ResultItemId, false)]));

        await service.SaveIntendedResultsAsync(SchemeId, update, CancellationToken.None);

        var saved = Assert.Single(repository.SavedIntendedResults);
        Assert.Empty(saved.TestResultItems);
        Assert.Single(saved.TestMethodItems);
    }

    [Fact]
    public async Task SaveIntendedResultsAsync_SampleValueChanged_WritesOnlyThatSampleResultItem()
    {
        var (service, repository) = CreateService(Scheme(), SavedData());
        var update = Update(TestUpdate(samples: [SampleOneRow(true, Value(SampleOneResultValueId, "42"), Value(SampleOneRangeValueId, "", ""))]));

        await service.SaveIntendedResultsAsync(SchemeId, update, CancellationToken.None);

        var saved = Assert.Single(repository.SavedIntendedResults);
        var sampleResultItem = Assert.Single(saved.SampleResultItems);
        Assert.Equal(SampleOneResultValueId, sampleResultItem.SampleResultItemId);
        Assert.Equal("42", sampleResultItem.IntendedResult);
        Assert.Empty(saved.TestMethodItems);
    }

    // Mirrors legacy's disabled controls: a non-applicable sample/test pair is never posted back
    // by a real browser, so it must never be read as "changed to blank".
    [Fact]
    public async Task SaveIntendedResultsAsync_NonApplicableSample_SkipsDiffingEvenWhenValuesDiffer()
    {
        var data = SavedData();
        var (service, repository) = CreateService(Scheme(), data);
        var update = Update(TestUpdate(
            methodItems: [MethodUpdate("54")],
            samples:
            [
                UnchangedSampleOneRow(),
                new IntendedResultsSampleRowUpdate(SampleTwoId, false, [Value(SampleTwoResultValueId, "999")]),
            ]));

        await service.SaveIntendedResultsAsync(SchemeId, update, CancellationToken.None);

        Assert.Empty(Assert.Single(repository.SavedIntendedResults).SampleResultItems);
        Assert.Equal(string.Empty, data.SampleResultItems.Single(s => s.SampleResultItemId == SampleTwoResultValueId).IntendedResult);
    }

    // Legacy's second control is never even rendered outside Range mode, so it is never postable;
    // a posted IntendedResult2 is only meaningful when the posted IsRange for that item is true.
    [Fact]
    public async Task SaveIntendedResultsAsync_SecondValueChanged_IgnoredWhenPostedIsRangeFalse()
    {
        var data = SavedData();
        var (service, repository) = CreateService(Scheme(), data);
        var update = Update(TestUpdate(
            resultItems: [new IntendedResultsResultItemUpdate(RangeResultItemId, false)],
            samples: [SampleOneRow(true, Value(SampleOneResultValueId, ""), Value(SampleOneRangeValueId, "", "99"))]));

        await service.SaveIntendedResultsAsync(SchemeId, update, CancellationToken.None);

        var saved = Assert.Single(repository.SavedIntendedResults);
        Assert.Empty(saved.SampleResultItems);
        Assert.Single(saved.TestResultItems); // the IsRange toggle itself is still dirty and written
    }

    [Fact]
    public async Task SaveIntendedResultsAsync_SecondValueChanged_WrittenWhenPostedIsRangeTrue()
    {
        var (service, repository) = CreateService(Scheme(), SavedData());
        var update = Update(TestUpdate(
            resultItems: [new IntendedResultsResultItemUpdate(RangeResultItemId, true)],
            samples: [SampleOneRow(true, Value(SampleOneResultValueId, ""), Value(SampleOneRangeValueId, "10", "20"))]));

        await service.SaveIntendedResultsAsync(SchemeId, update, CancellationToken.None);

        var saved = Assert.Single(repository.SavedIntendedResults);
        var sampleResultItem = Assert.Single(saved.SampleResultItems);
        Assert.Equal("10", sampleResultItem.IntendedResult);
        Assert.Equal("20", sampleResultItem.IntendedResult2);
    }

    [Fact]
    public async Task SaveIntendedResultsAsync_AuditRequiredButNoReason_ReturnsNeedsAuditReasonAndWritesNothing()
    {
        var (service, repository) = CreateService(Scheme(), SavedData());
        var update = Update(TestUpdate(methodItems: [MethodUpdate("54")]), requiresAuditReason: true, auditWhy: "   ");

        var outcome = await service.SaveIntendedResultsAsync(SchemeId, update, CancellationToken.None);

        Assert.False(outcome.Success);
        Assert.True(outcome.AuditReasonRequired);
        Assert.Equal("Intended Results Set", outcome.AuditObjectName);
        Assert.Empty(repository.SavedIntendedResults);
    }

    // Matches Audit.aspx's own BtnSave_Click, which calls auditObject.Save() unconditionally once
    // a reason has been supplied - there is no further IsDirty gate on that page.
    [Fact]
    public async Task SaveIntendedResultsAsync_AuditRequiredWithReason_NothingDirty_StillSucceeds()
    {
        var (service, repository) = CreateService(Scheme(), SavedData());
        var update = Update(TestUpdate(), requiresAuditReason: true, auditWhy: AuditReason);

        var outcome = await service.SaveIntendedResultsAsync(SchemeId, update, CancellationToken.None);

        Assert.True(outcome.Success);
        Assert.False(outcome.AuditReasonRequired);
    }

    [Fact]
    public async Task SaveIntendedResultsAsync_AuditRequiredWithReason_WritesWithAuditedIds()
    {
        var (service, repository) = CreateService(Scheme(), SavedData());
        var update = Update(
            TestUpdate(
                methodItems: [MethodUpdate("54")],
                samples: [SampleOneRow(true, Value(SampleOneResultValueId, "42"), Value(SampleOneRangeValueId, "", ""))]),
            requiresAuditReason: true,
            auditWhy: AuditReason);

        var outcome = await service.SaveIntendedResultsAsync(SchemeId, update, CancellationToken.None);

        Assert.True(outcome.Success);
        var saved = Assert.Single(repository.SavedIntendedResults);
        Assert.Equal([MethodItemId], saved.AuditedTestMethodItemIds);
        Assert.Equal([SampleOneResultValueId], saved.AuditedSampleResultItems.Select(a => a.SampleResultItemId));
        Assert.Equal(AuditReason, saved.AuditWhy);
        Assert.Equal(AuditWho, saved.AuditWho);
    }

    // No audited id is ever supplied if audit was not required, even though the save is dirty.
    [Fact]
    public async Task SaveIntendedResultsAsync_AuditNotRequired_WritesWithNoAuditedIdsEvenThoughDirty()
    {
        var (service, repository) = CreateService(Scheme(), SavedData());
        var update = Update(TestUpdate(methodItems: [MethodUpdate("54")]), requiresAuditReason: false);

        await service.SaveIntendedResultsAsync(SchemeId, update, CancellationToken.None);

        var saved = Assert.Single(repository.SavedIntendedResults);
        Assert.Empty(saved.AuditedTestMethodItemIds);
        Assert.Empty(saved.AuditedSampleResultItems);
    }

    // Coarse audit gate: this save only touches the Method Item, yet because ANY result was ever
    // entered for the distribution (RequiresAuditReason was computed true at GET time), a reason
    // is demanded regardless of which particular field is the one that is dirty.
    [Fact]
    public async Task SaveIntendedResultsAsync_CoarseAuditGate_FiresRegardlessOfWhichFieldIsDirty()
    {
        var (service, repository) = CreateService(Scheme(), SavedData(anyResultEverEntered: true));
        var update = Update(TestUpdate(methodItems: [MethodUpdate("54")]), requiresAuditReason: true, auditWhy: "   ");

        var outcome = await service.SaveIntendedResultsAsync(SchemeId, update, CancellationToken.None);

        Assert.False(outcome.Success);
        Assert.True(outcome.AuditReasonRequired);
        Assert.Empty(repository.SavedIntendedResults);
    }

    // Matches legacy ButtonSave_Click's unconditional "If RequireAudit Then <Audit.aspx>" - the
    // gate is checked BEFORE the dirty/no-op check, so it fires even when this particular save
    // submission has nothing dirty at all.
    [Fact]
    public async Task SaveIntendedResultsAsync_AuditRequired_NothingDirtyThisSave_StillPromptsForAReason()
    {
        var (service, repository) = CreateService(Scheme(), SavedData());
        var update = Update(TestUpdate(), requiresAuditReason: true, auditWhy: "   ");

        var outcome = await service.SaveIntendedResultsAsync(SchemeId, update, CancellationToken.None);

        Assert.False(outcome.Success);
        Assert.True(outcome.AuditReasonRequired);
        Assert.Empty(repository.SavedIntendedResults);
    }

    [Fact]
    public async Task SaveIntendedResultsAsync_RoundTripsHeaderFieldsTheScreenNeverEdits()
    {
        var data = SavedData();
        var (service, repository) = CreateService(Scheme(), data);
        var update = Update(TestUpdate(methodItems: [MethodUpdate("54")]));

        await service.SaveIntendedResultsAsync(SchemeId, update, CancellationToken.None);

        var saved = Assert.Single(repository.SavedIntendedResults);
        Assert.Equal(data.Header.TestConsultantTabulationId, saved.Header.TestConsultantTabulationId);
        Assert.True(saved.Header.StoreRatings);
    }

    // tblDistributionTestMethodItem/tblSampleResultItem columns are nullable, so a stored NULL and
    // a posted "" are the same value.
    [Fact]
    public async Task SaveIntendedResultsAsync_StoredNullVersusPostedEmpty_IsNotDirty()
    {
        var data = SavedData();
        data.TestMethodItems[0].IntendedResult = null;
        data.SampleResultItems.Single(s => s.SampleResultItemId == SampleOneResultValueId).IntendedResult = null;
        var (service, repository) = CreateService(Scheme(), data);
        var update = Update(TestUpdate());

        var outcome = await service.SaveIntendedResultsAsync(SchemeId, update, CancellationToken.None);

        Assert.True(outcome.Success);
        Assert.Empty(repository.SavedIntendedResults);
    }

    [Fact]
    public async Task SaveIntendedResultsAsync_WriteSucceeds_CallsRepositoryExactlyOnce()
    {
        var (service, repository) = CreateService(Scheme(), SavedData());
        var update = Update(TestUpdate(methodItems: [MethodUpdate("54")]));

        await service.SaveIntendedResultsAsync(SchemeId, update, CancellationToken.None);

        Assert.Single(repository.SavedIntendedResults);
    }
}
