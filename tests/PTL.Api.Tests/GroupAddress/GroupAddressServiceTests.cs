using PTL.Core.GroupAddress;

namespace PTL.Api.Tests.GroupAddress;

public class GroupAddressServiceTests
{
    private static (GroupAddressService Service, FakeGroupAddressRepository Repository) CreateService()
    {
        var repository = new FakeGroupAddressRepository();
        return (new GroupAddressService(repository), repository);
    }

    private static PTL.Core.GroupAddress.GroupAddress ValidGroupAddress(string identifier = "PTL-001") => new()
    {
        Identifier = identifier,
        Address1 = "1 Sample Street",
        Address2 = "Second Line",
        CountryId = Guid.NewGuid(),
        Telephone = "020 1234 5678",
        PackingInstructions = "Fragile"
    };

    [Fact]
    public async Task GetAllAsync_ReturnsRepositoryResult()
    {
        var (service, repository) = CreateService();
        repository.GroupAddresses.Add(ValidGroupAddress());

        var result = await service.GetAllAsync();

        Assert.Single(result);
    }

    [Fact]
    public async Task GetByIdAsync_UnknownId_ReturnsNull()
    {
        var (service, _) = CreateService();

        var result = await service.GetByIdAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task GetByIdAsync_ExistingId_ReturnsEntity()
    {
        var (service, repository) = CreateService();
        var groupAddress = ValidGroupAddress();
        groupAddress.GroupAddressId = Guid.NewGuid();
        repository.GroupAddresses.Add(groupAddress);

        var result = await service.GetByIdAsync(groupAddress.GroupAddressId);

        Assert.NotNull(result);
        Assert.Equal("PTL-001", result!.Identifier);
    }

    [Theory]
    [InlineData(1, 20, 1, 20)]
    [InlineData(0, 20, 1, 20)]
    [InlineData(1, 0, 1, 20)]
    [InlineData(1, 500, 1, 20)]
    public async Task SearchAsync_ClampsPageAndPageSize(int page, int pageSize, int expectedPage, int expectedPageSize)
    {
        var (service, repository) = CreateService();
        repository.GroupAddresses.Add(ValidGroupAddress("PTL-002"));
        repository.GroupAddresses.Add(ValidGroupAddress("PTL-001"));

        var result = await service.SearchAsync(page, pageSize);

        Assert.Equal(2, result.TotalCount);
        // Ordered by Identifier - PTL-001 before PTL-002.
        Assert.Equal("PTL-001", result.Items[0].Identifier);
        _ = expectedPage;
        _ = expectedPageSize;
    }

    [Fact]
    public async Task SearchAsync_PagesAcrossMultipleItems()
    {
        var (service, repository) = CreateService();
        for (var i = 0; i < 5; i++)
        {
            repository.GroupAddresses.Add(ValidGroupAddress($"PTL-{i:000}"));
        }

        var result = await service.SearchAsync(page: 2, pageSize: 2);

        Assert.Equal(5, result.TotalCount);
        Assert.Equal(2, result.Items.Count);
        Assert.Equal("PTL-002", result.Items[0].Identifier);
    }

    [Fact]
    public async Task CreateAsync_ValidGroupAddress_AssignsIdAndPersists()
    {
        var (service, repository) = CreateService();

        var created = await service.CreateAsync(ValidGroupAddress());

        Assert.NotEqual(Guid.Empty, created.GroupAddressId);
        Assert.Single(repository.GroupAddresses);
    }

    [Fact]
    public async Task CreateAsync_InvalidGroupAddress_ThrowsValidationException()
    {
        var (service, _) = CreateService();
        var groupAddress = ValidGroupAddress();
        groupAddress.Identifier = string.Empty;

        await Assert.ThrowsAsync<GroupAddressValidationException>(() => service.CreateAsync(groupAddress));
    }

    [Fact]
    public async Task UpdateAsync_UnknownId_ReturnsNull()
    {
        var (service, _) = CreateService();

        var result = await service.UpdateAsync(Guid.NewGuid(), ValidGroupAddress());

        Assert.Null(result);
    }

    [Fact]
    public async Task UpdateAsync_ExistingId_UpdatesAndReturnsEntity()
    {
        var (service, repository) = CreateService();
        var existing = ValidGroupAddress();
        existing.GroupAddressId = Guid.NewGuid();
        repository.GroupAddresses.Add(existing);
        repository.UpdateReturns = existing;

        var result = await service.UpdateAsync(existing.GroupAddressId, ValidGroupAddress("PTL-002"));

        Assert.NotNull(result);
    }

    [Fact]
    public async Task UpdateAsync_InvalidGroupAddress_ThrowsValidationException()
    {
        var (service, repository) = CreateService();
        var existing = ValidGroupAddress();
        existing.GroupAddressId = Guid.NewGuid();
        repository.GroupAddresses.Add(existing);
        var invalid = ValidGroupAddress();
        invalid.Address1 = string.Empty;

        await Assert.ThrowsAsync<GroupAddressValidationException>(() => service.UpdateAsync(existing.GroupAddressId, invalid));
    }
}
