using CoreScheme = PTL.Core.Scheme.Scheme;

namespace PTL.Core.Distribution;

// Ports legacy IntendedResultsSet.DataPortal_Create / Test.NewTest / Tabulation.NewTabulation /
// Sample.NewSample. Pure: no I/O, so the whole copy is unit-testable. Sample numbers are left
// blank here and filled by the repository inside the creation transaction.
public static class DistributionStructureBuilder
{
    public static DistributionStructure Build(Guid monthlyDistributionSchemeId, CoreScheme scheme)
    {
        // Legacy's guidDictionary: scheme-level id -> this distribution's freshly generated id.
        // Tests MUST be built before Tabulations, because a tabulation's link rows resolve their
        // result/method item ids through this map.
        var idMap = new Dictionary<Guid, Guid>();

        var tests = BuildTests(scheme, idMap);
        var tabulations = BuildTabulations(monthlyDistributionSchemeId, scheme, idMap);
        var samples = BuildSamples(scheme, tests);

        return new DistributionStructure(
            tests,
            tabulations,
            samples,
            ResolveTestConsultantTabulationId(scheme, idMap),
            scheme.LastModified,
            scheme.DateOfReceipt,
            scheme.StorageConditions,
            scheme.ConditionOnReceipt,
            scheme.Instructions,
            scheme.StandardTabulationText ?? string.Empty);
    }

    // Legacy formats the allocated integer as "YY/NNNN" - zero padded to four digits, prefixed
    // with the two-digit year of the distribution date.
    public static string FormatSampleNumber(int nextSampleNumber, DateTime distributionDate)
    {
        var digits = nextSampleNumber.ToString(System.Globalization.CultureInfo.InvariantCulture).PadLeft(4, '0');
        var year = distributionDate.Year.ToString(System.Globalization.CultureInfo.InvariantCulture)[2..];
        return $"{year}/{digits}";
    }

    // Legacy passes DateTime.Parse(YearId + "/" + MonthId + "/01").AddSeconds(2) to
    // spgNextSampleNumber - the first of the distribution month, NOT the real distribution date.
    // The two seconds come straight from the legacy source and are preserved deliberately: the
    // procedure compares this against tblSchedule.fldUpdateDate to decide on a year rollover.
    public static DateTime BuildSampleNumberDate(int yearId, int monthId) =>
        new DateTime(yearId, monthId, 1, 0, 0, 0, DateTimeKind.Unspecified).AddSeconds(2);

    private static List<NewDistributionTest> BuildTests(CoreScheme scheme, Dictionary<Guid, Guid> idMap)
    {
        var tests = new List<NewDistributionTest>();
        foreach (var schemeTest in scheme.Tests.OrderBy(t => t.Order))
        {
            var testId = Guid.NewGuid();
            idMap[schemeTest.TestId] = testId;

            var resultItems = schemeTest.ResultItems.OrderBy(i => i.Order).Select(item =>
            {
                var newId = Guid.NewGuid();
                idMap[item.TestResultItemId] = newId;
                return new NewDistributionTestResultItem(newId, testId, item.TestResultItemTypeId, item.Order);
            }).ToList();

            var methodItems = schemeTest.MethodItems.OrderBy(i => i.Order).Select(item =>
            {
                var newId = Guid.NewGuid();
                idMap[item.TestMethodItemId] = newId;
                return new NewDistributionTestMethodItem(newId, testId, item.TestMethodItemTypeId, item.Order);
            }).ToList();

            // Category and criterion items get fresh ids but are NOT added to the map - legacy
            // CategoryItem.NewCategoryItem takes no dictionary because nothing references them.
            var categoryItems = schemeTest.Categories.OrderBy(c => c.Order).Select(category =>
            {
                var categoryItemId = Guid.NewGuid();
                var criteria = category.Criteria.OrderBy(c => c.Order)
                    .Select(c => new NewDistributionCriterionItem(Guid.NewGuid(), categoryItemId, c.CriterionItemTypeId, c.Order))
                    .ToList();
                return new NewDistributionCategoryItem(categoryItemId, testId, category.CategoryItemTypeId, category.Order, criteria);
            }).ToList();

            tests.Add(new NewDistributionTest(testId, schemeTest.TestTypeId, schemeTest.Order, resultItems, methodItems, categoryItems));
        }

        return tests;
    }

    private static List<NewDistributionTabulation> BuildTabulations(
        Guid monthlyDistributionSchemeId, CoreScheme scheme, Dictionary<Guid, Guid> idMap)
    {
        var tabulations = new List<NewDistributionTabulation>();
        foreach (var schemeTabulation in scheme.Tabulations)
        {
            var tabulationId = Guid.NewGuid();
            idMap[schemeTabulation.TabulationId] = tabulationId;

            var resultItems = schemeTabulation.ResultItemIds
                .Select(id => new NewDistributionTabulationItem(Guid.NewGuid(), tabulationId, ResolveItemId(idMap, id, schemeTabulation.Name, "result")))
                .ToList();

            var methodItems = schemeTabulation.MethodItemIds
                .Select(id => new NewDistributionTabulationItem(Guid.NewGuid(), tabulationId, ResolveItemId(idMap, id, schemeTabulation.Name, "method")))
                .ToList();

            tabulations.Add(new NewDistributionTabulation(
                tabulationId,
                monthlyDistributionSchemeId,
                schemeTabulation.Name,
                schemeTabulation.IntendedResultsOnly,
                scheme.LastModified,
                schemeTabulation.SingleParticipantTabulation,
                schemeTabulation.ShowRatings,
                schemeTabulation.AvailableToParticipants,
                schemeTabulation.AvailableToViewers,
                resultItems,
                methodItems));
        }

        return tabulations;
    }

    // One sample per Scheme.NumberOfSamples, each carrying a SampleTest for every test and a
    // SampleResultItem for every one of that test's result items.
    private static List<NewDistributionSample> BuildSamples(CoreScheme scheme, List<NewDistributionTest> tests)
    {
        var samples = new List<NewDistributionSample>();
        for (var i = 0; i < scheme.NumberOfSamples; i++)
        {
            var sampleId = Guid.NewGuid();
            var sampleTests = tests.Select(test =>
            {
                var sampleTestId = Guid.NewGuid();
                var resultItems = test.ResultItems
                    .Select(item => new NewDistributionSampleResultItem(Guid.NewGuid(), sampleTestId, item.TestResultItemId))
                    .ToList();

                // Legacy SampleTest.DataPortal_Create: assessment schemes start UNALLOCATED, so
                // the user must tick allocation explicitly; everything else starts allocated.
                return new NewDistributionSampleTest(sampleTestId, sampleId, test.TestId, !scheme.RequiresAssessment, resultItems);
            }).ToList();

            samples.Add(new NewDistributionSample { SampleId = sampleId, SampleTests = sampleTests });
        }

        return samples;
    }

    // Legacy only maps this for non-assessment schemes, and does it with a bare dictionary lookup
    // that throws KeyNotFoundException on bad configuration. We fail just as hard but with a
    // business-readable message rather than creating a half-configured distribution.
    private static Guid ResolveTestConsultantTabulationId(CoreScheme scheme, Dictionary<Guid, Guid> idMap)
    {
        if (scheme.RequiresAssessment)
        {
            return Guid.Empty;
        }

        if (scheme.TestConsultantTabulationId is not { } schemeTabulationId || schemeTabulationId == Guid.Empty)
        {
            throw new DistributionStructureException(
                $"Scheme {scheme.Identifier} has no Test Consultant Tabulation. Set one on the Scheme before defining Sample Numbers.");
        }

        if (!idMap.TryGetValue(schemeTabulationId, out var tabulationId))
        {
            throw new DistributionStructureException(
                $"Scheme {scheme.Identifier} refers to a Test Consultant Tabulation that no longer exists on the Scheme. Correct the Scheme before defining Sample Numbers.");
        }

        return tabulationId;
    }

    private static Guid ResolveItemId(Dictionary<Guid, Guid> idMap, Guid schemeItemId, string tabulationName, string itemKind)
    {
        if (!idMap.TryGetValue(schemeItemId, out var id))
        {
            throw new DistributionStructureException(
                $"Results Tabulation '{tabulationName}' refers to a {itemKind} item that is not on any of the Scheme's tests. Correct the Scheme before defining Sample Numbers.");
        }

        return id;
    }
}
