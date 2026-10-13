using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using PTL.Api.Controllers;
using PTL.Api.Tests.Country;
using PTL.Contracts.Country;
using PTL.Core.Country;

namespace PTL.Api.Tests.Endpoints;

public class CountryControllerTests
{
    private static CountryController CreateController(FakeCountryRepository repository)
    {
        var controller = new CountryController(new CountryService(repository));

        // ValidationProblem() resolves ProblemDetailsFactory from HttpContext.RequestServices,
        // which a bare controller instance does not have without this wiring.
        var services = new ServiceCollection().AddMvc().Services.BuildServiceProvider();
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { RequestServices = services }
        };

        return controller;
    }

    [Fact]
    public async Task GetCountries_ReturnsMappedCountries()
    {
        var countryTypeId = Guid.NewGuid();
        var repository = new FakeCountryRepository
        {
            Countries = [new PTL.Core.Country.Country { CountryId = Guid.NewGuid(), CountryName = "France", CountryTypeId = countryTypeId, CountryType = "EU", AllocationCount = 2 }]
        };
        var controller = CreateController(repository);

        var result = await controller.GetCountries(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var countries = Assert.IsAssignableFrom<IReadOnlyList<CountryResponse>>(ok.Value);
        var country = Assert.Single(countries);
        Assert.Equal("France", country.Country);
        Assert.Equal(2, country.AllocationCount);
    }

    [Fact]
    public async Task GetCountryTypes_ReturnsMappedTypes()
    {
        var countryTypeId = Guid.NewGuid();
        var repository = new FakeCountryRepository { CountryTypes = [new CountryTypeEntity { CountryTypeId = countryTypeId, CountryType = "EU" }] };
        var controller = CreateController(repository);

        var result = await controller.GetCountryTypes(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var types = Assert.IsAssignableFrom<IReadOnlyList<CountryTypeResponse>>(ok.Value);
        Assert.Equal("EU", Assert.Single(types).CountryType);
    }

    [Fact]
    public async Task CreateCountry_Valid_ReturnsOk()
    {
        var repository = new FakeCountryRepository();
        var controller = CreateController(repository);

        var result = await controller.CreateCountry(new CountrySaveRequest("France", Guid.NewGuid()), CancellationToken.None);

        Assert.IsType<OkObjectResult>(result.Result);
        Assert.Single(repository.Created);
    }

    [Fact]
    public async Task CreateCountry_MissingName_ReturnsValidationProblem()
    {
        var controller = CreateController(new FakeCountryRepository());

        var result = await controller.CreateCountry(new CountrySaveRequest(string.Empty, Guid.NewGuid()), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task UpdateCountry_NotFound_ReturnsNotFound()
    {
        var controller = CreateController(new FakeCountryRepository());

        var result = await controller.UpdateCountry(Guid.NewGuid(), new CountrySaveRequest("France", Guid.NewGuid()), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task UpdateCountry_Valid_ReturnsOk()
    {
        var countryId = Guid.NewGuid();
        var repository = new FakeCountryRepository
        {
            Countries = [new PTL.Core.Country.Country { CountryId = countryId, CountryName = "France", CountryTypeId = Guid.NewGuid() }]
        };
        var controller = CreateController(repository);

        var result = await controller.UpdateCountry(countryId, new CountrySaveRequest("French Republic", Guid.NewGuid()), CancellationToken.None);

        Assert.IsType<OkObjectResult>(result.Result);
        Assert.Single(repository.Updated);
    }

    [Fact]
    public async Task DeleteCountry_InUse_ReturnsOkWithBlockedResult()
    {
        var countryId = Guid.NewGuid();
        var repository = new FakeCountryRepository
        {
            Countries = [new PTL.Core.Country.Country { CountryId = countryId, CountryName = "France", AllocationCount = 8 }]
        };
        var controller = CreateController(repository);

        var result = await controller.DeleteCountry(countryId, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<CountryDeleteResponse>(ok.Value);
        Assert.False(response.Success);
        Assert.Equal("This country is being used by 8 customer(s)/participant(s)/Group Addresses.", response.Message);
    }

    [Fact]
    public async Task DeleteCountry_NoDependencies_ReturnsOkWithSuccess()
    {
        var countryId = Guid.NewGuid();
        var repository = new FakeCountryRepository
        {
            Countries = [new PTL.Core.Country.Country { CountryId = countryId, CountryName = "France", AllocationCount = 0 }]
        };
        var controller = CreateController(repository);

        var result = await controller.DeleteCountry(countryId, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<CountryDeleteResponse>(ok.Value);
        Assert.True(response.Success);
        Assert.Contains(countryId, repository.Deleted);
    }
}
