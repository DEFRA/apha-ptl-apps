using System.Data;
using PTL.Data.Infrastructure;
using PTL.Data.Tests.Fakes;
using PTL.Data.User;

namespace PTL.Data.Tests.User;

public class RoleRepositoryTests
{
    public RoleRepositoryTests() => DapperColumnMappings.Register();

    [Fact]
    public async Task GetAllAsync_ReturnsMappedRoles()
    {
        var connection = new FakeDbConnection();
        var repository = new RoleRepository(new FakeDbConnectionFactory(connection));
        var roleId = Guid.NewGuid();
        var table = new DataTable();
        table.Columns.Add("fldRoleId", typeof(Guid));
        table.Columns.Add("fldRole", typeof(string));
        table.Rows.Add(roleId, "Admin");
        connection.RespondToQuery("EXEC dbo.spgaRole", table);

        var result = await repository.GetAllAsync();

        Assert.Equal("Admin", Assert.Single(result).Name);
    }
}
