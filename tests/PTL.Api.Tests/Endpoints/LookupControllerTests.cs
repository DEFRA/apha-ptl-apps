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
        var countries = Assert.IsAssignableFrom<IReadOnlyList<PTL.Contracts.Lookup.CountryResponse>>(ok.Value);
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
        var currencies = Assert.IsAssignableFrom<IReadOnlyList<PTL.Contracts.Lookup.CurrencyResponse>>(ok.Value);
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
        var customerTypes = Assert.IsAssignableFrom<IReadOnlyList<PTL.Contracts.Lookup.CustomerTypeResponse>>(ok.Value);
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
        var vatRatings = Assert.IsAssignableFrom<IReadOnlyList<PTL.Contracts.Lookup.VatRatingResponse>>(ok.Value);
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
        var labTypes = Assert.IsAssignableFrom<IReadOnlyList<PTL.Contracts.Lookup.LabTypeResponse>>(ok.Value);
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
        var years = Assert.IsAssignableFrom<IReadOnlyList<PTL.Contracts.Lookup.YearResponse>>(ok.Value);
        Assert.Single(years);
        Assert.Equal("2026/27", years[0].Year);
    }
}
