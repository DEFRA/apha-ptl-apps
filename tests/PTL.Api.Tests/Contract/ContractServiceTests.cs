using Microsoft.Extensions.Logging.Abstractions;
using PTL.Contracts.Contract;
using PTL.Core.Contract;
using PTL.Core.Participant;

namespace PTL.Api.Tests.Contract;

public class ContractServiceTests
{
    private static ContractService CreateService(FakeContractRepository repository, FakeParticipantSchemeRepository? participantSchemeRepository = null) =>
        new(repository, participantSchemeRepository ?? new FakeParticipantSchemeRepository(), NullLogger<ContractService>.Instance);

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

    private static ContractItemsAggregate ValidItems(Guid contractId, bool isReadOnly = false) => new()
    {
        ContractId = contractId,
        Suffix = "A",
        YearId = DateTime.UtcNow.Year,
        QalNumber = "QAL/00001",
        Symbol = "£",
        DiscountRate = 0.1m,
        AdministrationCharge = 5m,
        NumberCourier = 2,
        CourierPrice = 10m,
        NumberPostage = 1,
        PostagePrice = 3m,
        NumberSpecialDelivery = 0,
        SpecialDeliveryPrice = 0m,
        IsReadOnly = isReadOnly
    };

    [Fact]
    public async Task GetContractItemsAsync_ExistingContract_ReturnsAggregate()
    {
        var repository = new FakeContractRepository { ContractItems = ValidItems(Guid.NewGuid()) };
        var service = CreateService(repository);

        var items = await service.GetContractItemsAsync(repository.ContractItems!.ContractId);

        Assert.NotNull(items);
        Assert.Equal(20m, items!.CourierPriceTotal);
    }

    [Fact]
    public async Task GetContractItemsAsync_UnknownContract_ReturnsNull()
    {
        var service = CreateService(new FakeContractRepository());

        var items = await service.GetContractItemsAsync(Guid.NewGuid());

        Assert.Null(items);
    }

    [Fact]
    public async Task RemoveContractItemAsync_UnknownContract_ReturnsFalse()
    {
        var service = CreateService(new FakeContractRepository());

        var removed = await service.RemoveContractItemAsync(Guid.NewGuid(), Guid.NewGuid());

        Assert.False(removed);
    }

    [Fact]
    public async Task RemoveContractItemAsync_ReadOnlyContract_ThrowsContractValidationException()
    {
        var repository = new FakeContractRepository();
        var contractId = Guid.NewGuid();
        repository.Seed(new PTL.Core.Contract.Contract { ContractId = contractId, IsReadOnly = true });
        var service = CreateService(repository);

        await Assert.ThrowsAsync<ContractValidationException>(() => service.RemoveContractItemAsync(contractId, Guid.NewGuid()));
    }

    [Fact]
    public async Task RemoveContractItemAsync_ItemNotFound_ReturnsFalse()
    {
        var repository = new FakeContractRepository();
        var contractId = Guid.NewGuid();
        repository.Seed(new PTL.Core.Contract.Contract { ContractId = contractId, IsReadOnly = false });
        var service = CreateService(repository);

        var removed = await service.RemoveContractItemAsync(contractId, Guid.NewGuid());

        Assert.False(removed);
    }

    [Fact]
    public async Task RemoveContractItemAsync_ItemBelongsToDifferentContract_ReturnsFalse()
    {
        var repository = new FakeContractRepository();
        var contractId = Guid.NewGuid();
        repository.Seed(new PTL.Core.Contract.Contract { ContractId = contractId, IsReadOnly = false });
        var participantSchemeRepository = new FakeParticipantSchemeRepository();
        var participantSchemeId = Guid.NewGuid();
        participantSchemeRepository.Add(new ParticipantSchemeRecord { ParticipantSchemeId = participantSchemeId, ContractId = Guid.NewGuid() });
        var service = CreateService(repository, participantSchemeRepository);

        var removed = await service.RemoveContractItemAsync(contractId, participantSchemeId);

        Assert.False(removed);
    }

    [Fact]
    public async Task RemoveContractItemAsync_ValidItem_SoftDeletesAndReturnsTrue()
    {
        var repository = new FakeContractRepository();
        var contractId = Guid.NewGuid();
        repository.Seed(new PTL.Core.Contract.Contract { ContractId = contractId, IsReadOnly = false });
        var participantSchemeRepository = new FakeParticipantSchemeRepository();
        var participantSchemeId = Guid.NewGuid();
        participantSchemeRepository.Add(new ParticipantSchemeRecord { ParticipantSchemeId = participantSchemeId, ContractId = contractId });
        var service = CreateService(repository, participantSchemeRepository);

        var removed = await service.RemoveContractItemAsync(contractId, participantSchemeId);

        Assert.True(removed);
        var record = await participantSchemeRepository.GetByIdAsync(participantSchemeId);
        Assert.True(record!.IsRemoved);
    }
}
