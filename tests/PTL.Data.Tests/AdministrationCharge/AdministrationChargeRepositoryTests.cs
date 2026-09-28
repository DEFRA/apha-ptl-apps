using System.Data;
using PTL.Data.AdministrationCharge;
using PTL.Data.Infrastructure;
using PTL.Data.Tests.Fakes;

namespace PTL.Data.Tests.AdministrationCharge;

// Exercises AdministrationChargeRepository against a fake ADO.NET connection (see Fakes/) instead
// of a live SQL Server - see ContractRepositoryTests/LookupRepositoryTests for the pattern this follows.
public class AdministrationChargeRepositoryTests
{
    public AdministrationChargeRepositoryTests() => DapperColumnMappings.Register();

    private const string GetPriceSql = "EXEC dbo.spgAdministrationChargeCurrencyByAdministrationChargeIdCurrencyId @AdministrationChargeId, @CurrencyId";
    private const string InsertSql = "EXEC dbo.spiAdministrationChargeCurrency @AdministrationChargeCurrencyId, @AdministrationChargeId, @CurrencyId, @Price";
    private const string UpdateSql = "EXEC dbo.spuAdministrationChargeCurrency @AdministrationChargeCurrencyId, @AdministrationChargeId, @CurrencyId, @Price";

    private static (AdministrationChargeRepository Repository, FakeDbConnection Connection) CreateRepository()
    {
        var connection = new FakeDbConnection();
        var repository = new AdministrationChargeRepository(new FakeDbConnectionFactory(connection));
        return (repository, connection);
    }

    [Fact]
    public async Task GetChargesAsync_ReturnsMappedCharges()
    {
        var (repository, connection) = CreateRepository();
        var table = new DataTable();
        table.Columns.Add("fldAdministrationChargeId", typeof(Guid));
        table.Columns.Add("fldAdministrationCharge", typeof(string));
        table.Rows.Add(Guid.NewGuid(), "Administration Charge");
        connection.RespondToQuery("EXEC dbo.spgaAdministrationCharge", table);

        var result = await repository.GetChargesAsync();

        Assert.Single(result);
        Assert.Equal("Administration Charge", result[0].Name);
    }

    [Fact]
    public async Task GetPricesAsync_ReturnsMappedPrices()
    {
        var (repository, connection) = CreateRepository();
        var table = new DataTable();
        table.Columns.Add("fldAdministrationChargeCurrencyId", typeof(Guid));
        table.Columns.Add("fldCurrencyId", typeof(Guid));
        table.Columns.Add("fldAdministrationChargeId", typeof(Guid));
        table.Columns.Add("fldPrice", typeof(decimal));
        table.Rows.Add(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 25.00m);
        connection.RespondToQuery("EXEC dbo.spgaAdministrationChargeCurrency", table);

        var result = await repository.GetPricesAsync();

        Assert.Single(result);
        Assert.Equal(25.00m, result[0].Price);
    }

    [Fact]
    public async Task GetPriceAsync_Found_ReturnsMappedPrice()
    {
        var (repository, connection) = CreateRepository();
        var chargeId = Guid.NewGuid();
        var currencyId = Guid.NewGuid();
        var table = new DataTable();
        table.Columns.Add("fldAdministrationChargeCurrencyId", typeof(Guid));
        table.Columns.Add("fldCurrencyId", typeof(Guid));
        table.Columns.Add("fldAdministrationChargeId", typeof(Guid));
        table.Columns.Add("fldPrice", typeof(decimal));
        table.Rows.Add(Guid.NewGuid(), currencyId, chargeId, 25.00m);
        connection.RespondToQuery(GetPriceSql, table);

        var result = await repository.GetPriceAsync(chargeId, currencyId);

        Assert.NotNull(result);
        Assert.Equal(25.00m, result!.Price);
    }

    [Fact]
    public async Task GetPriceAsync_NotFound_ReturnsNull()
    {
        var (repository, connection) = CreateRepository();
        connection.RespondToQuery(GetPriceSql, new DataTable());

        var result = await repository.GetPriceAsync(Guid.NewGuid(), Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task InsertPriceAsync_ExecutesInsertWithParameters()
    {
        var (repository, connection) = CreateRepository();
        connection.RespondToNonQuery(InsertSql, 1);
        var price = new Core.AdministrationCharge.AdministrationChargeCurrencyEntity
        {
            AdministrationChargeCurrencyId = Guid.NewGuid(),
            AdministrationChargeId = Guid.NewGuid(),
            CurrencyId = Guid.NewGuid(),
            Price = 30.00m
        };

        await repository.InsertPriceAsync(price);

        var command = Assert.Single(connection.ExecutedCommands, c => c.CommandText == InsertSql);
        Assert.Equal(price.Price, command.ParameterValue("@Price"));
    }

    [Fact]
    public async Task UpdatePriceAsync_ExecutesUpdateWithParameters()
    {
        var (repository, connection) = CreateRepository();
        connection.RespondToNonQuery(UpdateSql, 1);
        var price = new Core.AdministrationCharge.AdministrationChargeCurrencyEntity
        {
            AdministrationChargeCurrencyId = Guid.NewGuid(),
            AdministrationChargeId = Guid.NewGuid(),
            CurrencyId = Guid.NewGuid(),
            Price = 40.00m
        };

        await repository.UpdatePriceAsync(price);

        var command = Assert.Single(connection.ExecutedCommands, c => c.CommandText == UpdateSql);
        Assert.Equal(price.Price, command.ParameterValue("@Price"));
    }
}
