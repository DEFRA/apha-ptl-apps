using System.Data;
using PTL.Core.Viewer;
using PTL.Data.Infrastructure;
using PTL.Data.Tests.Fakes;
using PTL.Data.Viewer;

namespace PTL.Data.Tests.Viewer;

// Exercises ViewerRepository against a fake ADO.NET connection (see Fakes/) instead of a live SQL
// Server - see ParticipantRepositoryTests for the pattern this follows.
public class ViewerRepositoryTests
{
    public ViewerRepositoryTests() => DapperColumnMappings.Register();

    private const string GetAllSql = "EXEC dbo.spgaViewers";
    private const string GetBySsoIdExtSql = "EXEC dbo.spgViewerBySsoId @SsoIdExt=@SsoIdExt";
    private const string GetByEmailSql = "EXEC dbo.spgViewerByEmail @Email";
    private const string InsertSql = "EXEC dbo.spiViewer @ViewerId, @Name, @Email, @SsoId, @SsoIdExt";
    private const string UpdateSql = "EXEC dbo.spuViewer @ViewerId, @Name, @Email, @SsoId, @SsoIdExt";

    private static DataTable ViewerTable(Guid viewerId, Guid? ssoIdExt = null, string email = "", string name = "Test Viewer")
    {
        var table = new DataTable();
        table.Columns.Add("fldViewerId", typeof(Guid));
        table.Columns.Add("fldName", typeof(string));
        table.Columns.Add("fldEmail", typeof(string));
        table.Columns.Add("fldSsoId", typeof(Guid));
        table.Columns.Add("fldSsoIdExt", typeof(Guid));
        table.Rows.Add(viewerId, name, email, Guid.NewGuid(), (object?)ssoIdExt ?? DBNull.Value);
        return table;
    }

    private static (ViewerRepository Repository, FakeDbConnection Connection) CreateRepository()
    {
        var connection = new FakeDbConnection();
        var repository = new ViewerRepository(new FakeDbConnectionFactory(connection));
        return (repository, connection);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsMappedViewers()
    {
        var (repository, connection) = CreateRepository();
        var viewerId = Guid.NewGuid();
        connection.RespondToQuery(GetAllSql, ViewerTable(viewerId));

        var result = await repository.GetAllAsync();

        Assert.Single(result);
        Assert.Equal(viewerId, result[0].ViewerId);
    }

    [Fact]
    public async Task GetBySsoIdExtAsync_Found_ReturnsMappedViewer()
    {
        var (repository, connection) = CreateRepository();
        var viewerId = Guid.NewGuid();
        var ssoIdExt = Guid.NewGuid();
        connection.RespondToQuery(GetBySsoIdExtSql, ViewerTable(viewerId, ssoIdExt: ssoIdExt));

        var result = await repository.GetBySsoIdExtAsync(ssoIdExt);

        Assert.NotNull(result);
        Assert.Equal(ssoIdExt, result!.SsoIdExt);
    }

    [Fact]
    public async Task GetBySsoIdExtAsync_NotFound_ReturnsNull()
    {
        var (repository, connection) = CreateRepository();
        connection.RespondToQuery(GetBySsoIdExtSql, new DataTable());

        var result = await repository.GetBySsoIdExtAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task GetByEmailAsync_Found_ReturnsMappedViewer()
    {
        var (repository, connection) = CreateRepository();
        var viewerId = Guid.NewGuid();
        const string email = "viewer@example.com";
        connection.RespondToQuery(GetByEmailSql, ViewerTable(viewerId, email: email));

        var result = await repository.GetByEmailAsync(email);

        Assert.NotNull(result);
        Assert.Equal(email, result!.Email);
    }

    [Fact]
    public async Task GetByEmailAsync_NotFound_ReturnsNull()
    {
        var (repository, connection) = CreateRepository();
        connection.RespondToQuery(GetByEmailSql, new DataTable());

        var result = await repository.GetByEmailAsync("missing@example.com");

        Assert.Null(result);
    }

    [Fact]
    public async Task CreateAsync_ExecutesInsertAndReturnsViewer()
    {
        var (repository, connection) = CreateRepository();
        var viewer = new ViewerEntity { ViewerId = Guid.NewGuid(), Name = "New Viewer", Email = "new@example.com", SsoIdExt = Guid.NewGuid() };
        connection.RespondToNonQuery(InsertSql, 1);

        var result = await repository.CreateAsync(viewer);

        Assert.Same(viewer, result);
        var insertCommand = Assert.Single(connection.ExecutedCommands, c => c.CommandText == InsertSql);
        Assert.Equal(viewer.ViewerId, insertCommand.ParameterValue("@ViewerId"));
        Assert.Equal(viewer.SsoIdExt, insertCommand.ParameterValue("@SsoIdExt"));
    }

    [Fact]
    public async Task UpdateAsync_ExistingViewer_ReturnsViewer()
    {
        var (repository, connection) = CreateRepository();
        var viewer = new ViewerEntity { ViewerId = Guid.NewGuid(), Name = "Updated Viewer", Email = "updated@example.com" };
        connection.RespondToNonQuery(UpdateSql, 1);

        var result = await repository.UpdateAsync(viewer);

        Assert.Same(viewer, result);
    }

    [Fact]
    public async Task UpdateAsync_UnknownViewer_ReturnsNull()
    {
        var (repository, connection) = CreateRepository();
        var viewer = new ViewerEntity { ViewerId = Guid.NewGuid() };
        connection.RespondToNonQuery(UpdateSql, 0);

        var result = await repository.UpdateAsync(viewer);

        Assert.Null(result);
    }
}
