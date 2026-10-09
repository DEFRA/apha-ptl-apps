using PTL.Core.Country;

namespace PTL.Api.Tests.Country;

public class CountryServiceTests
{
    private static CountryService CreateService(FakeCountryRepository repository) => new(repository);

    [Fact]
    public async Task CreateAsync_ValidRequest_PersistsAndReturnsCountry()
    {
        var repository = new FakeCountryRepository();
        var countryTypeId = Guid.NewGuid();
        var service = CreateService(repository);

        var created = await service.CreateAsync("France", countryTypeId);

        Assert.Equal("France", created.CountryName);
        Assert.Single(repository.Created);
    }

    [Fact]
    public async Task CreateAsync_MissingName_ThrowsValidationException()
    {
        var service = CreateService(new FakeCountryRepository());

        var ex = await Assert.ThrowsAsync<CountryValidationException>(() => service.CreateAsync(" ", Guid.NewGuid()));

        Assert.Contains(ex.Errors, e => e.Field == "Country");
    }

    [Fact]
    public async Task CreateAsync_DuplicateName_ThrowsValidationExceptionWithLegacyMessage()
    {
        var repository = new FakeCountryRepository
        {
            Countries = [new PTL.Core.Country.Country { CountryId = Guid.NewGuid(), CountryName = "France", CountryTypeId = Guid.NewGuid() }]
        };
        var service = CreateService(repository);

        var ex = await Assert.ThrowsAsync<CountryValidationException>(() => service.CreateAsync("france", Guid.NewGuid()));

        Assert.Contains(ex.Errors, e => e.Message == CountryValidator.DuplicateNameMessage);
        Assert.Empty(repository.Created);
    }

    [Fact]
    public async Task UpdateAsync_NotFound_ReturnsNull()
    {
        var service = CreateService(new FakeCountryRepository());

        var result = await service.UpdateAsync(Guid.NewGuid(), "France", Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task UpdateAsync_DuplicateNameExcludesSelf_Succeeds()
    {
        var countryId = Guid.NewGuid();
        var repository = new FakeCountryRepository
        {
            Countries = [new PTL.Core.Country.Country { CountryId = countryId, CountryName = "France", CountryTypeId = Guid.NewGuid() }]
        };
        var service = CreateService(repository);

        var result = await service.UpdateAsync(countryId, "France", Guid.NewGuid());

        Assert.NotNull(result);
        Assert.Single(repository.Updated);
    }

    [Fact]
    public async Task UpdateAsync_DuplicateNameAgainstAnotherCountry_ThrowsValidationException()
    {
        var countryId = Guid.NewGuid();
        var repository = new FakeCountryRepository
        {
            Countries =
            [
                new PTL.Core.Country.Country { CountryId = countryId, CountryName = "France", CountryTypeId = Guid.NewGuid() },
                new PTL.Core.Country.Country { CountryId = Guid.NewGuid(), CountryName = "Germany", CountryTypeId = Guid.NewGuid() }
            ]
        };
        var service = CreateService(repository);

        var ex = await Assert.ThrowsAsync<CountryValidationException>(() => service.UpdateAsync(countryId, "germany", Guid.NewGuid()));

        Assert.Contains(ex.Errors, e => e.Message == CountryValidator.DuplicateNameMessage);
    }

    [Fact]
    public async Task DeleteAsync_NotFound_ReturnsFailure()
    {
        var service = CreateService(new FakeCountryRepository());

        var result = await service.DeleteAsync(Guid.NewGuid());

        Assert.False(result.Success);
    }

    [Fact]
    public async Task DeleteAsync_CountryInUse_ReturnsLegacyWarningMessageAndDoesNotDelete()
    {
        var countryId = Guid.NewGuid();
        var repository = new FakeCountryRepository
        {
            Countries = [new PTL.Core.Country.Country { CountryId = countryId, CountryName = "France", AllocationCount = 8 }]
        };
        var service = CreateService(repository);

        var result = await service.DeleteAsync(countryId);

        Assert.False(result.Success);
        Assert.Equal("This country is being used by 8 customer(s)/participant(s)/Group Addresses.", result.Message);
        Assert.Empty(repository.Deleted);
    }

    [Fact]
    public async Task DeleteAsync_NoDependencies_DeletesAndReturnsSuccess()
    {
        var countryId = Guid.NewGuid();
        var repository = new FakeCountryRepository
        {
            Countries = [new PTL.Core.Country.Country { CountryId = countryId, CountryName = "France", AllocationCount = 0 }]
        };
        var service = CreateService(repository);

        var result = await service.DeleteAsync(countryId);

        Assert.True(result.Success);
        Assert.Contains(countryId, repository.Deleted);
    }
}
