using System.Data;
using PTL.Data.CriterionItemType;
using PTL.Data.Infrastructure;
using PTL.Data.Tests.Fakes;

namespace PTL.Data.Tests.CriterionItemType;

// Exercises CriterionItemTypeRepository against a fake ADO.NET connection - see
// CategoryItemTypeRepositoryTests for the pattern this follows.
public class CriterionItemTypeRepositoryTests
{
    public CriterionItemTypeRepositoryTests() => DapperColumnMappings.Register();

    private static (CriterionItemTypeRepository Repository, FakeDbConnection Connection) CreateRepository()
    {
        var connection = new FakeDbConnection();
        var repository = new CriterionItemTypeRepository(new FakeDbConnectionFactory(connection));
        return (repository, connection);
    }

    [Fact]
    public async Task GetByYearAsync_ReturnsMappedItemsIncludingForeignInstances()
    {
        var (repository, connection) = CreateRepository();
        var id = Guid.NewGuid();
        var table = new DataTable();
        table.Columns.Add("fldCriterionItemTypeId", typeof(Guid));
        table.Columns.Add("fldName", typeof(string));
        table.Columns.Add("fldStartYearId", typeof(int));
        table.Columns.Add("fldEndYearId", typeof(int));
        table.Columns.Add("fldNoLongerInUse", typeof(bool));
        table.Columns.Add("ForeignInstances", typeof(int));
        table.Columns.Add("ForeignInstancesString", typeof(string));
        table.Rows.Add(id, "Appearance", 2026, 9999, false, 2, "Scheme A, Scheme B");
        connection.RespondToQuery("EXEC dbo.spgCriterionItemTypeByYearId @YearId", table);

        var result = await repository.GetByYearAsync(2026);

        var item = Assert.Single(result);
        Assert.Equal("Appearance", item.Name);
        Assert.Equal(2026, item.StartYearId);
        Assert.Equal(9999, item.EndYearId);
        Assert.Equal(2, item.ForeignInstances);
        Assert.Equal("Scheme A, Scheme B", item.ForeignInstancesString);
    }

    [Fact]
    public async Task CreateAsync_ExecutesSpiCriterionItemType()
    {
        var (repository, connection) = CreateRepository();
        const string insertSql = "EXEC dbo.spiCriterionItemType @CriterionItemTypeId, @Name, @StartYearId, @EndYearId, @NoLongerInUse";
        connection.RespondToNonQuery(insertSql, 1);
        var item = new PTL.Core.CriterionItemType.CriterionItemType { CriterionItemTypeId = Guid.NewGuid(), Name = "Appearance", StartYearId = 2026, EndYearId = 9999, NoLongerInUse = false };

        await repository.CreateAsync(item);

        Assert.Contains(connection.ExecutedCommands, c => c.CommandText == insertSql);
    }

    [Fact]
    public async Task UpdateAsync_ExecutesSpuCriterionItemType()
    {
        var (repository, connection) = CreateRepository();
        const string updateSql = "EXEC dbo.spuCriterionItemType @CriterionItemTypeId, @Name, @StartYearId, @EndYearId, @NoLongerInUse";
        connection.RespondToNonQuery(updateSql, 1);
        var item = new PTL.Core.CriterionItemType.CriterionItemType { CriterionItemTypeId = Guid.NewGuid(), Name = "Appearance", StartYearId = 2026, EndYearId = 9999, NoLongerInUse = true };

        await repository.UpdateAsync(item);

        Assert.Contains(connection.ExecutedCommands, c => c.CommandText == updateSql);
    }

    [Fact]
    public async Task DeleteAsync_ExecutesSpdCriterionItemType()
    {
        var (repository, connection) = CreateRepository();
        const string deleteSql = "EXEC dbo.spdCriterionItemType @CriterionItemTypeId";
        connection.RespondToNonQuery(deleteSql, 1);

        await repository.DeleteAsync(Guid.NewGuid());

        Assert.Contains(connection.ExecutedCommands, c => c.CommandText == deleteSql);
    }
}
