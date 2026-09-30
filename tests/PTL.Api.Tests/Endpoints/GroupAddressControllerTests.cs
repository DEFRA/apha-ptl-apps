using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using PTL.Api.Controllers;
using PTL.Api.Tests.GroupAddress;
using PTL.Contracts.GroupAddress;
using PTL.Core.GroupAddress;

namespace PTL.Api.Tests.Endpoints;

public class GroupAddressControllerTests
{
    private static (PTL.Api.Controllers.GroupAddressController Controller, FakeGroupAddressRepository Repository) CreateController()
    {
        var repository = new FakeGroupAddressRepository();
        var service = new GroupAddressService(repository, NullLogger<GroupAddressService>.Instance);
        return (new PTL.Api.Controllers.GroupAddressController(service, NullLogger<PTL.Api.Controllers.GroupAddressController>.Instance), repository);
    }

    private static PTL.Core.GroupAddress.GroupAddress ValidGroupAddress(string identifier = "PTL-001") => new()
    {
        Identifier = identifier,
        Address1 = "1 Sample Street",
        Address2 = "Second Line",
        Address3 = "Third Line",
        Address4 = "Fourth Line",
        Address5 = "Fifth Line",
        CountryId = Guid.NewGuid(),
        Telephone = "020 1234 5678",
        PackingInstructions = "Fragile"
    };

    private static GroupAddressSaveRequest ValidRequest(Guid countryId) => new(
        "PTL-001", "1 Sample Street", "Second Line", "Third Line", "Fourth Line", "Fifth Line",
        countryId, "020 1234 5678", "Fragile");

    [Fact]
    public async Task GetGroupAddresses_ReturnsAllMappedResponses()
    {
        var (controller, repository) = CreateController();
        repository.GroupAddresses.Add(ValidGroupAddress());

        var result = await controller.GetGroupAddresses(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var items = Assert.IsType<List<GroupAddressResponse>>(ok.Value);
        Assert.Single(items);
        Assert.Equal("PTL-001", items[0].Identifier);
        Assert.Equal("1 Sample Street", items[0].Address1);
        Assert.Equal("Second Line", items[0].Address2);
        Assert.Equal("Third Line", items[0].Address3);
        Assert.Equal("Fourth Line", items[0].Address4);
        Assert.Equal("Fifth Line", items[0].Address5);
        Assert.Equal("020 1234 5678", items[0].Telephone);
        Assert.Equal("Fragile", items[0].PackingInstructions);
    }

    [Fact]
    public async Task SearchGroupAddresses_ReturnsPagedResponse()
    {
        var (controller, repository) = CreateController();
        repository.GroupAddresses.Add(ValidGroupAddress("PTL-002"));
        repository.GroupAddresses.Add(ValidGroupAddress("PTL-001"));

        var result = await controller.SearchGroupAddresses(new GroupAddressSearchRequest(1, 20), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<GroupAddressSearchResponse>(ok.Value);
        Assert.Equal(2, response.TotalCount);
        Assert.Equal(1, response.Page);
        Assert.Equal(20, response.PageSize);
        Assert.Equal("PTL-001", response.Items[0].Identifier);
    }

    [Fact]
    public async Task GetGroupAddress_UnknownId_ReturnsNotFound()
    {
        var (controller, _) = CreateController();

        var result = await controller.GetGroupAddress(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task GetGroupAddress_ExistingId_ReturnsMappedResponse()
    {
        var (controller, repository) = CreateController();
        var groupAddress = ValidGroupAddress();
        groupAddress.GroupAddressId = Guid.NewGuid();
        repository.GroupAddresses.Add(groupAddress);

        var result = await controller.GetGroupAddress(groupAddress.GroupAddressId, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<GroupAddressResponse>(ok.Value);
        Assert.Equal(groupAddress.GroupAddressId, response.GroupAddressId);
    }

    [Fact]
    public async Task CreateGroupAddress_ValidRequest_ReturnsCreatedResult()
    {
        var (controller, _) = CreateController();

        var result = await controller.CreateGroupAddress(ValidRequest(Guid.NewGuid()), CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        var response = Assert.IsType<GroupAddressResponse>(created.Value);
        Assert.Equal("PTL-001", response.Identifier);
    }

    [Fact]
    public async Task CreateGroupAddress_InvalidRequest_ReturnsValidationProblem()
    {
        var (controller, _) = CreateController();

        var result = await controller.CreateGroupAddress(ValidRequest(Guid.Empty) with { Identifier = string.Empty }, CancellationToken.None);

        Assert.IsType<ObjectResult>(result.Result);
    }

    [Fact]
    public async Task UpdateGroupAddress_UnknownId_ReturnsNotFound()
    {
        var (controller, _) = CreateController();

        var result = await controller.UpdateGroupAddress(Guid.NewGuid(), ValidRequest(Guid.NewGuid()), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task UpdateGroupAddress_ValidRequest_ReturnsOk()
    {
        var (controller, repository) = CreateController();
        var existing = ValidGroupAddress();
        existing.GroupAddressId = Guid.NewGuid();
        repository.GroupAddresses.Add(existing);
        repository.UpdateReturns = existing;

        var result = await controller.UpdateGroupAddress(existing.GroupAddressId, ValidRequest(Guid.NewGuid()), CancellationToken.None);

        Assert.IsType<OkObjectResult>(result.Result);
    }

    [Fact]
    public async Task UpdateGroupAddress_InvalidRequest_ReturnsValidationProblem()
    {
        var (controller, repository) = CreateController();
        var existing = ValidGroupAddress();
        existing.GroupAddressId = Guid.NewGuid();
        repository.GroupAddresses.Add(existing);

        var result = await controller.UpdateGroupAddress(existing.GroupAddressId, ValidRequest(Guid.NewGuid()) with { Address1 = string.Empty }, CancellationToken.None);

        Assert.IsType<ObjectResult>(result.Result);
    }
}
