using PTL.Core.Distribution;
using PTL.Core.Scheme;
using CoreScheme = PTL.Core.Scheme.Scheme;

namespace PTL.Api.Tests.Distribution;

// Pins legacy IntendedResultsSet.DataPortal_Create (plus Test.NewTest / Tabulation.NewTabulation /
// Sample.NewSample): the first-time "Define Sample Numbers" copy of a Scheme into a distribution.
// The copy must be a deep one - every id freshly generated - so a later Scheme edit cannot
// retrospectively alter a distribution that has already gone out.
public class DistributionStructureBuilderTests
{
    private static readonly Guid DistributionSchemeId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");

    private static SchemeTest Test(
        Guid testId,
        int order,
        IList<SchemeTestResultItem>? resultItems = null,
        IList<SchemeTestMethodItem>? methodItems = null,
        IList<SchemeCategoryItem>? categories = null) =>
        new()
        {
            TestId = testId,
            TestTypeId = Guid.NewGuid(),
            Order = order,
            ResultItems = resultItems ?? [],
            MethodItems = methodItems ?? [],
            Categories = categories ?? [],
        };

    private static SchemeTestResultItem ResultItem(Guid id, int order) =>
        new() { TestResultItemId = id, TestResultItemTypeId = Guid.NewGuid(), Order = order };

    private static SchemeTestMethodItem MethodItem(Guid id, int order) =>
        new() { TestMethodItemId = id, TestMethodItemTypeId = Guid.NewGuid(), Order = order };

    // A scheme with one test (two result items, two method items) and one tabulation that
    // references both of them - the smallest shape that exercises the whole id map.
    private static CoreScheme Scheme(
        bool requiresAssessment = false,
        Guid? testConsultantTabulationId = null,
        int numberOfSamples = 2,
        IList<SchemeTest>? tests = null,
        IList<SchemeTabulation>? tabulations = null)
    {
        var resultItemId = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000001");
        var methodItemId = Guid.Parse("cccccccc-0000-0000-0000-000000000001");
        var tabulationId = Guid.Parse("dddddddd-0000-0000-0000-000000000001");

        return new CoreScheme
        {
            SchemeId = Guid.Parse("eeeeeeee-0000-0000-0000-000000000001"),
            Identifier = "PT0060",
            ScheduleId = Guid.Parse("ffffffff-0000-0000-0000-000000000001"),
            NumberOfSamples = numberOfSamples,
            RequiresAssessment = requiresAssessment,
            LastModified = new DateTime(2026, 1, 15, 9, 30, 0, DateTimeKind.Unspecified),
            DateOfReceipt = true,
            StorageConditions = true,
            ConditionOnReceipt = true,
            Instructions = "Keep refrigerated",
            StandardTabulationText = "Standard text",
            TestConsultantTabulationId = testConsultantTabulationId ?? tabulationId,
            Tests = tests ??
            [
                Test(Guid.Parse("11111111-0000-0000-0000-000000000001"), 1,
                    [ResultItem(resultItemId, 1), ResultItem(Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002"), 2)],
                    [MethodItem(methodItemId, 1), MethodItem(Guid.Parse("cccccccc-0000-0000-0000-000000000002"), 2)]),
            ],
            Tabulations = tabulations ??
            [
                new SchemeTabulation
                {
                    TabulationId = tabulationId,
                    Name = "Main tabulation",
                    IntendedResultsOnly = true,
                    SingleParticipantTabulation = true,
                    ShowRatings = true,
                    AvailableToParticipants = true,
                    AvailableToViewers = true,
                    ResultItemIds = [resultItemId],
                    MethodItemIds = [methodItemId],
                },
            ],
        };
    }

    // The deep-copy guarantee: not one scheme id survives into the distribution.
    [Fact]
    public void Build_GeneratesFreshIdsForEveryCopiedRow()
    {
        var scheme = Scheme(requiresAssessment: true, tests:
        [
            Test(Guid.Parse("11111111-0000-0000-0000-000000000001"), 1,
                [ResultItem(Guid.Parse("bbbbbbbb-0000-0000-0000-000000000001"), 1)],
                [MethodItem(Guid.Parse("cccccccc-0000-0000-0000-000000000001"), 1)],
                [
                    new SchemeCategoryItem
                    {
                        CategoryItemId = Guid.Parse("99999999-0000-0000-0000-000000000001"),
                        CategoryItemTypeId = Guid.NewGuid(),
                        Order = 1,
                        Criteria = [new SchemeCriterionItem { CriterionItemId = Guid.Parse("88888888-0000-0000-0000-000000000001"), CriterionItemTypeId = Guid.NewGuid(), Order = 1 }],
                    },
                ]),
        ]);

        var structure = DistributionStructureBuilder.Build(DistributionSchemeId, scheme);

        var schemeTest = scheme.Tests[0];
        var test = Assert.Single(structure.Tests);
        Assert.NotEqual(schemeTest.TestId, test.TestId);
        Assert.NotEqual(schemeTest.ResultItems[0].TestResultItemId, test.ResultItems[0].TestResultItemId);
        Assert.NotEqual(schemeTest.MethodItems[0].TestMethodItemId, test.MethodItems[0].TestMethodItemId);
        Assert.NotEqual(schemeTest.Categories[0].CategoryItemId, test.CategoryItems[0].CategoryItemId);
        Assert.NotEqual(schemeTest.Categories[0].Criteria[0].CriterionItemId, test.CategoryItems[0].CriterionItems[0].CriterionItemId);
        Assert.NotEqual(scheme.Tabulations[0].TabulationId, Assert.Single(structure.Tabulations).TabulationId);
    }

    // The copied rows must still hang together: items point at their new parent, not the old one.
    [Fact]
    public void Build_RepointsCopiedChildrenAtTheirNewParents()
    {
        var structure = DistributionStructureBuilder.Build(DistributionSchemeId, Scheme());

        var test = Assert.Single(structure.Tests);
        Assert.All(test.ResultItems, item => Assert.Equal(test.TestId, item.TestId));
        Assert.All(test.MethodItems, item => Assert.Equal(test.TestId, item.TestId));
    }

    [Fact]
    public void Build_CategoryCriteriaPointAtTheirNewCategory()
    {
        var scheme = Scheme(tests:
        [
            Test(Guid.NewGuid(), 1, categories:
            [
                new SchemeCategoryItem
                {
                    CategoryItemId = Guid.NewGuid(),
                    CategoryItemTypeId = Guid.NewGuid(),
                    Order = 1,
                    Criteria =
                    [
                        new SchemeCriterionItem { CriterionItemId = Guid.NewGuid(), CriterionItemTypeId = Guid.NewGuid(), Order = 2 },
                        new SchemeCriterionItem { CriterionItemId = Guid.NewGuid(), CriterionItemTypeId = Guid.NewGuid(), Order = 1 },
                    ],
                },
            ]),
        ], tabulations: []);
        scheme.RequiresAssessment = true;

        var structure = DistributionStructureBuilder.Build(DistributionSchemeId, scheme);

        var category = Assert.Single(Assert.Single(structure.Tests).CategoryItems);
        Assert.All(category.CriterionItems, criterion => Assert.Equal(category.CategoryItemId, criterion.CategoryItemId));
        Assert.Equal([1, 2], category.CriterionItems.Select(c => c.Order));
    }

    [Fact]
    public void Build_OrdersTestsAndTheirItemsByOrder()
    {
        // Assessment scheme so the copy does not also need a Test Consultant Tabulation, which
        // the empty tabulation list below deliberately does not provide.
        var scheme = Scheme(requiresAssessment: true, tests:
        [
            Test(Guid.NewGuid(), 3, [ResultItem(Guid.NewGuid(), 2), ResultItem(Guid.NewGuid(), 1)], [MethodItem(Guid.NewGuid(), 2), MethodItem(Guid.NewGuid(), 1)]),
            Test(Guid.NewGuid(), 1),
            Test(Guid.NewGuid(), 2),
        ], tabulations: []);

        var structure = DistributionStructureBuilder.Build(DistributionSchemeId, scheme);

        Assert.Equal([1, 2, 3], structure.Tests.Select(t => t.Order));
        var lastTest = structure.Tests[^1];
        Assert.Equal([1, 2], lastTest.ResultItems.Select(i => i.Order));
        Assert.Equal([1, 2], lastTest.MethodItems.Select(i => i.Order));
    }

    // The whole point of the id map: a tabulation's link rows must reach the distribution's own
    // items, never the Scheme's.
    [Fact]
    public void Build_TabulationLinksResolveToTheNewItemIds()
    {
        var scheme = Scheme();

        var structure = DistributionStructureBuilder.Build(DistributionSchemeId, scheme);

        var test = Assert.Single(structure.Tests);
        var tabulation = Assert.Single(structure.Tabulations);
        Assert.Equal(test.ResultItems[0].TestResultItemId, Assert.Single(tabulation.ResultItems).ItemId);
        Assert.Equal(test.MethodItems[0].TestMethodItemId, Assert.Single(tabulation.MethodItems).ItemId);
        Assert.DoesNotContain(tabulation.ResultItems, i => i.ItemId == scheme.Tabulations[0].ResultItemIds[0]);
        Assert.DoesNotContain(tabulation.MethodItems, i => i.ItemId == scheme.Tabulations[0].MethodItemIds[0]);
    }

    [Fact]
    public void Build_TabulationLinkRowsGetTheirOwnIdsAndPointAtTheNewTabulation()
    {
        var structure = DistributionStructureBuilder.Build(DistributionSchemeId, Scheme());

        var tabulation = Assert.Single(structure.Tabulations);
        var links = tabulation.ResultItems.Concat(tabulation.MethodItems).ToList();
        Assert.All(links, link => Assert.Equal(tabulation.TabulationId, link.TabulationId));
        Assert.All(links, link => Assert.NotEqual(Guid.Empty, link.LinkId));
        Assert.Equal(links.Count, links.Select(l => l.LinkId).Distinct().Count());
    }

    [Fact]
    public void Build_CopiesTabulationFlagsAndStampsTheDistributionAndSchemeVersion()
    {
        var scheme = Scheme();

        var structure = DistributionStructureBuilder.Build(DistributionSchemeId, scheme);

        var tabulation = Assert.Single(structure.Tabulations);
        Assert.Equal(DistributionSchemeId, tabulation.MonthlyDistributionSchemeId);
        Assert.Equal("Main tabulation", tabulation.Name);
        Assert.True(tabulation.IntendedResultsOnly);
        Assert.True(tabulation.SingleParticipantTabulation);
        Assert.True(tabulation.ShowRatings);
        Assert.True(tabulation.AvailableToParticipants);
        Assert.True(tabulation.AvailableToViewers);
        Assert.Equal(scheme.LastModified, tabulation.SchemeVersionDate);
    }

    [Fact]
    public void Build_TabulationReferencingAnItemOnNoTest_ThrowsNamingTheTabulation()
    {
        var scheme = Scheme();
        scheme.Tabulations[0].ResultItemIds = [Guid.NewGuid()];

        var exception = Assert.Throws<DistributionStructureException>(() => DistributionStructureBuilder.Build(DistributionSchemeId, scheme));

        Assert.Contains("Main tabulation", exception.Message, StringComparison.Ordinal);
        Assert.Contains("result item", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_TabulationReferencingAMethodItemOnNoTest_ThrowsNamingTheTabulation()
    {
        var scheme = Scheme();
        scheme.Tabulations[0].MethodItemIds = [Guid.NewGuid()];

        var exception = Assert.Throws<DistributionStructureException>(() => DistributionStructureBuilder.Build(DistributionSchemeId, scheme));

        Assert.Contains("Main tabulation", exception.Message, StringComparison.Ordinal);
        Assert.Contains("method item", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_NonAssessmentScheme_MapsTestConsultantTabulationToTheNewTabulationId()
    {
        var structure = DistributionStructureBuilder.Build(DistributionSchemeId, Scheme());

        Assert.Equal(Assert.Single(structure.Tabulations).TabulationId, structure.TestConsultantTabulationId);
        Assert.NotEqual(Guid.Empty, structure.TestConsultantTabulationId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public void Build_NonAssessmentSchemeWithNoTestConsultantTabulation_Throws(string? tabulationId)
    {
        var scheme = Scheme();
        scheme.TestConsultantTabulationId = tabulationId is null ? null : Guid.Parse(tabulationId);

        var exception = Assert.Throws<DistributionStructureException>(() => DistributionStructureBuilder.Build(DistributionSchemeId, scheme));

        Assert.Contains("has no Test Consultant Tabulation", exception.Message, StringComparison.Ordinal);
    }

    // A different failure from "none set": the id is there but no longer matches a tabulation.
    [Fact]
    public void Build_TestConsultantTabulationNotAmongTheSchemesTabulations_ThrowsADifferentMessage()
    {
        var scheme = Scheme(testConsultantTabulationId: Guid.Parse("7a7a7a7a-0000-0000-0000-000000000001"));

        var exception = Assert.Throws<DistributionStructureException>(() => DistributionStructureBuilder.Build(DistributionSchemeId, scheme));

        Assert.Contains("no longer exists on the Scheme", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("has no Test Consultant Tabulation", exception.Message, StringComparison.Ordinal);
    }

    // Legacy only maps the Test Consultant Tabulation for non-assessment schemes, so an assessment
    // scheme with none set is valid and must not fail.
    [Fact]
    public void Build_AssessmentScheme_LeavesTestConsultantTabulationEmptyWithoutThrowing()
    {
        var scheme = Scheme(requiresAssessment: true);
        scheme.TestConsultantTabulationId = null;

        var structure = DistributionStructureBuilder.Build(DistributionSchemeId, scheme);

        Assert.Equal(Guid.Empty, structure.TestConsultantTabulationId);
    }

    [Fact]
    public void Build_CreatesOneSamplePerSchemeNumberOfSamplesWithATestAndItemPerTest()
    {
        var scheme = Scheme(numberOfSamples: 3);

        var structure = DistributionStructureBuilder.Build(DistributionSchemeId, scheme);

        Assert.Equal(3, structure.Samples.Count);
        Assert.Equal(3, structure.Samples.Select(s => s.SampleId).Distinct().Count());
        foreach (var sample in structure.Samples)
        {
            var sampleTest = Assert.Single(sample.SampleTests);
            Assert.Equal(sample.SampleId, sampleTest.SampleId);
            Assert.Equal(structure.Tests[0].TestId, sampleTest.TestId);
            Assert.Equal(
                structure.Tests[0].ResultItems.Select(i => i.TestResultItemId),
                sampleTest.ResultItems.Select(i => i.TestResultItemId));
            Assert.All(sampleTest.ResultItems, item => Assert.Equal(sampleTest.SampleTestId, item.SampleTestId));
        }
    }

    [Fact]
    public void Build_ZeroSamples_ProducesNoSamples()
    {
        var structure = DistributionStructureBuilder.Build(DistributionSchemeId, Scheme(numberOfSamples: 0));

        Assert.Empty(structure.Samples);
    }

    [Fact]
    public void Build_SampleNumbersAreLeftForTheRepositoryToAllocate()
    {
        var structure = DistributionStructureBuilder.Build(DistributionSchemeId, Scheme());

        Assert.All(structure.Samples, sample => Assert.Equal(string.Empty, sample.SampleNumber));
    }

    // Legacy SampleTest.DataPortal_Create: assessment schemes start unallocated so the user has to
    // tick allocation explicitly; everything else starts allocated.
    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void Build_SampleTestAllocation_FollowsRequiresAssessment(bool requiresAssessment, bool expectedIsApplicable)
    {
        var scheme = Scheme(requiresAssessment: requiresAssessment);

        var structure = DistributionStructureBuilder.Build(DistributionSchemeId, scheme);

        Assert.All(structure.Samples, sample => Assert.All(sample.SampleTests, t => Assert.Equal(expectedIsApplicable, t.IsApplicable)));
    }

    [Fact]
    public void Build_MethodAndResultItemsStartUnsetAndNotRanged()
    {
        var structure = DistributionStructureBuilder.Build(DistributionSchemeId, Scheme());

        var test = Assert.Single(structure.Tests);
        Assert.All(test.MethodItems, item =>
        {
            Assert.False(item.IsApplicable);
            Assert.Equal(string.Empty, item.IntendedResult);
        });
        Assert.All(test.ResultItems, item => Assert.False(item.IsRange));
    }

    [Fact]
    public void Build_CopiesTheDistributionLevelFieldsFromTheScheme()
    {
        var scheme = Scheme();

        var structure = DistributionStructureBuilder.Build(DistributionSchemeId, scheme);

        Assert.Equal(scheme.LastModified, structure.SchemeVersionDate);
        Assert.True(structure.DateOfReceipt);
        Assert.True(structure.StorageConditions);
        Assert.True(structure.ConditionOnReceipt);
        Assert.Equal("Keep refrigerated", structure.Instructions);
        Assert.Equal("Standard text", structure.StandardTabulationText);
    }

    [Fact]
    public void Build_NullStandardTabulationText_IsCoercedToEmpty()
    {
        var scheme = Scheme();
        scheme.StandardTabulationText = null;

        var structure = DistributionStructureBuilder.Build(DistributionSchemeId, scheme);

        Assert.Equal(string.Empty, structure.StandardTabulationText);
    }

    [Fact]
    public void Build_FalseSchemeFlags_AreCopiedAsFalse()
    {
        var scheme = Scheme();
        scheme.DateOfReceipt = false;
        scheme.StorageConditions = false;
        scheme.ConditionOnReceipt = false;

        var structure = DistributionStructureBuilder.Build(DistributionSchemeId, scheme);

        Assert.False(structure.DateOfReceipt);
        Assert.False(structure.StorageConditions);
        Assert.False(structure.ConditionOnReceipt);
    }

    // Legacy formats the allocated integer as "YY/NNNN".
    [Theory]
    [InlineData(7, "26/0007")]
    [InlineData(1234, "26/1234")]
    [InlineData(1, "26/0001")]
    [InlineData(12345, "26/12345")]
    public void FormatSampleNumber_ZeroPadsToFourDigitsWithoutTruncating(int next, string expected) =>
        Assert.Equal(expected, DistributionStructureBuilder.FormatSampleNumber(next, new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Unspecified)));

    [Fact]
    public void FormatSampleNumber_UsesTheTwoDigitYearOfTheSuppliedDate() =>
        Assert.Equal("09/0042", DistributionStructureBuilder.FormatSampleNumber(42, new DateTime(2009, 12, 1, 0, 0, 0, DateTimeKind.Unspecified)));

    // Legacy passes the first of the distribution month plus two seconds; spgNextSampleNumber
    // compares that against tblSchedule.fldUpdateDate to decide on a year rollover.
    [Fact]
    public void BuildSampleNumberDate_IsTheFirstOfTheMonthPlusTwoSeconds() =>
        Assert.Equal(new DateTime(2026, 4, 1, 0, 0, 2, DateTimeKind.Unspecified), DistributionStructureBuilder.BuildSampleNumberDate(2026, 4));

    [Fact]
    public void BuildSampleNumberDate_HandlesEveryMonthOfTheFinancialYear()
    {
        for (var month = 1; month <= 12; month++)
        {
            Assert.Equal(new DateTime(2026, month, 1, 0, 0, 2, DateTimeKind.Unspecified), DistributionStructureBuilder.BuildSampleNumberDate(2026, month));
        }
    }
}
