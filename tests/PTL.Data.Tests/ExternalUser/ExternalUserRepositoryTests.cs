using System.Data;
using PTL.Contracts.ExternalUser;
using PTL.Data.ExternalUser;
using PTL.Data.Tests.Fakes;

namespace PTL.Data.Tests.ExternalUser;

// Exercises ExternalUserRepository against a fake ADO.NET connection (see Fakes/) instead of a live SQL
// Server - see ParticipantRepositoryTests for the pattern this follows.
public class ExternalUserRepositoryTests
{
    private const string BySsoIdExtSql = "EXEC dbo.spgExternalUserBySsoIdExt @SsoIdExt";
    private const string ByEmailSql = "EXEC dbo.spgExternalUserByEmail @Email";
    private const string LinkSql = "EXEC dbo.spuExternalUserSsoIdExt @UserType, @Email, @SsoIdExt";

    private static DataTable MatchTable(params (int UserType, Guid UserId, string? Email, Guid? SsoIdExt, bool IsActive, Guid? LabId, string? LabCode, string? LabName)[] rows)
    {
        var table = new DataTable();
        table.Columns.Add("UserType", typeof(int));
        table.Columns.Add("UserId", typeof(Guid));
        table.Columns.Add("Email", typeof(string));
        table.Columns.Add("SsoIdExt", typeof(Guid));
        table.Columns.Add("IsActive", typeof(bool));
        table.Columns.Add("LabParticipantId", typeof(Guid));
        table.Columns.Add("LabCode", typeof(string));
        table.Columns.Add("LabName", typeof(string));
        foreach (var row in rows)
        {
            table.Rows.Add(row.UserType, row.UserId, (object?)row.Email ?? DBNull.Value, (object?)row.SsoIdExt ?? DBNull.Value, row.IsActive, (object?)row.LabId ?? DBNull.Value, (object?)row.LabCode ?? DBNull.Value, (object?)row.LabName ?? DBNull.Value);
        }

        return table;
    }

    private static (ExternalUserRepository Repository, FakeDbConnection Connection) CreateRepository()
    {
        var connection = new FakeDbConnection();
        return (new ExternalUserRepository(new FakeDbConnectionFactory(connection)), connection);
    }

    [Fact]
    public async Task FindBySsoIdExtAsync_ReturnsEveryMappedRowInOrder()
    {
        var (repository, connection) = CreateRepository();
        var ssoIdExt = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var labId = Guid.NewGuid();
        connection.RespondToQuery(BySsoIdExtSql, MatchTable(
            (4, userId, "lab@example.com", ssoIdExt, true, labId, "LAB001", "North Lab"),
            (4, userId, "lab@example.com", ssoIdExt, false, null, null, null)));

        var result = await repository.FindBySsoIdExtAsync(ssoIdExt);

        Assert.Equal(2, result.Count);
        var first = result[0];
        Assert.Equal(ExternalUserType.LabUser, first.UserType);
        Assert.Equal(userId, first.UserId);
        Assert.Equal("lab@example.com", first.Email);
        Assert.Equal(ssoIdExt, first.SsoIdExt);
        Assert.True(first.IsActive);
        Assert.Equal(labId, first.LabParticipantId);
        Assert.Equal("LAB001", first.LabCode);
        Assert.Equal("North Lab", first.LabName);
        Assert.False(result[1].IsActive);
        Assert.Null(result[1].LabParticipantId);
        Assert.Null(result[1].LabCode);
        Assert.Equal(ssoIdExt, connection.ExecutedCommands.Single().ParameterValue("@SsoIdExt"));
    }

    [Fact]
    public async Task FindBySsoIdExtAsync_NoRows_ReturnsEmpty()
    {
        var (repository, connection) = CreateRepository();
        connection.RespondToQuery(BySsoIdExtSql, MatchTable());

        var result = await repository.FindBySsoIdExtAsync(Guid.NewGuid());

        Assert.Empty(result);
    }

    [Fact]
    public async Task FindByEmailAsync_ReturnsUnlinkedAndLinkedRows_AndSendsTheEmailAsVarchar()
    {
        var (repository, connection) = CreateRepository();
        connection.RespondToQuery(ByEmailSql, MatchTable(
            (1, Guid.NewGuid(), "user@example.com", null, true, Guid.NewGuid(), "LAB001", "A"),
            (2, Guid.NewGuid(), "user@example.com", Guid.NewGuid(), true, null, null, null)));

        var result = await repository.FindByEmailAsync("user@example.com");

        Assert.Equal([ExternalUserType.Participant, ExternalUserType.Viewer], result.Select(row => row.UserType));
        Assert.Null(result[0].SsoIdExt);
        Assert.NotNull(result[1].SsoIdExt);
        var command = connection.ExecutedCommands.Single();
        Assert.Equal(DbType.AnsiString, command.Parameter("@Email").DbType);
        Assert.Equal(255, command.Parameter("@Email").Size);
        Assert.Equal("user@example.com", command.ParameterValue("@Email"));
    }

    [Fact]
    public async Task LinkAsync_SendsTheTypeEmailAndContactIdAndReturnsWhatIsNowLinked()
    {
        var (repository, connection) = CreateRepository();
        var ssoIdExt = Guid.NewGuid();
        var viewerId = Guid.NewGuid();
        connection.RespondToQuery(LinkSql, MatchTable((2, viewerId, "viewer@example.com", ssoIdExt, true, null, null, null)));

        var result = await repository.LinkAsync(ExternalUserType.Viewer, "viewer@example.com", ssoIdExt);

        var linked = Assert.Single(result);
        Assert.Equal(viewerId, linked.UserId);
        var command = connection.ExecutedCommands.Single();
        Assert.Equal(2, command.ParameterValue("@UserType"));
        Assert.Equal("viewer@example.com", command.ParameterValue("@Email"));
        Assert.Equal(DbType.AnsiString, command.Parameter("@Email").DbType);
        Assert.Equal(ssoIdExt, command.ParameterValue("@SsoIdExt"));
    }

    [Theory]
    [InlineData(ExternalUserType.Participant, 1)]
    [InlineData(ExternalUserType.Viewer, 2)]
    [InlineData(ExternalUserType.TestConsultant, 3)]
    [InlineData(ExternalUserType.LabUser, 4)]
    public async Task LinkAsync_SendsTheNumericUserTypeTheDatabaseExpects(ExternalUserType userType, int expected)
    {
        var (repository, connection) = CreateRepository();
        connection.RespondToQuery(LinkSql, MatchTable());

        await repository.LinkAsync(userType, "user@example.com", Guid.NewGuid());

        Assert.Equal(expected, connection.ExecutedCommands.Single().ParameterValue("@UserType"));
    }
}
