using Microsoft.Extensions.Logging.Abstractions;
using PTL.Core.Scheme;

namespace PTL.Api.Tests.Scheme;

public class SchemeServiceTests
{
    private static SchemeService CreateService(FakeSchemeRepository repository) =>
        new(repository, NullLogger<SchemeService>.Instance);

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
        CustomsVolume = "1kg"
    };

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
        var created = await service.CreateSchemeAsync(scheme);

        var updatedFields = ValidScheme(created.YearId);
        updatedFields.RequiresAssessment = false;
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
    public async Task GetSchemeFamilyHistoryAsync_ReturnsSchemesForSharedId()
    {
        var repository = new FakeSchemeRepository();
        var service = CreateService(repository);
        var created = await service.CreateSchemeAsync(ValidScheme());

        var history = await service.GetSchemeFamilyHistoryAsync(created.SharedId);

        Assert.Single(history);
        Assert.Equal(created.SchemeId, history[0].SchemeId);
    }
}
