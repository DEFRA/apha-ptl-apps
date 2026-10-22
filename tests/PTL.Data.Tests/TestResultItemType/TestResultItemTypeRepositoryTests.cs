using System.Data;
using PTL.Data.Infrastructure;
using PTL.Data.Tests.Fakes;
using PTL.Data.TestResultItemType;

namespace PTL.Data.Tests.TestResultItemType;

// Exercises TestResultItemTypeRepository against a fake ADO.NET connection - see
// TestTypeRepositoryTests for the pattern this follows.
public class TestResultItemTypeRepositoryTests
{
    public TestResultItemTypeRepositoryTests() => DapperColumnMappings.Register();

    private static (TestResultItemTypeRepository Repository, FakeDbConnection Connection) CreateRepository()
    {
        var connection = new FakeDbConnection();
        var repository = new TestResultItemTypeRepository(new FakeDbConnectionFactory(connection));
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
        table.Columns.Add("fldTestResultItemTypeId", typeof(Guid));
        table.Columns.Add("fldSharedId", typeof(Guid));
        table.Columns.Add("fldTestResultItemType", typeof(string));
        table.Columns.Add("fldStartYearId", typeof(int));
        table.Columns.Add("fldEndYearId", typeof(int));
        table.Columns.Add("fldNoLongerInUse", typeof(bool));
        table.Columns.Add("fldTestReturnValueTypeId", typeof(Guid));
        table.Columns.Add("fldTestReturnValueType", typeof(string));
        table.Columns.Add("ForeignInstances", typeof(int));
        table.Columns.Add("ForeignInstancesString", typeof(string));
        table.Rows.Add(id, sharedId, "Weight", 2026, 9999, false, returnTypeId, "Numeric", 2, "Scheme A, Scheme B");
        connection.RespondToQuery("EXEC dbo.spgTestResultItemTypeByYearId @YearId", table);

        var result = await repository.GetByYearAsync(2026);

        var item = Assert.Single(result);
        Assert.Equal("Weight", item.Name);
        Assert.Equal(sharedId, item.SharedId);
        Assert.Equal(returnTypeId, item.TestReturnValueTypeId);
        Assert.Equal("Numeric", item.TestReturnValueTypeName);
        Assert.Equal(2026, item.StartYearId);
        Assert.Equal(9999, item.EndYearId);
        Assert.Equal(2, item.ForeignInstances);
        Assert.Equal("Scheme A, Scheme B", item.ForeignInstancesString);
    }

    [Fact]
    public async Task CreateAsync_ExecutesSpiTestResultItemType()
    {
        var (repository, connection) = CreateRepository();
        const string insertSql = "EXEC dbo.spiTestResultItemType @TestResultItemTypeId, @SharedId, @TestResultItemType, @StartYearId, @EndYearId, @TestReturnValueTypeId, @NoLongerInUse";
        connection.RespondToNonQuery(insertSql, 1);
        var item = new PTL.Core.TestResultItemType.TestResultItemType { TestResultItemTypeId = Guid.NewGuid(), SharedId = Guid.NewGuid(), Name = "Weight", TestReturnValueTypeId = Guid.NewGuid(), StartYearId = 2026, EndYearId = 9999, NoLongerInUse = false };

        await repository.CreateAsync(item);

        Assert.Contains(connection.ExecutedCommands, c => c.CommandText == insertSql);
    }

    [Fact]
    public async Task UpdateAsync_ExecutesSpuTestResultItemType()
    {
        var (repository, connection) = CreateRepository();
        const string updateSql = "EXEC dbo.spuTestResultItemType @TestResultItemTypeId, @SharedId, @TestResultItemType, @StartYearId, @EndYearId, @TestReturnValueTypeId, @NoLongerInUse";
        connection.RespondToNonQuery(updateSql, 1);
        var item = new PTL.Core.TestResultItemType.TestResultItemType { TestResultItemTypeId = Guid.NewGuid(), SharedId = Guid.NewGuid(), Name = "Weight", TestReturnValueTypeId = Guid.NewGuid(), StartYearId = 2026, EndYearId = 9999, NoLongerInUse = true };

        await repository.UpdateAsync(item);

        Assert.Contains(connection.ExecutedCommands, c => c.CommandText == updateSql);
    }

    [Fact]
    public async Task DeleteAsync_ExecutesSpdTestResultItemType()
    {
        var (repository, connection) = CreateRepository();
        const string deleteSql = "EXEC dbo.spdTestResultItemType @TestResultItemTypeId";
        connection.RespondToNonQuery(deleteSql, 1);

        await repository.DeleteAsync(Guid.NewGuid());

        Assert.Contains(connection.ExecutedCommands, c => c.CommandText == deleteSql);
    }
}
