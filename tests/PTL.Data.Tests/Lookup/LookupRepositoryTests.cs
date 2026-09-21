using System.Data;
using PTL.Data.Infrastructure;
using PTL.Data.Lookup;
using PTL.Data.Tests.Fakes;

namespace PTL.Data.Tests.Lookup;

// Exercises LookupRepository against a fake ADO.NET connection (see Fakes/) instead of a live SQL
// Server - see ContractRepositoryTests for the pattern this follows. Every method here is a
// parameterless (or single-parameter) read with no Create/Update counterpart.
public class LookupRepositoryTests
{
    public LookupRepositoryTests() => DapperColumnMappings.Register();

    private static (LookupRepository Repository, FakeDbConnection Connection) CreateRepository()
    {
        var connection = new FakeDbConnection();
        var repository = new LookupRepository(new FakeDbConnectionFactory(connection));
        return (repository, connection);
    }

    [Fact]
    public async Task GetCountriesAsync_ReturnsMappedCountries()
    {
        var (repository, connection) = CreateRepository();
        var table = new DataTable();
        table.Columns.Add("fldCountryId", typeof(Guid));
        table.Columns.Add("fldCountry", typeof(string));
        table.Rows.Add(Guid.NewGuid(), "United Kingdom");
        connection.RespondToQuery("EXEC dbo.spgaCountry", table);

        var result = await repository.GetCountriesAsync();

        Assert.Single(result);
        Assert.Equal("United Kingdom", result[0].Country);
    }

    [Fact]
    public async Task GetCurrenciesAsync_ReturnsMappedCurrencies()
    {
        var (repository, connection) = CreateRepository();
        var table = new DataTable();
        table.Columns.Add("fldCurrencyId", typeof(Guid));
        table.Columns.Add("fldName", typeof(string));
        table.Columns.Add("fldSymbol", typeof(string));
        table.Rows.Add(Guid.NewGuid(), "British Pound", "£");
        connection.RespondToQuery("EXEC dbo.spgaCurrency", table);

        var result = await repository.GetCurrenciesAsync();

        Assert.Single(result);
        Assert.Equal("£", result[0].Symbol);
    }

    [Fact]
    public async Task GetCustomerTypesAsync_ReturnsMappedCustomerTypes()
    {
        var (repository, connection) = CreateRepository();
        var table = new DataTable();
        table.Columns.Add("fldCustomerTypeId", typeof(Guid));
        table.Columns.Add("fldCustomerType", typeof(string));
        table.Rows.Add(Guid.NewGuid(), "Commercial");
        connection.RespondToQuery("EXEC dbo.spgaCustomerType", table);

        var result = await repository.GetCustomerTypesAsync();

        Assert.Single(result);
        Assert.Equal("Commercial", result[0].CustomerType);
    }

    [Fact]
    public async Task GetVatRatingsAsync_ReturnsMappedVatRatings()
    {
        var (repository, connection) = CreateRepository();
        var table = new DataTable();
        table.Columns.Add("fldVatRatingId", typeof(Guid));
        table.Columns.Add("fldVatRating", typeof(string));
        table.Rows.Add(Guid.NewGuid(), "Standard");
        connection.RespondToQuery("EXEC dbo.spgaVatRating", table);

        var result = await repository.GetVatRatingsAsync();

        Assert.Single(result);
        Assert.Equal("Standard", result[0].VatRating);
    }

    [Fact]
    public async Task GetLabTypesAsync_ReturnsMappedLabTypes()
    {
        var (repository, connection) = CreateRepository();
        var table = new DataTable();
        table.Columns.Add("fldLabTypeId", typeof(Guid));
        table.Columns.Add("fldName", typeof(string));
        table.Rows.Add(Guid.NewGuid(), "Reference laboratory");
        connection.RespondToQuery("EXEC dbo.spgaLabType", table);

        var result = await repository.GetLabTypesAsync();

        Assert.Single(result);
        Assert.Equal("Reference laboratory", result[0].Name);
    }

    [Fact]
    public async Task GetCurrentYearsAsync_ReturnsMappedYears()
    {
        var (repository, connection) = CreateRepository();
        var table = new DataTable();
        table.Columns.Add("fldYearId", typeof(int));
        table.Columns.Add("fldYear", typeof(string));
        table.Rows.Add(2026, "2026/27");
        connection.RespondToQuery("EXEC dbo.spgaYearCurrent", table);

        var result = await repository.GetCurrentYearsAsync();

        Assert.Single(result);
        Assert.Equal(2026, result[0].YearId);
    }

    [Fact]
    public async Task GetSchemeCurrenciesAsync_ReturnsMappedSchemeCurrencies()
    {
        var (repository, connection) = CreateRepository();
        var table = new DataTable();
        table.Columns.Add("fldSchemeCurrencyId", typeof(Guid));
        table.Columns.Add("fldSchemeId", typeof(Guid));
        table.Columns.Add("fldCurrencyId", typeof(Guid));
        table.Columns.Add("fldPrice", typeof(decimal));
        table.Columns.Add("fldCurrencyName", typeof(string));
        table.Columns.Add("fldCurrencySymbol", typeof(string));
        table.Rows.Add(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 12.5m, "British Pound", "£");
        connection.RespondToQuery("EXEC dbo.spgaSchemeCurrency", table);

        var result = await repository.GetSchemeCurrenciesAsync();

        Assert.Single(result);
        Assert.Equal(12.5m, result[0].Price);
    }

    [Fact]
    public async Task GetPostagePricingPlansForYearAsync_ReturnsMappedPlans()
    {
        var (repository, connection) = CreateRepository();
        var table = new DataTable();
        table.Columns.Add("fldPostageId", typeof(Guid));
        table.Columns.Add("fldName", typeof(string));
        table.Columns.Add("fldUkPrice", typeof(decimal));
        table.Columns.Add("fldYearId", typeof(int));
        table.Rows.Add(Guid.NewGuid(), "Standard", 5.5m, 2026);
        connection.RespondToQuery("EXEC dbo.spgPostageByYearID @YearId", table);

        var result = await repository.GetPostagePricingPlansForYearAsync(2026);

        Assert.Single(result);
        Assert.Equal("Standard", result[0].Name);
    }
}
