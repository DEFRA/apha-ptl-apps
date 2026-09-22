using PTL.Core.Lookup;

namespace PTL.Api.Tests.Lookup;

public class LookupServiceTests
{
    [Fact]
    public async Task GetCountriesAsync_ReturnsRepositoryResult()
    {
        var repository = new FakeLookupRepository
        {
            Countries = [new CountryEntity { CountryId = Guid.NewGuid(), Country = "United Kingdom" }]
        };
        var service = new LookupService(repository);

        var result = await service.GetCountriesAsync();

        Assert.Single(result);
        Assert.Equal("United Kingdom", result[0].Country);
    }

    [Fact]
    public async Task GetCurrenciesAsync_ReturnsRepositoryResult()
    {
        var repository = new FakeLookupRepository
        {
            Currencies = [new CurrencyEntity { CurrencyId = Guid.NewGuid(), Name = "British Pound", Symbol = "£" }]
        };
        var service = new LookupService(repository);

        var result = await service.GetCurrenciesAsync();

        Assert.Single(result);
        Assert.Equal("£ - British Pound", result[0].LongName);
    }

    [Fact]
    public async Task GetCustomerTypesAsync_ReturnsRepositoryResult()
    {
        var repository = new FakeLookupRepository
        {
            CustomerTypes = [new CustomerTypeEntity { CustomerTypeId = Guid.NewGuid(), CustomerType = "Commercial" }]
        };
        var service = new LookupService(repository);

        var result = await service.GetCustomerTypesAsync();

        Assert.Single(result);
        Assert.Equal("Commercial", result[0].CustomerType);
    }

    [Fact]
    public async Task GetVatRatingsAsync_ReturnsRepositoryResult()
    {
        var repository = new FakeLookupRepository
        {
            VatRatings = [new VatRatingEntity { VatRatingId = Guid.NewGuid(), VatRating = "Standard" }]
        };
        var service = new LookupService(repository);

        var result = await service.GetVatRatingsAsync();

        Assert.Single(result);
        Assert.Equal("Standard", result[0].VatRating);
    }

    [Fact]
    public async Task GetLabTypesAsync_ReturnsRepositoryResult()
    {
        var repository = new FakeLookupRepository
        {
            LabTypes = [new LabTypeEntity { LabTypeId = Guid.NewGuid(), Name = "Reference laboratory" }]
        };
        var service = new LookupService(repository);

        var result = await service.GetLabTypesAsync();

        Assert.Single(result);
        Assert.Equal("Reference laboratory", result[0].Name);
    }

    [Fact]
    public async Task GetCurrentYearsAsync_ReturnsRepositoryResult()
    {
        var repository = new FakeLookupRepository
        {
            Years = [new YearEntity { YearId = 2026, Year = "2026/27" }]
        };
        var service = new LookupService(repository);

        var result = await service.GetCurrentYearsAsync();

        Assert.Single(result);
        Assert.Equal("2026/27", result[0].Year);
    }

    [Fact]
    public async Task GetSchemeCurrenciesAsync_FiltersRepositoryResultBySchemeId()
    {
        var schemeId = Guid.NewGuid();
        var repository = new FakeLookupRepository
        {
            SchemeCurrencies =
            [
                new SchemeCurrencyEntity { SchemeCurrencyId = Guid.NewGuid(), SchemeId = schemeId, CurrencyId = Guid.NewGuid(), Price = 12.5m, CurrencyName = "British Pound", CurrencySymbol = "£" },
                new SchemeCurrencyEntity { SchemeCurrencyId = Guid.NewGuid(), SchemeId = Guid.NewGuid(), CurrencyId = Guid.NewGuid(), Price = 20m, CurrencyName = "Euro", CurrencySymbol = "€" }
            ]
        };
        var service = new LookupService(repository);

        var result = await service.GetSchemeCurrenciesAsync(schemeId);

        Assert.Single(result);
        Assert.Equal(schemeId, result[0].SchemeId);
    }

    [Fact]
    public async Task GetPostagePricingPlansForYearAsync_ReturnsRepositoryResult()
    {
        var repository = new FakeLookupRepository
        {
            PostagePricingPlans = [new PostagePricingPlanEntity { PostageId = Guid.NewGuid(), Name = "Standard", UKPrice = 5.5m, YearId = 2026 }]
        };
        var service = new LookupService(repository);

        var result = await service.GetPostagePricingPlansForYearAsync(2026);

        Assert.Single(result);
        Assert.Equal("Standard", result[0].Name);
    }
}
