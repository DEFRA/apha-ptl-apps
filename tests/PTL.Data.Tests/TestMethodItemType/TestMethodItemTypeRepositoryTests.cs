using System.Data;
using PTL.Data.Infrastructure;
using PTL.Data.TestMethodItemType;
using PTL.Data.Tests.Fakes;

namespace PTL.Data.Tests.TestMethodItemType;

// Exercises TestMethodItemTypeRepository against a fake ADO.NET connection - see
// TestResultItemTypeRepositoryTests for the pattern this follows.
public class TestMethodItemTypeRepositoryTests
{
    public TestMethodItemTypeRepositoryTests() => DapperColumnMappings.Register();

    private static (TestMethodItemTypeRepository Repository, FakeDbConnection Connection) CreateRepository()
    {
        var connection = new FakeDbConnection();
        var repository = new TestMethodItemTypeRepository(new FakeDbConnectionFactory(connection));
        return (repository, connection);
    }

    [Fact]
    public async Task GetByYearAsync_ReturnsMappedItemsIncludingReturnTypeAndForeignInstances()
    {
        var (repository, connection) = CreateRepository();
        var id = Guid.NewGuid();
        var sharedId = Guid.NewGuid();
        var returnTypeId = Guid.NewGuid();
        var table = new DataTable();
        table.Columns.Add("fldTestMethodItemTypeId", typeof(Guid));
        table.Columns.Add("fldSharedId", typeof(Guid));
        table.Columns.Add("fldTestMethodItemType", typeof(string));
        table.Columns.Add("fldStartYearId", typeof(int));
        table.Columns.Add("fldEndYearId", typeof(int));
        table.Columns.Add("fldNoLongerInUse", typeof(bool));
        table.Columns.Add("fldTestReturnValueTypeId", typeof(Guid));
        table.Columns.Add("fldTestReturnValueType", typeof(string));
        table.Columns.Add("ForeignInstances", typeof(int));
        table.Columns.Add("ForeignInstancesString", typeof(string));
        table.Rows.Add(id, sharedId, "Titration", 2026, 9999, false, returnTypeId, "Numeric", 2, "Scheme A, Scheme B");
        connection.RespondToQuery("EXEC dbo.spgTestMethodItemTypeByYearId @YearId", table);

        var result = await repository.GetByYearAsync(2026);

        var item = Assert.Single(result);
        Assert.Equal("Titration", item.Name);
        Assert.Equal(sharedId, item.SharedId);
        Assert.Equal(returnTypeId, item.TestReturnValueTypeId);
        Assert.Equal("Numeric", item.TestReturnValueTypeName);
        Assert.Equal(2026, item.StartYearId);
        Assert.Equal(9999, item.EndYearId);
        Assert.Equal(2, item.ForeignInstances);
        Assert.Equal("Scheme A, Scheme B", item.ForeignInstancesString);
    }

    [Fact]
    public async Task CreateAsync_ExecutesSpiTestMethodItemType()
    {
        var (repository, connection) = CreateRepository();
        const string insertSql = "EXEC dbo.spiTestMethodItemType @TestMethodItemTypeId, @SharedId, @TestMethodItemType, @StartYearId, @EndYearId, @TestReturnValueTypeId, @NoLongerInUse";
        connection.RespondToNonQuery(insertSql, 1);
        var item = new PTL.Core.TestMethodItemType.TestMethodItemType { TestMethodItemTypeId = Guid.NewGuid(), SharedId = Guid.NewGuid(), Name = "Titration", TestReturnValueTypeId = Guid.NewGuid(), StartYearId = 2026, EndYearId = 9999, NoLongerInUse = false };

        await repository.CreateAsync(item);

        Assert.Contains(connection.ExecutedCommands, c => c.CommandText == insertSql);
    }

    [Fact]
    public async Task UpdateAsync_ExecutesSpuTestMethodItemType()
    {
        var (repository, connection) = CreateRepository();
        const string updateSql = "EXEC dbo.spuTestMethodItemType @TestMethodItemTypeId, @SharedId, @TestMethodItemType, @StartYearId, @EndYearId, @TestReturnValueTypeId, @NoLongerInUse";
        connection.RespondToNonQuery(updateSql, 1);
        var item = new PTL.Core.TestMethodItemType.TestMethodItemType { TestMethodItemTypeId = Guid.NewGuid(), SharedId = Guid.NewGuid(), Name = "Titration", TestReturnValueTypeId = Guid.NewGuid(), StartYearId = 2026, EndYearId = 9999, NoLongerInUse = true };

        await repository.UpdateAsync(item);

        Assert.Contains(connection.ExecutedCommands, c => c.CommandText == updateSql);
    }

    [Fact]
    public async Task DeleteAsync_ExecutesSpdTestMethodItemType()
    {
        var (repository, connection) = CreateRepository();
        const string deleteSql = "EXEC dbo.spdTestMethodItemType @TestMethodItemTypeId";
        connection.RespondToNonQuery(deleteSql, 1);

        await repository.DeleteAsync(Guid.NewGuid());

        Assert.Contains(connection.ExecutedCommands, c => c.CommandText == deleteSql);
    }
}
