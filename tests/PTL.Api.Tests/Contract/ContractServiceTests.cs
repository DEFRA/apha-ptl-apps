using Microsoft.Extensions.Logging.Abstractions;
using PTL.Contracts.Contract;
using PTL.Core.Contract;

namespace PTL.Api.Tests.Contract;

public class ContractServiceTests
{
    private static ContractService CreateService(FakeContractRepository repository) =>
        new(repository, NullLogger<ContractService>.Instance);

    private static PTL.Core.Contract.Contract ValidContract(Guid? customerId = null, int? yearId = null) => new()
    {
        CustomerId = customerId ?? Guid.NewGuid(),
        YearId = yearId ?? DateTime.UtcNow.Year + 1,
        UTNumber = "UT3/306",
        ContractSignatory = "Alice Example",
        AcknowledgementPostedDate = new DateTime(2026, 1, 1),
        AcknowledgementReturnedDate = new DateTime(2026, 1, 5),
        JobSheetPostedDate = new DateTime(2026, 1, 10),
        DateOfLeaving = new DateTime(2026, 12, 31),
        IsActive = true,
        Suffix = "A"
    };

    [Fact]
    public async Task CreateContractAsync_ValidContract_PersistsAndAssignsServerGeneratedFields()
    {
        var service = CreateService(new FakeContractRepository());

        var created = await service.CreateContractAsync(ValidContract());

        Assert.NotEqual(Guid.Empty, created.ContractId);
        Assert.NotEqual(default, created.CommencementDate);
        Assert.StartsWith("QAL/", created.QalNumber);
    }

    [Fact]
    public async Task CreateContractAsync_BothUtAndFtNumbers_ThrowsContractValidationException()
    {
        var service = CreateService(new FakeContractRepository());
        var contract = ValidContract();
        contract.FTNumber = "FT99999";

        await Assert.ThrowsAsync<ContractValidationException>(() => service.CreateContractAsync(contract));
    }

    [Fact]
    public async Task CreateContractAsync_NeitherUtNorFtNumber_ThrowsContractValidationException()
    {
        var service = CreateService(new FakeContractRepository());
        var contract = ValidContract();
        contract.UTNumber = string.Empty;

        await Assert.ThrowsAsync<ContractValidationException>(() => service.CreateContractAsync(contract));
    }

    [Fact]
    public async Task CreateContractAsync_SentinelDate_ThrowsContractValidationException()
    {
        var service = CreateService(new FakeContractRepository());
        var contract = ValidContract();
        contract.DateOfLeaving = new DateTime(9999, 1, 1);

        await Assert.ThrowsAsync<ContractValidationException>(() => service.CreateContractAsync(contract));
    }

    [Fact]
    public async Task UpdateContractAsync_UnknownContract_ReturnsNull()
    {
        var service = CreateService(new FakeContractRepository());

        var result = await service.UpdateContractAsync(Guid.NewGuid(), ValidContract());

        Assert.Null(result);
    }

    [Fact]
    public async Task UpdateContractAsync_PreservesCommencementDateAndApprovedFields()
    {
        var repository = new FakeContractRepository();
        var service = CreateService(repository);
        var created = await service.CreateContractAsync(ValidContract());

        var updatedFields = ValidContract(created.CustomerId, created.YearId);
        updatedFields.ContractSignatory = "Bob Example";
        var updated = await service.UpdateContractAsync(created.ContractId, updatedFields);

        Assert.NotNull(updated);
        Assert.Equal(created.CommencementDate, updated!.CommencementDate);
        Assert.Equal("Bob Example", updated.ContractSignatory);
    }

    [Fact]
    public async Task UpdateContractAsync_ReadOnlyContract_ThrowsContractValidationException()
    {
        var repository = new FakeContractRepository();
        var service = CreateService(repository);
        var created = await service.CreateContractAsync(ValidContract(yearId: DateTime.UtcNow.Year - 1));

        await Assert.ThrowsAsync<ContractValidationException>(() => service.UpdateContractAsync(created.ContractId, ValidContract(created.CustomerId, created.YearId)));
    }

    [Fact]
    public async Task SearchContractsAsync_ByYear_ReturnsExactYearMatchesOnly()
    {
        var repository = new FakeContractRepository();
        var service = CreateService(repository);
        var customerId = Guid.NewGuid();
        var thisYear = DateTime.UtcNow.Year;
        await service.CreateContractAsync(ValidContract(customerId, thisYear));
        await service.CreateContractAsync(ValidContract(customerId, thisYear + 1));

        var result = await service.SearchContractsAsync(customerId, thisYear, ContractPeriodFilter.All, null, 1, 20);

        Assert.Single(result.Items);
        Assert.Equal(thisYear, result.Items[0].YearId);
    }

    [Fact]
    public async Task SearchContractsAsync_FiltersBySuffixWithPaging()
    {
        var repository = new FakeContractRepository();
        var service = CreateService(repository);
        var customerId = Guid.NewGuid();
        var year = DateTime.UtcNow.Year + 1;
        var first = ValidContract(customerId, year);
        first.Suffix = "A";
        var second = ValidContract(customerId, year);
        second.Suffix = "B";
        await service.CreateContractAsync(first);
        await service.CreateContractAsync(second);

        var searchResult = await service.SearchContractsAsync(customerId, null, ContractPeriodFilter.All, "B", 1, 20);
        var pagedResult = await service.SearchContractsAsync(customerId, null, ContractPeriodFilter.All, null, 1, 1);

        Assert.Single(searchResult.Items);
        Assert.Equal("B", searchResult.Items[0].Suffix);
        Assert.Equal(2, pagedResult.TotalCount);
        Assert.Single(pagedResult.Items);
    }

    [Fact]
    public async Task CreateContractAsync_AlwaysForcesIsOnlineOrderFalse()
    {
        var service = CreateService(new FakeContractRepository());
        var contract = ValidContract();
        contract.IsOnlineOrder = true;

        var created = await service.CreateContractAsync(contract);

        Assert.False(created.IsOnlineOrder);
    }

    [Fact]
    public async Task UpdateContractAsync_PreservesIsOnlineOrder()
    {
        var repository = new FakeContractRepository();
        var service = CreateService(repository);
        var contract = ValidContract();
        contract.IsOnlineOrder = false;
        var created = await service.CreateContractAsync(contract);

        var updatedFields = ValidContract(created.CustomerId, created.YearId);
        updatedFields.IsOnlineOrder = true;
        var updated = await service.UpdateContractAsync(created.ContractId, updatedFields);

        Assert.NotNull(updated);
        Assert.False(updated!.IsOnlineOrder);
    }
}
