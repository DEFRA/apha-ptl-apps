using System.Data;
using PTL.Core.GroupAddress;
using PTL.Data.GroupAddress;
using PTL.Data.Infrastructure;
using PTL.Data.Tests.Fakes;
using CoreGroupAddress = PTL.Core.GroupAddress.GroupAddress;

namespace PTL.Data.Tests.GroupAddress;

public class GroupAddressRepositoryTests
{
    public GroupAddressRepositoryTests() => DapperColumnMappings.Register();

    private const string GetAllSql = "EXEC dbo.spgaGroupAddress";
    private const string GetByIdSql = "EXEC dbo.spgGroupAddressByGroupAddressId @GroupAddressId";
    private const string InsertSql =
        "EXEC dbo.spiGroupAddress @GroupAddressId, @Identifier, @Address1, @Address2, @Address3, @Address4, @Address5, @CountryId, @Telephone, @PackingInstructions";
    private const string UpdateSql =
        "EXEC dbo.spuGroupAddress @GroupAddressId, @Identifier, @Address1, @Address2, @Address3, @Address4, @Address5, @CountryId, @Telephone, @PackingInstructions";

    private static DataTable GroupAddressTable(Guid groupAddressId, Guid countryId, string identifier = "PTL-001")
    {
        var table = new DataTable();
        table.Columns.Add("fldGroupAddressId", typeof(Guid));
        table.Columns.Add("fldIdentifier", typeof(string));
        table.Columns.Add("fldAddress1", typeof(string));
        table.Columns.Add("fldAddress2", typeof(string));
        table.Columns.Add("fldAddress3", typeof(string));
        table.Columns.Add("fldAddress4", typeof(string));
        table.Columns.Add("fldAddress5", typeof(string));
        table.Columns.Add("fldCountryId", typeof(Guid));
        table.Columns.Add("fldTelephone", typeof(string));
        table.Columns.Add("fldPackingInstructions", typeof(string));
        table.Rows.Add(groupAddressId, identifier, "1 Sample Street", "Second Line", "Third Line", "Fourth Line", "Fifth Line", countryId, "020 1234 5678", "Fragile");
        return table;
    }

    private static (GroupAddressRepository Repository, FakeDbConnection Connection) CreateRepository()
    {
        var connection = new FakeDbConnection();
        var repository = new GroupAddressRepository(new FakeDbConnectionFactory(connection));
        return (repository, connection);
    }

    private static CoreGroupAddress SampleGroupAddress(Guid groupAddressId, Guid countryId) => new()
    {
        GroupAddressId = groupAddressId,
        Identifier = "PTL-001",
        Address1 = "1 Sample Street",
        Address2 = "Second Line",
        Address3 = "Third Line",
        Address4 = "Fourth Line",
        Address5 = "Fifth Line",
        CountryId = countryId,
        Telephone = "020 1234 5678",
        PackingInstructions = "Fragile"
    };

    [Fact]
    public async Task GetAllAsync_ReturnsMappedList()
    {
        var (repository, connection) = CreateRepository();
        var groupAddressId = Guid.NewGuid();
        var countryId = Guid.NewGuid();
        connection.RespondToQuery(GetAllSql, GroupAddressTable(groupAddressId, countryId));

        var result = await repository.GetAllAsync();

        Assert.Single(result);
        Assert.Equal("PTL-001", result[0].Identifier);
        Assert.Equal(countryId, result[0].CountryId);
    }

    [Fact]
    public async Task GetByIdAsync_Found_ReturnsMappedEntity()
    {
        var (repository, connection) = CreateRepository();
        var groupAddressId = Guid.NewGuid();
        var countryId = Guid.NewGuid();
        connection.RespondToQuery(GetByIdSql, GroupAddressTable(groupAddressId, countryId));

        var result = await repository.GetByIdAsync(groupAddressId);

        Assert.NotNull(result);
        Assert.Equal(groupAddressId, result!.GroupAddressId);
        Assert.Equal("Fragile", result.PackingInstructions);
    }

    [Fact]
    public async Task GetByIdAsync_NotFound_ReturnsNull()
    {
        var (repository, connection) = CreateRepository();
        var emptyTable = GroupAddressTable(Guid.NewGuid(), Guid.NewGuid());
        emptyTable.Rows.Clear();
        connection.RespondToQuery(GetByIdSql, emptyTable);

        var result = await repository.GetByIdAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task CreateAsync_InsertsAndReReadsEntity()
    {
        var (repository, connection) = CreateRepository();
        var groupAddressId = Guid.NewGuid();
        var countryId = Guid.NewGuid();
        connection.RespondToNonQuery(InsertSql, 1);
        connection.RespondToQuery(GetByIdSql, GroupAddressTable(groupAddressId, countryId));

        var result = await repository.CreateAsync(SampleGroupAddress(groupAddressId, countryId));

        Assert.Equal(groupAddressId, result.GroupAddressId);
    }

    [Fact]
    public async Task CreateAsync_InsertedButNotReReadable_Throws()
    {
        var (repository, connection) = CreateRepository();
        var groupAddressId = Guid.NewGuid();
        connection.RespondToNonQuery(InsertSql, 1);
        var emptyTable = GroupAddressTable(groupAddressId, Guid.NewGuid());
        emptyTable.Rows.Clear();
        connection.RespondToQuery(GetByIdSql, emptyTable);

        await Assert.ThrowsAsync<InvalidOperationException>(() => repository.CreateAsync(SampleGroupAddress(groupAddressId, Guid.NewGuid())));
    }

    [Fact]
    public async Task UpdateAsync_RowsAffected_ReReadsAndReturnsEntity()
    {
        var (repository, connection) = CreateRepository();
        var groupAddressId = Guid.NewGuid();
        var countryId = Guid.NewGuid();
        connection.RespondToNonQuery(UpdateSql, 1);
        connection.RespondToQuery(GetByIdSql, GroupAddressTable(groupAddressId, countryId));

        var result = await repository.UpdateAsync(groupAddressId, SampleGroupAddress(groupAddressId, countryId));

        Assert.NotNull(result);
    }

    [Fact]
    public async Task UpdateAsync_NoRowsAffected_ReturnsNull()
    {
        var (repository, connection) = CreateRepository();
        connection.RespondToNonQuery(UpdateSql, 0);

        var result = await repository.UpdateAsync(Guid.NewGuid(), SampleGroupAddress(Guid.NewGuid(), Guid.NewGuid()));

        Assert.Null(result);
    }
}
