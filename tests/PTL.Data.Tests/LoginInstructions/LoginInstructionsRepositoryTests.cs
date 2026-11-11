using PTL.Core.Audit;
using PTL.Core.LoginInstructions;
using PTL.Data.LoginInstructions;
using PTL.Data.Tests.Fakes;

namespace PTL.Data.Tests.LoginInstructions;

public class LoginInstructionsRepositoryTests
{
    private const string AuditSql = "EXEC dbo.spiLog @LogId, @LogDate, @UserId, @Description";

    private static (LoginInstructionsRepository Repository, FakeDbConnection Connection) CreateRepository()
    {
        var connection = new FakeDbConnection();
        return (new LoginInstructionsRepository(new FakeDbConnectionFactory(connection)), connection);
    }

    private static AuditEntry Audit(LoginInstructionUserType userType, Guid id) =>
        new(Guid.NewGuid(), "SendLoginInstructions", userType.ToString(), id.ToString(), "Template: test");

    [Theory]
    [InlineData(LoginInstructionUserType.Participant, "EXEC dbo.spuParticipantLoginInstructions @ParticipantId = @Id, @LoginInstructionsSent = @LoginInstructionsSent, @LoginInstructionsSentDate = @LoginInstructionsSentDate")]
    [InlineData(LoginInstructionUserType.Viewer, "EXEC dbo.spuViewerLoginInstructions @ViewerId = @Id, @LoginInstructionsSent = @LoginInstructionsSent, @LoginInstructionsSentDate = @LoginInstructionsSentDate")]
    [InlineData(LoginInstructionUserType.ExternalTestConsultant, "EXEC dbo.spuExtTestConsultantLoginInstructions @ExternalTestConsultantId = @Id, @LoginInstructionsSent = @LoginInstructionsSent, @LoginInstructionsSentDate = @LoginInstructionsSentDate")]
    [InlineData(LoginInstructionUserType.LabUser, "EXEC dbo.spuLabUserLoginInstructions @LabUserParticipantId = @Id, @LoginInstructionsSent = @LoginInstructionsSent, @LoginInstructionsSentDate = @LoginInstructionsSentDate")]
    public async Task MarkSentAsync_RunsTheUserTypesProcedureAndAuditsInOneTransaction(LoginInstructionUserType userType, string sql)
    {
        var (repository, connection) = CreateRepository();
        connection.RespondToNonQuery(sql, 1);
        connection.RespondToNonQuery(AuditSql, 1);
        var id = Guid.NewGuid();
        var sentDate = new DateTime(2026, 11, 10, 9, 30, 0, DateTimeKind.Utc);

        await repository.MarkSentAsync(userType, id, sentDate, Audit(userType, id));

        var command = Assert.Single(connection.ExecutedCommands, c => c.CommandText == sql);
        Assert.Equal(id, command.ParameterValue("@Id"));
        Assert.Equal(true, command.ParameterValue("@LoginInstructionsSent"));
        Assert.Equal(sentDate, command.ParameterValue("@LoginInstructionsSentDate"));
        Assert.NotNull(connection.LastTransaction);
        Assert.True(connection.LastTransaction!.Committed);
        var auditCommand = Assert.Single(connection.ExecutedCommands, c => c.CommandText == AuditSql);
        Assert.StartsWith("SysAdmin|SendLoginInstructions|", (string)auditCommand.ParameterValue("@Description")!);
    }

    [Fact]
    public async Task MarkSentAsync_AuditWriteFails_RollsBackTheSentState()
    {
        var (repository, connection) = CreateRepository();
        const string sql = "EXEC dbo.spuViewerLoginInstructions @ViewerId = @Id, @LoginInstructionsSent = @LoginInstructionsSent, @LoginInstructionsSentDate = @LoginInstructionsSentDate";
        connection.RespondToNonQuery(sql, 1);
        var id = Guid.NewGuid();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            repository.MarkSentAsync(LoginInstructionUserType.Viewer, id, DateTime.UtcNow, Audit(LoginInstructionUserType.Viewer, id)));

        Assert.True(connection.LastTransaction!.RolledBack);
        Assert.False(connection.LastTransaction.Committed);
    }

    [Fact]
    public async Task MarkSentAsync_UnknownUserType_Throws()
    {
        var (repository, _) = CreateRepository();
        var id = Guid.NewGuid();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            repository.MarkSentAsync((LoginInstructionUserType)99, id, DateTime.UtcNow, Audit(LoginInstructionUserType.Viewer, id)));
    }
}
