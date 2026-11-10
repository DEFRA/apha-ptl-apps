using Dapper;
using PTL.Core.Audit;
using PTL.Core.LoginInstructions;
using PTL.Data.Audit;
using PTL.Data.Infrastructure;

namespace PTL.Data.LoginInstructions;

// One small procedure per table (spuParticipantLoginInstructions, spuViewerLoginInstructions,
// spuExtTestConsultantLoginInstructions - see PTL.Database script 0011); the sent flag is only ever set here.
public sealed class LoginInstructionsRepository(IDbConnectionFactory connectionFactory) : ILoginInstructionsRepository
{
    public Task MarkSentAsync(LoginInstructionUserType userType, Guid id, DateTime sentDate, AuditEntry auditEntry, CancellationToken cancellationToken = default) =>
        AuditTrailWriter.ExecuteAsync(connectionFactory, auditEntry, async (connection, transaction) =>
        {
            await connection.ExecuteAsync(
                Sql(userType),
                new { Id = id, LoginInstructionsSent = true, LoginInstructionsSentDate = sentDate },
                transaction);

            return true;
        }, cancellationToken);

    private static string Sql(LoginInstructionUserType userType) => userType switch
    {
        LoginInstructionUserType.Participant => "EXEC dbo.spuParticipantLoginInstructions @ParticipantId = @Id, @LoginInstructionsSent = @LoginInstructionsSent, @LoginInstructionsSentDate = @LoginInstructionsSentDate",
        LoginInstructionUserType.Viewer => "EXEC dbo.spuViewerLoginInstructions @ViewerId = @Id, @LoginInstructionsSent = @LoginInstructionsSent, @LoginInstructionsSentDate = @LoginInstructionsSentDate",
        LoginInstructionUserType.ExternalTestConsultant => "EXEC dbo.spuExtTestConsultantLoginInstructions @ExternalTestConsultantId = @Id, @LoginInstructionsSent = @LoginInstructionsSent, @LoginInstructionsSentDate = @LoginInstructionsSentDate",
        _ => throw new ArgumentOutOfRangeException(nameof(userType), userType, null),
    };
}
