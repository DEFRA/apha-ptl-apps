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
        Assert.Equal(ssoIdExt, result.SsoIdExt);
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
        Assert.Equal(email, result.Email);
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

    [Fact]
    public async Task GetAllWithAssignmentsAsync_AttachesSchemesAndParticipantsToMatchingViewer()
    {
        var (repository, connection) = CreateRepository();
        var viewerId = Guid.NewGuid();
        var otherViewerId = Guid.NewGuid();

        var viewerTable = ViewerTable(viewerId, name: "Jane Smith");
        var schemeTable = new DataTable();
        schemeTable.Columns.Add("fldViewerId", typeof(Guid));
        schemeTable.Columns.Add("fldIdentifier", typeof(string));
        schemeTable.Columns.Add("fldName", typeof(string));
        schemeTable.Rows.Add(viewerId, "SFW1234", "Heavy Metals");
        schemeTable.Rows.Add(otherViewerId, "SFW9999", "Unrelated Scheme");

        var participantTable = new DataTable();
        participantTable.Columns.Add("fldViewerId", typeof(Guid));
        participantTable.Columns.Add("fldLabCode", typeof(string));
        participantTable.Columns.Add("fldLabName", typeof(string));
        participantTable.Rows.Add(viewerId, "LAB001", "Example Lab");

        var dataSet = new DataSet();
        dataSet.Tables.Add(viewerTable);
        dataSet.Tables.Add(schemeTable);
        dataSet.Tables.Add(participantTable);
        connection.RespondToQuery(GetAllSql, dataSet);

        var result = await repository.GetAllWithAssignmentsAsync();

        var viewer = Assert.Single(result);
        Assert.Equal("Jane Smith", viewer.Name);
        var scheme = Assert.Single(viewer.Schemes);
        Assert.Equal("SFW1234", scheme.Identifier);
        Assert.Equal("Heavy Metals", scheme.Name);
        var participant = Assert.Single(viewer.Participants);
        Assert.Equal("LAB001", participant.LabCode);
        Assert.Equal("Example Lab", participant.LabName);
    }

    [Fact]
    public async Task DeleteAsync_ExistingViewer_ReturnsTrue()
    {
        var (repository, connection) = CreateRepository();
        var viewerId = Guid.NewGuid();
        connection.RespondToNonQuery("EXEC dbo.spdViewer @ViewerId", 1);

        var result = await repository.DeleteAsync(viewerId);

        Assert.True(result);
        var deleteCommand = Assert.Single(connection.ExecutedCommands, c => c.CommandText == "EXEC dbo.spdViewer @ViewerId");
        Assert.Equal(viewerId, deleteCommand.ParameterValue("@ViewerId"));
    }

    [Fact]
    public async Task DeleteAsync_UnknownViewer_ReturnsFalse()
    {
        var (repository, connection) = CreateRepository();
        connection.RespondToNonQuery("EXEC dbo.spdViewer @ViewerId", 0);

        var result = await repository.DeleteAsync(Guid.NewGuid());

        Assert.False(result);
    }

    [Fact]
    public async Task GetSchemeViewersAsync_ReturnsOnlyLinksForTheRequestedScheme()
    {
        var (repository, connection) = CreateRepository();
        var schemeId = Guid.NewGuid();
        var viewerSchemeId = Guid.NewGuid();
        var viewerId = Guid.NewGuid();

        // spgaViewers' 3 result sets: viewers (discarded by GetSchemeViewersAsync), viewer-scheme
        // links (filtered down to the requested scheme), viewer-participants (never read here).
        var viewersTable = new DataTable();
        viewersTable.Columns.Add("fldViewerId", typeof(Guid));
        viewersTable.Rows.Add(viewerId);

        var linksTable = new DataTable();
        linksTable.Columns.Add("fldViewerSchemeId", typeof(Guid));
        linksTable.Columns.Add("fldViewerId", typeof(Guid));
        linksTable.Columns.Add("fldSchemeId", typeof(Guid));
        linksTable.Rows.Add(viewerSchemeId, viewerId, schemeId);
        linksTable.Rows.Add(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        var dataSet = new DataSet();
        dataSet.Tables.Add(viewersTable);
        dataSet.Tables.Add(linksTable);
        connection.RespondToQuery(GetAllSql, dataSet);

        var result = await repository.GetSchemeViewersAsync(schemeId);

        var link = Assert.Single(result);
        Assert.Equal(viewerSchemeId, link.ViewerSchemeId);
        Assert.Equal(viewerId, link.ViewerId);
    }

    [Fact]
    public async Task AddSchemeViewerAsync_ExecutesInsertWithTheGivenIds()
    {
        var (repository, connection) = CreateRepository();
        var viewerSchemeId = Guid.NewGuid();
        var viewerId = Guid.NewGuid();
        var schemeId = Guid.NewGuid();
        const string insertLinkSql = "EXEC dbo.spiViewerScheme @ViewerSchemeId = @ViewerSchemeId, @ViewerId = @ViewerId, @SchemeId = @SchemeId";
        connection.RespondToNonQuery(insertLinkSql, 1);

        await repository.AddSchemeViewerAsync(viewerSchemeId, viewerId, schemeId);

        var command = Assert.Single(connection.ExecutedCommands, c => c.CommandText == insertLinkSql);
        Assert.Equal(viewerSchemeId, command.ParameterValue("@ViewerSchemeId"));
        Assert.Equal(viewerId, command.ParameterValue("@ViewerId"));
        Assert.Equal(schemeId, command.ParameterValue("@SchemeId"));
    }

    [Fact]
    public async Task RemoveSchemeViewerAsync_ExecutesDeleteWithTheGivenId()
    {
        var (repository, connection) = CreateRepository();
        var viewerSchemeId = Guid.NewGuid();
        const string deleteLinkSql = "EXEC dbo.spdViewerScheme @ViewerSchemeId";
        connection.RespondToNonQuery(deleteLinkSql, 1);

        await repository.RemoveSchemeViewerAsync(viewerSchemeId);

        var command = Assert.Single(connection.ExecutedCommands, c => c.CommandText == deleteLinkSql);
        Assert.Equal(viewerSchemeId, command.ParameterValue("@ViewerSchemeId"));
    }
}
