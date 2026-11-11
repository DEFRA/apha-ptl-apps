using System.Data;
using PTL.Core.Audit;
using PTL.Core.LabUsers;
using PTL.Data.LabUsers;
using PTL.Data.Tests.Fakes;

namespace PTL.Data.Tests.LabUsers;

// Exercises LabUserRepository against a fake ADO.NET connection (see Fakes/) instead of a live SQL Server.
public class LabUserRepositoryTests
{
    private const string ListSql = "EXEC dbo.spgLabUsersByParticipant @ParticipantId";
    private const string GetSql = "EXEC dbo.spgLabUsersByParticipant @ParticipantId, @LabUserId";
    private const string LinkSql = "EXEC dbo.spgLabUserLinkById @LabUserParticipantId";
    private const string AddSql = "EXEC dbo.spiLabUserForParticipant @ParticipantId, @Name, @Email, @IsActive";
    private const string UpdateSql = "EXEC dbo.spuLabUserForParticipant @ParticipantId, @LabUserId, @Name, @Email, @IsActive, @ChangeDetails";
    private const string AuditSql = "EXEC dbo.spiLog @LogId, @LogDate, @UserId, @Description";

    private static (LabUserRepository Repository, FakeDbConnection Connection) CreateRepository()
    {
        var connection = new FakeDbConnection();
        return (new LabUserRepository(new FakeDbConnectionFactory(connection)), connection);
    }

    private static DataTable LinkTable(params (Guid Link, Guid User, Guid Lab, string Name, string Email, bool Active, bool Sent, int Others, bool SignedIn)[] rows)
    {
        var table = new DataTable();
        table.Columns.Add("LabUserParticipantId", typeof(Guid));
        table.Columns.Add("LabUserId", typeof(Guid));
        table.Columns.Add("ParticipantId", typeof(Guid));
        table.Columns.Add("Name", typeof(string));
        table.Columns.Add("Email", typeof(string));
        table.Columns.Add("IsActive", typeof(bool));
        table.Columns.Add("LoginInstructionsSent", typeof(bool));
        table.Columns.Add("OtherLabCount", typeof(int));
        table.Columns.Add("HasSignedIn", typeof(bool));
        table.Columns.Add("LabCode", typeof(string));
        table.Columns.Add("LabName", typeof(string));
        foreach (var row in rows)
        {
            table.Rows.Add(row.Link, row.User, row.Lab, row.Name, row.Email, row.Active, row.Sent, row.Others, row.SignedIn, "0142", "Bristol");
        }

        return table;
    }

    private static DataTable AddTable(int outcome, Guid? labUserId)
    {
        var table = new DataTable();
        table.Columns.Add("Outcome", typeof(int));
        table.Columns.Add("LabUserId", typeof(Guid));
        table.Rows.Add(outcome, (object?)labUserId ?? DBNull.Value);
        return table;
    }

    private static DataTable OutcomeTable(int outcome)
    {
        var table = new DataTable();
        table.Columns.Add("Outcome", typeof(int));
        table.Rows.Add(outcome);
        return table;
    }

    // ---- reads ----

    [Fact]
    public async Task GetByParticipantAsync_ReturnsTheMappedLinks()
    {
        var (repository, connection) = CreateRepository();
        var lab = Guid.NewGuid();
        var link = Guid.NewGuid();
        var user = Guid.NewGuid();
        connection.RespondToQuery(ListSql, LinkTable((link, user, lab, "Emma Wright", "emma@example.org", true, false, 2, true)));

        var result = await repository.GetByParticipantAsync(lab);

        var mapped = Assert.Single(result);
        Assert.Equal(new LabUserLink
        {
            LabUserParticipantId = link,
            LabUserId = user,
            ParticipantId = lab,
            Name = "Emma Wright",
            Email = "emma@example.org",
            IsActive = true,
            LoginInstructionsSent = false,
            OtherLabCount = 2,
            HasSignedIn = true,
            LabCode = "0142",
            LabName = "Bristol"
        }, mapped);
        Assert.Equal(lab, connection.ExecutedCommands.Single().ParameterValue("@ParticipantId"));
    }

    [Fact]
    public async Task GetByParticipantAsync_NoLinks_ReturnsEmpty()
    {
        var (repository, connection) = CreateRepository();
        connection.RespondToQuery(ListSql, LinkTable());

        Assert.Empty(await repository.GetByParticipantAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task GetAsync_FiltersByLabAndPerson_AndReturnsNullWhenNotLinked()
    {
        var (repository, connection) = CreateRepository();
        var lab = Guid.NewGuid();
        var user = Guid.NewGuid();
        connection.RespondToQuery(GetSql, LinkTable((Guid.NewGuid(), user, lab, "Emma", "e@example.org", true, true, 0, false)));

        var found = await repository.GetAsync(lab, user);

        Assert.Equal(user, found!.LabUserId);
        var command = connection.ExecutedCommands.Single();
        Assert.Equal(lab, command.ParameterValue("@ParticipantId"));
        Assert.Equal(user, command.ParameterValue("@LabUserId"));

        connection.RespondToQuery(GetSql, LinkTable());
        Assert.Null(await repository.GetAsync(lab, Guid.NewGuid()));
    }

    [Fact]
    public async Task FindByLinkIdAsync_ReturnsTheLinkOrNull()
    {
        var (repository, connection) = CreateRepository();
        var link = Guid.NewGuid();
        connection.RespondToQuery(LinkSql, LinkTable((link, Guid.NewGuid(), Guid.NewGuid(), "Emma", "e@example.org", true, false, 0, false)));

        Assert.Equal(link, (await repository.FindByLinkIdAsync(link))!.LabUserParticipantId);

        connection.RespondToQuery(LinkSql, LinkTable());
        Assert.Null(await repository.FindByLinkIdAsync(Guid.NewGuid()));
    }

    // ---- add ----

    [Theory]
    [InlineData(1, LabUserAddOutcome.Created)]
    [InlineData(2, LabUserAddOutcome.AddedExistingAccount)]
    public async Task AddAsync_SomethingAdded_WritesTheAuditInTheSameTransactionAndCommits(int outcome, LabUserAddOutcome expected)
    {
        var (repository, connection) = CreateRepository();
        var lab = Guid.NewGuid();
        var labUserId = Guid.NewGuid();
        connection.RespondToQuery(AddSql, AddTable(outcome, labUserId));
        connection.RespondToNonQuery(AuditSql, 1);

        var result = await repository.AddAsync(lab, "Ramesh", "ramesh@lab.com", true, id => new AuditEntry(lab, "AddLabUser", "LabUser", id.ToString(), "Details"));

        Assert.Equal(new LabUserAddResult(expected, labUserId), result);
        var add = Assert.Single(connection.ExecutedCommands, c => c.CommandText == AddSql);
        Assert.Equal(lab, add.ParameterValue("@ParticipantId"));
        Assert.Equal("Ramesh", add.ParameterValue("@Name"));
        Assert.Equal("ramesh@lab.com", add.ParameterValue("@Email"));
        Assert.Equal(true, add.ParameterValue("@IsActive"));
        var audit = Assert.Single(connection.ExecutedCommands, c => c.CommandText == AuditSql);
        Assert.Equal($"ExternalWeb|AddLabUser|LabUser|{labUserId}|Details", (string)audit.ParameterValue("@Description")!);
        Assert.True(connection.LastTransaction!.Committed);
    }

    [Theory]
    [InlineData(3, LabUserAddOutcome.AlreadyLinked, true)]
    [InlineData(4, LabUserAddOutcome.EmailUsedByOtherUserType, false)]
    public async Task AddAsync_NothingAdded_WritesNoAudit(int outcome, LabUserAddOutcome expected, bool hasLabUserId)
    {
        var (repository, connection) = CreateRepository();
        connection.RespondToQuery(AddSql, AddTable(outcome, hasLabUserId ? Guid.NewGuid() : null));

        var result = await repository.AddAsync(Guid.NewGuid(), "Ramesh", "ramesh@lab.com", true, _ => throw new InvalidOperationException("no audit expected"));

        Assert.Equal(expected, result.Outcome);
        Assert.DoesNotContain(connection.ExecutedCommands, c => c.CommandText == AuditSql);
        Assert.True(connection.LastTransaction!.Committed);
    }

    [Fact]
    public async Task AddAsync_AuditWriteFails_RollsBackTheAddition()
    {
        var (repository, connection) = CreateRepository();
        connection.RespondToQuery(AddSql, AddTable(1, Guid.NewGuid()));
        // AuditSql deliberately has no configured response, so the audit write throws.

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            repository.AddAsync(Guid.NewGuid(), "Ramesh", "ramesh@lab.com", true, id => new AuditEntry(null, "AddLabUser", "LabUser", id.ToString(), null)));

        Assert.True(connection.LastTransaction!.RolledBack);
        Assert.False(connection.LastTransaction.Committed);
    }

    // ---- update ----

    [Theory]
    [InlineData(1, LabUserUpdateOutcome.Updated)]
    [InlineData(5, LabUserUpdateOutcome.UpdatedEmailChanged)]
    public async Task UpdateAsync_Changed_WritesTheAuditInTheSameTransactionAndCommits(int outcome, LabUserUpdateOutcome expected)
    {
        var (repository, connection) = CreateRepository();
        var lab = Guid.NewGuid();
        var user = Guid.NewGuid();
        connection.RespondToQuery(UpdateSql, OutcomeTable(outcome));
        connection.RespondToNonQuery(AuditSql, 1);

        var result = await repository.UpdateAsync(lab, user, "Emma", "e@example.org", false, true, new AuditEntry(lab, "UpdateLabUser", "LabUser", user.ToString(), "Details"));

        Assert.Equal(expected, result);
        var update = Assert.Single(connection.ExecutedCommands, c => c.CommandText == UpdateSql);
        Assert.Equal(lab, update.ParameterValue("@ParticipantId"));
        Assert.Equal(user, update.ParameterValue("@LabUserId"));
        Assert.Equal(false, update.ParameterValue("@IsActive"));
        Assert.Equal(true, update.ParameterValue("@ChangeDetails"));
        Assert.Equal($"ExternalWeb|UpdateLabUser|LabUser|{user}|Details", (string)Assert.Single(connection.ExecutedCommands, c => c.CommandText == AuditSql).ParameterValue("@Description")!);
        Assert.True(connection.LastTransaction!.Committed);
    }

    [Theory]
    [InlineData(0, LabUserUpdateOutcome.NotFound)]
    [InlineData(2, LabUserUpdateOutcome.EmailUsedByOtherLabUser)]
    [InlineData(3, LabUserUpdateOutcome.SharedWithOtherLabs)]
    [InlineData(4, LabUserUpdateOutcome.EmailUsedByOtherUserType)]
    public async Task UpdateAsync_Refused_WritesNoAudit(int outcome, LabUserUpdateOutcome expected)
    {
        var (repository, connection) = CreateRepository();
        connection.RespondToQuery(UpdateSql, OutcomeTable(outcome));

        var result = await repository.UpdateAsync(Guid.NewGuid(), Guid.NewGuid(), "Emma", "e@example.org", true, true, new AuditEntry(null, "UpdateLabUser", "LabUser", null, null));

        Assert.Equal(expected, result);
        Assert.DoesNotContain(connection.ExecutedCommands, c => c.CommandText == AuditSql);
    }
}
