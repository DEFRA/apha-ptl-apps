using PTL.Core.Distribution;
using PTL.Core.Scheme;
using CoreScheme = PTL.Core.Scheme.Scheme;

namespace PTL.Api.Tests.Distribution;

// Pins the first-time "Define Sample Numbers" path through SampleNumbersService: legacy
// SampleNumbers.aspx.vb rebuilt the structure from the Scheme on every fresh GET (re-consuming
// sample numbers each refresh); this persists it once as a draft and re-reads instead.
public class SampleNumbersServiceCreateTests
{
    private static readonly Guid DistributionSchemeId = Guid.Parse("aaaaaaaa-1111-1111-1111-111111111111");
    private static readonly Guid SchemeId = Guid.Parse("bbbbbbbb-1111-1111-1111-111111111111");
    private static readonly Guid ScheduleId = Guid.Parse("cccccccc-1111-1111-1111-111111111111");
    private static readonly Guid TabulationId = Guid.Parse("dddddddd-1111-1111-1111-111111111111");

    private static MonthlyDistributionSchemeEntity Distribution() => new()
    {
        MonthlyDistributionSchemeId = DistributionSchemeId,
        SchemeId = SchemeId,
        YearId = 2026,
        MonthId = 4,
        SchemeIdentifier = "PT0060",
        SchemeName = "Mastitis bovine",
        DistributionReference = "17152",
        DistributionReferenceSuffix = "A",
        ScheduleCode = "BA",
        TotalSetsOfSamplesRequired = 9,
    };

    private static CoreScheme FullScheme() => new()
    {
        SchemeId = SchemeId,
        Identifier = "PT0060",
        // Deliberately different from the distribution's own id - the repository must be given
        // the SCHEME's schedule, because that is what owns the sample number sequence.
        ScheduleId = ScheduleId,
        NumberOfSamples = 2,
        LastModified = new DateTime(2026, 1, 15, 9, 30, 0, DateTimeKind.Unspecified),
        TestConsultantTabulationId = TabulationId,
        Tests = [new SchemeTest { TestId = Guid.NewGuid(), TestTypeId = Guid.NewGuid(), Order = 1 }],
        Tabulations = [new SchemeTabulation { TabulationId = TabulationId, Name = "Main tabulation" }],
    };

    // An existing distribution with no samples at all - the only shape that triggers a create.
    private static IntendedResultsData EmptyData(bool hasSampleNumbersDefined = false) =>
        new(
            new IntendedResultsHeaderEntity
            {
                MonthlyDistributionSchemeId = DistributionSchemeId,
                Comments = string.Empty,
                HasSampleNumbersDefined = hasSampleNumbersDefined,
            },
            [],
            [],
            [],
            [],
            [],
            [],
            []);

    private static IntendedResultsData PopulatedData(string sampleNumber = "26/0001")
    {
        var sampleId = Guid.NewGuid();
        var testId = Guid.NewGuid();
        return new IntendedResultsData(
            new IntendedResultsHeaderEntity { MonthlyDistributionSchemeId = DistributionSchemeId, Comments = "After create" },
            [new DistributionSampleEntity { SampleId = sampleId, DistributionSchemeId = DistributionSchemeId, SampleNumber = sampleNumber }],
            [new DistributionSampleTestEntity { SampleTestId = Guid.NewGuid(), SampleId = sampleId, TestId = testId, IsApplicable = true }],
            [new DistributionTestEntity { TestId = testId, TestName = "Somatic cell count", Order = 1 }],
            [],
            [],
            [],
            []);
    }

    private static (SampleNumbersService Service, FakeDistributionRepository Repository, FakeSchemeService SchemeService) CreateService(
        IntendedResultsData? data = null, CoreScheme? scheme = null)
    {
        var repository = new FakeDistributionRepository();
        repository.SchemesById[DistributionSchemeId] = Distribution();
        if (data is not null)
        {
            repository.IntendedResults[DistributionSchemeId] = data;
        }

        var schemeService = new FakeSchemeService();
        if (scheme is not null)
        {
            schemeService.Schemes[SchemeId] = scheme;
        }

        return (new SampleNumbersService(repository, schemeService), repository, schemeService);
    }

    [Fact]
    public async Task GetSampleNumbersAsync_NeverDefined_CreatesTheStructureAndReturnsTheReReadPage()
    {
        var (service, repository, _) = CreateService(EmptyData(), FullScheme());
        repository.IntendedResultsAfterCreate = PopulatedData();

        var page = await service.GetSampleNumbersAsync(DistributionSchemeId, CancellationToken.None);

        Assert.Single(repository.CreatedStructures);
        Assert.NotNull(page);
        Assert.Equal("26/0001", Assert.Single(page!.Samples).SampleNumber);
        Assert.Equal("After create", page.Comments);
        Assert.Equal("Somatic cell count", Assert.Single(page.Tests).TestName);
    }

    // An already-defined distribution that happens to have no samples is not a create trigger -
    // legacy only built a new set when the distribution had never been defined.
    [Fact]
    public async Task GetSampleNumbersAsync_AlreadyDefinedButEmpty_DoesNotCreate()
    {
        var (service, repository, _) = CreateService(EmptyData(hasSampleNumbersDefined: true), FullScheme());

        var page = await service.GetSampleNumbersAsync(DistributionSchemeId, CancellationToken.None);

        Assert.Empty(repository.CreatedStructures);
        Assert.NotNull(page);
        Assert.Empty(page!.Samples);
    }

    // The no-drift guarantee: refreshing a distribution that already has samples must never
    // re-run the create, because spgNextSampleNumber would advance the sequence again.
    [Fact]
    public async Task GetSampleNumbersAsync_SamplesAlreadyExist_DoesNotCreate()
    {
        var (service, repository, _) = CreateService(PopulatedData(), FullScheme());

        var page = await service.GetSampleNumbersAsync(DistributionSchemeId, CancellationToken.None);

        Assert.Empty(repository.CreatedStructures);
        Assert.Equal("26/0001", Assert.Single(page!.Samples).SampleNumber);
    }

    [Fact]
    public async Task GetSampleNumbersAsync_SchemeNotLoadable_ThrowsNamingTheDistribution()
    {
        var (service, _, _) = CreateService(EmptyData(), scheme: null);

        var exception = await Assert.ThrowsAsync<DistributionStructureException>(
            () => service.GetSampleNumbersAsync(DistributionSchemeId, CancellationToken.None));

        Assert.Contains("PT0060", exception.Message, StringComparison.Ordinal);
        Assert.Contains("17152A/BA", exception.Message, StringComparison.Ordinal);
    }

    // Losing the concurrent-create race is not an error: the other request has already written
    // the structure, so the re-read still produces a page.
    [Fact]
    public async Task GetSampleNumbersAsync_LosesTheCreateRace_StillReturnsTheReReadPage()
    {
        var (service, repository, _) = CreateService(EmptyData(), FullScheme());
        repository.CreateStructureSucceeds = false;
        repository.IntendedResultsAfterCreate = PopulatedData("26/0099");

        var page = await service.GetSampleNumbersAsync(DistributionSchemeId, CancellationToken.None);

        Assert.NotNull(page);
        Assert.Equal("26/0099", Assert.Single(page!.Samples).SampleNumber);
    }

    // If the re-read somehow comes back empty the screen must still render rather than 404.
    [Fact]
    public async Task GetSampleNumbersAsync_ReReadReturnsNothing_FallsBackToTheOriginalData()
    {
        var (service, repository, _) = CreateService(EmptyData(), FullScheme());
        repository.CreateStructureSucceeds = true;

        var page = await service.GetSampleNumbersAsync(DistributionSchemeId, CancellationToken.None);

        Assert.NotNull(page);
        Assert.Empty(page!.Samples);
    }

    [Fact]
    public async Task GetSampleNumbersAsync_PassesTheSchemesScheduleAndTheDistributionsMonthToTheRepository()
    {
        var (service, repository, _) = CreateService(EmptyData(), FullScheme());

        await service.GetSampleNumbersAsync(DistributionSchemeId, CancellationToken.None);

        var created = Assert.Single(repository.CreatedStructures);
        Assert.Equal(DistributionSchemeId, created.SchemeId);
        Assert.Equal(ScheduleId, created.ScheduleId);
        Assert.Equal(new DateTime(2026, 4, 1, 0, 0, 2, DateTimeKind.Unspecified), created.SampleNumberDate);
    }

    [Fact]
    public async Task GetSampleNumbersAsync_PassesAStructureBuiltFromTheScheme()
    {
        var scheme = FullScheme();
        scheme.NumberOfSamples = 3;
        var (service, repository, _) = CreateService(EmptyData(), scheme);

        await service.GetSampleNumbersAsync(DistributionSchemeId, CancellationToken.None);

        var structure = Assert.Single(repository.CreatedStructures).Structure;
        Assert.Equal(3, structure.Samples.Count);
        Assert.Single(structure.Tests);
        Assert.Equal(scheme.LastModified, structure.SchemeVersionDate);
        Assert.Equal(Assert.Single(structure.Tabulations).TabulationId, structure.TestConsultantTabulationId);
    }

    // An unreadable Scheme configuration must surface as the builder's business message, not as a
    // half-created distribution.
    [Fact]
    public async Task GetSampleNumbersAsync_SchemeCannotProduceAStructure_PropagatesTheBuilderFailure()
    {
        var scheme = FullScheme();
        scheme.TestConsultantTabulationId = null;
        var (service, repository, _) = CreateService(EmptyData(), scheme);

        var exception = await Assert.ThrowsAsync<DistributionStructureException>(
            () => service.GetSampleNumbersAsync(DistributionSchemeId, CancellationToken.None));

        Assert.Contains("has no Test Consultant Tabulation", exception.Message, StringComparison.Ordinal);
        Assert.Empty(repository.CreatedStructures);
    }

    [Fact]
    public async Task GetSampleNumbersAsync_UnknownDistribution_ReturnsNullWithoutCreating()
    {
        var (service, repository, _) = CreateService(EmptyData(), FullScheme());

        Assert.Null(await service.GetSampleNumbersAsync(Guid.NewGuid(), CancellationToken.None));
        Assert.Empty(repository.CreatedStructures);
    }
}
