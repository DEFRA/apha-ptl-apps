using Microsoft.Extensions.Logging.Abstractions;
using PTL.Api.Tests.Lookup;
using PTL.Core.Lookup;
using PTL.Core.Scheme;

namespace PTL.Api.Tests.Scheme;

public class SchemeServiceTests
{
    private static readonly Guid TestConsultantTabulationId = Guid.NewGuid();
    private static SchemeService CreateService(FakeSchemeRepository repository) =>
        new(repository, new FakeLookupRepository(), new PTL.Api.Tests.Participant.FakeViewerRepository(), NullLogger<SchemeService>.Instance);

    private static SchemeService CreateService(FakeSchemeRepository repository, FakeLookupRepository lookupRepository) =>
        new(repository, lookupRepository, new PTL.Api.Tests.Participant.FakeViewerRepository(), NullLogger<SchemeService>.Instance);

    private static SchemeService CreateService(FakeSchemeRepository repository, FakeLookupRepository lookupRepository, PTL.Api.Tests.Participant.FakeViewerRepository viewerRepository) =>
        new(repository, lookupRepository, viewerRepository, NullLogger<SchemeService>.Instance);

    private static PTL.Core.Scheme.Scheme ValidScheme(int? yearId = null) => new()
    {
        YearId = yearId ?? DateTime.UtcNow.Year + 1,
        Identifier = "PT1234",
        Name = "Test Scheme",
        ScheduleId = Guid.NewGuid(),
        ScheduleCodeId = Guid.NewGuid(),
        DistributionMonthApr = true,
        NumberOfSamples = 5,
        SampleOrigin = "UK",
        Deadline = 10,
        Instructions = "Follow the packing instructions.",
        CustomsDescription = "Biological samples",
        CustomsVolume = "1kg",
        // Not an assessment scheme, so a Primary Test Consultant is required.
        TestConsultant1 = Guid.NewGuid(),
        // A non-assessment scheme needs one tabulation for the Test Consultant and one that can
        // be published.
        TestConsultantTabulationId = TestConsultantTabulationId,
        Tabulations =
        [
            new SchemeTabulation { TabulationId = TestConsultantTabulationId, Name = "Test Consultant" },
            new SchemeTabulation { TabulationId = Guid.NewGuid(), Name = "Published" }
        ]
    };

    [Fact]
    public async Task CreateSchemeAsync_MonthAlreadyDistributed_IgnoresItAndFlagsItLocked()
    {
        var lookupRepository = new FakeLookupRepository
        {
            SystemSettings = new SystemSettingsEntity { ContractStartDate = new DateTime(2026, 4, 1) },
            MonthlyDistributions = [new MonthlyDistributionEntity { YearId = 2026, MonthId = 6 }]
        };
        var service = CreateService(new FakeSchemeRepository(), lookupRepository);
        var scheme = ValidScheme(2026);
        scheme.DistributionMonthJun = true;

        var created = await service.CreateSchemeAsync(scheme, CancellationToken.None);

        Assert.False(created.DistributionMonthJun);
        Assert.True(created.DistributionMonthApr);
        Assert.False(created.CanEditJun);
        Assert.True(created.CanEditApr);
    }

    [Fact]
    public async Task CreateSchemeAsync_ValidScheme_PersistsAndAssignsServerGeneratedFields()
    {
        var service = CreateService(new FakeSchemeRepository());

        var created = await service.CreateSchemeAsync(ValidScheme());

        Assert.NotEqual(Guid.Empty, created.SchemeId);
        Assert.NotEqual(Guid.Empty, created.SharedId);
        Assert.NotEqual(default, created.LastModified);
    }

    [Fact]
    public async Task CreateSchemeAsync_TabulationShowsRatings_TurnsOnScoreSamples()
    {
        var service = CreateService(new FakeSchemeRepository());
        var scheme = ValidScheme();
        scheme.StoreRatings = false;
        scheme.Tabulations[1].ShowRatings = true;

        var created = await service.CreateSchemeAsync(scheme);

        Assert.True(created.StoreRatings);
    }

    [Fact]
    public async Task CreateSchemeAsync_NoTabulationShowsRatings_LeavesScoreSamplesAlone()
    {
        var service = CreateService(new FakeSchemeRepository());
        var scheme = ValidScheme();
        scheme.StoreRatings = false;

        var created = await service.CreateSchemeAsync(scheme);

        Assert.False(created.StoreRatings);
    }

    [Fact]
    public async Task CreateSchemeAsync_CombinedPackaging_ForcesWeekNumberToOne()
    {
        var service = CreateService(new FakeSchemeRepository());
        var scheme = ValidScheme();
        scheme.CombinedPackaging = true;
        scheme.WeekNumber = 3;

        var created = await service.CreateSchemeAsync(scheme);

        Assert.Equal(1, created.WeekNumber);
    }

    [Fact]
    public async Task CreateSchemeAsync_WithoutCombinedPackaging_KeepsTheChosenWeekNumber()
    {
        var service = CreateService(new FakeSchemeRepository());
        var scheme = ValidScheme();
        scheme.CombinedPackaging = false;
        scheme.WeekNumber = 3;

        var created = await service.CreateSchemeAsync(scheme);

        Assert.Equal(3, created.WeekNumber);
    }

    [Fact]
    public async Task CreateSchemeAsync_IdentifierAlreadyUsedByAnotherScheme_ThrowsSchemeValidationException()
    {
        var lookupRepository = new FakeLookupRepository
        {
            PTNumbers = [new PTNumberEntity { SchemeId = Guid.NewGuid(), Identifier = "PT1234" }]
        };
        var service = CreateService(new FakeSchemeRepository(), lookupRepository);

        var exception = await Assert.ThrowsAsync<SchemeValidationException>(() => service.CreateSchemeAsync(ValidScheme()));

        Assert.Contains(exception.Errors, e =>
            e.Field == "Identifier" &&
            e.Message == "This PT number already exists. Please choose a unique PT number");
    }

    [Fact]
    public async Task CreateSchemeAsync_IdentifierUnused_Succeeds()
    {
        var lookupRepository = new FakeLookupRepository
        {
            PTNumbers = [new PTNumberEntity { SchemeId = Guid.NewGuid(), Identifier = "PT9999" }]
        };
        var service = CreateService(new FakeSchemeRepository(), lookupRepository);

        var created = await service.CreateSchemeAsync(ValidScheme());

        Assert.Equal("PT1234", created.Identifier);
    }

    [Fact]
    public async Task CreateSchemeAsync_ExternalPrimaryTestConsultant_ThrowsSchemeValidationException()
    {
        var externalConsultantId = Guid.NewGuid();
        var lookupRepository = new FakeLookupRepository
        {
            TestConsultants = [new SchemeUserEntity { UserId = externalConsultantId, FriendlyName = "External TC", IsExternal = true }]
        };
        var service = CreateService(new FakeSchemeRepository(), lookupRepository);
        var scheme = ValidScheme();
        scheme.TestConsultant1 = externalConsultantId;

        var exception = await Assert.ThrowsAsync<SchemeValidationException>(() => service.CreateSchemeAsync(scheme));

        Assert.Contains(exception.Errors, e =>
            e.Field == "TestConsultant1" &&
            e.Message == "The Primary Test Consultant must be an internal Test Consultant");
    }

    [Fact]
    public async Task CreateSchemeAsync_InternalPrimaryTestConsultant_Succeeds()
    {
        var internalConsultantId = Guid.NewGuid();
        var lookupRepository = new FakeLookupRepository
        {
            TestConsultants = [new SchemeUserEntity { UserId = internalConsultantId, FriendlyName = "Internal TC", IsExternal = false }]
        };
        var service = CreateService(new FakeSchemeRepository(), lookupRepository);
        var scheme = ValidScheme();
        scheme.TestConsultant1 = internalConsultantId;

        var created = await service.CreateSchemeAsync(scheme);

        Assert.Equal(internalConsultantId, created.TestConsultant1);
    }

    [Fact]
    public async Task CreateSchemeAsync_WithViewers_LinksEachViewerToTheScheme()
    {
        var viewerRepository = new PTL.Api.Tests.Participant.FakeViewerRepository();
        var service = new SchemeService(new FakeSchemeRepository(), new FakeLookupRepository(), viewerRepository, NullLogger<SchemeService>.Instance);
        var scheme = ValidScheme();
        var firstViewer = Guid.NewGuid();
        var secondViewer = Guid.NewGuid();
        scheme.ViewerIds = [firstViewer, secondViewer];

        var created = await service.CreateSchemeAsync(scheme);

        var links = await viewerRepository.GetSchemeViewersAsync(created.SchemeId);
        Assert.Equal(2, links.Count);
        Assert.Contains(links, l => l.ViewerId == firstViewer);
        Assert.Contains(links, l => l.ViewerId == secondViewer);
    }

    [Fact]
    public async Task UpdateSchemeAsync_ViewerSelectionChanged_AddsAndRemovesOnlyTheDifference()
    {
        var repository = new FakeSchemeRepository();
        var viewerRepository = new PTL.Api.Tests.Participant.FakeViewerRepository();
        var service = new SchemeService(repository, new FakeLookupRepository(), viewerRepository, NullLogger<SchemeService>.Instance);

        var keptViewer = Guid.NewGuid();
        var removedViewer = Guid.NewGuid();
        var addedViewer = Guid.NewGuid();

        var scheme = ValidScheme();
        scheme.ViewerIds = [keptViewer, removedViewer];
        var created = await service.CreateSchemeAsync(scheme);

        var keptLinkId = (await viewerRepository.GetSchemeViewersAsync(created.SchemeId)).Single(l => l.ViewerId == keptViewer).ViewerSchemeId;

        var update = ValidScheme();
        update.ViewerIds = [keptViewer, addedViewer];
        await service.UpdateSchemeAsync(created.SchemeId, update);

        var links = await viewerRepository.GetSchemeViewersAsync(created.SchemeId);
        Assert.Equal(2, links.Count);
        Assert.DoesNotContain(links, l => l.ViewerId == removedViewer);
        Assert.Contains(links, l => l.ViewerId == addedViewer);

        // The untouched link keeps its original fldViewerSchemeId rather than being deleted and
        // re-inserted.
        Assert.Equal(keptLinkId, links.Single(l => l.ViewerId == keptViewer).ViewerSchemeId);
    }

    [Fact]
    public async Task CreateSchemeAsync_InvalidIdentifier_ThrowsSchemeValidationException()
    {
        var service = CreateService(new FakeSchemeRepository());
        var scheme = ValidScheme();
        scheme.Identifier = "BADID";

        await Assert.ThrowsAsync<SchemeValidationException>(() => service.CreateSchemeAsync(scheme));
    }

    [Fact]
    public async Task CreateSchemeAsync_BothDistributionMonthsAndAsAvailable_ThrowsSchemeValidationException()
    {
        var service = CreateService(new FakeSchemeRepository());
        var scheme = ValidScheme();
        scheme.DistributionAsAvailable = true;

        await Assert.ThrowsAsync<SchemeValidationException>(() => service.CreateSchemeAsync(scheme));
    }

    [Fact]
    public async Task UpdateSchemeAsync_UnknownScheme_ReturnsNull()
    {
        var service = CreateService(new FakeSchemeRepository());

        var result = await service.UpdateSchemeAsync(Guid.NewGuid(), ValidScheme());

        Assert.Null(result);
    }

    [Fact]
    public async Task UpdateSchemeAsync_PreservesSharedIdAndRequiresAssessment()
    {
        var repository = new FakeSchemeRepository();
        var service = CreateService(repository);
        var scheme = ValidScheme();
        scheme.RequiresAssessment = true;
        scheme.Assessor1 = Guid.NewGuid();
        scheme.Assessor2 = Guid.NewGuid();
        var created = await service.CreateSchemeAsync(scheme);

        // RequiresAssessment is forced back to the stored value, so the update is still validated
        // as an assessment scheme and must carry its assessors.
        var updatedFields = ValidScheme(created.YearId);
        updatedFields.RequiresAssessment = false;
        updatedFields.Assessor1 = scheme.Assessor1;
        updatedFields.Assessor2 = scheme.Assessor2;
        updatedFields.Name = "Updated Name";
        var updated = await service.UpdateSchemeAsync(created.SchemeId, updatedFields);

        Assert.NotNull(updated);
        Assert.Equal(created.SharedId, updated!.SharedId);
        Assert.True(updated.RequiresAssessment);
        Assert.Equal("Updated Name", updated.Name);
    }

    [Fact]
    public async Task UpdateSchemeAsync_ReadOnlyScheme_ThrowsSchemeValidationException()
    {
        var repository = new FakeSchemeRepository();
        var service = CreateService(repository);
        var created = await service.CreateSchemeAsync(ValidScheme(yearId: DateTime.UtcNow.Year - 1));

        await Assert.ThrowsAsync<SchemeValidationException>(() => service.UpdateSchemeAsync(created.SchemeId, ValidScheme(created.YearId)));
    }

    [Fact]
    public async Task SearchSchemesAsync_FiltersByYearAndSearchTerm()
    {
        var repository = new FakeSchemeRepository();
        var service = CreateService(repository);
        var year = DateTime.UtcNow.Year + 1;
        var scheme1 = ValidScheme(year);
        scheme1.Identifier = "PT1111";
        await service.CreateSchemeAsync(scheme1);
        var scheme2 = ValidScheme(year);
        scheme2.Identifier = "PT2222";
        await service.CreateSchemeAsync(scheme2);

        var result = await service.SearchSchemesAsync(year, "PT1111", 1, 20);

        Assert.Single(result.Items);
        Assert.Equal("PT1111", result.Items[0].CurrentIdentifier);
    }

    [Fact]
    public async Task GetSchemeFamiliesAsync_FiltersByNameOnly_SimpleSubstringMatch()
    {
        var repository = new FakeSchemeRepository();
        var service = CreateService(repository);
        var scheme1 = ValidScheme();
        scheme1.Name = "Salmonella Scheme";
        await service.CreateSchemeAsync(scheme1);
        var scheme2 = ValidScheme();
        scheme2.Name = "Listeria Scheme";
        await service.CreateSchemeAsync(scheme2);

        var result = await service.GetSchemeFamiliesAsync(1, 20, "salmonella");

        Assert.Single(result.Items);
        Assert.Equal("Salmonella Scheme", result.Items[0].Name);
    }

    [Fact]
    public async Task GetSchemeFamilyHistoryAsync_ReturnsSchemesForSharedId()
    {
        var repository = new FakeSchemeRepository();
        var service = CreateService(repository);
        var created = await service.CreateSchemeAsync(ValidScheme());

        var history = await service.GetSchemeFamilyHistoryAsync(created.SharedId);

        Assert.Single(history);
        Assert.Equal(created.SchemeId, history[0].SchemeId);
    }

    [Fact]
    public async Task RenewSchemeAsync_UnknownScheme_ReturnsNull()
    {
        var service = CreateService(new FakeSchemeRepository());

        var renewed = await service.RenewSchemeAsync(Guid.NewGuid());

        Assert.Null(renewed);
    }

    [Fact]
    public async Task RenewSchemeAsync_CopiesScalarFieldsAndKeepsTheFamilyTogether()
    {
        var repository = new FakeSchemeRepository();
        var service = CreateService(repository);
        var original = ValidScheme();
        original.DistributionMonthApr = true;
        var created = await service.CreateSchemeAsync(original);

        var renewed = await service.RenewSchemeAsync(created.SchemeId);

        Assert.NotNull(renewed);
        Assert.Equal(created.SharedId, renewed.SharedId);
        Assert.Equal(created.YearId + 1, renewed.YearId);
        Assert.Equal(created.Identifier, renewed.Identifier);
        Assert.Equal(created.Name, renewed.Name);
        Assert.True(renewed.DistributionMonthApr);
        Assert.Equal(Guid.Empty, renewed.SchemeId);
    }

    [Fact]
    public async Task RenewSchemeAsync_MatchingNamedPostagePlanExistsNextYear_ResolvesItsId()
    {
        var oldPostageId = Guid.NewGuid();
        var nextYearPostageId = Guid.NewGuid();
        var year = DateTime.UtcNow.Year + 1;
        var lookupRepository = new FakeLookupRepository
        {
            PostagePricingPlans =
            [
                new PostagePricingPlanEntity { PostageId = oldPostageId, Name = "Standard", YearId = year },
                new PostagePricingPlanEntity { PostageId = nextYearPostageId, Name = "Standard", YearId = year + 1 }
            ]
        };
        var repository = new FakeSchemeRepository();
        var service = CreateService(repository, lookupRepository);
        var original = ValidScheme(year);
        original.Postage = oldPostageId;
        var created = await service.CreateSchemeAsync(original);

        var renewed = await service.RenewSchemeAsync(created.SchemeId);

        Assert.Equal(nextYearPostageId, renewed!.Postage);
    }

    [Fact]
    public async Task RenewSchemeAsync_NoMatchingNamedPostagePlanNextYear_ResolvesToNullWithoutThrowing()
    {
        var oldPostageId = Guid.NewGuid();
        var year = DateTime.UtcNow.Year + 1;
        var lookupRepository = new FakeLookupRepository
        {
            PostagePricingPlans = [new PostagePricingPlanEntity { PostageId = oldPostageId, Name = "Standard", YearId = year }]
        };
        var repository = new FakeSchemeRepository();
        var service = CreateService(repository, lookupRepository);
        var original = ValidScheme(year);
        original.Postage = oldPostageId;
        var created = await service.CreateSchemeAsync(original);

        var renewed = await service.RenewSchemeAsync(created.SchemeId);

        Assert.Null(renewed!.Postage);
    }

    [Fact]
    public async Task RenewSchemeAsync_CopiesTestsAndTabulationsWithFreshIdsAndRemapsReferences()
    {
        var repository = new FakeSchemeRepository();
        var service = CreateService(repository);
        var resultItemId = Guid.NewGuid();
        var original = ValidScheme();
        original.Tests =
        [
            new SchemeTest
            {
                TestId = Guid.NewGuid(),
                TestTypeId = Guid.NewGuid(),
                TestType = "Serology",
                ResultItems = [new SchemeTestResultItem { TestResultItemId = resultItemId, TestResultItemTypeId = Guid.NewGuid(), TestResultItemType = "Positive" }]
            }
        ];
        var publishedTabulationId = original.Tabulations.Single(t => t.Name == "Published").TabulationId;
        original.Tabulations =
        [
            .. original.Tabulations.Where(t => t.TabulationId != publishedTabulationId),
            new SchemeTabulation { TabulationId = publishedTabulationId, Name = "Published", ResultItemIds = [resultItemId] }
        ];
        var created = await service.CreateSchemeAsync(original);

        var renewed = await service.RenewSchemeAsync(created.SchemeId);

        var renewedTest = Assert.Single(renewed!.Tests);
        var renewedResultItem = Assert.Single(renewedTest.ResultItems);
        Assert.NotEqual(resultItemId, renewedResultItem.TestResultItemId);

        var renewedTabulation = Assert.Single(renewed.Tabulations, t => t.Name == "Published");
        Assert.NotEqual(publishedTabulationId, renewedTabulation.TabulationId);
        Assert.Equal(renewedResultItem.TestResultItemId, Assert.Single(renewedTabulation.ResultItemIds));

        // The Test Consultant tabulation selection follows the same remap.
        var renewedConsultantTabulation = Assert.Single(renewed.Tabulations, t => t.Name == "Test Consultant");
        Assert.Equal(renewedConsultantTabulation.TabulationId, renewed.TestConsultantTabulationId);
    }

    [Fact]
    public async Task RenewSchemeAsync_CopiesViewers()
    {
        var repository = new FakeSchemeRepository();
        var viewerRepository = new PTL.Api.Tests.Participant.FakeViewerRepository();
        var service = CreateService(repository, new FakeLookupRepository(), viewerRepository);
        var viewerId = Guid.NewGuid();
        var original = ValidScheme();
        original.ViewerIds = [viewerId];
        var created = await service.CreateSchemeAsync(original);

        var renewed = await service.RenewSchemeAsync(created.SchemeId);

        Assert.Equal(viewerId, Assert.Single(renewed!.ViewerIds));
    }

    [Fact]
    public async Task CreateSchemeAsync_SharedIdSupplied_PreservesItInsteadOfGeneratingANewOne()
    {
        var service = CreateService(new FakeSchemeRepository());
        var scheme = ValidScheme();
        var existingSharedId = Guid.NewGuid();
        scheme.SharedId = existingSharedId;

        var created = await service.CreateSchemeAsync(scheme);

        Assert.Equal(existingSharedId, created.SharedId);
    }

    [Fact]
    public async Task CreateSchemeAsync_RenewalReusesItsOwnFamilysIdentifier_DoesNotFlagItAsADuplicate()
    {
        var repository = new FakeSchemeRepository();
        var created = await CreateService(repository).CreateSchemeAsync(ValidScheme());
        var lookupRepository = new FakeLookupRepository
        {
            // The original scheme's own PT number is already on record under its own SchemeId -
            // exactly what a genuine family member looks like to the uniqueness check.
            PTNumbers = [new PTNumberEntity { SchemeId = created.SchemeId, Identifier = "PT1234" }]
        };
        var service = CreateService(repository, lookupRepository);

        // A Renew draft carries the same SharedId and the same (unchanged) Identifier forward.
        var renewalDraft = ValidScheme(created.YearId + 1);
        renewalDraft.SharedId = created.SharedId;

        var renewed = await service.CreateSchemeAsync(renewalDraft);

        Assert.Equal("PT1234", renewed.Identifier);
    }

    [Fact]
    public async Task CreateSchemeAsync_IdentifierCollidesWithADifferentFamily_StillThrows()
    {
        var lookupRepository = new FakeLookupRepository
        {
            PTNumbers = [new PTNumberEntity { SchemeId = Guid.NewGuid(), Identifier = "PT1234" }]
        };
        var service = CreateService(new FakeSchemeRepository(), lookupRepository);

        await Assert.ThrowsAsync<SchemeValidationException>(() => service.CreateSchemeAsync(ValidScheme()));
    }

    [Fact]
    public async Task CreateSchemeAsync_RenewalWithALockedMonth_RestoresItsInheritedValueInsteadOfWipingItFalse()
    {
        var lookupRepository = new FakeLookupRepository
        {
            SystemSettings = new SystemSettingsEntity { ContractStartDate = new DateTime(2026, 4, 1) },
            // The new (renewed) year's April is already locked system-wide.
            MonthlyDistributions = [new MonthlyDistributionEntity { YearId = 2027, MonthId = 4 }]
        };
        var repository = new FakeSchemeRepository();
        var service = CreateService(repository, lookupRepository);
        var original = ValidScheme(2026);
        original.DistributionMonthApr = true;
        var created = await service.CreateSchemeAsync(original);

        var renewalDraft = ValidScheme(2027);
        renewalDraft.SharedId = created.SharedId;
        renewalDraft.DistributionMonthApr = true;

        var renewed = await service.CreateSchemeAsync(renewalDraft);

        Assert.False(renewed.CanEditApr);
        Assert.True(renewed.DistributionMonthApr);
    }
}
