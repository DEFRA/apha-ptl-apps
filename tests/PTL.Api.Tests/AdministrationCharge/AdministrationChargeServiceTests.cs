using PTL.Api.Tests.Audit;
using PTL.Core.AdministrationCharge;
using PTL.Core.Audit;

namespace PTL.Api.Tests.AdministrationCharge;

public class AdministrationChargeServiceTests
{
    [Fact]
    public async Task GetChargesAsync_ReturnsRepositoryResult()
    {
        var repository = new FakeAdministrationChargeRepository
        {
            Charges = [new AdministrationChargeEntity { AdministrationChargeId = Guid.NewGuid(), Name = "Administration Charge" }]
        };
        var service = new AdministrationChargeService(repository, new FakeActingUserAccessor());

        var result = await service.GetChargesAsync();

        Assert.Single(result);
        Assert.Equal("Administration Charge", result[0].Name);
    }

    [Fact]
    public async Task GetPricesAsync_ReturnsRepositoryResult()
    {
        var chargeId = Guid.NewGuid();
        var currencyId = Guid.NewGuid();
        var repository = new FakeAdministrationChargeRepository
        {
            Prices = [new AdministrationChargeCurrencyEntity { AdministrationChargeCurrencyId = Guid.NewGuid(), AdministrationChargeId = chargeId, CurrencyId = currencyId, Price = 25.00m }]
        };
        var service = new AdministrationChargeService(repository, new FakeActingUserAccessor());

        var result = await service.GetPricesAsync();

        Assert.Single(result);
        Assert.Equal(25.00m, result[0].Price);
    }

    [Fact]
    public async Task SetPriceAsync_NoExistingPrice_Inserts()
    {
        var chargeId = Guid.NewGuid();
        var currencyId = Guid.NewGuid();
        var repository = new FakeAdministrationChargeRepository();
        var actingUser = new FakeActingUserAccessor { ActingUserId = Guid.NewGuid() };
        var service = new AdministrationChargeService(repository, actingUser);

        var result = await service.SetPriceAsync(chargeId, currencyId, 30.00m);

        Assert.True(result.IsValid);
        var inserted = Assert.Single(repository.Inserted);
        Assert.Equal(chargeId, inserted.AdministrationChargeId);
        Assert.Equal(currencyId, inserted.CurrencyId);
        Assert.Equal(30.00m, inserted.Price);
        Assert.Empty(repository.Updated);
        var audit = Assert.IsType<AuditEntry>(repository.LastAuditEntry);
        Assert.Equal("SetAdministrationCharge", audit.Action);
        Assert.Equal("AdministrationCharge", audit.EntityType);
        Assert.Equal(chargeId.ToString(), audit.EntityId);
        Assert.Equal(actingUser.ActingUserId, audit.ActingUserId);
    }

    [Fact]
    public async Task SetPriceAsync_ExistingPrice_Updates()
    {
        var chargeId = Guid.NewGuid();
        var currencyId = Guid.NewGuid();
        var repository = new FakeAdministrationChargeRepository
        {
            Prices = [new AdministrationChargeCurrencyEntity { AdministrationChargeCurrencyId = Guid.NewGuid(), AdministrationChargeId = chargeId, CurrencyId = currencyId, Price = 25.00m }]
        };
        var actingUser = new FakeActingUserAccessor { ActingUserId = Guid.NewGuid() };
        var service = new AdministrationChargeService(repository, actingUser);

        var result = await service.SetPriceAsync(chargeId, currencyId, 40.00m);

        Assert.True(result.IsValid);
        Assert.Empty(repository.Inserted);
        var updated = Assert.Single(repository.Updated);
        Assert.Equal(40.00m, updated.Price);
        var audit = Assert.IsType<AuditEntry>(repository.LastAuditEntry);
        Assert.Equal("SetAdministrationCharge", audit.Action);
        Assert.Equal(chargeId.ToString(), audit.EntityId);
        Assert.Equal(actingUser.ActingUserId, audit.ActingUserId);
    }

    [Fact]
    public async Task SetPriceAsync_NegativePrice_ReturnsErrorAndDoesNotPersist()
    {
        var repository = new FakeAdministrationChargeRepository();
        var service = new AdministrationChargeService(repository, new FakeActingUserAccessor());

        var result = await service.SetPriceAsync(Guid.NewGuid(), Guid.NewGuid(), -1.00m);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Field == "Price");
        Assert.Empty(repository.Inserted);
        Assert.Empty(repository.Updated);
        Assert.Null(repository.LastAuditEntry);
    }
}
