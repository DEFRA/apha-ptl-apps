using PTL.Core.Distribution;

namespace PTL.Api.Tests.Distribution;

// Pins legacy Scheduling/SampleNumbers.aspx(.vb) read/save behaviour: IntendedResultsSet.Fetch ->
// PopulateTabContainer for the read, LoadObjectFromForm -> Save() for the write.
public class SampleNumbersServiceTests
{
    private static readonly Guid SchemeId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid SampleOneId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid SampleTwoId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid TestId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid SampleOneTestId = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private static readonly Guid SampleTwoTestId = Guid.Parse("66666666-6666-6666-6666-666666666666");
    private static readonly Guid CategoryItemId = Guid.Parse("77777777-7777-7777-7777-777777777777");

    // Legacy Audit.aspx captures these before an auditable save is allowed to run.
    private const string AuditWho = "APHA\\tester";
    private const string AuditReason = "Corrected a typo in the tabulation description";

    private static MonthlyDistributionSchemeEntity Scheme() => new()
    {
        MonthlyDistributionSchemeId = SchemeId,
        SchemeIdentifier = "PT0060",
        SchemeName = "Mastitis bovine",
        DistributionReference = "17152",
        DistributionReferenceSuffix = "A",
        ScheduleCode = "BA",
        TotalSetsOfSamplesRequired = 9,
    };

    private static IntendedResultsData Data(
        bool requiresAssessment = false,
        DateTime? isReadyForAssessor = null,
        IReadOnlyList<DistributionCategoryItemEntity>? categoryItems = null,
        IReadOnlyList<DistributionCriterionItemEntity>? criterionItems = null,
        IReadOnlyList<DistributionTestEntity>? extraTests = null) =>
        new(
            new IntendedResultsHeaderEntity
            {
                MonthlyDistributionSchemeId = SchemeId,
                Comments = "Existing comments",
                TestConsultantTabulationId = Guid.Parse("88888888-8888-8888-8888-888888888888"),
                StoreRatings = true,
                RequiresAssessment = requiresAssessment,
                IsReadyForAssessor = isReadyForAssessor,
            },
            [
                new DistributionSampleEntity
                {
                    SampleId = SampleOneId,
                    DistributionSchemeId = SchemeId,
                    SampleNumber = "S1",
                    InternalDescription = "Internal 1",
                    ExternalDescription = "External 1",
                    TabulationDescription = "Tabulation 1",
                    TestConsultantsDescription = "TC 1",
                },
                // Every nullable string left null, to prove the read model coerces them to "".
                new DistributionSampleEntity { SampleId = SampleTwoId, DistributionSchemeId = SchemeId },
            ],
            [
                new DistributionSampleTestEntity { SampleTestId = SampleOneTestId, SampleId = SampleOneId, TestId = TestId, IsApplicable = true },
                new DistributionSampleTestEntity { SampleTestId = SampleTwoTestId, SampleId = SampleTwoId, TestId = TestId, IsApplicable = false },
            ],
            [new DistributionTestEntity { TestId = TestId, TestName = "Somatic cell count", Order = 1 }, .. extraTests ?? []],
            [
                new DistributionTestMethodItemEntity { TestMethodItemId = Guid.NewGuid(), TestId = TestId, Name = "Method A", Order = 1 },
                new DistributionTestMethodItemEntity { TestMethodItemId = Guid.NewGuid(), TestId = TestId, Name = "Method B", Order = 2 },
            ],
            [
                new DistributionTestResultItemEntity { TestResultItemId = Guid.NewGuid(), TestId = TestId, Name = "Result A", Order = 1 },
                new DistributionTestResultItemEntity { TestResultItemId = Guid.NewGuid(), TestId = TestId, Name = "Result B", Order = 2 },
            ],
            categoryItems ?? [],
            criterionItems ?? []);

    private static SampleNumberRowUpdate Row(
        Guid sampleId,
        string sampleNumber = "S1",
        string internalDescription = "Internal",
        string externalDescription = "",
        string tabulationDescription = "",
        string testConsultantsDescription = "",
        IReadOnlyList<SampleAllocationUpdate>? allocations = null) =>
        new(sampleId, sampleNumber, internalDescription, externalDescription, tabulationDescription, testConsultantsDescription, allocations ?? []);

    // Sample One posted back exactly as stored, so nothing on the row is dirty.
    private static SampleNumberRowUpdate UnchangedSampleOne(IReadOnlyList<SampleAllocationUpdate>? allocations = null) =>
        new(SampleOneId, "S1", "Internal 1", "External 1", "Tabulation 1", "TC 1", allocations ?? []);

    // A distribution that has already been saved once, so only the posted edits can make it dirty.
    private static IntendedResultsData SavedData(bool requiresAssessment = false, DateTime? isReadyForAssessor = null)
    {
        var data = Data(requiresAssessment, isReadyForAssessor);
        data.Header.HasSampleNumbersDefined = true;
        data.Header.HasIntendedResults = true;
        return data;
    }

    private static (SampleNumbersService Service, FakeDistributionRepository Repository) CreateService(
        MonthlyDistributionSchemeEntity? scheme = null, IntendedResultsData? data = null, FakeSchemeService? schemeService = null)
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

        return (new SampleNumbersService(repository, schemeService ?? new FakeSchemeService()), repository);
    }

    [Fact]
    public async Task GetSampleNumbersAsync_SchemeNotFound_ReturnsNull()
    {
        var (service, _) = CreateService(scheme: null, data: Data());

        Assert.Null(await service.GetSampleNumbersAsync(SchemeId, CancellationToken.None));
    }

    [Fact]
    public async Task GetSampleNumbersAsync_IntendedResultsNotFound_ReturnsNull()
    {
        var (service, _) = CreateService(scheme: Scheme(), data: null);

        Assert.Null(await service.GetSampleNumbersAsync(SchemeId, CancellationToken.None));
    }

    [Fact]
    public async Task GetSampleNumbersAsync_MapsHeadingAndSetsRequiredFromScheme()
    {
        var (service, _) = CreateService(Scheme(), Data());

        var page = await service.GetSampleNumbersAsync(SchemeId, CancellationToken.None);

        Assert.NotNull(page);
        Assert.Equal(SchemeId, page!.MonthlyDistributionSchemeId);
        Assert.Equal("17152A/BA", page.DistributionReferenceFull);
        Assert.Equal("PT0060", page.SchemeIdentifier);
        Assert.Equal("Mastitis bovine", page.SchemeName);
        Assert.Equal(9, page.NumberOfSetsRequired);
        Assert.Equal("Existing comments", page.Comments);
    }

    [Fact]
    public async Task GetSampleNumbersAsync_NullStrings_AreCoercedToEmpty()
    {
        var (service, _) = CreateService(Scheme(), Data());

        var page = await service.GetSampleNumbersAsync(SchemeId, CancellationToken.None);

        var sample = page!.Samples.Single(s => s.SampleId == SampleTwoId);
        Assert.Equal(string.Empty, sample.SampleNumber);
        Assert.Equal(string.Empty, sample.InternalDescription);
        Assert.Equal(string.Empty, sample.ExternalDescription);
        Assert.Equal(string.Empty, sample.TabulationDescription);
        Assert.Equal(string.Empty, sample.TestConsultantsDescription);
    }

    [Fact]
    public async Task GetSampleNumbersAsync_NullHeaderComments_AreCoercedToEmpty()
    {
        var data = Data();
        data.Header.Comments = null;
        var (service, _) = CreateService(Scheme(), data);

        var page = await service.GetSampleNumbersAsync(SchemeId, CancellationToken.None);

        Assert.Equal(string.Empty, page!.Comments);
    }

    // tblTest/tblTestResultItem/tblTestMethodItem/tblCriterionItem names are all nullable columns.
    [Fact]
    public async Task GetSampleNumbersAsync_NullTestAndItemNames_AreCoercedToEmpty()
    {
        var data = Data(
            categoryItems: [new DistributionCategoryItemEntity { CategoryItemId = CategoryItemId, TestId = TestId, Name = "Appearance", Order = 1 }],
            criterionItems: [new DistributionCriterionItemEntity { CriterionItemId = Guid.NewGuid(), CategoryItemId = CategoryItemId, TestId = TestId, Name = null, Order = 1 }]);
        data.Tests[0].TestName = null;
        foreach (var item in data.TestResultItems)
        {
            item.Name = null;
        }

        foreach (var item in data.TestMethodItems)
        {
            item.Name = null;
        }

        var (service, _) = CreateService(Scheme(), data);

        var page = await service.GetSampleNumbersAsync(SchemeId, CancellationToken.None);

        var test = Assert.Single(page!.Tests);
        Assert.Equal(string.Empty, test.TestName);
        Assert.All(test.TestResultItemNames, n => Assert.Equal(string.Empty, n));
        Assert.All(test.TestMethodItemNames, n => Assert.Equal(string.Empty, n));
        Assert.Equal(["Appearance ()"], test.CategoryItemNames);
    }

    [Fact]
    public async Task GetSampleNumbersAsync_GroupsAllocationsPerSample()
    {
        var (service, _) = CreateService(Scheme(), Data());

        var page = await service.GetSampleNumbersAsync(SchemeId, CancellationToken.None);

        var first = Assert.Single(page!.Samples.Single(s => s.SampleId == SampleOneId).Allocations);
        Assert.Equal(SampleOneTestId, first.SampleTestId);
        Assert.Equal(TestId, first.TestId);
        Assert.True(first.IsApplicable);

        var second = Assert.Single(page.Samples.Single(s => s.SampleId == SampleTwoId).Allocations);
        Assert.Equal(SampleTwoTestId, second.SampleTestId);
        Assert.False(second.IsApplicable);
    }

    [Fact]
    public async Task GetSampleNumbersAsync_SampleWithNoSampleTestRows_HasNoAllocations()
    {
        var data = Data();
        var withoutSampleTests = data with { SampleTests = [] };
        var (service, _) = CreateService(Scheme(), withoutSampleTests);

        var page = await service.GetSampleNumbersAsync(SchemeId, CancellationToken.None);

        Assert.All(page!.Samples, s => Assert.Empty(s.Allocations));
    }

    [Fact]
    public async Task GetSampleNumbersAsync_MapsResultAndMethodItemNamesInOrder()
    {
        var (service, _) = CreateService(Scheme(), Data());

        var page = await service.GetSampleNumbersAsync(SchemeId, CancellationToken.None);

        var test = Assert.Single(page!.Tests);
        Assert.Equal(TestId, test.TestId);
        Assert.Equal("Somatic cell count", test.TestName);
        Assert.Equal(["Result A", "Result B"], test.TestResultItemNames);
        Assert.Equal(["Method A", "Method B"], test.TestMethodItemNames);
    }

    [Fact]
    public async Task GetSampleNumbersAsync_CategoryItem_RendersCommaSeparatedCriteriaInBrackets()
    {
        var data = Data(
            requiresAssessment: true,
            categoryItems: [new DistributionCategoryItemEntity { CategoryItemId = CategoryItemId, TestId = TestId, Name = "Appearance", Order = 1 }],
            criterionItems:
            [
                new DistributionCriterionItemEntity { CriterionItemId = Guid.NewGuid(), CategoryItemId = CategoryItemId, TestId = TestId, Name = "Colour", Order = 1 },
                new DistributionCriterionItemEntity { CriterionItemId = Guid.NewGuid(), CategoryItemId = CategoryItemId, TestId = TestId, Name = "Clarity", Order = 2 },
            ]);
        var (service, _) = CreateService(Scheme(), data);

        var page = await service.GetSampleNumbersAsync(SchemeId, CancellationToken.None);

        Assert.Equal(["Appearance (Colour, Clarity)"], Assert.Single(page!.Tests).CategoryItemNames);
    }

    [Fact]
    public async Task GetSampleNumbersAsync_CategoryItemWithNoCriteria_RendersEmptyBrackets()
    {
        var data = Data(
            requiresAssessment: true,
            categoryItems: [new DistributionCategoryItemEntity { CategoryItemId = CategoryItemId, TestId = TestId, Name = "Appearance", Order = 1 }]);
        var (service, _) = CreateService(Scheme(), data);

        var page = await service.GetSampleNumbersAsync(SchemeId, CancellationToken.None);

        Assert.Equal(["Appearance ()"], Assert.Single(page!.Tests).CategoryItemNames);
    }

    // Each tab must only show the items belonging to its own test.
    [Fact]
    public async Task GetSampleNumbersAsync_ItemsAreNotSharedBetweenTests()
    {
        var otherTestId = Guid.Parse("99999999-9999-9999-9999-999999999999");
        var data = Data(
            categoryItems: [new DistributionCategoryItemEntity { CategoryItemId = CategoryItemId, TestId = TestId, Name = "Appearance", Order = 1 }],
            criterionItems: [new DistributionCriterionItemEntity { CriterionItemId = Guid.NewGuid(), CategoryItemId = CategoryItemId, TestId = TestId, Name = "Colour", Order = 1 }],
            extraTests: [new DistributionTestEntity { TestId = otherTestId, TestName = "Total viable count", Order = 2 }]);
        var (service, _) = CreateService(Scheme(), data);

        var page = await service.GetSampleNumbersAsync(SchemeId, CancellationToken.None);

        var other = page!.Tests.Single(t => t.TestId == otherTestId);
        Assert.Empty(other.TestResultItemNames);
        Assert.Empty(other.TestMethodItemNames);
        Assert.Empty(other.CategoryItemNames);
        Assert.Equal(["Appearance (Colour)"], page.Tests.Single(t => t.TestId == TestId).CategoryItemNames);
    }

    [Fact]
    public async Task GetSampleNumbersAsync_ReadyForAssessor_ReportsIsReadyForAssessment()
    {
        var (service, _) = CreateService(Scheme(), Data(requiresAssessment: true, isReadyForAssessor: new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc)));

        var page = await service.GetSampleNumbersAsync(SchemeId, CancellationToken.None);

        Assert.True(page!.RequiresAssessment);
        Assert.True(page.IsReadyForAssessment);
    }

    [Fact]
    public async Task GetSampleNumbersAsync_NotReadyForAssessor_ReportsNotReadyForAssessment()
    {
        var (service, _) = CreateService(Scheme(), Data(requiresAssessment: true));

        var page = await service.GetSampleNumbersAsync(SchemeId, CancellationToken.None);

        Assert.True(page!.RequiresAssessment);
        Assert.False(page.IsReadyForAssessment);
    }

    [Fact]
    public async Task SaveSampleNumbersAsync_InvalidSample_FailsAndWritesNothing()
    {
        var (service, repository) = CreateService(Scheme(), Data());
        var update = new SampleNumbersUpdate("ok", [Row(SampleOneId, sampleNumber: string.Empty)]);

        var outcome = await service.SaveSampleNumbersAsync(SchemeId, update, CancellationToken.None);

        Assert.False(outcome.Success);
        Assert.Empty(outcome.PageErrors);
        var errors = Assert.Single(outcome.FieldErrorsBySampleId);
        Assert.Equal(SampleOneId, errors.Key);
        Assert.Contains(errors.Value, e => e.Message == "Enter the Sample Number");
        Assert.Empty(repository.SavedSampleNumbers);
    }

    [Fact]
    public async Task SaveSampleNumbersAsync_OverLongComments_FailsAndWritesNothing()
    {
        var (service, repository) = CreateService(Scheme(), Data());
        var update = new SampleNumbersUpdate(new string('x', 8001), [Row(SampleOneId)]);

        var outcome = await service.SaveSampleNumbersAsync(SchemeId, update, CancellationToken.None);

        Assert.False(outcome.Success);
        Assert.Single(outcome.PageErrors);
        Assert.Empty(outcome.FieldErrorsBySampleId);
        Assert.Empty(repository.SavedSampleNumbers);
    }

    [Fact]
    public async Task SaveSampleNumbersAsync_OneInvalidSample_BlocksTheWholePage()
    {
        var (service, repository) = CreateService(Scheme(), Data());
        var update = new SampleNumbersUpdate("ok",
        [
            Row(SampleOneId, sampleNumber: "S1", internalDescription: "Internal 1"),
            Row(SampleTwoId, internalDescription: string.Empty),
        ]);

        var outcome = await service.SaveSampleNumbersAsync(SchemeId, update, CancellationToken.None);

        Assert.False(outcome.Success);
        Assert.Equal([SampleTwoId], outcome.FieldErrorsBySampleId.Keys);
        Assert.Empty(repository.SavedSampleNumbers);
    }

    [Fact]
    public async Task SaveSampleNumbersAsync_DistributionCannotBeReRead_Fails()
    {
        var (service, repository) = CreateService(Scheme(), data: null);
        var update = new SampleNumbersUpdate("ok", [Row(SampleOneId)]);

        var outcome = await service.SaveSampleNumbersAsync(SchemeId, update, CancellationToken.None);

        Assert.False(outcome.Success);
        Assert.Empty(outcome.PageErrors);
        Assert.Empty(outcome.FieldErrorsBySampleId);
        Assert.Empty(repository.SavedSampleNumbers);
    }

    [Fact]
    public async Task SaveSampleNumbersAsync_Valid_WritesEditedFieldsAndForcesBothFlags()
    {
        var (service, repository) = CreateService(Scheme(), Data());
        var update = new SampleNumbersUpdate("New comments",
        [
            Row(SampleOneId, "S1-edited", "Internal edited", "External edited", "Tabulation edited", "TC edited",
                [new SampleAllocationUpdate(SampleOneTestId, false)]),
        ], AuditWho, AuditReason);

        var outcome = await service.SaveSampleNumbersAsync(SchemeId, update, CancellationToken.None);

        Assert.True(outcome.Success);
        var saved = Assert.Single(repository.SavedSampleNumbers);
        Assert.Equal("New comments", saved.Header.Comments);
        Assert.True(saved.Header.HasSampleNumbersDefined);
        Assert.True(saved.Header.HasIntendedResults);

        var sample = Assert.Single(saved.Samples);
        Assert.Equal("S1-edited", sample.SampleNumber);
        Assert.Equal("Internal edited", sample.InternalDescription);
        Assert.Equal("External edited", sample.ExternalDescription);
        Assert.Equal("Tabulation edited", sample.TabulationDescription);
        Assert.Equal("TC edited", sample.TestConsultantsDescription);

        var sampleTest = Assert.Single(saved.SampleTests);
        Assert.Equal(SampleOneTestId, sampleTest.SampleTestId);
        Assert.False(sampleTest.IsApplicable);
    }

    [Fact]
    public async Task SaveSampleNumbersAsync_RoundTripsFieldsTheScreenNeverEdits()
    {
        var data = Data();
        var (service, repository) = CreateService(Scheme(), data);
        var update = new SampleNumbersUpdate("New comments", [Row(SampleOneId)], AuditWho, AuditReason);

        await service.SaveSampleNumbersAsync(SchemeId, update, CancellationToken.None);

        var saved = Assert.Single(repository.SavedSampleNumbers);
        Assert.Equal(data.Header.TestConsultantTabulationId, saved.Header.TestConsultantTabulationId);
        Assert.True(saved.Header.StoreRatings);
    }

    [Fact]
    public async Task SaveSampleNumbersAsync_UnknownSampleId_IsIgnored()
    {
        var (service, repository) = CreateService(Scheme(), Data());
        var update = new SampleNumbersUpdate("ok", [Row(SampleOneId), Row(Guid.NewGuid())], AuditWho, AuditReason);

        await service.SaveSampleNumbersAsync(SchemeId, update, CancellationToken.None);

        var saved = Assert.Single(repository.SavedSampleNumbers);
        Assert.Equal(SampleOneId, Assert.Single(saved.Samples).SampleId);
    }

    [Fact]
    public async Task SaveSampleNumbersAsync_UnknownSampleTestId_IsIgnored()
    {
        var (service, repository) = CreateService(Scheme(), Data());
        var update = new SampleNumbersUpdate("ok",
            [Row(SampleOneId, allocations: [new SampleAllocationUpdate(Guid.NewGuid(), true)])], AuditWho, AuditReason);

        await service.SaveSampleNumbersAsync(SchemeId, update, CancellationToken.None);

        Assert.Empty(Assert.Single(repository.SavedSampleNumbers).SampleTests);
    }

    [Fact]
    public async Task SaveSampleNumbersAsync_SampleTestBelongingToAnotherSample_IsIgnored()
    {
        var (service, repository) = CreateService(Scheme(), Data());
        var update = new SampleNumbersUpdate("ok",
            [Row(SampleOneId, allocations: [new SampleAllocationUpdate(SampleTwoTestId, true)])], AuditWho, AuditReason);

        await service.SaveSampleNumbersAsync(SchemeId, update, CancellationToken.None);

        Assert.Empty(Assert.Single(repository.SavedSampleNumbers).SampleTests);
    }

    // Legacy disables the tick boxes once the scheme is with the assessor, so the posted values
    // are meaningless: the allocation stays as stored and no spuSampleTest is run for it.
    [Fact]
    public async Task SaveSampleNumbersAsync_ReadyForAssessment_PreservesStoredAllocations()
    {
        var data = Data(requiresAssessment: true, isReadyForAssessor: new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc));
        var (service, repository) = CreateService(Scheme(), data);
        var update = new SampleNumbersUpdate("ok",
            [Row(SampleOneId, allocations: [new SampleAllocationUpdate(SampleOneTestId, false)])], AuditWho, AuditReason);

        await service.SaveSampleNumbersAsync(SchemeId, update, CancellationToken.None);

        Assert.Empty(Assert.Single(repository.SavedSampleNumbers).SampleTests);
        Assert.True(data.SampleTests.Single(t => t.SampleTestId == SampleOneTestId).IsApplicable);
    }

    // A locked tick box must not make an otherwise clean page dirty - nothing is written at all.
    [Fact]
    public async Task SaveSampleNumbersAsync_ReadyForAssessment_AllocationOnlyChange_WritesNothing()
    {
        var data = SavedData(requiresAssessment: true, isReadyForAssessor: new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc));
        var (service, repository) = CreateService(Scheme(), data);
        var update = new SampleNumbersUpdate("Existing comments",
            [UnchangedSampleOne([new SampleAllocationUpdate(SampleOneTestId, false)])]);

        var outcome = await service.SaveSampleNumbersAsync(SchemeId, update, CancellationToken.None);

        Assert.True(outcome.Success);
        Assert.Empty(repository.SavedSampleNumbers);
    }

    [Fact]
    public async Task SaveSampleNumbersAsync_RequiresAssessmentButNotYetReleased_AppliesPostedAllocations()
    {
        var (service, repository) = CreateService(Scheme(), Data(requiresAssessment: true));
        var update = new SampleNumbersUpdate("ok",
            [Row(SampleOneId, allocations: [new SampleAllocationUpdate(SampleOneTestId, false)])], AuditWho, AuditReason);

        await service.SaveSampleNumbersAsync(SchemeId, update, CancellationToken.None);

        Assert.False(Assert.Single(Assert.Single(repository.SavedSampleNumbers).SampleTests).IsApplicable);
    }

    [Fact]
    public async Task SaveSampleNumbersAsync_RequiresAssessment_RequestsSampleReceiptSetup()
    {
        var (service, repository) = CreateService(Scheme(), Data(requiresAssessment: true));
        var update = new SampleNumbersUpdate("ok", [Row(SampleOneId)], AuditWho, AuditReason);

        await service.SaveSampleNumbersAsync(SchemeId, update, CancellationToken.None);

        Assert.True(Assert.Single(repository.SavedSampleNumbers).SetupSampleReceipt);
    }

    [Fact]
    public async Task SaveSampleNumbersAsync_NonAssessmentScheme_DoesNotRequestSampleReceiptSetup()
    {
        var (service, repository) = CreateService(Scheme(), Data());
        var update = new SampleNumbersUpdate("ok", [Row(SampleOneId)], AuditWho, AuditReason);

        await service.SaveSampleNumbersAsync(SchemeId, update, CancellationToken.None);

        Assert.False(Assert.Single(repository.SavedSampleNumbers).SetupSampleReceipt);
    }

    // Legacy: If mIntendedResults.IsDirty is false, Save() never runs - so no spuIntendedResults,
    // no spuSample, no spuSampleTest and, critically, no spiSetupSampleReceipt.
    [Fact]
    public async Task SaveSampleNumbersAsync_NothingDirty_SucceedsWithoutTouchingTheRepository()
    {
        var (service, repository) = CreateService(Scheme(), SavedData());
        var update = new SampleNumbersUpdate("Existing comments", [UnchangedSampleOne()]);

        var outcome = await service.SaveSampleNumbersAsync(SchemeId, update, CancellationToken.None);

        Assert.True(outcome.Success);
        Assert.False(outcome.AuditReasonRequired);
        Assert.Empty(repository.SavedSampleNumbers);
    }

    [Fact]
    public async Task SaveSampleNumbersAsync_NothingDirtyOnAnAssessmentScheme_DoesNotReSetUpSampleReceipts()
    {
        var (service, repository) = CreateService(Scheme(), SavedData(requiresAssessment: true));
        var update = new SampleNumbersUpdate("Existing comments", [UnchangedSampleOne()]);

        await service.SaveSampleNumbersAsync(SchemeId, update, CancellationToken.None);

        Assert.Empty(repository.SavedSampleNumbers);
    }

    [Fact]
    public async Task SaveSampleNumbersAsync_OnlyCommentsChanged_WritesTheHeaderWithNoSamples()
    {
        var (service, repository) = CreateService(Scheme(), SavedData());
        var update = new SampleNumbersUpdate("New comments", [UnchangedSampleOne()]);

        var outcome = await service.SaveSampleNumbersAsync(SchemeId, update, CancellationToken.None);

        Assert.True(outcome.Success);
        var saved = Assert.Single(repository.SavedSampleNumbers);
        Assert.Equal("New comments", saved.Header.Comments);
        Assert.Empty(saved.Samples);
        Assert.Empty(saved.SampleTests);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task SaveSampleNumbersAsync_FlagNotYetSet_WritesEvenWhenNothingElseChanged(
        bool hasSampleNumbersDefined, bool hasIntendedResults)
    {
        var data = SavedData();
        data.Header.HasSampleNumbersDefined = hasSampleNumbersDefined;
        data.Header.HasIntendedResults = hasIntendedResults;
        var (service, repository) = CreateService(Scheme(), data);
        var update = new SampleNumbersUpdate("Existing comments", [UnchangedSampleOne()]);

        await service.SaveSampleNumbersAsync(SchemeId, update, CancellationToken.None);

        var saved = Assert.Single(repository.SavedSampleNumbers);
        Assert.True(saved.Header.HasSampleNumbersDefined);
        Assert.True(saved.Header.HasIntendedResults);
        Assert.Empty(saved.Samples);
    }

    // tblSample's description columns are nullable, so a stored NULL and a posted "" are equal -
    // otherwise every untouched sample would be rewritten on every save.
    [Fact]
    public async Task SaveSampleNumbersAsync_StoredNullVersusPostedEmpty_IsNotDirty()
    {
        var data = SavedData();
        var sampleOne = data.Samples.Single(s => s.SampleId == SampleOneId);
        sampleOne.ExternalDescription = null;
        sampleOne.TabulationDescription = null;
        sampleOne.TestConsultantsDescription = null;
        var (service, repository) = CreateService(Scheme(), data);
        var update = new SampleNumbersUpdate("Existing comments",
            [Row(SampleOneId, "S1", "Internal 1", string.Empty, string.Empty, string.Empty)]);

        var outcome = await service.SaveSampleNumbersAsync(SchemeId, update, CancellationToken.None);

        Assert.True(outcome.Success);
        Assert.False(outcome.AuditReasonRequired);
        Assert.Empty(repository.SavedSampleNumbers);
    }

    // A JSON body can omit a description entirely, which binds as null - that must still compare
    // equal to a stored NULL rather than being treated as an edit.
    [Fact]
    public async Task SaveSampleNumbersAsync_NullPostedDescriptions_AreNotDirtyAgainstStoredNulls()
    {
        var data = SavedData();
        var sampleOne = data.Samples.Single(s => s.SampleId == SampleOneId);
        sampleOne.ExternalDescription = null;
        sampleOne.TabulationDescription = null;
        sampleOne.TestConsultantsDescription = null;
        var (service, repository) = CreateService(Scheme(), data);
        var update = new SampleNumbersUpdate("Existing comments",
            [new SampleNumberRowUpdate(SampleOneId, "S1", "Internal 1", null!, null!, null!, [])]);

        var outcome = await service.SaveSampleNumbersAsync(SchemeId, update, CancellationToken.None);

        Assert.True(outcome.Success);
        Assert.Empty(repository.SavedSampleNumbers);
    }

    [Fact]
    public async Task SaveSampleNumbersAsync_NullPostedComments_AreNotDirtyAgainstAStoredNull()
    {
        var data = SavedData();
        data.Header.Comments = null;
        var (service, repository) = CreateService(Scheme(), data);
        var update = new SampleNumbersUpdate(null!, [UnchangedSampleOne()]);

        var outcome = await service.SaveSampleNumbersAsync(SchemeId, update, CancellationToken.None);

        Assert.True(outcome.Success);
        Assert.Empty(repository.SavedSampleNumbers);
    }

    [Fact]
    public async Task SaveSampleNumbersAsync_OnlyTheDirtySampleIsWritten()
    {
        var (service, repository) = CreateService(Scheme(), SavedData());
        var update = new SampleNumbersUpdate("Existing comments",
        [
            UnchangedSampleOne(),
            Row(SampleTwoId, "S2-new", "Internal 2"),
        ]);

        await service.SaveSampleNumbersAsync(SchemeId, update, CancellationToken.None);

        var saved = Assert.Single(repository.SavedSampleNumbers);
        Assert.Equal(SampleTwoId, Assert.Single(saved.Samples).SampleId);
    }

    // Legacy Sample.IsDirty is "my own fields OR mSampleTests.IsDirty", and a dirty Sample always
    // runs spuSample - so an allocation-only change still rewrites the tblSample row.
    [Fact]
    public async Task SaveSampleNumbersAsync_AllocationOnlyChange_StillWritesTheSampleRow()
    {
        var (service, repository) = CreateService(Scheme(), SavedData());
        var update = new SampleNumbersUpdate("Existing comments",
            [UnchangedSampleOne([new SampleAllocationUpdate(SampleOneTestId, false)])]);

        await service.SaveSampleNumbersAsync(SchemeId, update, CancellationToken.None);

        var saved = Assert.Single(repository.SavedSampleNumbers);
        Assert.Equal(SampleOneId, Assert.Single(saved.Samples).SampleId);
        Assert.Equal(SampleOneTestId, Assert.Single(saved.SampleTests).SampleTestId);
    }

    // spuSampleTest clears tblSampleReceipt's submission dates, so an unchanged tick box must
    // never be rewritten even when the sample row itself is dirty.
    [Fact]
    public async Task SaveSampleNumbersAsync_UnchangedAllocation_IsNotRewritten()
    {
        var (service, repository) = CreateService(Scheme(), SavedData());
        var update = new SampleNumbersUpdate("Existing comments",
        [
            Row(SampleOneId, "S1-edited", "Internal 1", "External 1", "Tabulation 1", "TC 1",
                [new SampleAllocationUpdate(SampleOneTestId, true)]),
        ]);

        await service.SaveSampleNumbersAsync(SchemeId, update, CancellationToken.None);

        var saved = Assert.Single(repository.SavedSampleNumbers);
        Assert.Single(saved.Samples);
        Assert.Empty(saved.SampleTests);
    }

    [Theory]
    [InlineData("S1-edited", "Internal 1", "External 1", "TC 1")]
    [InlineData("S1", "Internal edited", "External 1", "TC 1")]
    [InlineData("S1", "Internal 1", "External edited", "TC 1")]
    [InlineData("S1", "Internal 1", "External 1", "TC edited")]
    public async Task SaveSampleNumbersAsync_AnySingleFieldChange_MakesTheSampleDirty(
        string sampleNumber, string internalDescription, string externalDescription, string testConsultantsDescription)
    {
        var (service, repository) = CreateService(Scheme(), SavedData());
        var update = new SampleNumbersUpdate("Existing comments",
            [Row(SampleOneId, sampleNumber, internalDescription, externalDescription, "Tabulation 1", testConsultantsDescription)]);

        await service.SaveSampleNumbersAsync(SchemeId, update, CancellationToken.None);

        var saved = Assert.Single(repository.SavedSampleNumbers);
        Assert.Equal(SampleOneId, Assert.Single(saved.Samples).SampleId);
        Assert.Empty(saved.AuditedSampleIds);
    }

    // Legacy Sample.TabulationDescription is an <AuditableProperty>, so SampleNumbers.aspx.vb
    // redirects to Audit.aspx instead of saving until a reason has been captured.
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SaveSampleNumbersAsync_TabulationDescriptionChangedWithoutAReason_DemandsOneAndWritesNothing(string auditWhy)
    {
        var (service, repository) = CreateService(Scheme(), SavedData());
        var update = new SampleNumbersUpdate("Existing comments",
            [Row(SampleOneId, "S1", "Internal 1", "External 1", "Tabulation edited", "TC 1")], AuditWho, auditWhy);

        var outcome = await service.SaveSampleNumbersAsync(SchemeId, update, CancellationToken.None);

        Assert.False(outcome.Success);
        Assert.True(outcome.AuditReasonRequired);
        Assert.Equal("Intended Results Set", outcome.AuditObjectName);
        Assert.Empty(outcome.PageErrors);
        Assert.Empty(outcome.FieldErrorsBySampleId);
        Assert.Empty(repository.SavedSampleNumbers);
    }

    [Fact]
    public async Task SaveSampleNumbersAsync_TabulationDescriptionChangedWithAReason_SavesAndAuditsThatSample()
    {
        var (service, repository) = CreateService(Scheme(), SavedData());
        var update = new SampleNumbersUpdate("Existing comments",
            [Row(SampleOneId, "S1", "Internal 1", "External 1", "Tabulation edited", "TC 1")], AuditWho, AuditReason);

        var outcome = await service.SaveSampleNumbersAsync(SchemeId, update, CancellationToken.None);

        Assert.True(outcome.Success);
        Assert.False(outcome.AuditReasonRequired);
        var saved = Assert.Single(repository.SavedSampleNumbers);
        Assert.Equal(SampleOneId, Assert.Single(saved.AuditedSampleIds));
        Assert.Equal(AuditWho, saved.AuditWho);
        Assert.Equal(AuditReason, saved.AuditWhy);
        Assert.Equal("Tabulation edited", Assert.Single(saved.Samples).TabulationDescription);
    }

    [Fact]
    public async Task SaveSampleNumbersAsync_ChangingOtherFieldsOnly_NeitherAuditsNorDemandsAReason()
    {
        var (service, repository) = CreateService(Scheme(), SavedData());
        var update = new SampleNumbersUpdate("Existing comments",
            [Row(SampleOneId, "S1-edited", "Internal edited", "External edited", "Tabulation 1", "TC edited")]);

        var outcome = await service.SaveSampleNumbersAsync(SchemeId, update, CancellationToken.None);

        Assert.True(outcome.Success);
        Assert.False(outcome.AuditReasonRequired);
        Assert.Empty(Assert.Single(repository.SavedSampleNumbers).AuditedSampleIds);
    }

    [Fact]
    public async Task SaveSampleNumbersAsync_OnlyTheSamplesWhoseTabulationChangedAreAudited()
    {
        var (service, repository) = CreateService(Scheme(), SavedData());
        var update = new SampleNumbersUpdate("Existing comments",
        [
            Row(SampleOneId, "S1-edited", "Internal 1", "External 1", "Tabulation 1", "TC 1"),
            Row(SampleTwoId, "S2", "Internal 2", string.Empty, "Tabulation 2", string.Empty),
        ], AuditWho, AuditReason);

        await service.SaveSampleNumbersAsync(SchemeId, update, CancellationToken.None);

        var saved = Assert.Single(repository.SavedSampleNumbers);
        Assert.Equal(2, saved.Samples.Count);
        Assert.Equal([SampleTwoId], saved.AuditedSampleIds);
    }

    // Legacy order: the ValidationGroup runs first, then IsDirty, then IsAuditDirty.
    [Fact]
    public async Task SaveSampleNumbersAsync_InvalidAndAuditable_ReturnsValidationErrorsNotTheAuditDiversion()
    {
        var (service, repository) = CreateService(Scheme(), SavedData());
        var update = new SampleNumbersUpdate("Existing comments",
            [Row(SampleOneId, string.Empty, "Internal 1", "External 1", "Tabulation edited", "TC 1")]);

        var outcome = await service.SaveSampleNumbersAsync(SchemeId, update, CancellationToken.None);

        Assert.False(outcome.Success);
        Assert.False(outcome.AuditReasonRequired);
        Assert.Null(outcome.AuditObjectName);
        Assert.Single(outcome.FieldErrorsBySampleId);
        Assert.Empty(repository.SavedSampleNumbers);
    }

    // An unknown sample id is skipped before the dirty check, so it can never raise an audit.
    [Fact]
    public async Task SaveSampleNumbersAsync_UnknownSampleWithATabulationDescription_DoesNotDemandAReason()
    {
        var (service, repository) = CreateService(Scheme(), SavedData());
        var update = new SampleNumbersUpdate("Existing comments",
            [UnchangedSampleOne(), Row(Guid.NewGuid(), "S9", "Internal 9", string.Empty, "Tabulation 9")]);

        var outcome = await service.SaveSampleNumbersAsync(SchemeId, update, CancellationToken.None);

        Assert.True(outcome.Success);
        Assert.False(outcome.AuditReasonRequired);
        Assert.Empty(repository.SavedSampleNumbers);
    }

    [Fact]
    public async Task SaveSampleNumbersAsync_NoAuditableChange_PassesTheIdentityThroughAnyway()
    {
        var (service, repository) = CreateService(Scheme(), SavedData());
        var update = new SampleNumbersUpdate("New comments", [UnchangedSampleOne()], AuditWho, string.Empty);

        await service.SaveSampleNumbersAsync(SchemeId, update, CancellationToken.None);

        var saved = Assert.Single(repository.SavedSampleNumbers);
        Assert.Equal(AuditWho, saved.AuditWho);
        Assert.Equal(string.Empty, saved.AuditWhy);
    }
}
