using Microsoft.AspNetCore.Mvc;
using PTL.Api.Controllers;
using PTL.Api.Tests.Lookup;
using PTL.Core.Lookup;

namespace PTL.Api.Tests.Endpoints;

public class LookupControllerTests
{
    private static LookupController CreateController(FakeLookupRepository repository) =>
        new(new LookupService(repository));

    [Fact]
    public async Task GetCountries_ReturnsMappedResponses()
    {
        var countryId = Guid.NewGuid();
        var repository = new FakeLookupRepository { Countries = [new CountryEntity { CountryId = countryId, Country = "United Kingdom" }] };
        var controller = CreateController(repository);

        var result = await controller.GetCountries(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var countries = Assert.IsType<IReadOnlyList<PTL.Contracts.Lookup.CountryResponse>>(ok.Value, exactMatch: false);
        Assert.Single(countries);
        Assert.Equal(countryId, countries[0].CountryId);
        Assert.Equal("United Kingdom", countries[0].Country);
    }

    [Fact]
    public async Task GetCurrencies_ReturnsMappedResponsesWithLongName()
    {
        var currencyId = Guid.NewGuid();
        var repository = new FakeLookupRepository { Currencies = [new CurrencyEntity { CurrencyId = currencyId, Name = "British Pound", Symbol = "£" }] };
        var controller = CreateController(repository);

        var result = await controller.GetCurrencies(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var currencies = Assert.IsType<IReadOnlyList<PTL.Contracts.Lookup.CurrencyResponse>>(ok.Value, exactMatch: false);
        Assert.Single(currencies);
        Assert.Equal("£ - British Pound", currencies[0].LongName);
    }

    [Fact]
    public async Task GetCustomerTypes_ReturnsMappedResponses()
    {
        var customerTypeId = Guid.NewGuid();
        var repository = new FakeLookupRepository { CustomerTypes = [new CustomerTypeEntity { CustomerTypeId = customerTypeId, CustomerType = "Commercial" }] };
        var controller = CreateController(repository);

        var result = await controller.GetCustomerTypes(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var customerTypes = Assert.IsType<IReadOnlyList<PTL.Contracts.Lookup.CustomerTypeResponse>>(ok.Value, exactMatch: false);
        Assert.Single(customerTypes);
        Assert.Equal(customerTypeId, customerTypes[0].CustomerTypeId);
    }

    [Fact]
    public async Task GetVatRatings_ReturnsMappedResponses()
    {
        var vatRatingId = Guid.NewGuid();
        var repository = new FakeLookupRepository { VatRatings = [new VatRatingEntity { VatRatingId = vatRatingId, VatRating = "Standard" }] };
        var controller = CreateController(repository);

        var result = await controller.GetVatRatings(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var vatRatings = Assert.IsType<IReadOnlyList<PTL.Contracts.Lookup.VatRatingResponse>>(ok.Value, exactMatch: false);
        Assert.Single(vatRatings);
        Assert.Equal(vatRatingId, vatRatings[0].VatRatingId);
    }

    [Fact]
    public async Task GetLabTypes_ReturnsMappedResponses()
    {
        var labTypeId = Guid.NewGuid();
        var repository = new FakeLookupRepository { LabTypes = [new LabTypeEntity { LabTypeId = labTypeId, Name = "Reference laboratory" }] };
        var controller = CreateController(repository);

        var result = await controller.GetLabTypes(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var labTypes = Assert.IsType<IReadOnlyList<PTL.Contracts.Lookup.LabTypeResponse>>(ok.Value, exactMatch: false);
        Assert.Single(labTypes);
        Assert.Equal(labTypeId, labTypes[0].LabTypeId);
    }

    [Fact]
    public async Task GetCurrentYears_ReturnsMappedResponses()
    {
        var repository = new FakeLookupRepository { Years = [new YearEntity { YearId = 2026, Year = "2026/27" }] };
        var controller = CreateController(repository);

        var result = await controller.GetCurrentYears(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var years = Assert.IsType<IReadOnlyList<PTL.Contracts.Lookup.YearResponse>>(ok.Value, exactMatch: false);
        Assert.Single(years);
        Assert.Equal("2026/27", years[0].Year);
    }

    [Fact]
    public async Task GetAllYears_ReturnsMappedResponses()
    {
        var repository = new FakeLookupRepository { AllYears = [new YearEntity { YearId = 2020, Year = "2020/21" }] };
        var controller = CreateController(repository);

        var result = await controller.GetAllYears(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var years = Assert.IsType<IReadOnlyList<PTL.Contracts.Lookup.YearResponse>>(ok.Value, exactMatch: false);
        Assert.Single(years);
        Assert.Equal("2020/21", years[0].Year);
    }

    [Fact]
    public async Task GetSchemeCurrencies_ReturnsMappedResponses()
    {
        var schemeId = Guid.NewGuid();
        var repository = new FakeLookupRepository
        {
            SchemeCurrencies = [new SchemeCurrencyEntity { SchemeCurrencyId = Guid.NewGuid(), SchemeId = schemeId, CurrencyId = Guid.NewGuid(), Price = 12.5m, CurrencyName = "British Pound", CurrencySymbol = "£" }]
        };
        var controller = CreateController(repository);

        var result = await controller.GetSchemeCurrencies(schemeId, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var currencies = Assert.IsType<IReadOnlyList<PTL.Contracts.Lookup.SchemeCurrencyResponse>>(ok.Value, exactMatch: false);
        Assert.Single(currencies);
        Assert.Equal(schemeId, currencies[0].SchemeId);
        Assert.Equal(12.5m, currencies[0].Price);
        Assert.Equal("£", currencies[0].CurrencySymbol);
    }

    [Fact]
    public async Task GetPostagePricingPlans_ReturnsMappedResponses()
    {
        var postageId = Guid.NewGuid();
        var repository = new FakeLookupRepository
        {
            PostagePricingPlans = [new PostagePricingPlanEntity { PostageId = postageId, Name = "Standard", UKPrice = 5.5m, EUPrice = 8m, NonEUPrice = 12m, YearId = 2026 }]
        };
        var controller = CreateController(repository);

        var result = await controller.GetPostagePricingPlans(2026, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var plans = Assert.IsType<IReadOnlyList<PTL.Contracts.Lookup.PostagePricingPlanResponse>>(ok.Value, exactMatch: false);
        Assert.Single(plans);
        Assert.Equal(postageId, plans[0].PostageId);
        Assert.Equal(2026, plans[0].YearId);
    }

    [Fact]
    public async Task GetSystemSettings_ReturnsMappedResponse()
    {
        var repository = new FakeLookupRepository { SystemSettings = new PTL.Core.Lookup.SystemSettingsEntity { UTNumber = "UT3/306" } };
        var controller = CreateController(repository);

        var result = await controller.GetSystemSettings(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var settings = Assert.IsType<PTL.Contracts.Lookup.SystemSettingsResponse>(ok.Value);
        Assert.Equal("UT3/306", settings.UTNumber);
    }
}
