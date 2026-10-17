using System.Data;
using PTL.Core.Audit;
using PTL.Data.Country;
using PTL.Data.Infrastructure;
using PTL.Data.Tests.Fakes;

namespace PTL.Data.Tests.Country;

// Exercises CountryRepository against a fake ADO.NET connection (see Fakes/) instead of a live
// SQL Server - see PostagePricingPlanRepositoryTests for the pattern this follows.
public class CountryRepositoryTests
{
    public CountryRepositoryTests() => DapperColumnMappings.Register();

    private static (CountryRepository Repository, FakeDbConnection Connection) CreateRepository()
    {
        var connection = new FakeDbConnection();
        var repository = new CountryRepository(new FakeDbConnectionFactory(connection));
        return (repository, connection);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsMappedCountriesIncludingAllocationCount()
    {
        var (repository, connection) = CreateRepository();
        var countryId = Guid.NewGuid();
        var countryTypeId = Guid.NewGuid();
        var table = new DataTable();
        table.Columns.Add("fldCountryId", typeof(Guid));
        table.Columns.Add("fldCountry", typeof(string));
        table.Columns.Add("fldCountryTypeId", typeof(Guid));
        table.Columns.Add("fldCountryType", typeof(string));
        table.Columns.Add("fldAllocationCount", typeof(int));
        table.Rows.Add(countryId, "France", countryTypeId, "EU", 3);
        connection.RespondToQuery("EXEC dbo.spgaCountry", table);

        var result = await repository.GetAllAsync();

        var country = Assert.Single(result);
        Assert.Equal("France", country.CountryName);
        Assert.Equal("EU", country.CountryType);
        Assert.Equal(3, country.AllocationCount);
    }

    [Fact]
    public async Task GetCountryTypesAsync_ReturnsMappedTypes()
    {
        var (repository, connection) = CreateRepository();
        var countryTypeId = Guid.NewGuid();
        var table = new DataTable();
        table.Columns.Add("fldCountryTypeId", typeof(Guid));
        table.Columns.Add("fldCountryType", typeof(string));
        table.Rows.Add(countryTypeId, "EU");
        connection.RespondToQuery("EXEC dbo.spgaCountryType", table);

        var result = await repository.GetCountryTypesAsync();

        Assert.Equal("EU", Assert.Single(result).CountryType);
    }

    [Fact]
    public async Task CreateAsync_ExecutesSpiCountry()
    {
        var (repository, connection) = CreateRepository();
        const string insertSql = "EXEC dbo.spiCountry @CountryId, @Country, @CountryTypeId";
        connection.RespondToNonQuery(insertSql, 1);

        await repository.CreateAsync(new PTL.Core.Country.Country { CountryId = Guid.NewGuid(), CountryName = "France", CountryTypeId = Guid.NewGuid() });

        Assert.Contains(connection.ExecutedCommands, c => c.CommandText == insertSql);
    }

    [Fact]
    public async Task UpdateAsync_ExecutesSpuCountry()
    {
        var (repository, connection) = CreateRepository();
        const string updateSql = "EXEC dbo.spuCountry @CountryId, @Country, @CountryTypeId";
        connection.RespondToNonQuery(updateSql, 1);

        await repository.UpdateAsync(new PTL.Core.Country.Country { CountryId = Guid.NewGuid(), CountryName = "France", CountryTypeId = Guid.NewGuid() });

        Assert.Contains(connection.ExecutedCommands, c => c.CommandText == updateSql);
    }

    [Fact]
    public async Task DeleteAsync_ExecutesSpdCountry()
    {
        var (repository, connection) = CreateRepository();
        const string deleteSql = "EXEC dbo.spdCountry @CountryId";
        connection.RespondToNonQuery(deleteSql, 1);

        await repository.DeleteAsync(Guid.NewGuid());

        Assert.Contains(connection.ExecutedCommands, c => c.CommandText == deleteSql);
    }

    private const string AuditSql = "EXEC dbo.spiSystemAdministrationAudit @AuditId, @ActingUserId, @Action, @EntityType, @EntityId, @Details";

    [Fact]
    public async Task DeleteAsync_WithAudit_CommitsChangeAndAuditInOneTransaction()
    {
        var (repository, connection) = CreateRepository();
        const string deleteSql = "EXEC dbo.spdCountry @CountryId";
        connection.RespondToNonQuery(deleteSql, 1);
        connection.RespondToNonQuery(AuditSql, 1);
        var countryId = Guid.NewGuid();
        var audit = new AuditEntry(Guid.NewGuid(), "DeleteCountry", "Country", countryId.ToString(), "Name=France");

        await repository.DeleteAsync(countryId, audit);

        Assert.NotNull(connection.LastTransaction);
        Assert.True(connection.LastTransaction!.Committed);
        Assert.Contains(connection.ExecutedCommands, c => c.CommandText == deleteSql);
        var auditCommand = Assert.Single(connection.ExecutedCommands, c => c.CommandText == AuditSql);
        Assert.Equal("DeleteCountry", auditCommand.ParameterValue("@Action"));
    }

    [Fact]
    public async Task CreateAsync_AuditWriteFails_RollsBackTheTransaction()
    {
        var (repository, connection) = CreateRepository();
        connection.RespondToNonQuery("EXEC dbo.spiCountry @CountryId, @Country, @CountryTypeId", 1);
        var country = new PTL.Core.Country.Country { CountryId = Guid.NewGuid(), CountryName = "France", CountryTypeId = Guid.NewGuid() };
        var audit = new AuditEntry(Guid.NewGuid(), "CreateCountry", "Country", country.CountryId.ToString(), "Name=France");

        await Assert.ThrowsAsync<InvalidOperationException>(() => repository.CreateAsync(country, audit));

        Assert.NotNull(connection.LastTransaction);
        Assert.True(connection.LastTransaction!.RolledBack);
        Assert.False(connection.LastTransaction!.Committed);
    }
}
