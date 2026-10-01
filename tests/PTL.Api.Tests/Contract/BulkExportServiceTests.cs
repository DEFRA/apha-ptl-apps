using PTL.Core.Contract.Export.Bulk;
using PTL.Core.Contract.Renewal;
using PTL.Core.Contract.SampleAddress;

namespace PTL.Api.Tests.Contract;

public class BulkExportServiceTests
{
    private static BulkContractEntity Contract(decimal itemPrice, decimal adminCharge = 0m, decimal discountRate = 0m) => new()
    {
        ContractId = Guid.NewGuid(),
        AdministrationCharge = adminCharge,
        DiscountRate = discountRate,
        Items = [new BulkContractItemEntity { Price = itemPrice }]
    };

    // Legacy MailMergeContract: "Don't export contracts with a Grand Total of 0".
    [Fact]
    public async Task GetExportableContractsAsync_ExcludesZeroTotalContracts()
    {
        var repository = new FakeBulkExportRepository
        {
            Contracts = [Contract(0m), Contract(100m)]
        };

        var contracts = await new BulkExportService(repository).GetExportableContractsAsync();

        Assert.Equal(100m, Assert.Single(contracts).TotalPrice);
    }

    [Fact]
    public async Task GetExportableContractsAsync_KeepsAContractWhoseTotalComesOnlyFromTheAdminCharge()
    {
        var repository = new FakeBulkExportRepository { Contracts = [Contract(0m, adminCharge: 25m)] };

        Assert.Single(await new BulkExportService(repository).GetExportableContractsAsync());
    }

    [Fact]
    public void TotalPrice_MatchesTheLegacyFormula()
    {
        var contract = new BulkContractEntity
        {
            AdministrationCharge = 50m,
            DiscountRate = 0.10m,
            NumberPostage = 2,
            PostagePrice = 5m,
            NumberCourier = 3,
            CourierPrice = 10m,
            NumberSpecialDelivery = 1,
            SpecialDeliveryPrice = 20m,
            Items = [new BulkContractItemEntity { Price = 100m }, new BulkContractItemEntity { Price = 200m }]
        };

        Assert.Equal(300m, contract.TotalPriceItems);
        Assert.Equal(-30m, contract.DiscountPrice);

        // 300 - 30 + 50 + (2*5) + (3*10) + (1*20)
        Assert.Equal(380m, contract.TotalPrice);
    }

    [Fact]
    public async Task GetRenewalsAsync_PassesTheNonUkFlagStraightThrough()
    {
        var repository = new FakeBulkExportRepository();
        var service = new BulkExportService(repository);

        await service.GetRenewalsAsync(nonUk: true);
        Assert.True(repository.LastNonUk);

        await service.GetRenewalsAsync(nonUk: false);
        Assert.False(repository.LastNonUk);
    }

    private sealed class FakeBulkExportRepository : IBulkExportRepository
    {
        public IReadOnlyList<BulkContractEntity> Contracts { get; set; } = [];

        public bool? LastNonUk { get; private set; }

        public Task<IReadOnlyList<BulkContractEntity>> GetContractsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(Contracts);

        public Task<IReadOnlyList<SampleAddressEntity>> GetSampleAddressesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<SampleAddressEntity>>([]);

        public Task<IReadOnlyList<ContractRenewalEntity>> GetRenewalsAsync(bool nonUk, CancellationToken cancellationToken = default)
        {
            LastNonUk = nonUk;
            return Task.FromResult<IReadOnlyList<ContractRenewalEntity>>([]);
        }
    }
}
