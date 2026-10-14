using System.Data;
using PTL.Data.Infrastructure;
using PTL.Data.Tests.Fakes;
using PTL.Data.TestType;

namespace PTL.Data.Tests.TestType;

// Exercises TestTypeRepository against a fake ADO.NET connection - see
// CategoryItemTypeRepositoryTests for the pattern this follows.
public class TestTypeRepositoryTests
{
    public TestTypeRepositoryTests() => DapperColumnMappings.Register();

    private static (TestTypeRepository Repository, FakeDbConnection Connection) CreateRepository()
    {
        var connection = new FakeDbConnection();
        var repository = new TestTypeRepository(new FakeDbConnectionFactory(connection));
        return (repository, connection);
    }

    [Fact]
    public async Task GetByYearAsync_ReturnsMappedItemsIncludingSharedIdAndForeignInstances()
    {
        var (repository, connection) = CreateRepository();
        var id = Guid.NewGuid();
        var sharedId = Guid.NewGuid();
        var table = new DataTable();
        table.Columns.Add("fldTestTypeId", typeof(Guid));
        table.Columns.Add("fldSharedId", typeof(Guid));
        table.Columns.Add("fldTestType", typeof(string));
        table.Columns.Add("fldStartYearId", typeof(int));
        table.Columns.Add("fldEndYearId", typeof(int));
        table.Columns.Add("fldNoLongerInUse", typeof(bool));
        table.Columns.Add("ForeignInstances", typeof(int));
        table.Columns.Add("ForeignInstancesString", typeof(string));
        table.Rows.Add(id, sharedId, "Chemical", 2026, 9999, false, 2, "Scheme A, Scheme B");
        connection.RespondToQuery("EXEC dbo.spgTestTypeByYearId @YearId", table);

        var result = await repository.GetByYearAsync(2026);

        var item = Assert.Single(result);
        Assert.Equal("Chemical", item.Name);
        Assert.Equal(sharedId, item.SharedId);
        Assert.Equal(2026, item.StartYearId);
        Assert.Equal(9999, item.EndYearId);
        Assert.Equal(2, item.ForeignInstances);
        Assert.Equal("Scheme A, Scheme B", item.ForeignInstancesString);
    }

    [Fact]
    public async Task CreateAsync_ExecutesSpiTestType()
    {
        var (repository, connection) = CreateRepository();
        const string insertSql = "EXEC dbo.spiTestType @TestTypeId, @SharedId, @TestType, @StartYearId, @EndYearId, @NoLongerInUse";
        connection.RespondToNonQuery(insertSql, 1);
        var item = new PTL.Core.TestType.TestType { TestTypeId = Guid.NewGuid(), SharedId = Guid.NewGuid(), Name = "Chemical", StartYearId = 2026, EndYearId = 9999, NoLongerInUse = false };

        await repository.CreateAsync(item);

        Assert.Contains(connection.ExecutedCommands, c => c.CommandText == insertSql);
    }

    [Fact]
    public async Task UpdateAsync_ExecutesSpuTestType()
    {
        var (repository, connection) = CreateRepository();
        const string updateSql = "EXEC dbo.spuTestType @TestTypeId, @SharedId, @TestType, @StartYearId, @EndYearId, @NoLongerInUse";
        connection.RespondToNonQuery(updateSql, 1);
        var item = new PTL.Core.TestType.TestType { TestTypeId = Guid.NewGuid(), SharedId = Guid.NewGuid(), Name = "Chemical", StartYearId = 2026, EndYearId = 9999, NoLongerInUse = true };

        await repository.UpdateAsync(item);

        Assert.Contains(connection.ExecutedCommands, c => c.CommandText == updateSql);
    }

    [Fact]
    public async Task DeleteAsync_ExecutesSpdTestType()
    {
        var (repository, connection) = CreateRepository();
        const string deleteSql = "EXEC dbo.spdTestType @TestTypeId";
        connection.RespondToNonQuery(deleteSql, 1);

        await repository.DeleteAsync(Guid.NewGuid());

        Assert.Contains(connection.ExecutedCommands, c => c.CommandText == deleteSql);
    }
}
